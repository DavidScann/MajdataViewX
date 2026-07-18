using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using FFmpeg.AutoGen;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public static unsafe class FFmpegMediaEncoder
{
    private const int MaxPendingWorkItems = 16;

    private enum EncoderWorkKind
    {
        Video,
        Audio
    }

    private readonly struct EncoderWorkItem
    {
        public readonly EncoderWorkKind Kind;
        public readonly NativeArray<byte> VideoData;
        public readonly NativeArray<float> AudioData;
        public readonly int AudioOffset;
        public readonly int AudioCount;
        public readonly ManualResetEventSlim VideoCompletion;

        private EncoderWorkItem(
            EncoderWorkKind kind,
            NativeArray<byte> videoData,
            NativeArray<float> audioData,
            int audioOffset,
            int audioCount,
            ManualResetEventSlim videoCompletion)
        {
            Kind = kind;
            VideoData = videoData;
            AudioData = audioData;
            AudioOffset = audioOffset;
            AudioCount = audioCount;
            VideoCompletion = videoCompletion;
        }

        public static EncoderWorkItem Video(
            NativeArray<byte> data,
            ManualResetEventSlim completion) =>
            new(
                EncoderWorkKind.Video,
                data,
                default,
                0,
                0,
                completion);

        public static EncoderWorkItem Audio(
            NativeArray<float> data,
            int offset,
            int count) =>
            new(
                EncoderWorkKind.Audio,
                default,
                data,
                offset,
                count,
                null);
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private static IntPtr _winPthreadHandle;

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string libraryPath);
#endif

    private static AVFormatContext* _formatContext;
    private static AVCodecContext* _videoContext;
    private static AVCodecContext* _audioContext;
    private static AVFrame* _videoFrame;
    private static AVFrame* _audioFrame;
    private static AVPacket* _packet;
    private static SwsContext* _scaleContext;

    private static int _videoStreamIndex;
    private static int _audioStreamIndex;
    private static int _width;
    private static int _height;
    private static int _channels;
    private static int _audioFrameSize;
    private static int _audioBufferPosition;
    private static long _videoFrameCount;
    private static long _audioFramesEncoded;
    private static float[] _audioBuffer;
    private static bool _headerWritten;
    private static BlockingCollection<EncoderWorkItem> _workQueue;
    private static Thread _workerThread;
    private static ExceptionDispatchInfo _workerFailure;

    public static bool IsInitialized => _formatContext != null;
    public static long VideoFrameCount => _videoFrameCount;

    public static void Initialize(
        string outputPath,
        int width,
        int height,
        int framesPerSecond,
        int sampleRate,
        int channels)
    {
        if (IsInitialized)
            throw new InvalidOperationException("The FFmpeg encoder is already initialized.");
        if (width <= 0 || height <= 0 || (width & 1) != 0 || (height & 1) != 0)
            throw new ArgumentOutOfRangeException(nameof(width), "YUV420P requires a positive even width and height.");
        if (framesPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        if (sampleRate <= 0 || channels <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));

        ConfigureNativeLibraryPath();

        _width = width;
        _height = height;
        _channels = channels;
        _videoFrameCount = 0;
        _audioFramesEncoded = 0;
        _audioBufferPosition = 0;
        _headerWritten = false;

        try
        {
            AVFormatContext* formatContext = null;
            Check(ffmpeg.avformat_alloc_output_context2(
                &formatContext, null, "mp4", outputPath), "allocate MP4 output context");
            _formatContext = formatContext;
            if (_formatContext == null)
                throw new InvalidOperationException("FFmpeg returned a null output context.");

            InitializeVideo(width, height, framesPerSecond);
            InitializeAudio(sampleRate, channels);

            _packet = ffmpeg.av_packet_alloc();
            if (_packet == null)
                throw new OutOfMemoryException("Could not allocate an FFmpeg packet.");

            if ((_formatContext->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0)
                Check(ffmpeg.avio_open(&_formatContext->pb, outputPath, ffmpeg.AVIO_FLAG_WRITE), "open output file");

            AVDictionary* options = null;
            try
            {
                Check(ffmpeg.av_dict_set(&options, "movflags", "+faststart", 0), "set MP4 movflags");
                Check(ffmpeg.avformat_write_header(_formatContext, &options), "write MP4 header");
                _headerWritten = true;
            }
            finally
            {
                ffmpeg.av_dict_free(&options);
            }

            Debug.Log(
                $"[FFmpeg] Recording {_width}x{_height}, {framesPerSecond} fps, " +
                $"{sampleRate} Hz, {channels} channels.");

            StartWorker();
        }
        catch
        {
            StopWorker();
            FreeResources();
            throw;
        }
    }

    private static void InitializeVideo(int width, int height, int framesPerSecond)
    {
        var codec = ffmpeg.avcodec_find_encoder_by_name("libx264");
        if (codec == null)
            codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);
        if (codec == null)
            codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_MPEG4);
        if (codec == null)
            throw new InvalidOperationException("No H.264 or MPEG-4 video encoder is available.");

        var stream = ffmpeg.avformat_new_stream(_formatContext, null);
        if (stream == null)
            throw new OutOfMemoryException("Could not allocate the video stream.");
        _videoStreamIndex = stream->index;

        _videoContext = ffmpeg.avcodec_alloc_context3(codec);
        if (_videoContext == null)
            throw new OutOfMemoryException("Could not allocate the video codec context.");

        var timeBase = new AVRational { num = 1, den = framesPerSecond };
        _videoContext->codec_id = codec->id;
        _videoContext->width = width;
        _videoContext->height = height;
        _videoContext->time_base = timeBase;
        _videoContext->framerate = new AVRational { num = framesPerSecond, den = 1 };
        _videoContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
        _videoContext->gop_size = Math.Max(framesPerSecond * 2, 1);
        _videoContext->max_b_frames = 0;
        _videoContext->codec_tag = 0;

        if ((_formatContext->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
            _videoContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

        var codecName = Marshal.PtrToStringAnsi((IntPtr)codec->name) ?? string.Empty;
        if (codecName == "libx264")
        {
            ffmpeg.av_opt_set(_videoContext->priv_data, "preset", "veryfast", 0);
            ffmpeg.av_opt_set(_videoContext->priv_data, "crf", "18", 0);
        }
        else
        {
            _videoContext->bit_rate = 8_000_000;
        }

        // Opening the codec creates codec extradata. Copy parameters only afterwards.
        Check(ffmpeg.avcodec_open2(_videoContext, codec, null), $"open video encoder {codecName}");
        Check(ffmpeg.avcodec_parameters_from_context(stream->codecpar, _videoContext), "copy video parameters");
        stream->time_base = timeBase;
        stream->avg_frame_rate = _videoContext->framerate;

        _videoFrame = ffmpeg.av_frame_alloc();
        if (_videoFrame == null)
            throw new OutOfMemoryException("Could not allocate the video frame.");
        _videoFrame->format = (int)_videoContext->pix_fmt;
        _videoFrame->width = width;
        _videoFrame->height = height;
        Check(ffmpeg.av_frame_get_buffer(_videoFrame, 32), "allocate video frame buffer");

        _scaleContext = ffmpeg.sws_getContext(
            width,
            height,
            AVPixelFormat.AV_PIX_FMT_RGBA,
            width,
            height,
            AVPixelFormat.AV_PIX_FMT_YUV420P,
            (int)SwsFlags.SWS_BILINEAR,
            null,
            null,
            null);
        if (_scaleContext == null)
            throw new InvalidOperationException("Could not create the RGBA-to-YUV conversion context.");
    }

    private static void InitializeAudio(int sampleRate, int channels)
    {
        var codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_AAC);
        if (codec == null)
            throw new InvalidOperationException("The FFmpeg build does not contain an AAC encoder.");

        var stream = ffmpeg.avformat_new_stream(_formatContext, null);
        if (stream == null)
            throw new OutOfMemoryException("Could not allocate the audio stream.");
        _audioStreamIndex = stream->index;

        _audioContext = ffmpeg.avcodec_alloc_context3(codec);
        if (_audioContext == null)
            throw new OutOfMemoryException("Could not allocate the audio codec context.");

        var timeBase = new AVRational { num = 1, den = sampleRate };
        _audioContext->codec_id = codec->id;
        _audioContext->sample_rate = sampleRate;
        _audioContext->sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_FLTP;
        ffmpeg.av_channel_layout_default(&_audioContext->ch_layout, channels);
        _audioContext->time_base = timeBase;
        _audioContext->bit_rate = 320_000;
        _audioContext->codec_tag = 0;

        if ((_formatContext->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
            _audioContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

        Check(ffmpeg.avcodec_open2(_audioContext, codec, null), "open AAC encoder");
        Check(ffmpeg.avcodec_parameters_from_context(stream->codecpar, _audioContext), "copy audio parameters");
        stream->time_base = timeBase;

        _audioFrameSize = _audioContext->frame_size;
        if (_audioFrameSize <= 0)
            throw new InvalidOperationException("The AAC encoder returned an invalid frame size.");
        _audioBuffer = new float[_audioFrameSize * channels];

        _audioFrame = ffmpeg.av_frame_alloc();
        if (_audioFrame == null)
            throw new OutOfMemoryException("Could not allocate the audio frame.");
        _audioFrame->format = (int)_audioContext->sample_fmt;
        _audioFrame->sample_rate = sampleRate;
        _audioFrame->nb_samples = _audioFrameSize;
        Check(
            ffmpeg.av_channel_layout_copy(&_audioFrame->ch_layout, &_audioContext->ch_layout),
            "copy audio channel layout");
        Check(ffmpeg.av_frame_get_buffer(_audioFrame, 0), "allocate audio frame buffer");
    }

    public static void QueueVideoFrame(
        NativeArray<byte> rgbaData,
        ManualResetEventSlim completion)
    {
        EnsureInitialized();
        ThrowIfEncodingFailed();
        var requiredBytes = checked(_width * _height * 4);
        if (!rgbaData.IsCreated || rgbaData.Length != requiredBytes)
            throw new ArgumentException(
                $"Expected exactly {requiredBytes} RGBA bytes, got {rgbaData.Length}.",
                nameof(rgbaData));
        if (completion == null)
            throw new ArgumentNullException(nameof(completion));

        completion.Reset();
        try
        {
            QueueWork(EncoderWorkItem.Video(rgbaData, completion));
        }
        catch
        {
            completion.Set();
            throw;
        }
    }

    private static void WriteVideoFrameCore(NativeArray<byte> rgbaData)
    {
        EnsureInitialized();

        Check(ffmpeg.av_frame_make_writable(_videoFrame), "make video frame writable");

        var stride = _width * 4;
        var source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(rgbaData);
        var sourceData = new byte_ptrArray4();
        var sourceLineSize = new int_array4();
        // AsyncGPUReadback uses the opposite vertical origin from the old
        // ReadPixels pipeline. Forward row order flips the encoded output.
        sourceData[0] = source;
        sourceLineSize[0] = stride;

        var destinationData = new byte_ptrArray4();
        var destinationLineSize = new int_array4();
        destinationData[0] = _videoFrame->data[0];
        destinationData[1] = _videoFrame->data[1];
        destinationData[2] = _videoFrame->data[2];
        destinationLineSize[0] = _videoFrame->linesize[0];
        destinationLineSize[1] = _videoFrame->linesize[1];
        destinationLineSize[2] = _videoFrame->linesize[2];

        var scaledRows = ffmpeg.sws_scale(
            _scaleContext,
            sourceData,
            sourceLineSize,
            0,
            _height,
            destinationData,
            destinationLineSize);
        if (scaledRows != _height)
            throw new InvalidOperationException($"FFmpeg converted {scaledRows} of {_height} video rows.");

        _videoFrame->pts = _videoFrameCount++;
        SendFrameAndWritePackets(_videoContext, _videoFrame, _videoStreamIndex, "video");
    }

    public static void WriteAudioSamples(
        NativeArray<float> samples,
        int sourceOffset,
        int sampleCount)
    {
        EnsureInitialized();
        ThrowIfEncodingFailed();
        if (!samples.IsCreated)
            throw new ArgumentException("The audio NativeArray has not been created.", nameof(samples));
        if (sourceOffset < 0 || sampleCount < 0 || sourceOffset > samples.Length - sampleCount)
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        if ((sourceOffset % _channels) != 0 || (sampleCount % _channels) != 0)
            throw new ArgumentException("Audio ranges must contain complete interleaved sample frames.");

        if (sampleCount > 0)
            QueueWork(EncoderWorkItem.Audio(samples, sourceOffset, sampleCount));
    }

    private static void WriteAudioSamplesCore(
        NativeArray<float> samples,
        int sourceOffset,
        int sampleCount)
    {
        EnsureInitialized();

        var source = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(samples) + sourceOffset;
        var consumed = 0;
        while (consumed < sampleCount)
        {
            var copyCount = Math.Min(sampleCount - consumed, _audioBuffer.Length - _audioBufferPosition);
            fixed (float* destination = &_audioBuffer[_audioBufferPosition])
            {
                var copyBytes = (long)copyCount * sizeof(float);
                Buffer.MemoryCopy(source + consumed, destination, copyBytes, copyBytes);
            }

            consumed += copyCount;
            _audioBufferPosition += copyCount;
            if (_audioBufferPosition == _audioBuffer.Length)
            {
                EncodeAudioFrame();
                _audioBufferPosition = 0;
            }
        }
    }

    public static void ThrowIfEncodingFailed()
    {
        var failure = Volatile.Read(ref _workerFailure);
        failure?.Throw();
    }

    private static void QueueWork(EncoderWorkItem item)
    {
        var queue = _workQueue;
        if (queue == null)
            throw new InvalidOperationException("The FFmpeg encoding worker is not running.");

        while (true)
        {
            ThrowIfEncodingFailed();
            try
            {
                if (queue.TryAdd(item, 50))
                    return;
            }
            catch (InvalidOperationException)
            {
                ThrowIfEncodingFailed();
                throw new InvalidOperationException("The FFmpeg encoding worker has stopped.");
            }
        }
    }

    private static void StartWorker()
    {
        _workerFailure = null;
        _workQueue = new BlockingCollection<EncoderWorkItem>(MaxPendingWorkItems);
        _workerThread = new Thread(EncodingWorkerMain)
        {
            IsBackground = true,
            Name = "FFmpeg encoding worker"
        };
        try
        {
            _workerThread.Start();
        }
        catch
        {
            _workerThread = null;
            _workQueue.Dispose();
            _workQueue = null;
            throw;
        }
    }

    private static void EncodingWorkerMain()
    {
        try
        {
            foreach (var item in _workQueue.GetConsumingEnumerable())
            {
                try
                {
                    switch (item.Kind)
                    {
                        case EncoderWorkKind.Video:
                            WriteVideoFrameCore(item.VideoData);
                            break;
                        case EncoderWorkKind.Audio:
                            WriteAudioSamplesCore(
                                item.AudioData,
                                item.AudioOffset,
                                item.AudioCount);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception exception)
                {
                    Interlocked.CompareExchange(
                        ref _workerFailure,
                        ExceptionDispatchInfo.Capture(exception),
                        null);
                    throw;
                }
                finally
                {
                    if (item.Kind == EncoderWorkKind.Video)
                        item.VideoCompletion.Set();
                }
            }
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(
                ref _workerFailure,
                ExceptionDispatchInfo.Capture(exception),
                null);
            try
            {
                _workQueue.CompleteAdding();
            }
            catch (InvalidOperationException)
            {
            }
        }
        finally
        {
            while (_workQueue.TryTake(out var item))
            {
                if (item.Kind == EncoderWorkKind.Video)
                    item.VideoCompletion.Set();
            }
        }
    }

    private static void StopWorker()
    {
        var queue = _workQueue;
        var worker = _workerThread;

        if (queue != null && !queue.IsAddingCompleted)
            queue.CompleteAdding();
        worker?.Join();

        _workerThread = null;
        _workQueue = null;
        queue?.Dispose();
    }

    private static void EncodeAudioFrame()
    {
        Check(ffmpeg.av_frame_make_writable(_audioFrame), "make audio frame writable");

        for (var channel = 0; channel < _channels; channel++)
        {
            var plane = (float*)_audioFrame->data[(uint)channel];
            for (var frame = 0; frame < _audioFrameSize; frame++)
                plane[frame] = _audioBuffer[frame * _channels + channel];
        }

        _audioFrame->pts = _audioFramesEncoded;
        _audioFramesEncoded += _audioFrameSize;
        SendFrameAndWritePackets(_audioContext, _audioFrame, _audioStreamIndex, "audio");
    }

    private static void SendFrameAndWritePackets(
        AVCodecContext* codecContext,
        AVFrame* frame,
        int streamIndex,
        string mediaType)
    {
        Check(ffmpeg.avcodec_send_frame(codecContext, frame), $"send {mediaType} frame");
        ReceiveAndWritePackets(codecContext, streamIndex, mediaType);
    }

    private static void ReceiveAndWritePackets(
        AVCodecContext* codecContext,
        int streamIndex,
        string mediaType)
    {
        while (true)
        {
            var result = ffmpeg.avcodec_receive_packet(codecContext, _packet);
            if (result == ffmpeg.AVERROR(ffmpeg.EAGAIN) || result == ffmpeg.AVERROR_EOF)
                return;
            Check(result, $"receive {mediaType} packet");

            _packet->stream_index = streamIndex;
            ffmpeg.av_packet_rescale_ts(
                _packet,
                codecContext->time_base,
                _formatContext->streams[streamIndex]->time_base);

            try
            {
                Check(
                    ffmpeg.av_interleaved_write_frame(_formatContext, _packet),
                    $"mux {mediaType} packet");
            }
            finally
            {
                // av_interleaved_write_frame takes ownership on success; unref is
                // still required on an error and is harmless for an empty packet.
                ffmpeg.av_packet_unref(_packet);
            }
        }
    }

    public static void Dispose()
    {
        if (!IsInitialized)
            return;

        ExceptionDispatchInfo failure = null;
        try
        {
            StopWorker();
            ThrowIfEncodingFailed();

            if (_headerWritten)
            {
                if (_audioBufferPosition > 0)
                {
                    Array.Clear(
                        _audioBuffer,
                        _audioBufferPosition,
                        _audioBuffer.Length - _audioBufferPosition);
                    EncodeAudioFrame();
                    _audioBufferPosition = 0;
                }

                FlushEncoder(_videoContext, _videoStreamIndex, "video");
                FlushEncoder(_audioContext, _audioStreamIndex, "audio");
                Check(ffmpeg.av_write_trailer(_formatContext), "write MP4 trailer");
            }
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            try
            {
                StopWorker();
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            FreeResources();
            _workerFailure = null;
        }

        failure?.Throw();
    }

    private static void FlushEncoder(
        AVCodecContext* codecContext,
        int streamIndex,
        string mediaType)
    {
        if (codecContext == null)
            return;

        var result = ffmpeg.avcodec_send_frame(codecContext, null);
        if (result < 0 && result != ffmpeg.AVERROR_EOF)
            Check(result, $"flush {mediaType} encoder");
        ReceiveAndWritePackets(codecContext, streamIndex, mediaType);
    }

    private static void FreeResources()
    {
        if (_packet != null)
        {
            var packet = _packet;
            ffmpeg.av_packet_free(&packet);
            _packet = null;
        }

        if (_videoFrame != null)
        {
            var frame = _videoFrame;
            ffmpeg.av_frame_free(&frame);
            _videoFrame = null;
        }

        if (_audioFrame != null)
        {
            var frame = _audioFrame;
            ffmpeg.av_frame_free(&frame);
            _audioFrame = null;
        }

        if (_scaleContext != null)
        {
            ffmpeg.sws_freeContext(_scaleContext);
            _scaleContext = null;
        }

        if (_videoContext != null)
        {
            var context = _videoContext;
            ffmpeg.avcodec_free_context(&context);
            _videoContext = null;
        }

        if (_audioContext != null)
        {
            var context = _audioContext;
            ffmpeg.avcodec_free_context(&context);
            _audioContext = null;
        }

        if (_formatContext != null)
        {
            if (_formatContext->pb != null &&
                (_formatContext->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0)
            {
                var ioContext = _formatContext->pb;
                ffmpeg.avio_closep(&ioContext);
                _formatContext->pb = null;
            }

            ffmpeg.avformat_free_context(_formatContext);
            _formatContext = null;
        }

        _audioBuffer = null;
        _audioBufferPosition = 0;
        _audioFramesEncoded = 0;
        _videoFrameCount = 0;
        _headerWritten = false;
    }

    private static void EnsureInitialized()
    {
        if (!IsInitialized || !_headerWritten)
            throw new InvalidOperationException("The FFmpeg encoder has not been initialized.");
    }

    private static void ConfigureNativeLibraryPath()
    {
        var platformDirectory = GetNativePlatformDirectory();
        var streamingAssetsPath = Path.Combine(
            Application.streamingAssetsPath,
            "FFmpeg",
            platformDirectory);
        var applicationPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "FFmpeg",
            platformDirectory);

        string rootPath;
        if (Directory.Exists(streamingAssetsPath))
            rootPath = streamingAssetsPath;
        else if (Directory.Exists(applicationPath))
            rootPath = applicationPath;
        else
            throw new DirectoryNotFoundException(
                $"FFmpeg runtime for {platformDirectory} was not found. " +
                $"Run ffmpeg-builder/build.ps1 on the target platform.");

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        // FFmpeg built by MSYS2 uses two POSIX timing functions from winpthreads.
        // AutoGen loads avutil by name, so preload this dependency by absolute path first.
        if (_winPthreadHandle == IntPtr.Zero)
        {
            var dependencyPath = Path.Combine(rootPath, "libwinpthread-1.dll");
            _winPthreadHandle = LoadLibraryW(dependencyPath);
            if (_winPthreadHandle == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Could not load FFmpeg dependency: {dependencyPath} " +
                    $"(Win32 error {Marshal.GetLastWin32Error()}).");
        }
#endif

        ffmpeg.RootPath = rootPath;
    }

    private static string GetNativePlatformDirectory()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        return "win-x64";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        return "osx";
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        return "linux-x64";
#else
        throw new PlatformNotSupportedException(
            "Offline FFmpeg recording is supported only on Windows, macOS, and Linux x64.");
#endif
    }

    private static void Check(int result, string operation)
    {
        if (result >= 0)
            return;

        const int bufferSize = 1024;
        var buffer = stackalloc byte[bufferSize];
        ffmpeg.av_strerror(result, buffer, bufferSize);
        var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
        throw new InvalidOperationException(
            $"FFmpeg could not {operation}: {message ?? "unknown error"} ({result}).");
    }
}

using System;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using UnityEngine;

public static unsafe class FFmpegVideoEncoder
{
    // Video
    static AVFormatContext* _fmtCtx;
    static AVCodecContext* _videoCtx;
    static AVFrame* _videoFrame;
    static AVPacket* _pkt;
    static SwsContext* _swsCtx;
    static int _videoStreamIndex;
    static int _width;
    static int _height;
    static int _videoFrameCount;

    // Audio
    static AVCodecContext* _audioCtx;
    static AVFrame* _audioFrame;
    static int _audioStreamIndex;
    static int _audioFrameSize;
    static int _audioSampleCount;
    static float[] _audioSampleBuffer;
    static int _audioBufferPos;

    public static bool IsInitialized => _fmtCtx != null;

    static void InitRootPath()
    {
        // Try StreamingAssets/FFmpeg first, then application directory
        var saPath = Path.Combine(Application.streamingAssetsPath, "FFmpeg");
        var appPath = AppDomain.CurrentDomain.BaseDirectory;

        if (Directory.Exists(saPath))
        {
            ffmpeg.RootPath = saPath;
            Debug.Log($"[FFmpegVideoEncoder] RootPath={saPath}");
        }
        else if (Directory.Exists(Path.Combine(appPath, "FFmpeg")))
        {
            ffmpeg.RootPath = Path.Combine(appPath, "FFmpeg");
            Debug.Log($"[FFmpegVideoEncoder] RootPath={ffmpeg.RootPath} (from app dir)");
        }
        else
        {
            ffmpeg.RootPath = appPath;
            Debug.LogWarning($"[FFmpegVideoEncoder] FFmpeg dir not found, using app dir: {appPath}");
        }
    }

    public static void Init(string outputPath, int width, int height, int fps,
        int sampleRate, int channels)
    {
        _width = width;
        _height = height;
        _videoFrameCount = 0;
        _audioSampleCount = 0;
        _audioBufferPos = 0;

        InitRootPath();

        // --- Output format context (mp4) ---
        AVFormatContext* pFmtCtx = null;
        var ret = ffmpeg.avformat_alloc_output_context2(&pFmtCtx, null, null, outputPath);
        _fmtCtx = pFmtCtx;
        if (ret < 0) throw new Exception($"avformat_alloc_output_context2 failed: {ret}");
        Debug.Log($"[FFmpegVideoEncoder] Output format: {Marshal.PtrToStringAnsi((IntPtr)_fmtCtx->oformat->name)}");

        // --- Video: try H.264 → mpeg4 → mjpeg ---
        AVCodecID[] videoCodecs = {
            AVCodecID.AV_CODEC_ID_H264,
            AVCodecID.AV_CODEC_ID_MPEG4,
            AVCodecID.AV_CODEC_ID_MJPEG,
        };
        AVCodec* videoCodec = null;
        string videoCodecName = null;
        foreach (var id in videoCodecs)
        {
            videoCodec = ffmpeg.avcodec_find_encoder(id);
            if (videoCodec != null)
            {
                videoCodecName = Marshal.PtrToStringAnsi((IntPtr)videoCodec->name);
                Debug.Log($"[FFmpegVideoEncoder] Found video encoder: {videoCodecName}");
                break;
            }
        }
        if (videoCodec == null)
        {
            throw new Exception("No video encoder found (H.264/MPEG4/MJPEG). Check FFmpeg DLLs in StreamingAssets/FFmpeg/");
        }

        bool isH264 = videoCodec->id == AVCodecID.AV_CODEC_ID_H264;

        var videoStream = ffmpeg.avformat_new_stream(_fmtCtx, null);
        _videoStreamIndex = videoStream->index;

        _videoCtx = ffmpeg.avcodec_alloc_context3(videoCodec);
        _videoCtx->width = width;
        _videoCtx->height = height;
        _videoCtx->time_base = new AVRational { num = 1, den = fps };
        _videoCtx->framerate = new AVRational { num = fps, den = 1 };
        _videoCtx->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
        _videoCtx->codec_tag = 0;
        _videoCtx->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

        if (isH264)
        {
            ffmpeg.av_opt_set(_videoCtx->priv_data, "preset", "veryfast", 0);
            ffmpeg.av_opt_set(_videoCtx->priv_data, "crf", "18", 0);
        }
        else
        {
            _videoCtx->bit_rate = 4000000;
        }

        ret = ffmpeg.avcodec_parameters_from_context(videoStream->codecpar, _videoCtx);
        if (ret < 0) throw new Exception($"avcodec_parameters_from_context (video) failed: {ret}");
        ret = ffmpeg.avcodec_open2(_videoCtx, videoCodec, null);
        if (ret < 0) throw new Exception($"avcodec_open2 ({videoCodecName}) failed: {ret}");

        _videoFrame = ffmpeg.av_frame_alloc();
        _videoFrame->format = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;
        _videoFrame->width = width;
        _videoFrame->height = height;
        ret = ffmpeg.av_frame_get_buffer(_videoFrame, 32);
        if (ret < 0) throw new Exception($"av_frame_get_buffer (video) failed: {ret}");

        _swsCtx = ffmpeg.sws_getContext(
            width, height, AVPixelFormat.AV_PIX_FMT_RGBA,
            width, height, AVPixelFormat.AV_PIX_FMT_YUV420P,
            (int)SwsFlags.SWS_FAST_BILINEAR, null, null, null);
        if (_swsCtx == null) throw new Exception("sws_getContext failed");

        // --- Audio: AAC → pcm_s16le ---
        var audioCodec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_AAC);
        bool useAac = audioCodec != null;
        AVSampleFormat audioFmt;
        int audioBitrate;
        if (useAac)
        {
            Debug.Log("[FFmpegVideoEncoder] Audio codec: AAC");
            audioFmt = AVSampleFormat.AV_SAMPLE_FMT_FLTP;
            audioBitrate = 320000;
        }
        else
        {
            Debug.LogWarning("[FFmpegVideoEncoder] AAC not available, using PCM");
            audioCodec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_PCM_S16LE);
            if (audioCodec == null) throw new Exception("AAC and PCM-S16LE encoders not found");
            audioFmt = AVSampleFormat.AV_SAMPLE_FMT_S16;
            audioBitrate = 0;
        }

        var audioStream = ffmpeg.avformat_new_stream(_fmtCtx, null);
        _audioStreamIndex = audioStream->index;

        _audioCtx = ffmpeg.avcodec_alloc_context3(audioCodec);
        _audioCtx->sample_rate = sampleRate;
        _audioCtx->sample_fmt = audioFmt;
        _audioCtx->ch_layout.nb_channels = channels;
        ffmpeg.av_channel_layout_default(&_audioCtx->ch_layout, channels);
        _audioCtx->bit_rate = audioBitrate;
        _audioCtx->time_base = new AVRational { num = 1, den = sampleRate };
        _audioCtx->codec_tag = 0;
        _audioCtx->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

        ret = ffmpeg.avcodec_parameters_from_context(audioStream->codecpar, _audioCtx);
        if (ret < 0) throw new Exception($"avcodec_parameters_from_context (audio) failed: {ret}");
        ret = ffmpeg.avcodec_open2(_audioCtx, audioCodec, null);
        if (ret < 0) throw new Exception($"avcodec_open2 (audio) failed: {ret}");

        _audioFrameSize = _audioCtx->frame_size;
        if (_audioFrameSize <= 0) _audioFrameSize = 1024;
        _audioSampleBuffer = new float[_audioFrameSize * channels];

        _audioFrame = ffmpeg.av_frame_alloc();
        _audioFrame->format = (int)audioFmt;
        _audioFrame->nb_samples = _audioFrameSize;
        _audioFrame->ch_layout = _audioCtx->ch_layout;
        ret = ffmpeg.av_frame_get_buffer(_audioFrame, 0);
        if (ret < 0) throw new Exception($"av_frame_get_buffer (audio) failed: {ret}");

        // --- Packet ---
        _pkt = ffmpeg.av_packet_alloc();

        // --- Open file ---
        if ((_fmtCtx->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0)
        {
            ret = ffmpeg.avio_open(&_fmtCtx->pb, outputPath, ffmpeg.AVIO_FLAG_WRITE);
            if (ret < 0) throw new Exception($"avio_open failed: {ret}");
        }

        ffmpeg.av_opt_set(_fmtCtx, "movflags", "faststart", 0);
        ret = ffmpeg.avformat_write_header(_fmtCtx, null);
        if (ret < 0) throw new Exception($"avformat_write_header failed: {ret}");

        Debug.Log($"[FFmpegVideoEncoder] Init OK: {width}x{height}@{fps}fps, {sampleRate}Hz {channels}ch, video={videoCodecName}, audio={(useAac ? "AAC" : "PCM")}");
    }

    // ======================== VIDEO ========================

    public static void WriteVideoFrame(byte[] rgbaData)
    {
        ffmpeg.av_frame_make_writable(_videoFrame);

        var stride = _width * 4;
        fixed (byte* pData = rgbaData)
        {
            var srcData = new byte_ptrArray4();
            srcData[0] = pData + (_height - 1) * stride;
            var srcLinesize = new int_array4();
            srcLinesize[0] = -stride;

            var dstData = new byte_ptrArray4();
            dstData[0] = _videoFrame->data[0];
            dstData[1] = _videoFrame->data[1];
            dstData[2] = _videoFrame->data[2];
            var dstLinesize = new int_array4();
            dstLinesize[0] = _videoFrame->linesize[0];
            dstLinesize[1] = _videoFrame->linesize[1];
            dstLinesize[2] = _videoFrame->linesize[2];

            ffmpeg.sws_scale(_swsCtx, srcData, srcLinesize, 0,
                _height, dstData, dstLinesize);
        }

        _videoFrame->pts = _videoFrameCount++;

        var ret = ffmpeg.avcodec_send_frame(_videoCtx, _videoFrame);
        if (ret < 0) throw new Exception($"avcodec_send_frame (video) failed: {ret}");

        ReceiveVideoPackets();

        if (_videoFrameCount % 100 == 0)
            Debug.Log($"[FFmpegVideoEncoder] Video: {_videoFrameCount} frames, Audio: {_audioSampleCount} floats");
    }

    static void ReceiveVideoPackets()
    {
        while (true)
        {
            var ret = ffmpeg.avcodec_receive_packet(_videoCtx, _pkt);
            if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF)
                break;
            if (ret < 0) throw new Exception($"avcodec_receive_packet (video) failed: {ret}");

            _pkt->stream_index = _videoStreamIndex;
            ffmpeg.av_packet_rescale_ts(_pkt, _videoCtx->time_base,
                _fmtCtx->streams[_videoStreamIndex]->time_base);
            ret = ffmpeg.av_write_frame(_fmtCtx, _pkt);
            if (ret < 0) Debug.LogError($"[FFmpegVideoEncoder] av_write_frame (video) failed: {ret}");
        }
    }

    // ======================== AUDIO ========================

    public static void WriteAudioSamples(float[] samples, int sampleCount)
    {
        var channels = _audioCtx->ch_layout.nb_channels;
        int written = 0;

        while (written < sampleCount)
        {
            var space = _audioSampleBuffer.Length - _audioBufferPos;
            var toCopy = Math.Min(sampleCount - written, space);
            Array.Copy(samples, written, _audioSampleBuffer, _audioBufferPos, toCopy);
            _audioBufferPos += toCopy;
            written += toCopy;

            if (_audioBufferPos >= _audioSampleBuffer.Length)
            {
                EncodeAudioFrame();
                _audioBufferPos = 0;
            }
        }
    }

    static void EncodeAudioFrame()
    {
        var channels = _audioCtx->ch_layout.nb_channels;
        ffmpeg.av_frame_make_writable(_audioFrame);

        if (_audioCtx->sample_fmt == AVSampleFormat.AV_SAMPLE_FMT_FLTP)
        {
            // Deinterleave: interleaved [L0,R0,L1,R1] → planar [L0,L1,...][R0,R1,...]
            fixed (float* pInterleaved = _audioSampleBuffer)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    var plane = (float*)_audioFrame->data[(uint)ch];
                    for (int i = 0; i < _audioFrameSize; i++)
                        plane[i] = pInterleaved[i * channels + ch];
                }
            }
        }
        else
        {
            // PCM_S16LE: convert float [-1,1] to int16 interleaved
            fixed (float* pInterleaved = _audioSampleBuffer)
            {
                var pOut = (short*)_audioFrame->data[0];
                for (int i = 0; i < _audioFrameSize * channels; i++)
                {
                    float s = Math.Clamp(pInterleaved[i], -1f, 1f);
                    pOut[i] = (short)(s * 32767f);
                }
            }
        }

        _audioFrame->pts = _audioSampleCount / channels;
        _audioSampleCount += _audioFrameSize * channels;

        var ret = ffmpeg.avcodec_send_frame(_audioCtx, _audioFrame);
        if (ret < 0) throw new Exception($"avcodec_send_frame (audio) failed: {ret}");

        ReceiveAudioPackets();
    }

    static void ReceiveAudioPackets()
    {
        while (true)
        {
            var ret = ffmpeg.avcodec_receive_packet(_audioCtx, _pkt);
            if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF)
                break;
            if (ret < 0) throw new Exception($"avcodec_receive_packet (audio) failed: {ret}");

            _pkt->stream_index = _audioStreamIndex;
            ffmpeg.av_packet_rescale_ts(_pkt, _audioCtx->time_base,
                _fmtCtx->streams[_audioStreamIndex]->time_base);
            ret = ffmpeg.av_write_frame(_fmtCtx, _pkt);
            if (ret < 0) Debug.LogError($"[FFmpegVideoEncoder] av_write_frame (audio) failed: {ret}");
        }
    }

    // ======================== FLUSH & DISPOSE ========================

    public static void Dispose()
    {
        if (_fmtCtx == null) return;

        Debug.Log($"[FFmpegVideoEncoder] Flushing: {_videoFrameCount} video, {_audioSampleCount} audio...");

        // Flush remaining audio samples
        if (_audioBufferPos > 0 && _audioFrame != null)
        {
            Array.Clear(_audioSampleBuffer, _audioBufferPos,
                _audioSampleBuffer.Length - _audioBufferPos);
            EncodeAudioFrame();
        }

        // Flush video encoder
        if (_videoCtx != null)
        {
            ffmpeg.avcodec_send_frame(_videoCtx, null);
            ReceiveVideoPackets();
        }

        // Flush audio encoder
        if (_audioCtx != null)
        {
            ffmpeg.avcodec_send_frame(_audioCtx, null);
            ReceiveAudioPackets();
        }

        // Write trailer
        ffmpeg.av_write_trailer(_fmtCtx);
        Debug.Log("[FFmpegVideoEncoder] Trailer written.");

        // Free resources
        if (_pkt != null) { var p = _pkt; ffmpeg.av_packet_free(&p); _pkt = null; }
        if (_videoFrame != null) { var p = _videoFrame; ffmpeg.av_frame_free(&p); _videoFrame = null; }
        if (_audioFrame != null) { var p = _audioFrame; ffmpeg.av_frame_free(&p); _audioFrame = null; }
        if (_swsCtx != null) { ffmpeg.sws_freeContext(_swsCtx); _swsCtx = null; }
        if (_videoCtx != null) { var p = _videoCtx; ffmpeg.avcodec_free_context(&p); _videoCtx = null; }
        if (_audioCtx != null) { var p = _audioCtx; ffmpeg.avcodec_free_context(&p); _audioCtx = null; }
        if (_fmtCtx != null)
        {
            if ((_fmtCtx->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0)
            {
                var pb = _fmtCtx->pb; ffmpeg.avio_closep(&pb);
            }
            ffmpeg.avformat_free_context(_fmtCtx);
            _fmtCtx = null;
        }

        _videoFrameCount = 0;
        _audioSampleCount = 0;
        _audioBufferPos = 0;
        _audioSampleBuffer = null;
        Debug.Log("[FFmpegVideoEncoder] Disposed.");
    }
}

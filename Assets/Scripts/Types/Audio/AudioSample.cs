#region

using ManagedBass;
using ManagedBass.Mix;
using System;

#endregion

public class AudioSample : IDisposable
{
    public SampleType SampleType { get; set; }

    /// <summary>
    /// 当前可操作的通道句柄
    /// Stream模式下 = Stream Handle
    /// Sample模式下 = 最近一次获取的播放通道
    /// </summary>
    public int Decode { get; private set; }

    public AudioMode Mode { get; }

    private readonly int _handle;
    private readonly int _mixerHandle;
    private readonly int[] _voiceHandles = Array.Empty<int>();
    private int _nextVoice;
    private float _volume;
    private readonly double _length;
    private bool UsesMixer => _mixerHandle != 0;

    public double CurrentSec
    {
        get
        {
            EnsureStream(nameof(CurrentSec));

            return Bass.ChannelBytes2Seconds(
                Decode,
                Bass.ChannelGetPosition(Decode));
        }
        set
        {
            EnsureStream(nameof(CurrentSec));

            Bass.ChannelSetPosition(
                Decode,
                Bass.ChannelSeconds2Bytes(Decode, value));
        }
    }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 2f);

            if (UsesMixer)
            {
                foreach (var voice in _voiceHandles)
                    Bass.ChannelSetAttribute(
                        voice,
                        ChannelAttribute.Volume,
                        _volume);
            }
            else if (Decode != 0)
            {
                Bass.ChannelSetAttribute(
                    Decode,
                    ChannelAttribute.Volume,
                    _volume);
            }
        }
    }

    private float _baseFrequency;

    public float Speed
    {
        get =>
            (float)Bass.ChannelGetAttribute(
                Decode,
                ChannelAttribute.Frequency) / _baseFrequency;

        set =>
            Bass.ChannelSetAttribute(
                Decode,
                ChannelAttribute.Frequency,
                _baseFrequency * value);
    }

    public double Length
    {
        get
        {
            return _length;
        }
    }

    public PlaybackState State
    {
        get
        {
            if (!UsesMixer)
                return Bass.ChannelIsActive(Decode);
            if (BassMix.ChannelGetMixer(Decode) == 0)
                return PlaybackState.Stopped;
            if (BassMix.ChannelHasFlag(Decode, BassFlags.MixerChanPause))
                return PlaybackState.Paused;
            return Bass.ChannelIsActive(Decode);
        }
    }

    public bool IsPlaying => State == PlaybackState.Playing;

    public AudioSample(string file, AudioMode mode, int max = 1, int mixerHandle = 0)
    {
        Mode = mode;
        _mixerHandle = mixerHandle;

        if (UsesMixer)
        {
            // Mixer sources must be decoding streams. Keep a small pre-created
            // voice pool so triggering an SFX never opens or decodes a file.
            var voiceCount = mode == AudioMode.Stream
                ? 1
                : Math.Clamp(max, 1, 32);
            _voiceHandles = new int[voiceCount];
            for (var i = 0; i < voiceCount; i++)
            {
                var voice = Bass.CreateStream(
                    file,
                    0,
                    0,
                    BassFlags.Decode | BassFlags.Float | BassFlags.Prescan);
                if (voice == 0)
                    throw new InvalidOperationException(
                        $"Could not create BASS mixer source '{file}': {Bass.LastError}");
                _voiceHandles[i] = voice;
            }

            _handle = _voiceHandles[0];
            Decode = _handle;
        }
        else if (mode == AudioMode.Stream)
        {
            _handle = Bass.CreateStream(file, 0, 0, BassFlags.Prescan);
            Decode = _handle;

            // Decode local tracks on demand instead of keeping another stream
            // buffer in front of the already-buffered output device.
            Bass.ChannelSetAttribute(Decode, ChannelAttribute.Buffer, 0);

            _length = Bass.ChannelBytes2Seconds(
                Decode,
                Bass.ChannelGetLength(Decode));
        }
        else
        {
            _handle = Bass.SampleLoad(file, 0, 0, max, BassFlags.SampleOverrideLongestPlaying);
            Decode = Bass.SampleGetChannel(_handle);
            _voiceHandles = Array.Empty<int>();
        }

        _length = Bass.ChannelBytes2Seconds(
            Decode,
            Bass.ChannelGetLength(Decode));
        _baseFrequency =
            (float)Bass.ChannelGetAttribute(
                Decode,
                ChannelAttribute.Frequency);
    }

    public void Play()
    {
        if (UsesMixer)
        {
            var resumedAny = false;
            foreach (var voice in _voiceHandles)
            {
                if (BassMix.ChannelGetMixer(voice) == 0)
                    continue;
                if (Bass.ChannelIsActive(voice) == PlaybackState.Stopped)
                {
                    BassMix.MixerRemoveChannel(voice);
                    continue;
                }
                BassMix.ChannelRemoveFlag(voice, BassFlags.MixerChanPause);
                resumedAny = true;
            }

            if (!resumedAny)
            {
                if (Mode == AudioMode.Stream)
                {
                    Bass.ChannelSetPosition(Decode, 0);
                    AddMixerVoice(Decode);
                }
                else
                    PlayOneShot();
            }
            return;
        }

        if (Mode == AudioMode.Stream)
        {
            Bass.ChannelPlay(Decode);
        }
        else
        {
            var channels = Bass.SampleGetChannels(_handle);
            if (channels != null && channels.Length > 0)
            {
                foreach (var ch in channels)
                    Bass.ChannelPlay(ch, false);
            }
            else
            {
                PlayOneShot();
            }
        }
    }

    public void Pause()
    {
        if (UsesMixer)
        {
            foreach (var voice in _voiceHandles)
            {
                if (BassMix.ChannelGetMixer(voice) != 0)
                    BassMix.ChannelAddFlag(voice, BassFlags.MixerChanPause);
            }
            return;
        }

        if (Mode == AudioMode.Sample)
        {
            var channels = Bass.SampleGetChannels(_handle);
            if (channels != null)
            {
                foreach (var ch in channels)
                    Bass.ChannelPause(ch);
            }
        }
        else
        {
            Bass.ChannelPause(Decode);
        }
    }

    public void Stop()
    {
        if (UsesMixer)
        {
            foreach (var voice in _voiceHandles)
            {
                if (BassMix.ChannelGetMixer(voice) != 0)
                    BassMix.MixerRemoveChannel(voice);
                Bass.ChannelSetPosition(voice, 0);
            }
            return;
        }

        if (Mode == AudioMode.Sample)
            Bass.SampleStop(_handle);
        else
            Bass.ChannelStop(Decode);
    }

    public void PlayOneShot()
    {
        if (UsesMixer)
        {
            var voice = 0;
            foreach (var candidate in _voiceHandles)
            {
                if (BassMix.ChannelGetMixer(candidate) == 0 ||
                    Bass.ChannelIsActive(candidate) == PlaybackState.Stopped)
                {
                    voice = candidate;
                    break;
                }
            }

            if (voice == 0)
            {
                voice = _voiceHandles[_nextVoice];
                _nextVoice = (_nextVoice + 1) % _voiceHandles.Length;
            }

            if (BassMix.ChannelGetMixer(voice) != 0)
                BassMix.MixerRemoveChannel(voice);

            Decode = voice;
            Bass.ChannelSetPosition(voice, 0);
            Bass.ChannelSetAttribute(voice, ChannelAttribute.Volume, _volume);
            AddMixerVoice(voice);
            return;
        }

        if (Mode == AudioMode.Stream)
        {
            Bass.ChannelSetPosition(Decode, 0);
            Bass.ChannelPlay(Decode, true);
        }
        else
        {
            Decode = Bass.SampleGetChannel(_handle);
            Bass.ChannelSetAttribute(
                Decode,
                ChannelAttribute.Volume,
                _volume);
            Bass.ChannelPlay(Decode, true);
        }
    }

    public void Dispose()
    {
        if (UsesMixer)
        {
            foreach (var voice in _voiceHandles)
                Bass.StreamFree(voice);
        }
        else if (Mode == AudioMode.Stream)
            Bass.StreamFree(_handle);
        else
            Bass.SampleFree(_handle);
    }

    private void AddMixerVoice(int voice)
    {
        BassMix.MixerAddChannel(
            _mixerHandle,
            voice,
            BassFlags.MixerChanNoRampin);
    }

    private void EnsureStream(string memberName)
    {
        if (Mode == AudioMode.Sample)
        {
            throw new NotSupportedException(
                $"{memberName} is not supported in Sample mode.");
        }
    }
}

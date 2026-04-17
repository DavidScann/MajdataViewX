using System;
using ManagedBass;

public class AudioSample
{
    public SampleType SampleType { get; set; }
    public int Decode { get; private set; }
    
    private double _length;
    
    public bool IsLoop
    {
        get => Bass.ChannelHasFlag(Decode, BassFlags.Loop);
        
        set
        {
            if (value)
            {
                if (!Bass.ChannelHasFlag(Decode, BassFlags.Loop))
                {
                    Bass.ChannelAddFlag(Decode, BassFlags.Loop);
                }
            }
            else
            {
                if (Bass.ChannelHasFlag(Decode, BassFlags.Loop))
                {
                    Bass.ChannelRemoveFlag(Decode, BassFlags.Loop);
                }
            }
        }
    }
    public double CurrentSec
    {
        get => Bass.ChannelBytes2Seconds(Decode, Bass.ChannelGetPosition(Decode));
        set => Bass.ChannelSetPosition(Decode, Bass.ChannelSeconds2Bytes(Decode, value));
    }
    public float Volume
    {
        get => (float)Bass.ChannelGetAttribute(Decode, ChannelAttribute.Volume);
        set
        {
            var volume = value.Clamp(0, 2);
            Bass.ChannelSetAttribute(Decode, ChannelAttribute.Volume, volume);
        }
    }
    public float Speed 
    {
        get => (float)Bass.ChannelGetAttribute(Decode, ChannelAttribute.Tempo) / 100f + 1f;
        set => Bass.ChannelSetAttribute(Decode, ChannelAttribute.Tempo, (value - 1) * 100f);
    }

    public double Length => _length;

    public AudioSample(string file)
    {
        Decode = Bass.CreateStream(file);
        _length = Bass.ChannelBytes2Seconds(Decode, Bass.ChannelGetLength(Decode));
    }

    public void Play()
    {
        Bass.ChannelPlay(Decode);
    }

    public void Pause()
    {
        Bass.ChannelPause(Decode);
    }

    public void Stop()
    {
        Bass.ChannelStop(Decode);
    }

    public void PlayOneShot()
    {
        Bass.ChannelSetPosition(Decode, 0);
        Bass.ChannelPlay(Decode);
    }

    public void Dispose()
    {
        Bass.StreamFree(Decode);
    }
}
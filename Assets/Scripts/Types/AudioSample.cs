using System;
using ManagedBass;

public class AudioSample
{
    public SampleType SampleType { get; set; }
    
    public bool IsLoop
    {
        get => Bass.ChannelHasFlag(decode, BassFlags.Loop);
        
        set
        {
            if (value)
            {
                if (!Bass.ChannelHasFlag(decode, BassFlags.Loop))
                {
                    Bass.ChannelAddFlag(decode, BassFlags.Loop);
                }
            }
            else
            {
                if (Bass.ChannelHasFlag(decode, BassFlags.Loop))
                {
                    Bass.ChannelRemoveFlag(decode, BassFlags.Loop);
                }
            }
        }
    }
    public double CurrentSec
    {
        get => Bass.ChannelBytes2Seconds(decode, Bass.ChannelGetPosition(decode));
        set => Bass.ChannelSetPosition(decode, Bass.ChannelSeconds2Bytes(decode, value));
    }
    public float Volume
    {
        get => (float)Bass.ChannelGetAttribute(decode, ChannelAttribute.Volume);
        set
        {
            var volume = value.Clamp(0, 2);
            Bass.ChannelSetAttribute(decode, ChannelAttribute.Volume, volume);
        }
    }
    public float Speed 
    {
        get => (float)Bass.ChannelGetAttribute(decode, ChannelAttribute.Tempo) / 100f + 1f;
        set => Bass.ChannelSetAttribute(decode, ChannelAttribute.Tempo, (value - 1) * 100f);
    }
    
    
    private int decode;
    public AudioSample(string file)
    {
        decode = Bass.CreateStream(file);
    }

    public void Play()
    {
        Bass.ChannelPlay(decode);
    }

    public void Pause()
    {
        Bass.ChannelPause(decode);
    }

    public void Stop()
    {
        Bass.ChannelStop(decode);
    }

    public void PlayOneShot()
    {
        Bass.ChannelSetPosition(decode, 0);
        Bass.ChannelPlay(decode);
    }

    public void Dispose()
    {
        Bass.StreamFree(decode);
    }
}
using System;
using System.IO;

public static class WavFileWriter
{
    public static void WriteFile(string filePath, int sampleRate, int channels, float[] dataSource)
    {
        using var fs = new FileStream(filePath, FileMode.Create);
        using var bw = new BinaryWriter(fs);
        
        bw.Write("RIFF".ToCharArray());
        bw.Write(36 + dataSource.Length * 2);
        bw.Write("WAVE".ToCharArray());
        bw.Write("fmt ".ToCharArray());
        bw.Write(16); // Chunk size
        bw.Write((short)1); // PCM
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * channels * 2);
        bw.Write((short)(channels * 2));
        bw.Write((short)16);
        bw.Write("data".ToCharArray());
        bw.Write(dataSource.Length * 2);

        foreach (float sample in dataSource)
        {
            short s = (short)(Math.Clamp(sample, -1f, 1f) * 32767);
            bw.Write(s);
        }
    }
}
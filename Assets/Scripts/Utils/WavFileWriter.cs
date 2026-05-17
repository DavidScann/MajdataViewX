#region

using System;
using System.IO;

#endregion

public static class WavFileWriter
{
    public static void WriteFile(string filePath, int sampleRate, int channels, float[] dataSource)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
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

        var buffer = new byte[dataSource.Length * 2];
        for (int i = 0; i < dataSource.Length; i++)
        {
            short s = (short)(Math.Clamp(dataSource[i], -1f, 1f) * 32767);
            buffer[i * 2] = (byte)s;
            buffer[i * 2 + 1] = (byte)(s >> 8);
        }
        bw.Write(buffer);
    }
}
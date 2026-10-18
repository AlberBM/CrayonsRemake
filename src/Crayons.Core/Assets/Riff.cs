namespace Crayons.Core.Assets;

/// <summary>Minimal format sniffing for the renamed .ASL files (BMP or RIFF WAVE).</summary>
public static class Riff
{
    public enum Kind { Unknown, Bitmap, Wave, Midi, Text }

    /// <summary>Playing time of a RIFF WAVE image in milliseconds (0 when it cannot be read).</summary>
    public static int WaveDurationMs(ReadOnlySpan<byte> data)
    {
        if (Sniff(data) != Kind.Wave) return 0;
        int byteRate = 0, dataLen = 0;
        for (int p = 12; p + 8 <= data.Length;)
        {
            int len = BitConverter.ToInt32(data[(p + 4)..]);
            if (data.Slice(p, 4).SequenceEqual("fmt "u8) && p + 20 <= data.Length) byteRate = BitConverter.ToInt32(data[(p + 16)..]);
            if (data.Slice(p, 4).SequenceEqual("data"u8)) dataLen = len;
            if (len < 0) break;
            p += 8 + len + (len & 1);
        }
        return byteRate > 0 ? (int)(dataLen * 1000L / byteRate) : 0;
    }

    public static Kind Sniff(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 2 && data[0] == 'B' && data[1] == 'M') return Kind.Bitmap;
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WAVE"u8)) return Kind.Wave;
        if (data.Length >= 4 && data[..4].SequenceEqual("MThd"u8)) return Kind.Midi;
        return data.Length > 0 && (data[0] == '\r' || (data[0] >= 0x20 && data[0] < 0x7f)) ? Kind.Text : Kind.Unknown;
    }
}

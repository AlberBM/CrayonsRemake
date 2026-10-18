using System.Text;

namespace Crayons.Core.Assets;

/// <summary>One component in a Delphi form: class, name, properties and children.</summary>
public sealed class DfmObject
{
    public required string ClassName { get; init; }
    public required string Name { get; init; }
    public Dictionary<string, object?> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<DfmObject> Children { get; } = new();

    public int Int(string prop, int fallback = 0) => Properties.TryGetValue(prop, out var v) && v is int i ? i : fallback;

    public DfmObject? Child(string name) =>
        Children.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Left/Top/Width/Height of this component.</summary>
    public (int X, int Y, int W, int H) Bounds => (Int("Left"), Int("Top"), Int("Width"), Int("Height"));

    /// <summary>
    /// The BMP file bytes of a TImage's Picture.Data (stored as ShortString class name
    /// "TBitmap", Int32 size, then a complete BMP file), or null.
    /// </summary>
    public byte[]? PictureBitmap()
    {
        if (!Properties.TryGetValue("Picture.Data", out var v) || v is not byte[] data || data.Length < 6) return null;
        int n = data[0];
        if (Encoding.Latin1.GetString(data, 1, n) != "TBitmap") return null;
        int size = BitConverter.ToInt32(data, 1 + n);
        return data.AsSpan(5 + n, Math.Min(size, data.Length - 5 - n)).ToArray();
    }

    /// <summary>
    /// A TImage's Picture.Data holding a TIcon (ShortString "TIcon" followed by a complete .ico
    /// file): its first image decoded, or null.
    /// </summary>
    public DibImage? PictureIcon()
    {
        if (!Properties.TryGetValue("Picture.Data", out var v) || v is not byte[] data || data.Length < 6) return null;
        int n = data[0];
        if (Encoding.Latin1.GetString(data, 1, n) != "TIcon") return null;
        var ico = data.AsSpan(1 + n);
        if (ico.Length < 22 || BitConverter.ToUInt16(ico[4..]) == 0) return null;
        int offset = BitConverter.ToInt32(ico[18..]);
        return offset > 0 && offset < ico.Length ? DibImage.FromIconDib(ico[offset..]) : null;
    }
}

/// <summary>Parser for Delphi 1 binary form streams ("TPF0").</summary>
public static class Dfm
{
    public static DfmObject Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 || !data[..4].SequenceEqual("TPF0"u8)) throw new InvalidDataException("Not a TPF0 form stream");
        var r = new Reader(data.ToArray()) { Pos = 4 };
        return r.Component();
    }

    sealed class Reader(byte[] d)
    {
        public int Pos;

        byte U8() => d[Pos++];
        string Str() { int n = U8(); var s = Encoding.Latin1.GetString(d, Pos, n); Pos += n; return s; }

        public DfmObject Component()
        {
            var obj = new DfmObject { ClassName = Str(), Name = Str() };
            while (d[Pos] != 0) { var prop = Str(); obj.Properties[prop] = Value(); }
            Pos++;
            while (d[Pos] != 0) obj.Children.Add(Component());
            Pos++;
            return obj;
        }

        object? Value()
        {
            byte t = U8();
            switch (t)
            {
                case 0: return null;
                case 1: { var list = new List<object?>(); while (d[Pos] != 0) list.Add(Value()); Pos++; return list; }
                case 2: return (int)(sbyte)U8();
                case 3: { int v = BitConverter.ToInt16(d, Pos); Pos += 2; return v; }
                case 4: { int v = BitConverter.ToInt32(d, Pos); Pos += 4; return v; }
                case 5: { // 80-bit extended
                    ulong m = BitConverter.ToUInt64(d, Pos); int e = BitConverter.ToUInt16(d, Pos + 8); Pos += 10;
                    double val = m / Math.Pow(2, 63) * Math.Pow(2, (e & 0x7fff) - 16383);
                    return (e & 0x8000) != 0 ? -val : val;
                }
                case 6: case 7: return Str();
                case 8: return false;
                case 9: return true;
                case 10: { int n = BitConverter.ToInt32(d, Pos); Pos += 4; var b = d.AsSpan(Pos, n).ToArray(); Pos += n; return b; }
                case 11: { var set = new List<string>(); for (; ; ) { var s = Str(); if (s.Length == 0) break; set.Add(s); } return set; }
                case 12: { int n = BitConverter.ToInt32(d, Pos); Pos += 4; var s = Encoding.Latin1.GetString(d, Pos, n); Pos += n; return s; }
                case 13: return null;
                case 14:
                {
                    var items = new List<Dictionary<string, object?>>();
                    while (d[Pos] != 0)
                    {
                        var item = new Dictionary<string, object?>();
                        while (d[Pos] != 0) { var k = Str(); item[k] = Value(); }
                        Pos++;
                        items.Add(item);
                    }
                    Pos++;
                    return items;
                }
                default: throw new InvalidDataException($"Unknown DFM value type {t} at {Pos - 1}");
            }
        }
    }
}

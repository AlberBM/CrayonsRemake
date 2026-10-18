namespace Crayons.Core.Assets;

/// <summary>
/// Reads resources from a 16-bit Windows (NE format) executable: the original
/// CRAYON*.EXE files keep their dialog layouts (Delphi DFM), cursors and some
/// bitmaps as resources.
/// </summary>
public sealed class NeResources
{
    public const ushort RtCursor = 1, RtBitmap = 2, RtIcon = 3, RtString = 6, RtRcData = 10, RtGroupCursor = 12, RtGroupIcon = 14;

    public sealed record Entry(string Type, string Name, int Offset, int Length);

    readonly byte[] _file;
    readonly List<Entry> _entries = new();

    public IReadOnlyList<Entry> Entries => _entries;

    public NeResources(byte[] file)
    {
        _file = file;
        if (file.Length < 0x40 || file[0] != 'M' || file[1] != 'Z') throw new InvalidDataException("Not an MZ executable");
        int ne = BitConverter.ToInt32(file, 0x3c);
        if (file[ne] != 'N' || file[ne + 1] != 'E') throw new InvalidDataException("Not an NE executable");
        int rt = ne + BitConverter.ToUInt16(file, ne + 0x24);
        int shift = BitConverter.ToUInt16(file, rt);
        string Name(ushort id) => (id & 0x8000) != 0
            ? (id & 0x7fff).ToString()
            : System.Text.Encoding.Latin1.GetString(file, rt + id + 1, file[rt + id]);

        for (int p = rt + 2; ;)
        {
            ushort typeId = BitConverter.ToUInt16(file, p);
            if (typeId == 0) break;
            int count = BitConverter.ToUInt16(file, p + 2);
            string type = (typeId & 0x8000) != 0 ? (typeId & 0x7fff).ToString() : Name(typeId);
            p += 8;
            for (int i = 0; i < count; i++, p += 12)
            {
                int off = BitConverter.ToUInt16(file, p) << shift;
                int len = BitConverter.ToUInt16(file, p + 2) << shift;
                _entries.Add(new Entry(type, Name(BitConverter.ToUInt16(file, p + 6)), off, len));
            }
        }
    }

    public static NeResources Load(string path) => new(File.ReadAllBytes(path));

    public Entry? Find(ushort type, string name) => Find(type.ToString(), name);

    public Entry? Find(string type, string name) =>
        _entries.FirstOrDefault(e => e.Type == type && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    public ReadOnlySpan<byte> Data(Entry e) => _file.AsSpan(e.Offset, Math.Min(e.Length, _file.Length - e.Offset));

    /// <summary>Returns a Delphi form resource (RCDATA named like "TJUKEBOX").</summary>
    public DfmObject? Form(string className)
    {
        var e = Find(RtRcData, className);
        return e == null ? null : Dfm.Parse(Data(e));
    }

    /// <summary>Builds a standard .ico file image for a named icon group (e.g. "ICON_1"), using its first image.</summary>
    public byte[]? IconFile(string groupName)
    {
        var group = Find(RtGroupIcon, groupName);
        if (group == null) return null;
        var g = Data(group);
        if (BitConverter.ToUInt16(g[4..]) == 0) return null;
        var entry = g.Slice(6, 14);
        int id = BitConverter.ToUInt16(entry[12..]);
        var icon = Find(RtIcon, id.ToString());
        if (icon == null) return null;
        var dib = Data(icon);
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)1);           // ICONDIR: reserved, type=icon, count
        w.Write(entry[..8]);                                                   // w, h, colors, reserved, planes, bitcount
        w.Write(dib.Length); w.Write(6 + 16);
        w.Write(dib);
        return ms.ToArray();
    }

    /// <summary>
    /// Decodes the first image of a named icon group (e.g. "ICON_1"). NE icon
    /// resources are DIBs: a 32x32 XOR plane plus a 1-bit AND mask.
    /// </summary>
    public DibImage? Icon(string groupName)
    {
        var group = Find(RtGroupIcon, groupName);
        if (group == null) return null;
        var g = Data(group);
        if (BitConverter.ToUInt16(g[4..]) == 0) return null;
        int id = BitConverter.ToUInt16(g[(6 + 12)..]);
        var icon = Find(RtIcon, id.ToString());
        return icon == null ? null : DibImage.FromIconDib(Data(icon));
    }

    /// <summary>Decodes the first image of a named cursor group with its hotspot.</summary>
    public (DibImage Image, int HotX, int HotY)? Cursor(string groupName)
    {
        var group = Find(RtGroupCursor, groupName);
        if (group == null) return null;
        var g = Data(group);
        if (BitConverter.ToUInt16(g[4..]) == 0) return null;
        int id = BitConverter.ToUInt16(g[(6 + 12)..]);
        var cur = Find(RtCursor, id.ToString());
        if (cur == null) return null;
        var c = Data(cur);
        return (DibImage.FromIconDib(c[4..]), BitConverter.ToUInt16(c), BitConverter.ToUInt16(c[2..]));
    }

    /// <summary>
    /// Builds a standard .cur file image for a named cursor group so it can be
    /// loaded by the UI layer (Win 3.x NE cursor resources are hotspot + DIB).
    /// </summary>
    public byte[]? CursorFile(string groupName)
    {
        var group = Find(RtGroupCursor, groupName);
        if (group == null) return null;
        var g = Data(group);
        int count = BitConverter.ToUInt16(g[4..]);
        if (count == 0) return null;
        // GRPCURSORDIRENTRY: w, h, planes, bitcount (each WORD), bytes DWORD, id WORD = 14 bytes
        int id = BitConverter.ToUInt16(g[(6 + 12)..]);
        var cur = Find(RtCursor, id.ToString());
        if (cur == null) return null;
        var c = Data(cur);
        int hotX = BitConverter.ToUInt16(c), hotY = BitConverter.ToUInt16(c[2..]);
        var dib = c[4..];
        int width = BitConverter.ToInt32(dib[4..]), height2 = BitConverter.ToInt32(dib[8..]);
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((ushort)0); w.Write((ushort)2); w.Write((ushort)1);            // ICONDIR: reserved, type=cursor, count
        w.Write((byte)width); w.Write((byte)(height2 / 2)); w.Write((byte)0); w.Write((byte)0);
        w.Write((ushort)hotX); w.Write((ushort)hotY);
        w.Write(dib.Length); w.Write(6 + 16);
        w.Write(dib);
        return ms.ToArray();
    }
}

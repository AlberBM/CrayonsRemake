namespace Crayons.Core.Assets;

/// <summary>
/// A decoded Windows icon/cursor image: a device-independent bitmap with an XOR
/// (colour) plane and a 1-bit AND (transparency) mask, as stored in NE ICON and
/// CURSOR resources. Pixels are top-down, colours are 0xRRGGBB.
/// Drawing follows the classic DrawIcon rule: dst = (dst AND mask) XOR colour.
/// </summary>
public sealed class DibImage
{
    public int Width { get; }
    public int Height { get; }
    public int BitCount { get; }

    /// <summary>XOR plane colours, 0xRRGGBB, top-down.</summary>
    public int[] Xor { get; }

    /// <summary>AND mask, top-down; true where the AND bit is 1 (screen kept).</summary>
    public bool[] And { get; }

    /// <summary>True when the XOR plane is 1 bpp (a classic monochrome cursor/icon).</summary>
    public bool IsMonochrome => BitCount == 1;

    DibImage(int w, int h, int bpp, int[] xor, bool[] and)
    {
        Width = w; Height = h; BitCount = bpp; Xor = xor; And = and;
    }

    /// <summary>Parses an icon/cursor DIB: BITMAPINFOHEADER (height doubled), palette, XOR rows, AND rows.</summary>
    public static DibImage FromIconDib(ReadOnlySpan<byte> dib)
    {
        int hdr = BitConverter.ToInt32(dib);
        int w = BitConverter.ToInt32(dib[4..]);
        int h2 = BitConverter.ToInt32(dib[8..]);
        int bpp = BitConverter.ToUInt16(dib[14..]);
        int clrUsed = hdr >= 36 ? BitConverter.ToInt32(dib[32..]) : 0;
        int h = Math.Abs(h2) / 2;
        int palCount = bpp <= 8 ? (clrUsed != 0 ? clrUsed : 1 << bpp) : 0;
        var pal = new int[palCount];
        int p = hdr;
        for (int i = 0; i < palCount; i++, p += 4)
            pal[i] = (dib[p + 2] << 16) | (dib[p + 1] << 8) | dib[p];

        int xorStride = ((w * bpp + 31) / 32) * 4;
        int andStride = ((w + 31) / 32) * 4;
        var xor = new int[w * h];
        var and = new bool[w * h];
        for (int row = 0; row < h; row++)
        {
            int y = h - 1 - row; // bottom-up
            var src = dib.Slice(p + row * xorStride, xorStride);
            for (int x = 0; x < w; x++)
            {
                int c;
                switch (bpp)
                {
                    case 1: c = pal[(src[x >> 3] >> (7 - (x & 7))) & 1]; break;
                    case 4: c = pal[(src[x >> 1] >> ((x & 1) == 0 ? 4 : 0)) & 0xf]; break;
                    case 8: c = pal[src[x]]; break;
                    case 24: c = (src[x * 3 + 2] << 16) | (src[x * 3 + 1] << 8) | src[x * 3]; break;
                    case 32: c = (src[x * 4 + 2] << 16) | (src[x * 4 + 1] << 8) | src[x * 4]; break;
                    default: throw new InvalidDataException($"Unsupported icon bit depth {bpp}");
                }
                xor[y * w + x] = c;
            }
        }
        int andStart = p + h * xorStride;
        for (int row = 0; row < h; row++)
        {
            int y = h - 1 - row;
            int o = andStart + row * andStride;
            for (int x = 0; x < w; x++)
                and[y * w + x] = o + (x >> 3) < dib.Length && ((dib[o + (x >> 3)] >> (7 - (x & 7))) & 1) != 0;
        }
        return new DibImage(w, h, bpp, xor, and);
    }

    /// <summary>Applies the DrawIcon rule to one destination pixel (0xRRGGBB).</summary>
    public int Compose(int x, int y, int dst)
    {
        int i = y * Width + x;
        return ((And[i] ? dst : 0) ^ Xor[i]) & 0xffffff;
    }
}

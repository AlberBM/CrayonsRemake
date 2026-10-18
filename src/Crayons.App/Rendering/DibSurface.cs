using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Crayons.App.Rendering;

/// <summary>
/// A 32-bpp top-down DIB section with its own memory DC. All drawing of the frame
/// and canvas goes through <see cref="Hdc"/> with real GDI calls; <see cref="Pixels"/>
/// gives direct access for per-pixel effects.
/// </summary>
public sealed class DibSurface : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] static extern bool GdiFlush();

    readonly IntPtr _bitmap, _oldBitmap;
    readonly IntPtr _bits;

    public int Width { get; }
    public int Height { get; }
    public IntPtr Hdc { get; }
    public IntPtr HBitmap => _bitmap;

    public DibSurface(int width, int height, int fillRgb = 0xffffff)
    {
        Width = width;
        Height = height;
        var bmi = new BITMAPINFOHEADER { biSize = Marshal.SizeOf<BITMAPINFOHEADER>(), biWidth = width, biHeight = -height, biPlanes = 1, biBitCount = 32 };
        Hdc = CreateCompatibleDC(IntPtr.Zero);
        _bitmap = CreateDIBSection(Hdc, ref bmi, 0, out _bits, IntPtr.Zero, 0);
        if (_bitmap == IntPtr.Zero) throw new InvalidOperationException("CreateDIBSection failed");
        _oldBitmap = Gdi.SelectObject(Hdc, _bitmap);
        Gdi.SetStretchBltMode(Hdc, Gdi.COLORONCOLOR);
        Fill(fillRgb);
    }

    /// <summary>Raw BGRA pixels (top-down). Call <see cref="Flush"/> before reading after GDI drawing.</summary>
    public unsafe Span<uint> Pixels => new((void*)_bits, Width * Height);

    public void Flush() => GdiFlush();

    public void Fill(int rgb)
    {
        Flush();
        uint px = 0xff000000u | (uint)rgb;
        Pixels.Fill(px);
    }

    /// <summary>Pixel as 0xRRGGBB, or -1 outside the surface.</summary>
    public int GetRgb(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return -1;
        Flush();
        return (int)(Pixels[y * Width + x] & 0xffffff);
    }

    public void SetRgb(int x, int y, int rgb)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
        Flush();
        Pixels[y * Width + x] = 0xff000000u | (uint)rgb;
    }

    /// <summary>Copies all of <paramref name="src"/> to (x, y) of this surface.</summary>
    public void Draw(DibSurface src, int x, int y, uint rop = Gdi.SRCCOPY) =>
        Gdi.BitBlt(Hdc, x, y, src.Width, src.Height, src.Hdc, 0, 0, rop);

    public void CopyFrom(DibSurface src) => Gdi.BitBlt(Hdc, 0, 0, Width, Height, src.Hdc, 0, 0, Gdi.SRCCOPY);

    /// <summary>
    /// BitBlt of the source rectangle (sx, sy, w, h) to (x, y), clipped to the source
    /// bitmap so pixels outside it are never read (they leave the destination unchanged).
    /// </summary>
    public void DrawPart(DibSurface src, int x, int y, int sx, int sy, int w, int h)
    {
        if (sx < 0) { x -= sx; w += sx; sx = 0; }
        if (sy < 0) { y -= sy; h += sy; sy = 0; }
        w = Math.Min(w, src.Width - sx);
        h = Math.Min(h, src.Height - sy);
        if (w <= 0 || h <= 0) return;
        Gdi.BitBlt(Hdc, x, y, w, h, src.Hdc, sx, sy, Gdi.SRCCOPY);
    }

    /// <summary>
    /// Draws an icon/cursor image with the DrawIcon rule (dst AND mask) XOR colour,
    /// optionally clipped to destination columns clipX0 &lt;= x &lt; clipX1 and rows y &lt; clipY1.
    /// </summary>
    public void DrawIcon(Crayons.Core.Assets.DibImage img, int x, int y, int clipX0 = int.MinValue, int clipX1 = int.MaxValue, int clipY1 = int.MaxValue)
    {
        Flush();
        var px = Pixels;
        for (int iy = 0; iy < img.Height; iy++)
        {
            int dy = y + iy;
            if ((uint)dy >= (uint)Height || dy >= clipY1) continue;
            for (int ix = 0; ix < img.Width; ix++)
            {
                int dx = x + ix;
                if ((uint)dx >= (uint)Width || dx < clipX0 || dx >= clipX1) continue;
                ref uint p = ref px[dy * Width + dx];
                p = 0xff000000u | (uint)img.Compose(ix, iy, (int)(p & 0xffffff));
            }
        }
    }

    /// <summary>Loads a BMP (any bit depth) into a new surface.</summary>
    public static DibSurface FromBmp(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var bmp = new Bitmap(ms);
        return FromBitmap(bmp);
    }

    public static DibSurface FromBitmap(Bitmap bmp)
    {
        var s = new DibSurface(bmp.Width, bmp.Height, 0);
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            s.Flush();
            var dst = s.Pixels;
            for (int y = 0; y < bmp.Height; y++)
            {
                unsafe
                {
                    var srcRow = new Span<uint>((void*)(data.Scan0 + y * data.Stride), bmp.Width);
                    for (int x = 0; x < bmp.Width; x++) dst[y * bmp.Width + x] = 0xff000000u | (srcRow[x] & 0xffffff);
                }
            }
        }
        finally { bmp.UnlockBits(data); }
        return s;
    }

    /// <summary>A GDI+ copy for presentation.</summary>
    public Bitmap ToBitmap()
    {
        Flush();
        var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppRgb);
        var data = bmp.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        unsafe
        {
            var src = Pixels;
            for (int y = 0; y < Height; y++)
                src.Slice(y * Width, Width).CopyTo(new Span<uint>((void*)(data.Scan0 + y * data.Stride), Width));
        }
        bmp.UnlockBits(data);
        return bmp;
    }

    public void Dispose()
    {
        Gdi.SelectObject(Hdc, _oldBitmap);
        Gdi.DeleteObject(_bitmap);
        DeleteDC(Hdc);
    }
}

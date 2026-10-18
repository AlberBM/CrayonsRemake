using System.Runtime.InteropServices;

namespace Crayons.App.Rendering;

/// <summary>
/// Win32 GDI bindings. Tool drawing uses real GDI (not GDI+) so wide pens, flood
/// fills, hatch brushes, XOR rubber bands, StretchBlt mirroring and text rasterize
/// exactly the way the original program's GDI calls did.
/// </summary>
public static class Gdi
{
    // Pen styles / brush styles / hatches
    public const int PS_SOLID = 0, PS_DOT = 2, PS_NULL = 5;
    public const int HS_HORIZONTAL = 0, HS_VERTICAL = 1, HS_FDIAGONAL = 2, HS_BDIAGONAL = 3, HS_CROSS = 4, HS_DIAGCROSS = 5;
    public const int NULL_BRUSH = 5, NULL_PEN = 8;

    // ROP2 / background modes / flood fill / stretch
    public const int R2_COPYPEN = 13, R2_XORPEN = 7, R2_NOT = 6;
    public const int TRANSPARENT = 1, OPAQUE = 2;
    public const int FLOODFILLBORDER = 0, FLOODFILLSURFACE = 1;
    public const int BLACKONWHITE = 1, COLORONCOLOR = 3;
    public const int RGN_AND = 1, RGN_COPY = 5, ALTERNATE = 1, WINDING = 2;

    // Raster ops
    public const uint SRCCOPY = 0x00CC0020, SRCAND = 0x008800C6, SRCPAINT = 0x00EE0086,
                      SRCINVERT = 0x00660046, DSTINVERT = 0x00550009, WHITENESS = 0x00FF0062, BLACKNESS = 0x00000042;

    // Text
    public const int TA_LEFT = 0, TA_TOP = 0, TA_CENTER = 6, TA_BASELINE = 24;

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }

    [DllImport("gdi32.dll")] public static extern IntPtr CreatePen(int style, int width, uint color);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateHatchBrush(int hatch, uint color);
    [DllImport("gdi32.dll")] public static extern IntPtr CreatePatternBrush(IntPtr hbm);
    [DllImport("gdi32.dll")] public static extern IntPtr GetStockObject(int obj);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool MoveToEx(IntPtr hdc, int x, int y, IntPtr prev);
    [DllImport("gdi32.dll")] public static extern bool LineTo(IntPtr hdc, int x, int y);
    [DllImport("gdi32.dll")] public static extern bool Ellipse(IntPtr hdc, int l, int t, int r, int b);
    [DllImport("gdi32.dll")] public static extern bool Rectangle(IntPtr hdc, int l, int t, int r, int b);
    [DllImport("gdi32.dll")] public static extern bool RoundRect(IntPtr hdc, int l, int t, int r, int b, int w, int h);
    [DllImport("gdi32.dll")] public static extern bool Polygon(IntPtr hdc, POINT[] pts, int count);
    [DllImport("gdi32.dll")] public static extern bool Arc(IntPtr hdc, int l, int t, int r, int b, int xs, int ys, int xe, int ye);
    [DllImport("gdi32.dll")] public static extern bool ExtFloodFill(IntPtr hdc, int x, int y, uint color, int type);
    [DllImport("gdi32.dll")] public static extern int SetROP2(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")] public static extern int SetBkMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")] public static extern uint SetBkColor(IntPtr hdc, uint color);
    [DllImport("gdi32.dll")] public static extern uint SetTextColor(IntPtr hdc, uint color);
    [DllImport("gdi32.dll")] public static extern uint GetPixel(IntPtr hdc, int x, int y);
    [DllImport("gdi32.dll")] public static extern uint SetPixel(IntPtr hdc, int x, int y, uint color);
    [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")] public static extern bool StretchBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int sw, int sh, uint rop);
    [DllImport("gdi32.dll")] public static extern int SetStretchBltMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")] public static extern bool PatBlt(IntPtr hdc, int x, int y, int w, int h, uint rop);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateEllipticRgn(int l, int t, int r, int b);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
    [DllImport("gdi32.dll")] public static extern IntPtr CreatePolygonRgn(POINT[] pts, int count, int mode);
    [DllImport("gdi32.dll")] public static extern int SelectClipRgn(IntPtr hdc, IntPtr rgn);
    [DllImport("gdi32.dll")] public static extern int ExtSelectClipRgn(IntPtr hdc, IntPtr rgn, int mode);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateFont(int height, int width, int escapement, int orientation, int weight,
        uint italic, uint underline, uint strike, uint charset, uint outPrecision, uint clipPrecision,
        uint quality, uint pitchAndFamily, string face);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern bool TextOut(IntPtr hdc, int x, int y, string s, int len);
    [DllImport("gdi32.dll")] public static extern uint SetTextAlign(IntPtr hdc, uint align);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern bool GetTextExtentPoint32(IntPtr hdc, string s, int len, out SIZE size);
    [DllImport("gdi32.dll")] public static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr prev);
    [DllImport("gdi32.dll")] public static extern bool SetViewportOrgEx(IntPtr hdc, int x, int y, IntPtr prev);
    [DllImport("gdi32.dll")] public static extern bool GetViewportOrgEx(IntPtr hdc, out POINT pt);
    [DllImport("gdi32.dll")] public static extern int GetClipRgn(IntPtr hdc, IntPtr rgn);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern int AddFontResourceEx(string path, uint flags, IntPtr reserved);
    public const uint FR_PRIVATE = 0x10;

    public const int SYSTEM_FONT = 13, BLACK_PEN = 7;
    public const int FW_NORMAL = 400, FW_BOLD = 700;
    public const int DI_NORMAL = 3;

    [StructLayout(LayoutKind.Sequential)]
    public struct ICONINFO { public bool fIcon; public int xHotspot, yHotspot; public IntPtr hbmMask, hbmColor; }

    [DllImport("gdi32.dll")] public static extern IntPtr CreateBitmap(int w, int h, uint planes, uint bitCount, byte[] bits);
    [DllImport("user32.dll")] public static extern IntPtr CreateIconIndirect(ref ICONINFO info);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr LoadImage(IntPtr hinst, IntPtr name, uint type, int cx, int cy, uint flags);
    public const int IDI_APPLICATION = 32512;
    public const uint IMAGE_ICON = 1, LR_SHARED = 0x8000;
    [DllImport("user32.dll")] public static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr icon, int w, int h, int step, IntPtr brush, int flags);

    /// <summary>A COLORREF (0x00BBGGRR) from 0xRRGGBB.</summary>
    public static uint Rgb(int rgb) => Crayons.Core.ColorTable.ToColorRef(rgb);

    /// <summary>Fills the rectangle l..r-1, t..b-1 with a solid colour.</summary>
    public static void FillRect(IntPtr hdc, int l, int t, int r, int b, int rgb)
    {
        using var pen = Use(hdc, GetStockObject(NULL_PEN), delete: false);
        using var br = Use(hdc, CreateSolidBrush(Rgb(rgb)));
        Rectangle(hdc, l, t, r + 1, b + 1); // a NULL pen leaves the last row/column undrawn
    }

    /// <summary>A hollow 1-px rectangle outline covering l..r-1, t..b-1.</summary>
    public static void FrameRect(IntPtr hdc, int l, int t, int r, int b, int rgb)
    {
        using var pen = Use(hdc, CreatePen(PS_SOLID, 1, Rgb(rgb)));
        using var br = Use(hdc, GetStockObject(NULL_BRUSH), delete: false);
        Rectangle(hdc, l, t, r, b);
    }

    /// <summary>One solid line (end pixel excluded) with a pen of the given width.</summary>
    public static void Line(IntPtr hdc, int x0, int y0, int x1, int y1, int rgb, int width = 1)
    {
        using var pen = Use(hdc, CreatePen(PS_SOLID, width, Rgb(rgb)));
        MoveToEx(hdc, x0, y0, IntPtr.Zero);
        LineTo(hdc, x1, y1);
    }

    public const uint NONANTIALIASED_QUALITY = 3;

    /// <summary>Creates a font the way the original's CreateFont(height, ..., face) calls did, without anti-aliasing.</summary>
    public static IntPtr Font(int height, string face, int escapement = 0) =>
        CreateFont(height, 0, escapement, escapement, 0, 0, 0, 0, 1 /*DEFAULT_CHARSET*/, 0, 0, NONANTIALIASED_QUALITY, 0, face);

    /// <summary>Selects an object for the lifetime of the returned scope and deletes it afterwards (unless it is a stock object).</summary>
    public static Selection Use(IntPtr hdc, IntPtr obj, bool delete = true) => new(hdc, obj, delete);

    public readonly struct Selection : IDisposable
    {
        readonly IntPtr _hdc, _obj, _old;
        readonly bool _delete;
        public Selection(IntPtr hdc, IntPtr obj, bool delete) { _hdc = hdc; _obj = obj; _delete = delete; _old = SelectObject(hdc, obj); }
        public void Dispose() { SelectObject(_hdc, _old); if (_delete) DeleteObject(_obj); }
    }
}

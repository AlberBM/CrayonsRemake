using Crayons.App.Rendering;
using Crayons.Core;
using Crayons.Core.Tools;

namespace Crayons.App.Tools;

/// <summary>
/// A brush for fill styles 0..15 (the original's FUN_1028_174f): null, solid, six hatches
/// (drawn TRANSPARENT) or an 8x8 PA00..PA07 pattern with black recoloured to the current
/// colour. Owns the GDI brush and, for patterns, the recoloured bitmap.
/// </summary>
public sealed class FillBrush : IDisposable
{
    readonly bool _stock;
    readonly DibSurface? _pattern;

    public IntPtr Handle { get; }

    /// <summary>Styles below 8 are drawn with SetBkMode(TRANSPARENT).</summary>
    public bool Transparent { get; }

    FillBrush(IntPtr handle, bool stock, bool transparent, DibSurface? pattern)
    {
        Handle = handle; _stock = stock; Transparent = transparent; _pattern = pattern;
    }

    /// <summary>
    /// Builds the brush for a style. Styles with no case (16 and above, which only the shape
    /// tools handle with their own fills) get the null brush.
    /// </summary>
    public static FillBrush Create(int style, int rgb, Func<string, DibSurface?> image)
    {
        bool transparent = style < 8;
        switch (PaintMath.BrushKind(style))
        {
            case FillBrushKind.Solid:
                return new(Gdi.CreateSolidBrush(Gdi.Rgb(rgb)), false, transparent, null);
            case FillBrushKind.Hatch:
                return new(Gdi.CreateHatchBrush(PaintMath.HatchStyle(style), Gdi.Rgb(rgb)), false, transparent, null);
            case FillBrushKind.Pattern when image(PaintMath.PatternFile(style)) is { } src:
                var pat = new DibSurface(8, 8, 0xFFFFFF);
                pat.Blit(src, 0, 0);
                pat.Flush();
                var px = pat.Pixels;
                uint recolour = 0xff000000u | (uint)rgb;
                for (int i = 0; i < px.Length; i++)
                    if ((px[i] & 0xffffff) == 0) px[i] = recolour;
                return new(Gdi.CreatePatternBrush(pat.HBitmap), false, transparent, pat);
            default:
                return new(Gdi.GetStockObject(Gdi.NULL_BRUSH), true, transparent, null);
        }
    }

    public void Dispose()
    {
        if (!_stock) Gdi.DeleteObject(Handle);
        _pattern?.Dispose();
    }
}

/// <summary>Region-clipped fills of the shape tools (gradients 16..23, picture tiles 24..31) and the shaded renderer.</summary>
public static class FillRenderer
{
    /// <summary>The clip region of a shape (rect, round rect, ellipse or ALTERNATE polygon).</summary>
    public static IntPtr CreateRegion(ShapeGeom g)
    {
        var (l, t, r, b) = g.Box;
        return g.Kind switch
        {
            ShapeKind.Ellipse => Gdi.CreateEllipticRgn(l, t, r, b),
            ShapeKind.Rectangle => Gdi.CreateRectRgn(l, t, r, b),
            ShapeKind.RoundRect => Gdi.CreateRoundRectRgn(l, t, r, b, g.CornerW, g.CornerH),
            _ => Gdi.CreatePolygonRgn(g.Points!.Select(p => new Gdi.POINT(p.X, p.Y)).ToArray(), g.Points!.Length, Gdi.ALTERNATE),
        };
    }

    /// <summary>Runs <paramref name="draw"/> with the shape's region selected as the clip region.</summary>
    public static void Clipped(IntPtr hdc, IntPtr region, Action draw)
    {
        Gdi.SelectClipRgn(hdc, region);
        try { draw(); }
        finally { Gdi.SelectClipRgn(hdc, IntPtr.Zero); }
    }

    /// <summary>Gradient style 16..23 over the box with ten 2-px pens of the colour group (the selected shade is ignored).</summary>
    public static void Gradient(IntPtr hdc, int style, (int MinX, int MinY, int MaxX, int MaxY) box, int colorGroup)
    {
        var pens = new IntPtr[10];
        for (int s = 0; s < 10; s++) pens[s] = Gdi.CreatePen(Gdi.PS_SOLID, 2, ColorTable.ColorRef(colorGroup * 10 + s));
        IntPtr old = Gdi.SelectObject(hdc, pens[0]);
        try
        {
            foreach (var l in ShapesMath.GradientLines(style, box.MinX, box.MinY, box.MaxX, box.MaxY))
            {
                Gdi.SelectObject(hdc, pens[l.Shade]);
                Gdi.MoveToEx(hdc, l.X0, l.Y0, IntPtr.Zero);
                Gdi.LineTo(hdc, l.X1, l.Y1);
            }
        }
        finally
        {
            Gdi.SelectObject(hdc, old);
            foreach (var p in pens) Gdi.DeleteObject(p);
        }
    }

    /// <summary>Picture style 24..31: 54x54 tiles of PB1..PB8 from (minX, minY), (w div 54 + 2) x (h div 54 + 2) of them.</summary>
    public static void Picture(IntPtr hdc, DibSurface? picture, (int MinX, int MinY, int MaxX, int MaxY) box)
    {
        if (picture == null) return;
        int s = PaintMath.SwatchSize;
        int nx = (box.MaxX - box.MinX) / s + 2, ny = (box.MaxY - box.MinY) / s + 2;
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
                Gdi.BitBlt(hdc, box.MinX + i * s, box.MinY + j * s, Math.Min(s, picture.Width), Math.Min(s, picture.Height), picture.Hdc, 0, 0, Gdi.SRCCOPY);
    }

    /// <summary>
    /// The shared shaded-shape renderer (FUN_1028_20f0): 10 passes, pass i with a 1-px pen and a
    /// solid brush in table[hue*10 + 9 - i]; kind 1 Ellipse, 2 Rectangle, 3 RoundRect with
    /// corners |width| div 2 and |height| div 2.
    /// </summary>
    public static void Shaded(IntPtr hdc, int hue, int light, int kind, int rx, int ry, int cx, int cy)
    {
        Gdi.SetROP2(hdc, Gdi.R2_COPYPEN);
        var passes = ShapesMath.ShadedPasses(light, rx, ry, cx, cy);
        for (int i = 0; i < passes.Length; i++)
        {
            uint c = ColorTable.ColorRef(hue * 10 + ShapesMath.ShadedPassShade(i));
            using var pen = Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, 1, c));
            using var br = Gdi.Use(hdc, Gdi.CreateSolidBrush(c));
            var (l, t, r, b) = passes[i];
            switch (kind)
            {
                case 1: Gdi.Ellipse(hdc, l, t, r, b); break;
                case 2: Gdi.Rectangle(hdc, l, t, r, b); break;
                default: Gdi.RoundRect(hdc, l, t, r, b, Math.Abs(r - l) / 2, Math.Abs(b - t) / 2); break;
            }
        }
    }

    /// <summary>
    /// Fills <paramref name="region"/> with any style 0..31 (used by the paint-can preview):
    /// brush styles with PatBlt over the box, gradients over the box, pictures as tiles at the box origin.
    /// </summary>
    public static void FillStyled(IntPtr hdc, IntPtr region, int style, int rgb, int colorGroup,
        (int MinX, int MinY, int MaxX, int MaxY) box, Func<string, DibSurface?> image)
    {
        Clipped(hdc, region, () =>
        {
            switch (PaintMath.BrushKind(style))
            {
                case FillBrushKind.Gradient:
                    Gradient(hdc, style, box, colorGroup);
                    break;
                case FillBrushKind.Picture:
                    Picture(hdc, image(PaintMath.PictureFile(style)), box);
                    break;
                case FillBrushKind.Null:
                    break;
                default:
                    using (var fb = FillBrush.Create(style, rgb, image))
                    {
                        int oldMode = Gdi.SetBkMode(hdc, fb.Transparent ? Gdi.TRANSPARENT : Gdi.OPAQUE);
                        Gdi.SetBrushOrgEx(hdc, box.MinX, box.MinY, IntPtr.Zero);
                        using (Gdi.Use(hdc, fb.Handle, delete: false))
                            Gdi.PatBlt(hdc, box.MinX, box.MinY, box.MaxX - box.MinX + 1, box.MaxY - box.MinY + 1, 0x00F00021 /*PATCOPY*/);
                        Gdi.SetBrushOrgEx(hdc, 0, 0, IntPtr.Zero);
                        Gdi.SetBkMode(hdc, oldMode);
                    }
                    break;
            }
        });
    }
}

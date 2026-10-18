namespace Crayons.Core.Tools;

/// <summary>A GDI primitive kind used by the shape tools.</summary>
public enum ShapeKind { Ellipse, Rectangle, RoundRect, Polygon }

/// <summary>
/// One shape: the primitive, its GDI box (as passed, not normalised), RoundRect corner sizes and,
/// for polygons, the points in drawing order.
/// </summary>
public sealed record ShapeGeom(ShapeKind Kind, int L, int T, int R, int B, int CornerW = 0, int CornerH = 0, (int X, int Y)[]? Points = null)
{
    /// <summary>The min/max box: sorted L/T/R/B, or the min/max of the polygon points.</summary>
    public (int MinX, int MinY, int MaxX, int MaxY) Box
    {
        get
        {
            if (Points is { Length: > 0 } p)
                return (p.Min(q => q.X), p.Min(q => q.Y), p.Max(q => q.X), p.Max(q => q.Y));
            return (Math.Min(L, R), Math.Min(T, B), Math.Max(L, R), Math.Max(T, B));
        }
    }

    public static ShapeGeom Poly(params (int X, int Y)[] pts)
    {
        var g = new ShapeGeom(ShapeKind.Polygon, 0, 0, 0, 0, Points: pts);
        var (l, t, r, b) = g.Box;
        return g with { L = l, T = t, R = r, B = b };
    }
}

/// <summary>One gradient line: shade 0..9 of the current colour group, MoveTo(X0,Y0) LineTo(X1,Y1).</summary>
public readonly record struct GradientLine(int Shade, int X0, int Y0, int X1, int Y1);

/// <summary>One cube face: polygon points (canvas coordinates) and its gradient style.</summary>
public readonly record struct CubeFace((int X, int Y)[] Points, int Style);

/// <summary>One 3D-line pass: shade and the two offsets the segment is drawn with, in order.</summary>
public readonly record struct Line3DPass(int Shade, int Dx1, int Dy1, int Dx2, int Dy2);

/// <summary>
/// Geometry of the Shapes and 3D tabs and the shared shaded-shape renderer
/// (behavior/tools-shapes.md, tools-wacky.md). Products that the original stored as
/// float32 are computed in <c>float</c> and rounded half-to-even.
/// </summary>
public static class ShapesMath
{
    // ---- Shared shaded renderer (FUN_1028_20f0) -------------------------------------

    /// <summary>Step divisors (KL, KR, KT, KB) for light 1..9.</summary>
    public static (int KL, int KR, int KT, int KB) LightK(int light) => light switch
    {
        2 => (14, 8, 14, 8),
        3 => (11, 11, 14, 8),
        4 => (8, 14, 14, 8),
        5 => (14, 8, 8, 14),
        6 => (11, 11, 8, 14),
        7 => (8, 14, 8, 14),
        8 => (8, 14, 11, 11),
        9 => (14, 8, 11, 11),
        _ => (11, 11, 11, 11),
    };

    /// <summary>
    /// The 10 pass rectangles of the shaded renderer. Pass i (0..9) is drawn in shade 9 - i:
    /// left = (cx-rx) + Round(i*sL), right = (cx+rx) - Round(i*sR), top = (cy-ry) + Round(i*sT),
    /// bottom = (cy+ry) - Round(i*sB), with sX = r/kX stored as float32.
    /// </summary>
    public static (int L, int T, int R, int B)[] ShadedPasses(int light, int rx, int ry, int cx, int cy)
    {
        var (kl, kr, kt, kb) = LightK(light);
        float sL = rx / (float)kl, sR = rx / (float)kr, sT = ry / (float)kt, sB = ry / (float)kb;
        var r = new (int, int, int, int)[10];
        for (int i = 0; i < 10; i++)
        {
            float pl = i * sL, pr = i * sR, pt = i * sT, pb = i * sB;
            r[i] = (cx - rx + DMath.Round(pl), cy - ry + DMath.Round(pt), cx + rx - DMath.Round(pr), cy + ry - DMath.Round(pb));
        }
        return r;
    }

    /// <summary>The shade used by pass i of the shaded renderer (outermost darkest).</summary>
    public static int ShadedPassShade(int pass) => 9 - pass;

    /// <summary>3D tab inputs from a drag: rx = dx div 2, ry = dy div 2, cx = min(X0,X)+rx, cy = min(Y0,Y)+ry.</summary>
    public static (int Rx, int Ry, int Cx, int Cy) ShadedFromDrag(int x0, int y0, int x, int y)
    {
        int rx = Math.Abs(x - x0) / 2, ry = Math.Abs(y - y0) / 2;
        return (rx, ry, Math.Min(x0, x) + rx, Math.Min(y0, y) + ry);
    }

    // ---- Gradients (FUN_1028_48ae, styles 16..23) -------------------------------------

    /// <summary>
    /// The gradient lines for style 16..23 over the box, in drawing order. Band i (0..9) covers
    /// v = base + Round(i*s) .. base + Round((i+1)*s) inclusive (s = span/10 in float32), so
    /// adjacent bands overlap by one line. Dark-first styles use shade 9-i, light-first shade i.
    /// </summary>
    public static List<GradientLine> GradientLines(int style, int minX, int minY, int maxX, int maxY)
    {
        int w = maxX - minX, h = maxY - minY;
        bool darkFirst = style is 16 or 18 or 20 or 22;
        int span = style switch { 16 or 17 => w, 18 or 19 => h, _ => w + h };
        int baseV = style is 18 or 19 ? minY : minX;
        float s = span / 10f;
        var lines = new List<GradientLine>();
        for (int i = 0; i < 10; i++)
        {
            int shade = darkFirst ? 9 - i : i;
            float a = i * s, b = (i + 1) * s;
            int from = baseV + DMath.Round(a), to = baseV + DMath.Round(b);
            for (int v = from; v <= to; v++)
                lines.Add(style switch
                {
                    16 or 17 => new GradientLine(shade, v, minY, v, maxY),
                    18 or 19 => new GradientLine(shade, minX, v, maxX, v),
                    20 or 23 => new GradientLine(shade, v, minY, v - h, maxY),  // "/"
                    _ => new GradientLine(shade, v, maxY, v - h, minY),        // "\" (21, 22)
                });
        }
        return lines;
    }

    // ---- Shapes tab geometry ----------------------------------------------------------

    static int Sgn(int v) => v < 0 ? -1 : 1;

    public static ShapeGeom Ellipse(int x0, int y0, int x, int y) => new(ShapeKind.Ellipse, x0, y0, x, y);
    public static ShapeGeom Rectangle(int x0, int y0, int x, int y) => new(ShapeKind.Rectangle, x0, y0, x, y);

    /// <summary>Rounded square: RoundRect(X0,Y0,X,Y, dx div 4, dy div 4).</summary>
    public static ShapeGeom RoundedSquare(int x0, int y0, int x, int y) =>
        new(ShapeKind.RoundRect, x0, y0, x, y, Math.Abs(x - x0) / 4, Math.Abs(y - y0) / 4);

    /// <summary>Triangle variants 1 (apex on the press row), 2 (right angle) and 3 (sideways).</summary>
    public static ShapeGeom Triangle(int variant, int x0, int y0, int x, int y)
    {
        int dx = Math.Abs(x - x0), dy = Math.Abs(y - y0);
        return variant switch
        {
            2 => ShapeGeom.Poly((x0, y0), (x, y), (x0, y)),
            3 => ShapeGeom.Poly((x0, y0), (x0, y), (x, y0 + Sgn(y - y0) * (dy / 2))),
            _ => ShapeGeom.Poly((x0, y), (x, y), (x0 + Sgn(x - x0) * (dx / 2), y0)),
        };
    }

    /// <summary>Trapezoid 1 (narrow edge on the press row) and 2 (narrow edge on the release column).</summary>
    public static ShapeGeom Trapezoid(int variant, int x0, int y0, int x, int y)
    {
        if (variant == 2)
        {
            int k = Math.Abs(y - y0) / 5, s = Sgn(y - y0);
            int a = y0 + s * k, b = y0 + s * 4 * k;
            return ShapeGeom.Poly((x0, y0), (x0, y), (x, b), (x, a));
        }
        else
        {
            int k = Math.Abs(x - x0) / 5, s = Sgn(x - x0);
            int a = x0 + s * k, b = x0 + s * 4 * k;
            return ShapeGeom.Poly((x0, y), (x, y), (b, y0), (a, y0));
        }
    }

    /// <summary>Diamond: mid points toward the release point.</summary>
    public static ShapeGeom Diamond(int x0, int y0, int x, int y)
    {
        int mx = x0 + (x - x0) / 2, my = y0 + (y - y0) / 2;
        return ShapeGeom.Poly((x0, my), (mx, y), (x, my), (mx, y0));
    }

    /// <summary>Hexagon: s = dx/5 (float32), x1 = X0+Round(s), x2 = X0+Round(4s), ym = Y0+Round(dy/2).</summary>
    public static ShapeGeom Hexagon(int x0, int y0, int x, int y)
    {
        float s = (x - x0) / 5f;
        float s4 = 4 * s;
        float half = (y - y0) / 2f;
        int x1 = x0 + DMath.Round(s), x2 = x0 + DMath.Round(s4), ym = y0 + DMath.Round(half);
        return ShapeGeom.Poly((x0, ym), (x1, y), (x2, y), (x, ym), (x2, y0), (x1, y0));
    }

    /// <summary>Octagon: thirds tx = dx div 3, ty = dy div 3.</summary>
    public static ShapeGeom Octagon(int x0, int y0, int x, int y)
    {
        int tx = (x - x0) / 3, ty = (y - y0) / 3;
        int x1 = x0 + tx, x2 = x0 + 2 * tx, y1 = y0 + ty, y2 = y0 + 2 * ty;
        return ShapeGeom.Poly((x0, y1), (x0, y2), (x1, y), (x2, y), (x, y2), (x, y1), (x2, y0), (x1, y0));
    }

    /// <summary>Star, upright (variant 1) or leaning (2): kx = dx div 10, ky = dy div 10 around the centre.</summary>
    public static ShapeGeom Star(int variant, int x0, int y0, int x, int y)
    {
        int kx = (x - x0) / 10, ky = (y - y0) / 10;
        int cx = x0 + (x - x0) / 2, cy = y0 + (y - y0) / 2;
        int xa = cx - kx, xb = cx + kx, ya = cy - ky, yb = cy + ky;
        return variant == 2
            ? ShapeGeom.Poly((x0, y0), (xa, cy), (x0, y), (cx, yb), (x, y), (xb, cy), (x, y0), (cx, ya))
            : ShapeGeom.Poly((x0, cy), (xa, yb), (cx, y), (xb, yb), (x, cy), (xb, ya), (cx, y0), (xa, ya));
    }

    /// <summary>
    /// Parallelogram step 1's XOR band (spec §9.1 R11): two polylines from the anchor, (X0,Y0)->(X1,Y0)->(X1,Y1)
    /// and (X0,Y0)->(X0,Y1)->(X1,Y1), as MoveTo/LineTo segments (end pixels excluded). The anchor is XORed
    /// twice and the opposite corner never, so both stay as they were (white on a white canvas), as observed.
    /// Step 2's first erase (a closed polygon) therefore leaves those two pixels toggled, like the original.
    /// </summary>
    public static (int X0, int Y0, int X1, int Y1)[] ParallelogramStep1Band(int x0, int y0, int x1, int y1) =>
        new[] { (x0, y0, x1, y0), (x1, y0, x1, y1), (x0, y0, x0, y1), (x0, y1, x1, y1) };

    /// <summary>Parallelogram step 1: the dragged rectangle as a 4-point polygon (the geometry step 2 starts from).</summary>
    public static ShapeGeom ParallelogramStep1(int x0, int y0, int x1, int y1) =>
        ShapeGeom.Poly((x0, y0), (x1, y0), (x1, y1), (x0, y1));

    /// <summary>
    /// Parallelogram step 2 with the stored signed step-1 extent (dx', dy'):
    /// variant 4 = (X0,Y0),(X0+dx',Y0),(X,Y),(X-dx',Y); variant 3 = (X0,Y0),(X0,Y0+dy'),(X,Y),(X,Y-dy').
    /// </summary>
    public static ShapeGeom Parallelogram(int variant, int x0, int y0, int dxp, int dyp, int x, int y) =>
        variant == 3
            ? ShapeGeom.Poly((x0, y0), (x0, y0 + dyp), (x, y), (x, y - dyp))
            : ShapeGeom.Poly((x0, y0), (x0 + dxp, y0), (x, y), (x - dxp, y));

    /// <summary>The Shapes-tab geometry for a mode (0x21, 0x1F, 0x22, 0x20, 0x23, 0x29, 0x28, 0x25, 0x24).</summary>
    public static ShapeGeom ForMode(int mode, int variant, int x0, int y0, int x, int y) => mode switch
    {
        0x21 => Ellipse(x0, y0, x, y),
        0x1F => Rectangle(x0, y0, x, y),
        0x22 => Triangle(variant, x0, y0, x, y),
        0x20 => RoundedSquare(x0, y0, x, y),
        0x23 => Trapezoid(variant, x0, y0, x, y),
        0x29 => Diamond(x0, y0, x, y),
        0x28 => Hexagon(x0, y0, x, y),
        0x25 => Octagon(x0, y0, x, y),
        0x24 => Star(variant, x0, y0, x, y),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    // ---- 3D tab -------------------------------------------------------------------------

    /// <summary>Torus: 20 hollow ellipses (d, shade): d 19→10 with shades 9→0, then d 9→0 with shades 0→9.</summary>
    public static (int D, int Shade)[] TorusPasses()
    {
        var r = new (int, int)[20];
        for (int k = 0; k < 10; k++) r[k] = (19 - k, 9 - k);
        for (int k = 0; k < 10; k++) r[10 + k] = (9 - k, k);
        return r;
    }

    /// <summary>
    /// 3D line passes: offset o from t+1 down to 0; the shade starts at 9 and drops by step
    /// (3 for t = 1, 2 for t = 2..3, else 1) after each pass while it is &gt; 0. Offsets:
    /// vertical line x ± o; horizontal y ± o; opposite signs (+o,+o)/(-o,-o); same signs (-o,+o)/(+o,-o).
    /// </summary>
    public static Line3DPass[] Line3DPasses(int thickness, int dx, int dy)
    {
        int step = thickness == 1 ? 3 : thickness <= 3 ? 2 : 1;
        int shade = 9;
        var r = new Line3DPass[thickness + 2];
        for (int k = 0; k < r.Length; k++)
        {
            int o = thickness + 1 - k;
            r[k] = dx == 0 ? new(shade, o, 0, -o, 0)
                 : dy == 0 ? new(shade, 0, o, 0, -o)
                 : (dx < 0) != (dy < 0) ? new(shade, o, o, -o, -o)
                 : new(shade, -o, o, o, -o);
            if (shade > 0) shade = Math.Max(shade - step, 0);
        }
        return r;
    }

    /// <summary>Cube offsets: ox = Round((dx/10)*depth), oy = Round((dy/10)*depth) in float32.</summary>
    public static (int Ox, int Oy) CubeOffsets(int dx, int dy, int depth)
    {
        float fx = Math.Abs(dx) / 10f, fy = Math.Abs(dy) / 10f;
        float px = fx * depth, py = fy * depth;
        return (DMath.Round(px), DMath.Round(py));
    }

    /// <summary>Front-face gradient style by box direction: 2→23, 5→21, 4→22, 7→20.</summary>
    public static int CubeFrontStyle(int dir) => dir switch { 5 => 21, 4 => 22, 7 => 20, _ => 23 };

    /// <summary>The cube's faces in drawing order: front (rectangle), top/bottom, side.</summary>
    public static CubeFace[] CubeFaces(int dir, int depth, int x0, int y0, int x, int y)
    {
        int minX = Math.Min(x0, x), maxX = Math.Max(x0, x), minY = Math.Min(y0, y), maxY = Math.Max(y0, y);
        var (ox, oy) = CubeOffsets(x - x0, y - y0, depth);
        var front = new CubeFace(new[] { (minX, minY), (maxX, minY), (maxX, maxY), (minX, maxY) }, CubeFrontStyle(dir));
        CubeFace topBottom = dir switch
        {
            4 => new(new[] { (maxX + ox, minY - oy), (maxX, minY), (minX, minY), (minX + ox, minY - oy) }, 18),
            5 => new(new[] { (minX - ox, maxY + oy), (maxX - ox, maxY + oy), (maxX, maxY), (minX, maxY) }, 19),
            7 => new(new[] { (minX + ox, maxY + oy), (maxX + ox, maxY + oy), (maxX, maxY), (minX, maxY) }, 19),
            _ => new(new[] { (minX - ox, minY - oy), (maxX - ox, minY - oy), (maxX, minY), (minX, minY) }, 18),
        };
        CubeFace side = dir switch
        {
            5 => new(new[] { (minX - ox, minY + oy), (minX - ox, maxY + oy), (minX, maxY), (minX, minY) }, 16),
            4 => new(new[] { (maxX + ox, minY - oy), (maxX + ox, maxY - oy), (maxX, maxY), (maxX, minY) }, 17),
            7 => new(new[] { (maxX, minY), (maxX + ox, minY + oy), (maxX + ox, maxY + oy), (maxX, maxY) }, 17),
            _ => new(new[] { (minX - ox, minY - oy), (minX - ox, maxY - oy), (minX, maxY), (minX, minY) }, 16),
        };
        return new[] { front, topBottom, side };
    }

    /// <summary>Jumble / smear brush radius for thickness 1..10: 2, 3, 4, 5, 6, 8, 10, 12, 14, 16.</summary>
    public static int BrushRadius(int thickness) => thickness switch
    {
        <= 5 => Math.Max(thickness, 1) + 1,
        _ => 8 + 2 * (Math.Min(thickness, 10) - 6),
    };
}

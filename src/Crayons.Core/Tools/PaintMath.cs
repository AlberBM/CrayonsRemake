namespace Crayons.Core.Tools;

/// <summary>How the fill-style brush (styles 0..15) is built (behavior/tools-paint.md §5).</summary>
public enum FillBrushKind { Null, Solid, Hatch, Pattern, Gradient, Picture }

/// <summary>Deterministic formulas of the Paintbrush family and its fills (behavior/tools-paint.md).</summary>
public static class PaintMath
{
    /// <summary>
    /// Nib stripe offsets for brush type 1..4 (tools-paint.md §3): N = 3t stripes,
    /// h = (N-1) div 2; stripe i is offset by (h-i, -h+i) "/", (-h+i, -h+i) "\",
    /// (0, -h+i) vertical or (-h+i, 0) horizontal.
    /// </summary>
    public static (int Dx, int Dy)[] NibOffsets(int brushType, int thickness)
    {
        int n = 3 * thickness;
        int h = (n - 1) / 2;
        var r = new (int, int)[Math.Max(n, 0)];
        for (int i = 0; i < n; i++)
            r[i] = brushType switch
            {
                1 => (h - i, -h + i),
                2 => (-h + i, -h + i),
                3 => (0, -h + i),
                _ => (-h + i, 0),
            };
        return r;
    }

    /// <summary>Roller print half size: direction 3 is tall (3t, 6t), 4 (default) is wide (6t, 3t).</summary>
    public static (int Hw, int Hh) RollerHalfSize(int dir, int thickness) =>
        dir == 3 ? (3 * thickness, 6 * thickness) : (6 * thickness, 3 * thickness);

    /// <summary>Spray samples per burst: 20 when t &lt; 2, else 10t.</summary>
    public static int SprayCount(int thickness) => thickness < 2 ? 20 : 10 * thickness;

    /// <summary>Spray radius R = 8t.</summary>
    public static int SprayRadius(int thickness) => 8 * thickness;

    /// <summary>A spray sample (dx, dy) is kept when Round(sqrt(dx²+dy²)) &lt;= R.</summary>
    public static bool SprayAccepts(int dx, int dy, int radius) => DMath.Dist(dx, dy) <= radius;

    /// <summary>Brush kind for a fill style 0..31.</summary>
    public static FillBrushKind BrushKind(int style) => style switch
    {
        0 => FillBrushKind.Null,
        1 => FillBrushKind.Solid,
        >= 2 and <= 7 => FillBrushKind.Hatch,
        >= 8 and <= 15 => FillBrushKind.Pattern,
        >= 16 and <= 23 => FillBrushKind.Gradient,
        >= 24 and <= 31 => FillBrushKind.Picture,
        _ => FillBrushKind.Null,
    };

    /// <summary>GDI hatch constant for styles 2..7: HORIZONTAL, VERTICAL, CROSS, BDIAGONAL, FDIAGONAL, DIAGCROSS.</summary>
    public static int HatchStyle(int style) => style switch
    {
        2 => 0, // HS_HORIZONTAL
        3 => 1, // HS_VERTICAL
        4 => 4, // HS_CROSS
        5 => 3, // HS_BDIAGONAL (///)
        6 => 2, // HS_FDIAGONAL (\\\)
        7 => 5, // HS_DIAGCROSS
        _ => throw new ArgumentOutOfRangeException(nameof(style)),
    };

    /// <summary>The 8x8 pattern file for styles 8..15 (PA00..PA07).</summary>
    public static string PatternFile(int style) => $"PA{style - 8:00}.ASL";

    /// <summary>The 54x54 picture file for styles 24..31 (PB1..PB8).</summary>
    public static string PictureFile(int style) => $"PB{style - 23}.ASL";

    // ---- FillStyles dialog -------------------------------------------------------------

    /// <summary>The dialog form's left edge and width (FormShow: Left = 82; the form is 257 wide).</summary>
    public const int FillStylesLeft = 82, FillStylesWidth = 257;

    /// <summary>FormShow placement: Top 280 / Height 68 for the Fill Bucket, Top 94 / Height 222 otherwise.</summary>
    public static (int Top, int Height) FillStylesPlacement(bool bucket) => bucket ? (280, 68) : (94, 222);

    /// <summary>
    /// Image1MouseDown hit map in picture coordinates (tools-paint.md §5): rows 0..33 → x div 32,
    /// 34..67 → +8, 68..93 → +16, 94..159 → x div 64 + 24, 160 and below → x div 64 + 28.
    /// The column is clamped to the last cell (the image control is wider than the picture).
    /// Returns -1 above the picture.
    /// </summary>
    public static int FillStyleAt(int ix, int iy)
    {
        if (iy < 0 || ix < 0) return -1;
        int c32 = Math.Min(ix / 32, 7), c64 = Math.Min(ix / 64, 3);
        return iy switch
        {
            <= 33 => c32,
            <= 67 => c32 + 8,
            <= 93 => c32 + 16,
            <= 159 => c64 + 24,
            _ => c64 + 28,
        };
    }

    // ---- Spilled Paint Can panel preview ------------------------------------------------

    /// <summary>The 26-point splat polygon of the paint-can panel (shell.md §3.3), in form coordinates.</summary>
    public static readonly (int X, int Y)[] PaintCanPolygon =
    {
        (19, 299), (19, 300), (22, 304), (21, 306), (17, 311), (15, 316), (16, 322), (19, 325), (24, 324),
        (28, 319), (31, 318), (41, 322), (52, 325), (57, 324), (60, 321), (59, 317), (61, 314), (64, 311),
        (65, 308), (63, 305), (57, 302), (44, 303), (32, 304), (26, 303), (20, 299), (19, 299),
    };

    /// <summary>Bounding box (min/max corners) of <see cref="PaintCanPolygon"/>: the preview's gradient box (observed).</summary>
    public static (int MinX, int MinY, int MaxX, int MaxY) PaintCanBounds =>
        (PaintCanPolygon.Min(p => p.X), PaintCanPolygon.Min(p => p.Y), PaintCanPolygon.Max(p => p.X), PaintCanPolygon.Max(p => p.Y));

    /// <summary>Where the 54x54 swatch of the preview is placed.</summary>
    public const int PaintCanSwatchX = 15, PaintCanSwatchY = 299, SwatchSize = 54;
}

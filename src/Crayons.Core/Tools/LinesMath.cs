namespace Crayons.Core.Tools;

/// <summary>Deterministic formulas of the Crayon/Lines tool family (behavior/tools-lines.md).</summary>
public static class LinesMath
{
    /// <summary>Formula canvas size W x H (spec §4.2: the original's outer panel size).</summary>
    public const int W = 558, H = 386;

    /// <summary>
    /// Crazy Three Color Pen strand offsets: n = thickness*3 - 1, n+1 strands; strand i is at
    /// (n div 2 - i, -(n div 2) + i).
    /// </summary>
    public static (int Dx, int Dy)[] CrazyPenOffsets(int thickness)
    {
        int n = thickness * 3 - 1;
        var r = new (int, int)[Math.Max(n + 1, 0)];
        for (int i = 0; i <= n; i++) r[i] = (n / 2 - i, -(n / 2) + i);
        return r;
    }

    /// <summary>
    /// The shade grid's X marker diagonals for the 16x16 box at (l, t), r = l + 16, b = t + 16 (spec §9.1 R8):
    /// (l,t)->(r,b) and (r,t)->(l,b), each drawn with LineTo semantics (the end point is excluded).
    /// </summary>
    public static (int X0, int Y0, int X1, int Y1)[] ShadeMarkerDiagonals(int l, int t) =>
        new[] { (l, t, l + 16, t + 16), (l + 16, t, l, t + 16) };

    /// <summary>Strand colour table entry: strands 0-1 red (95), 2-3 green (115), 4-5 blue (30).</summary>
    public static int CrazyPenColorIndex(int strand) => (strand / 2) switch { 0 => 95, 1 => 115, _ => 30 };

    /// <summary>Thick/Thin pen width from d = (|dx|+|dy|) div 2. Mode 0x0C thick→thin, 0x0D thin→thick. d &gt; 100 uses the 16..100 width.</summary>
    public static int ThickThinWidth(int mode, int d)
    {
        int band = d <= 3 ? 0 : d <= 8 ? 1 : d <= 15 ? 2 : 3;
        return mode == 0x0C ? 8 - 2 * band : 2 + 2 * band;
    }

    public static int ThickThinDistance(int dx, int dy) => (Math.Abs(dx) + Math.Abs(dy)) / 2;

    /// <summary>Left-right mirror: 2*(W div 2) - x.</summary>
    public static int MirrorX(int x) => 2 * (W / 2) - x;

    /// <summary>Top-bottom mirror: 2*(H div 2) - y.</summary>
    public static int MirrorY(int y) => 2 * (H / 2) - y;

    /// <summary>
    /// Segments drawn by one Symmetry move from prev to cur (mode 2 = left-right,
    /// 1 = top-bottom, 3 = four-way), in drawing order.
    /// </summary>
    public static IEnumerable<(int X0, int Y0, int X1, int Y1)> SymmetrySegments(int mode, int px, int py, int x, int y)
    {
        yield return (px, py, x, y);
        if (mode == 2 || mode == 3) yield return (MirrorX(px), py, MirrorX(x), y);
        if (mode == 1 || mode == 3) yield return (px, MirrorY(py), x, MirrorY(y));
        if (mode == 3) yield return (MirrorX(px), MirrorY(py), MirrorX(x), MirrorY(y));
    }

    /// <summary>The four Corner tool end points (0,0), (W,0), (W,H), (0,H).</summary>
    public static readonly (int X, int Y)[] Corners = { (0, 0), (W, 0), (W, H), (0, H) };
}

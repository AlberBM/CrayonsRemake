namespace Crayons.Core.Tools;

/// <summary>
/// Deterministic parts of the Change It (Magic Hat) effects (behavior/tools-text-edit.md §5).
/// Pixel routines work on 32-bpp 0xFFRRGGBB rows (a DIB section's memory) so they can be tested
/// without GDI.
/// </summary>
public static class ChangeItMath
{
    public const int W = LinesMath.W, H = LinesMath.H;

    /// <summary>Box from two corners, normalised to min/max.</summary>
    public static Box Normalize(int x0, int y0, int x1, int y1) =>
        new(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));

    /// <summary>Face squares: centres from (left+3, top+3) in steps of 5 while &lt; right / bottom.</summary>
    public static IEnumerable<(int X, int Y)> SquareCentres(Box b)
    {
        for (int y = b.T + 3; y < b.B; y += 5)
            for (int x = b.L + 3; x < b.R; x += 5)
                yield return (x, y);
    }

    static bool In(int x, int y, int w, int h) => (uint)x < (uint)w && (uint)y < (uint)h;

    /// <summary>
    /// Face, squares (mode 0x3F): each 5x5 block (x±2, y±2) takes the colour of its centre pixel.
    /// Pixels outside the surface are skipped; a centre outside the surface paints nothing.
    /// </summary>
    public static void MosaicSquares(Span<uint> px, int w, int h, Box b)
    {
        foreach (var (cx, cy) in SquareCentres(b))
        {
            if (!In(cx, cy, w, h)) continue;
            uint c = px[cy * w + cx];
            for (int y = cy - 2; y <= cy + 2; y++)
                for (int x = cx - 2; x <= cx + 2; x++)
                    if (In(x, y, w, h)) px[y * w + x] = c;
        }
    }

    /// <summary>
    /// Face, circles (mode 0x40): rows from top+3 in steps of 5; per row r = Random(6)+1 and x steps by
    /// r+1 from left+3; every pixel with Dist(dx, dy) ≤ r takes the centre colour (read when the circle is drawn).
    /// </summary>
    public static void MosaicCircles(Span<uint> px, int w, int h, Box b, DelphiRandom random)
    {
        for (int cy = b.T + 3; cy < b.B; cy += 5)
        {
            int r = random.Next(6) + 1;
            for (int cx = b.L + 3; cx < b.R; cx += r + 1)
            {
                if (!In(cx, cy, w, h)) continue;
                uint c = px[cy * w + cx];
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (DMath.Dist(dx, dy) <= r && In(cx + dx, cy + dy, w, h)) px[(cy + dy) * w + cx + dx] = c;
            }
        }
    }

    /// <summary>
    /// Eye Dropper (mode 0x44): c = the pixel under (x, y). If c equals the current colour nothing happens;
    /// otherwise every pixel equal to c becomes the current colour (a global replace). Returns the count.
    /// </summary>
    public static int ReplaceColor(Span<uint> px, int w, int h, int x, int y, int currentRgb)
    {
        if (!In(x, y, w, h)) return 0;
        uint c = px[y * w + x] & 0xFFFFFF;
        if (c == (uint)currentRgb) return 0;
        uint to = 0xFF000000u | (uint)currentRgb;
        int n = 0;
        for (int i = 0; i < w * h; i++)
            if ((px[i] & 0xFFFFFF) == c) { px[i] = to; n++; }
        return n;
    }

    /// <summary>Reverse (mode 0x42): clamp the box to left/top ≥ 1, right ≤ W-3, bottom ≤ H-3.</summary>
    public static Box ReverseClamp(Box b, int w = W, int h = H) =>
        new(Math.Max(b.L, 1), Math.Max(b.T, 1), Math.Min(b.R, w - 3), Math.Min(b.B, h - 3));

    /// <summary>
    /// Reverse and Copy Flipper mirror destination column x of the box [l, r) to source column l + r - x
    /// (rows: t + b - y), as observed in the original (spec §9.1 R4). Column l therefore reads column r.
    /// </summary>
    public static int MirrorSource(int lo, int hi, int v) => lo + hi - v;

    /// <summary>Top-left of the w x h source block that a plain mirror maps onto the box with <see cref="MirrorSource"/>.</summary>
    public static (int X, int Y) MirrorSourceOrigin(Box b, bool horizontal) =>
        horizontal ? (b.L + 1, b.T) : (b.L, b.T + 1);

    /// <summary>Cut/copy selection: clamp right &lt; W and bottom &lt; H.</summary>
    public static Box SelectionClamp(Box b, int w = W, int h = H) =>
        new(b.L, b.T, Math.Min(b.R, w - 1), Math.Min(b.B, h - 1));

    /// <summary>The blue selection frame: 5 nested rectangles growing outward by 0..4 px.</summary>
    public static Box[] SelectionFrame(Box b) =>
        Enumerable.Range(0, 5).Select(k => new Box(b.L - k, b.T - k, b.R + k, b.B + k)).ToArray();

    /// <summary>
    /// The striped marquee of a pending selection (spec §9.1 R6, matched to the original's screen):
    /// for each of the 5 nested rectangles, the top and bottom edges run left to right from the
    /// rectangle's left column and the left and right edges top to bottom from its top row, each
    /// with a 3-on / 3-off blue/white dash that starts at the edge's first pixel. Where two edges
    /// share a corner pixel, blue wins. Returns every frame pixel once with its colour (blue or white).
    /// </summary>
    public static IEnumerable<(int X, int Y, bool Blue)> SelectionMarquee(Box b)
    {
        var pixels = new Dictionary<(int, int), bool>();
        void Put(int x, int y, int phase)
        {
            bool blue = phase % 6 < 3;
            pixels[(x, y)] = pixels.TryGetValue((x, y), out var was) ? was || blue : blue;
        }
        foreach (var r in SelectionFrame(b))
        {
            for (int x = r.L; x < r.R; x++) { Put(x, r.T, x - r.L); Put(x, r.B - 1, x - r.L); }
            for (int y = r.T; y < r.B; y++) { Put(r.L, y, y - r.T); Put(r.R - 1, y, y - r.T); }
        }
        return pixels.Select(p => (p.Key.Item1, p.Key.Item2, p.Value));
    }

    /// <summary>Selection colour: blue (COLORREF 0x00FF0000).</summary>
    public const int SelectionRgb = 0x0000FF;
}

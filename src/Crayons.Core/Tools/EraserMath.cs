namespace Crayons.Core.Tools;

/// <summary>A rectangle as l, t, r, b (GDI style, right/bottom exclusive for fills).</summary>
public readonly record struct Box(int L, int T, int R, int B)
{
    public int Width => R - L;
    public int Height => B - T;
    public bool Contains(int x, int y) => x >= L && x < R && y >= T && y < B;
}

/// <summary>Geometry of the Eraser effects (behavior/tools-text-edit.md §4, spec §8.7).</summary>
public static class EraserMath
{
    public const int W = LinesMath.W, H = LinesMath.H;

    /// <summary>New slate variant 1..8 plays SND65..72.</summary>
    public static int NewSlateSound(int variant) => 64 + variant;

    /// <summary>
    /// New slate wipe steps. Variants 1-4 are 1-px white lines (each step = the line pair for one c,
    /// as (x0, y0, x1, y1) LineTo segments, end pixel excluded); variants 5-8 are 4x4 white squares
    /// (each step = one grid row or column, as rectangles).
    /// </summary>
    public static IReadOnlyList<Box[]> NewSlateSteps(int variant, int w = W, int h = H)
    {
        var steps = new List<Box[]>();
        switch (variant)
        {
            case 1: // vertical lines closing in from both sides
                for (int c = 0; c <= w / 2 + 1; c++) steps.Add(new[] { new Box(c, 0, c, h), new Box(w - c, 0, w - c, h) });
                break;
            case 2: // vertical lines from the centre outward
                for (int c = 0; c <= w / 2 + 1; c++) steps.Add(new[] { new Box(w / 2 - c, 0, w / 2 - c, h), new Box(w / 2 + c, 0, w / 2 + c, h) });
                break;
            case 3: // horizontal lines from top and bottom inward
                for (int c = 0; c <= h / 2 + 1; c++) steps.Add(new[] { new Box(0, c, w, c), new Box(0, h - c, w, h - c) });
                break;
            case 4: // horizontal lines from the centre outward
                for (int c = 0; c <= h / 2 + 1; c++) steps.Add(new[] { new Box(0, h / 2 - c, w, h / 2 - c), new Box(0, h / 2 + c, w, h / 2 + c) });
                break;
            default:
            {
                int cols = (w + 3) / 4, rows = (h + 3) / 4;
                Box Cell(int i, int j) => new(4 * i, 4 * j, 4 * i + 4, 4 * j + 4);
                if (variant is 5 or 6)
                    for (int n = 0; n < rows; n++)
                    {
                        int j = variant == 5 ? n : rows - 1 - n;
                        steps.Add(Enumerable.Range(0, cols).Select(i => Cell(i, j)).ToArray());
                    }
                else
                    for (int n = 0; n < cols; n++)
                    {
                        int i = variant == 7 ? n : cols - 1 - n;
                        steps.Add(Enumerable.Range(0, rows).Select(j => Cell(i, j)).ToArray());
                    }
                break;
            }
        }
        return steps;
    }

    /// <summary>True when the variant draws lines (1-4) rather than squares (5-8).</summary>
    public static bool NewSlateUsesLines(int variant) => variant is >= 1 and <= 4;

    /// <summary>Shrink away / tunnel copy i (1..12): the Barn Zoom inset k × (W div 25), k × (H div 25) (spec §8.7).</summary>
    public static (int X, int Y, int W, int H) ShrinkRect(int i, int w = W, int h = H) => WackyMath.BarnZoomRect(i, w, h);

    /// <summary>Crumple step i (1..9): the inset (i*W div 20, i*H div 20) and its rectangle.</summary>
    public static Box CrumpleInset(int i, int w = W, int h = H)
    {
        int ix = i * w / 20, iy = i * h / 20;
        return new Box(ix, iy, w - ix, h - iy);
    }

    /// <summary>
    /// The 51 random crumple points inside an inset rectangle: per point x = L + Random(width),
    /// then y = T + Random(height).
    /// </summary>
    public static (int X, int Y)[] CrumplePoints(Box inset, DelphiRandom random)
    {
        var pts = new (int X, int Y)[51];
        for (int k = 0; k < pts.Length; k++)
        {
            int x = inset.L + random.Next(inset.Width);
            int y = inset.T + random.Next(inset.Height);
            pts[k] = (x, y);
        }
        return pts;
    }

    /// <summary>Window shade step i (1..20): e = H - i*(H div 25).</summary>
    public static int WindowShadeEdge(int i, int h = H) => h - i * (h / 25);

    /// <summary>The top band height of the window shade: H div 10.</summary>
    public static int WindowShadeBand(int h = H) => h / 10;

    /// <summary>Explode step 1..8 → A930 frame 0,0,1,1,2,2,3,3.</summary>
    public static int ExplodeFrame(int step) => (step - 1) / 2;

    public const int ExplodeW = 76, ExplodeH = 49, ExplodeIntervalMs = 90;

    /// <summary>Explode ellipse k (2..80): radius 4k around the click.</summary>
    public static Box ExplodeRing(int k, int x, int y) => new(x - 4 * k, y - 4 * k, x + 4 * k, y + 4 * k);

    /// <summary>Square eraser: the white rectangle (X-10, Y-10)-(X+10, Y+10).</summary>
    public static Box SquareEraser(int x, int y) => new(x - 10, y - 10, x + 10, y + 10);
}

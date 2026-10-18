namespace Crayons.Core.Tools;

/// <summary>Deterministic tables and formulas of the Clown ("wacky") family (behavior/tools-wacky.md).</summary>
public static class WackyMath
{
    /// <summary>Mini Shapes triangle for v = 1..3 with half size r around (X, Y).</summary>
    public static (int X, int Y)[] MiniTriangle(int v, int x, int y, int r) => v switch
    {
        2 => new[] { (x - r, y - r), (x + r, y + r), (x - r, y + r) },
        3 => new[] { (x - r, y - r), (x - r, y + r), (x + r, y) },
        _ => new[] { (x, y - r), (x - r, y + r), (x + r, y + r) },
    };

    /// <summary>Grass (height range a, spread c): short (15, 20), medium (25, 35), tall (45, 55).</summary>
    public static (int A, int C) GrassSize(int grass) => grass switch
    {
        1 => (15, 20),
        3 => (45, 55),
        _ => (25, 35),
    };

    /// <summary>Hair arc offset: subtracted when the drawn value is even, added when odd.</summary>
    public static int HairSigned(int o) => o % 2 == 0 ? -o : o;

    // ---- Mr. Brush (A932) ---------------------------------------------------------------

    public const int MrBrushSize = 49, MrBrushMaskY = 54, MrBrushImageY = 1, MrBrushIntervalMs = 100;

    /// <summary>Mr. Brush waits about 1.4 s after the click before its first frame (observed, spec §9.1 R16).</summary>
    public const int MrBrushStartDelayMs = 1400;

    /// <summary>Paint-tip x offset for phases 1..8 (index phase-1).</summary>
    public static readonly int[] MrBrushTipOffsets = { 8, 12, 25, 36, 44, 36, 25, 12 };

    /// <summary>Sprite sheet source x for phases 1..8 (5 frames, 53 px apart, ping-pong).</summary>
    public static readonly int[] MrBrushFrameX = { 1, 54, 107, 160, 213, 160, 107, 54 };

    /// <summary>A new drift with magnitude Random(8)+1 and the opposite sign of the old one.</summary>
    public static int FlipDrift(int oldDrift, int magnitude) => oldDrift < 0 ? magnitude : -magnitude;

    // ---- Train (A931) ------------------------------------------------------------------

    public const int TrainW = 76, TrainH = 49, TrainMaskY = 54, TrainImageY = 1, TrainIntervalMs = 100;

    /// <summary>Sprite sheet source x for frames 1..4.</summary>
    public static readonly int[] TrainFrameX = { 1, 81, 161, 241 };

    /// <summary>Vertical drift from Random(3)+1: 1 → 1, 2 → 0, 3 → 1.</summary>
    public static int TrainDrift(int r) => r == 2 ? 0 : 1;

    // ---- Younger Barn Zoom ---------------------------------------------------------------

    /// <summary>Barn Zoom copy k (1..12): destination (k*dx, k*dy, W-2k*dx, H-2k*dy) with dx = W div 25, dy = H div 25.</summary>
    public static (int X, int Y, int W, int H) BarnZoomRect(int k, int w, int h)
    {
        int dx = w / 25, dy = h / 25;
        return (k * dx, k * dy, w - 2 * k * dx, h - 2 * k * dy);
    }
}

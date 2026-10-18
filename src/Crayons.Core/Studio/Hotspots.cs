namespace Crayons.Core.Studio;

/// <summary>A half-open hit rectangle: X0 &lt;= x &lt; X1 and Y0 &lt;= y &lt; Y1.</summary>
public readonly record struct HitRect(int X0, int Y0, int X1, int Y1)
{
    public bool Contains(int x, int y) => x >= X0 && x < X1 && y >= Y0 && y < Y1;
}

/// <summary>
/// Fixed hotspots and hit formulas of the studio frame (behavior/input.md §1-2,
/// spec §8.1/§8.3). All coordinates are 640x480 frame pixels.
/// </summary>
public static class Hotspots
{
    /// <summary>Canvas width as the original measured it (outer panel width).</summary>
    public const int CanvasW = 558, CanvasH = 386;
    /// <summary>Canvas client origin in the frame and its size.</summary>
    public const int CanvasX = 83, CanvasY = 93, CanvasClientW = 556, CanvasClientH = 384;
    /// <summary>Toolbar (TabSet) bounds.</summary>
    public const int ToolbarX = 82, ToolbarY = 60, ToolbarH = 32;

    public static readonly HitRect Help = new(0, 430, 80, 480);
    public static readonly HitRect Exit = new(CanvasW + 40, 0, CanvasW + 80, 40);
    public static readonly HitRect Palette = new(0, 0, 80, 133);
    public static readonly HitRect Undo = new(0, 134, 43, 157);
    public static readonly HitRect OptionsRow = new(100, 10, 500, 50);
    public static readonly HitRect Toolbar = new(ToolbarX, ToolbarY, 640, ToolbarY + ToolbarH);
    /// <summary>Canvas outer box including its 1-px border.</summary>
    public static readonly HitRect CanvasOuter = new(82, 92, 82 + CanvasW, 92 + CanvasH);
    public static readonly HitRect CanvasClient = new(CanvasX, CanvasY, CanvasX + CanvasClientW, CanvasY + CanvasClientH);

    /// <summary>Juggler ball rects (input.md §2.3), index = colour group.</summary>
    public static readonly HitRect[] Balls =
    {
        new(61, 75, 76, 90), new(63, 55, 88, 70), new(64, 119, 79, 134), new(55, 95, 70, 110),
        new(1, 49, 16, 64), new(31, 32, 46, 47), new(26, 77, 41, 93), new(5, 9, 20, 24),
        new(0, 28, 15, 43), new(45, 3, 60, 18), new(63, 35, 78, 50), new(60, 15, 75, 30),
        new(32, 62, 47, 77), new(2, 70, 17, 85), new(24, 3, 39, 18), new(27, 47, 43, 63),
    };

    /// <summary>Ball hit in table order, last match wins; null when no ball is hit.</summary>
    public static int? BallAt(int x, int y)
    {
        int? hit = null;
        for (int i = 0; i < Balls.Length; i++)
            if (Balls[i].Contains(x, y)) hit = i;
        return hit;
    }

    /// <summary>Shade click area: x &lt; 80 and 180 &lt;= y &lt;= 212 (observed in the original, spec §9.1 R14).</summary>
    public static bool IsShadeArea(int x, int y) => x >= 0 && x < 80 && y >= 180 && y <= 212;

    /// <summary>Shade index for a click in the shade area: x div 16, plus 5 when y &gt;= 197.</summary>
    public static int ShadeAt(int x, int y) => x / 16 + (y < 197 ? 0 : 5);

    /// <summary>Option slot 0..9 for a click in the options row.</summary>
    public static int OptionSlotAt(int x) => (x - 100) / 40;

    /// <summary>
    /// Toolbar tab index from a frame x (spec §8.1): localX = x - 82;
    /// 0 when localX &lt; 45, otherwise (localX - 10) div 40. Not clamped.
    /// </summary>
    public static int TabIndexAt(int x)
    {
        int local = x - ToolbarX;
        return local < 45 ? 0 : (local - 10) / 40;
    }

    /// <summary>Thirds of the modifier column: 0..26 / 28..53 / 55..79; x = 27 and 54 are dead (-1).</summary>
    public static int ThirdAt(int x) => x switch
    {
        >= 0 and < 27 => 0,
        >= 28 and < 54 => 1,
        >= 55 and < 80 => 2,
        _ => -1,
    };

    /// <summary>Halves of the modifier column: 0..39 / 40..79.</summary>
    public static int HalfAt(int x) => x < 40 ? 0 : 1;

    /// <summary>Modifier bands A-D (y 230/280/330/380, 50 px tall, x 0-79).</summary>
    public static HitRect Band(char band) => band switch
    {
        'A' => new(0, 230, 80, 280),
        'B' => new(0, 280, 80, 330),
        'C' => new(0, 330, 80, 380),
        'D' => new(0, 380, 80, 430),
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };
}

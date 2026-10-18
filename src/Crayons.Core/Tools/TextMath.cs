namespace Crayons.Core.Tools;

/// <summary>
/// Deterministic formulas of the Older Text styles, the rotated text and ABCD
/// (behavior/tools-text-edit.md §2-3, spec §8.6). PPI is fixed at 96.
/// </summary>
public static class TextMath
{
    public const int Ppi = 96;

    /// <summary>The text modes 0x4B-0x4F.</summary>
    public const int Plain = 0x4B, ThreeD = 0x4C, Shadow = 0x4D, Perspective = 0x4E, Rotated = 0x4F;

    /// <summary>Points to pixels: round(points * PPI / 72), half-to-even.</summary>
    public static int PointsToPixels(int points) => (int)Math.Round(points * Ppi / 72.0, MidpointRounding.ToEven);

    /// <summary>The rotated-text and ABCD font size: round(thickness * 7 * PPI / 72).</summary>
    public static int ThicknessFontSize(int thickness) => PointsToPixels(thickness * 7);

    /// <summary>Stop gap of the fitting search: 2 for plain and perspective, 3 for 3D and shadow.</summary>
    public static int FitGap(int mode) => mode is ThreeD or Shadow ? 3 : 2;

    /// <summary>
    /// The width-fit binary search (§2.2): lo = 2, hi = 152, s = 10; mid = lo + (hi-lo) div 2,
    /// s = round(mid*PPI/72), measure; equal stops, wider → hi = mid, narrower → lo = mid; repeat
    /// while |hi-lo| ≥ gap and hi ≠ lo. Returns the last computed s.
    /// </summary>
    /// <param name="widthAt">Text width for CreateFont(-s, face).</param>
    /// <param name="target">Box width |x1-x2|.</param>
    public static int FitSize(Func<int, int> widthAt, int target, int gap)
    {
        int lo = 2, hi = 152, s = 10;
        do
        {
            int mid = lo + (hi - lo) / 2;
            s = PointsToPixels(mid);
            int w = widthAt(s);
            if (w == target) break;
            if (w > target) hi = mid; else lo = mid;
        } while (Math.Abs(hi - lo) >= gap && hi != lo);
        return s;
    }

    /// <summary>Drop-shadow offset for direction 2..7 with d = s div 10.</summary>
    public static (int Dx, int Dy) ShadowOffset(int dir, int d) => dir switch
    {
        2 => (-d, -d),
        3 => (0, -d),
        4 => (d, -d),
        5 => (-d, d),
        6 => (0, d),
        _ => (d, d), // 7
    };

    /// <summary>3D text: layer positions k = 0..d (shade 9) and the face position, d = s div 3.</summary>
    public static ((int X, int Y)[] Layers, (int X, int Y) Face) ThreeDLayout(int dir, int s, int x0, int y0)
    {
        int d = s / 3;
        var layers = new (int X, int Y)[d + 1];
        for (int k = 0; k <= d; k++)
            layers[k] = dir switch
            {
                2 => (x0 + k, y0 + k),
                3 => (x0, y0 + k),
                4 => (x0 - k, y0 + k),
                5 => (x0 + k, y0 + d + 1 - k),
                6 => (x0, y0 + d + 1 - k),
                _ => (x0 + d + 1 - k, y0 + d + 1 - k), // 7
            };
        var face = dir switch
        {
            2 => (x0 + d + 1, y0 + d + 1),
            3 => (x0, y0 + d + 1),
            4 => (x0 - d - 1, y0 + d + 1),
            5 => (x0 + d + 1, y0),
            _ => (x0, y0), // 6, 7
        };
        return (layers, face);
    }

    /// <summary>
    /// Perspective text (§2.3): d = w div 8; copies k = 0..d with s-d+k &gt; 0 at cell height
    /// s-d+k and baseline yb-k (shade 9); the face at height s, baseline yb-d.
    /// </summary>
    public static ((int Height, int Baseline)[] Copies, (int Height, int Baseline) Face) PerspectiveLayout(int s, int boxWidth, int yb)
    {
        int d = boxWidth / 8;
        var copies = new List<(int, int)>();
        for (int k = 0; k <= d; k++)
            if (s - d + k > 0) copies.Add((s - d + k, yb - k));
        return (copies.ToArray(), (s, yb - d));
    }

    /// <summary>ABCD typing (§3): Backspace deletes, length &gt; 253 ignores, Enter commits, anything else appends.</summary>
    public enum KeyResult { Edited, Ignored, Commit }

    public static KeyResult ApplyKey(ref string text, char key)
    {
        if (key == '\b')
        {
            if (text.Length > 0) text = text[..^1];
            return KeyResult.Edited;
        }
        if (text.Length > 253) return KeyResult.Ignored;
        if (key == '\r') return KeyResult.Commit;
        if (key < ' ') return KeyResult.Ignored; // other control characters (Tab, Esc...): see decisions B57
        text += key;
        return KeyResult.Edited;
    }
}

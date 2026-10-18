namespace Crayons.Core.Studio;

/// <summary>The modifier panels of the left column (behavior/input.md §2.5).</summary>
public enum PanelId
{
    Thickness, BoxDepth, BoxDirection, O8, Font, Shadow, SO, TO, O9, BrushType, Roller, Size,
    MoveShape, Rug, Flip, ColorMode, Bubble, MiniShape, Unused, ThickThin, Grass, Spray,
    Symmetry, LightSource, PaintPatterns,
}

/// <summary>
/// One modifier panel: where it is drawn, its outer click rect, how a click maps to
/// a value, and which bitmap shows the current value. All tables are from
/// input.md §2.5, shell.md §3.4 and spec §8.3.
/// </summary>
public sealed class PanelDef
{
    public required PanelId Id { get; init; }
    /// <summary>Row origin y (230/280/330/380); panels are blitted at (0, RowY).</summary>
    public required int RowY { get; init; }
    /// <summary>Clicks inside this rect belong to the panel (and play SND96 unless silent).</summary>
    public required HitRect Outer { get; init; }
    public bool Silent { get; init; }
    /// <summary>Applies a click at (x, y) inside <see cref="Outer"/>; returns true when something changed.</summary>
    public required Func<StudioState, int, int, bool> Click { get; init; }
    /// <summary>Bitmap file (without extension) for the current state, or null.</summary>
    public required Func<StudioState, string?> Art { get; init; }
    /// <summary>Clicks that open a dialog (Paint Patterns, Font). Not built in Phase 1.</summary>
    public bool OpensDialog { get; init; }
}

public static class Panels
{
    static readonly HitRect A = Hotspots.Band('A'), B = Hotspots.Band('B'), C = Hotspots.Band('C'), D = Hotspots.Band('D');

    /// <summary>Value → bitmap for panels with a fixed table (spec §8.3).</summary>
    static Func<StudioState, string?> Map(Func<StudioState, int> get, params (int Value, string File)[] map) =>
        s => { int v = get(s); foreach (var (val, f) in map) if (val == v) return f; return null; };

    static Func<StudioState, int, int, bool> Halves(Func<StudioState, int> get, Action<StudioState, int> set, int left, int right) =>
        (s, x, _) => Assign(s, get, set, Hotspots.HalfAt(x) == 0 ? left : right);

    static Func<StudioState, int, int, bool> Thirds(Func<StudioState, int> get, Action<StudioState, int> set, int v0, int v1, int v2) =>
        (s, x, _) => Hotspots.ThirdAt(x) switch
        {
            0 => Assign(s, get, set, v0),
            1 => Assign(s, get, set, v1),
            2 => Assign(s, get, set, v2),
            _ => false, // dead pixel: SND96 still plays, nothing changes
        };

    static Func<StudioState, int, int, bool> Rects(Func<StudioState, int> get, Action<StudioState, int> set, params (HitRect R, int Value)[] rects) =>
        (s, x, y) =>
        {
            foreach (var (r, v) in rects) if (r.Contains(x, y)) return Assign(s, get, set, v);
            return false;
        };

    static Func<StudioState, int, int, bool> Arrows(Func<StudioState, int> get, Action<StudioState, int> set) =>
        (s, x, y) =>
        {
            if (new HitRect(4, 256, 24, 277).Contains(x, y)) return Assign(s, get, set, Math.Max(1, get(s) - 1));
            if (new HitRect(54, 256, 76, 277).Contains(x, y)) return Assign(s, get, set, Math.Min(10, get(s) + 1));
            return false;
        };

    static bool Assign(StudioState s, Func<StudioState, int> get, Action<StudioState, int> set, int v)
    {
        bool changed = get(s) != v;
        set(s, v);
        return changed;
    }

    /// <summary>All panels in the original's redraw order (shell.md §3.4 table order).</summary>
    public static readonly IReadOnlyList<PanelDef> All = new PanelDef[]
    {
        new() { Id = PanelId.Thickness, RowY = 230, Outer = new(0, 256, 80, 277),
            Click = Arrows(s => s.Thickness, (s, v) => s.Thickness = v),
            Art = s => $"A{30 - Math.Clamp(s.Thickness, 1, 10)}" },
        new() { Id = PanelId.BoxDepth, RowY = 230, Outer = new(0, 256, 80, 277),
            Click = Arrows(s => s.BoxDepth, (s, v) => s.BoxDepth = v),
            Art = s => $"A{80 - Math.Clamp(s.BoxDepth, 1, 10)}" },
        new() { Id = PanelId.BoxDirection, RowY = 280, Outer = B,
            Click = Rects(s => s.BoxDir, (s, v) => s.BoxDir = v,
                (new(0, 280, 40, 305), 2), (new(40, 280, 80, 305), 4), (new(0, 305, 40, 330), 5), (new(40, 305, 80, 330), 7)),
            Art = Map(s => s.BoxDir, (2, "A80"), (4, "A81"), (5, "A82"), (7, "A83")) },
        new() { Id = PanelId.O8, RowY = 330, Outer = C,
            Click = Halves(s => s.O8, (s, v) => s.O8 = v, 1, 2),
            Art = Map(s => s.O8, (1, "A51"), (2, "A52")) },
        new() { Id = PanelId.Font, RowY = 280, Outer = B, Silent = true, OpensDialog = true,
            Click = (_, _, _) => false, Art = _ => "A84" },
        new() { Id = PanelId.Shadow, RowY = 330, Outer = C,
            Click = (s, x, y) =>
            {
                int t = Hotspots.ThirdAt(x);
                if (t < 0) return false;
                if (y >= 330 && y < 355) return Assign(s, q => q.Shadow, (q, v) => q.Shadow = v, 2 + t);
                if (y >= 356 && y < 380) return Assign(s, q => q.Shadow, (q, v) => q.Shadow = v, 5 + t);
                return false;
            },
            Art = Map(s => s.Shadow, (2, "A90"), (3, "A91"), (4, "A92"), (5, "A93"), (6, "A94"), (7, "A95")) },
        new() { Id = PanelId.SO, RowY = 330, Outer = C,
            Click = Halves(s => s.SO, (s, v) => s.SO = v, 1, 2),
            Art = Map(s => s.SO, (1, "A53"), (2, "A54")) },
        new() { Id = PanelId.TO, RowY = 330, Outer = C,
            Click = Thirds(s => s.TO, (s, v) => s.TO = v, 1, 2, 3),
            Art = Map(s => s.TO, (1, "A55"), (2, "A56"), (3, "A57")) },
        new() { Id = PanelId.O9, RowY = 330, Outer = C,
            Click = Halves(s => s.O9, (s, v) => s.O9 = v, 3, 4),
            Art = Map(s => s.O9, (3, "A61"), (4, "A62")) },
        new() { Id = PanelId.BrushType, RowY = 280, Outer = B,
            Click = Rects(s => s.BrushType, (s, v) => s.BrushType = v,
                (new(6, 282, 32, 308), 1), (new(5, 313, 38, 323), 3), (new(40, 282, 63, 310), 2), (new(64, 296, 76, 326), 4)),
            Art = Map(s => s.BrushType, (1, "A11"), (2, "A12"), (3, "A13"), (4, "A14")) },
        new() { Id = PanelId.Roller, RowY = 280, Outer = B,
            Click = Halves(s => s.RollerDir, (s, v) => s.RollerDir = v, 4, 3),
            Art = Map(s => s.RollerDir, (4, "A7"), (3, "A6")) },
        new() { Id = PanelId.Size, RowY = 280, Outer = B,
            Click = Halves(s => s.Size, (s, v) => s.Size = v, 2, 1),
            Art = Map(s => s.Size, (2, "A64"), (1, "A65")) },
        new() { Id = PanelId.MoveShape, RowY = 230, Outer = A,
            Click = (s, x, y) =>
            {
                bool left = Hotspots.HalfAt(x) == 0;
                bool c = Assign(s, q => q.MoveShape, (q, v) => q.MoveShape = v, left ? 1 : 2);
                s.Mode = left ? 0x3F : 0x40;
                return c;
            },
            Art = Map(s => s.MoveShape, (1, "A8"), (2, "A9")) },
        new() { Id = PanelId.Rug, RowY = 230, Outer = A,
            Click = Halves(s => s.RugShape, (s, v) => s.RugShape = v, 2, 1),
            Art = Map(s => s.RugShape, (2, "A8"), (1, "A9")) },
        new() { Id = PanelId.Flip, RowY = 230, Outer = A,
            Click = Halves(s => s.Flip, (s, v) => s.Flip = v, 4, 3),
            Art = Map(s => s.Flip, (4, "A66"), (3, "A67")) },
        new() { Id = PanelId.ColorMode, RowY = 380, Outer = D,
            Click = (s, x, _) => { bool v = x < 40; bool c = s.MultiColor != v; s.MultiColor = v; return c; },
            Art = s => s.MultiColor ? "A17" : "A18" },
        new() { Id = PanelId.Bubble, RowY = 230, Outer = A,
            Click = (s, x, y) =>
            {
                bool left = Hotspots.HalfAt(x) == 0;
                bool c = Assign(s, q => q.Bubble, (q, v) => q.Bubble = v, left ? 1 : 2);
                s.Mode = left ? 0x1B : 0x1A;
                return c;
            },
            Art = Map(s => s.Bubble, (1, "A15"), (2, "A16")) },
        new() { Id = PanelId.MiniShape, RowY = 230, Outer = A,
            Click = Thirds(s => s.MiniShape, (s, v) => s.MiniShape = v, 3, 1, 2),
            Art = Map(s => s.MiniShape, (3, "A30"), (1, "A31"), (2, "A32")) },
        new() { Id = PanelId.Unused, RowY = 230, Outer = A,
            Click = (s, x, y) =>
            {
                int t = Hotspots.ThirdAt(x);
                if (t < 0) return false;
                bool c = Assign(s, q => q.MiniShapeAlt, (q, v) => q.MiniShapeAlt = v, t + 1);
                s.Mode = t switch { 0 => 0x1E, 1 => 0x1D, _ => 0x14 };
                return c;
            },
            Art = Map(s => s.MiniShapeAlt, (1, "A33"), (2, "A34"), (3, "A35")) },
        new() { Id = PanelId.ThickThin, RowY = 230, Outer = A,
            Click = (s, x, y) =>
            {
                bool left = Hotspots.HalfAt(x) == 0;
                bool c = Assign(s, q => q.LineShape, (q, v) => q.LineShape = v, left ? 2 : 1);
                s.Mode = left ? 0x0D : 0x0C;
                return c;
            },
            Art = Map(s => s.LineShape, (2, "A36"), (1, "A37")) },
        new() { Id = PanelId.Grass, RowY = 230, Outer = A,
            Click = Thirds(s => s.Grass, (s, v) => s.Grass = v, 1, 2, 3),
            Art = Map(s => s.Grass, (1, "A38"), (2, "A39"), (3, "A40")) },
        new() { Id = PanelId.Spray, RowY = 280, Outer = B,
            Click = Halves(s => s.SprayMode, (s, v) => s.SprayMode = v, 1, 3),
            Art = Map(s => s.SprayMode, (1, "A41"), (3, "A42")) },
        new() { Id = PanelId.Symmetry, RowY = 280, Outer = B,
            Click = Thirds(s => s.Symmetry, (s, v) => s.Symmetry = v, 2, 1, 3),
            Art = Map(s => s.Symmetry, (2, "A58"), (1, "A59"), (3, "A60")) },
        new() { Id = PanelId.LightSource, RowY = 230, Outer = new(0, 230, 80, 310),
            Click = (s, x, y) =>
            {
                int t = Hotspots.ThirdAt(x);
                int? v = null;
                if (y >= 230 && y < 256 && t >= 0) v = 2 + t;
                else if (y >= 258 && y < 283 && t == 0) v = 9;
                else if (y >= 258 && y < 283 && t == 2) v = 8;
                else if (y >= 285 && y < 310 && t >= 0) v = 5 + t;
                return v is int val && Assign(s, q => q.LightSource, (q, nv) => q.LightSource = nv, val);
            },
            Art = Map(s => s.LightSource, (2, "A43"), (3, "A44"), (4, "A45"), (5, "A49"), (6, "A48"), (7, "A47"), (8, "A46"), (9, "A50")) },
        new() { Id = PanelId.PaintPatterns, RowY = 280, Outer = B, Silent = true, OpensDialog = true,
            Click = (_, _, _) => false, Art = _ => "A3" },
    };

    static readonly Dictionary<PanelId, PanelDef> ById = All.ToDictionary(p => p.Id);

    public static PanelDef Get(PanelId id) => ById[id];
}

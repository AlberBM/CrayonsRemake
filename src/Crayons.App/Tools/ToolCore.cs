using Crayons.App.Rendering;
using Crayons.Core;
using Crayons.Core.Studio;
using Crayons.Core.Tools;

namespace Crayons.App.Tools;

/// <summary>One GDI segment drawn on the canvas (recorded for tests).</summary>
public readonly record struct Segment(int X0, int Y0, int X1, int Y1, int Width, int Rgb, bool Xor = false);

/// <summary>
/// Draws on the canvas client surface with real GDI, the way the original's
/// one-segment helper did: CreatePen(PS_SOLID, width, colour), MoveTo, LineTo
/// (end pixel excluded), R2_COPYPEN. Coordinates are canvas client coordinates.
/// </summary>
public sealed class CanvasPainter
{
    public CanvasPainter(DibSurface surface) => Surface = surface;

    public DibSurface Surface { get; }

    public IntPtr Hdc => Surface.Hdc;

    /// <summary>When set, every segment is appended here (used by tests).</summary>
    public List<Segment>? Log { get; set; }

    public void Segment(int x0, int y0, int x1, int y1, int width, int rgb)
    {
        Log?.Add(new Segment(x0, y0, x1, y1, width, rgb));
        IntPtr hdc = Surface.Hdc;
        using var pen = Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, width, Gdi.Rgb(rgb)));
        Gdi.MoveToEx(hdc, x0, y0, IntPtr.Zero);
        Gdi.LineTo(hdc, x1, y1);
    }

    /// <summary>A 1-px R2_XORPEN line (rubber band), then back to R2_COPYPEN.</summary>
    public void XorSegment(int x0, int y0, int x1, int y1, int rgb)
    {
        Log?.Add(new Segment(x0, y0, x1, y1, 1, rgb, Xor: true));
        IntPtr hdc = Surface.Hdc;
        Gdi.SetROP2(hdc, Gdi.R2_XORPEN);
        using (var pen = Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, 1, Gdi.Rgb(rgb))))
        {
            Gdi.MoveToEx(hdc, x0, y0, IntPtr.Zero);
            Gdi.LineTo(hdc, x1, y1);
        }
        Gdi.SetROP2(hdc, Gdi.R2_COPYPEN);
    }

    /// <summary>SetPixel in plain RGB.</summary>
    public void Pixel(int x, int y, int rgb) => Gdi.SetPixel(Surface.Hdc, x, y, Gdi.Rgb(rgb));

    /// <summary>An ellipse with a width-<paramref name="penWidth"/> pen and a solid brush (hollow when <paramref name="fillRgb"/> is null).</summary>
    public void Ellipse(int l, int t, int r, int b, int penWidth, int penRgb, int? fillRgb)
    {
        IntPtr hdc = Surface.Hdc;
        using var pen = Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, penWidth, Gdi.Rgb(penRgb)));
        using var br = fillRgb is int f ? Gdi.Use(hdc, Gdi.CreateSolidBrush(Gdi.Rgb(f))) : Gdi.Use(hdc, Gdi.GetStockObject(Gdi.NULL_BRUSH), delete: false);
        Gdi.Ellipse(hdc, l, t, r, b);
    }

    /// <summary>Draws a shape with whatever pen and brush are selected.</summary>
    public static void Primitive(IntPtr hdc, ShapeGeom g)
    {
        switch (g.Kind)
        {
            case ShapeKind.Ellipse: Gdi.Ellipse(hdc, g.L, g.T, g.R, g.B); break;
            case ShapeKind.Rectangle: Gdi.Rectangle(hdc, g.L, g.T, g.R, g.B); break;
            case ShapeKind.RoundRect: Gdi.RoundRect(hdc, g.L, g.T, g.R, g.B, g.CornerW, g.CornerH); break;
            default:
                var pts = g.Points!.Select(p => new Gdi.POINT(p.X, p.Y)).ToArray();
                Gdi.Polygon(hdc, pts, pts.Length);
                break;
        }
    }

    /// <summary>The XOR rubber band of a shape: R2_XORPEN, 1-px white pen, hollow brush.</summary>
    public void XorOutline(ShapeGeom g)
    {
        IntPtr hdc = Surface.Hdc;
        Gdi.SetROP2(hdc, Gdi.R2_XORPEN);
        using (Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, 1, Gdi.Rgb(0xFFFFFF))))
        using (Gdi.Use(hdc, Gdi.GetStockObject(Gdi.NULL_BRUSH), delete: false))
            Primitive(hdc, g);
        Gdi.SetROP2(hdc, Gdi.R2_COPYPEN);
    }
}

/// <summary>
/// A timed, non-blocking replacement for one of the original's busy-loop animations
/// (spec §3.5). The host calls <see cref="Step"/> once each time more than
/// <see cref="IntervalMs"/> has elapsed; input is ignored until it returns false.
/// </summary>
public interface IAnimation
{
    int IntervalMs { get; }

    /// <summary>One frame. Returns false when the animation has finished (including its cleanup).</summary>
    bool Step();
}

/// <summary>What a tool can use: state, the shared random generator, the canvas, sounds, assets and animations.</summary>
public sealed class ToolContext
{
    public required StudioState State { get; init; }
    public required DelphiRandom Random { get; init; }
    public required CanvasPainter Canvas { get; init; }
    public Action<int> PlaySound { get; init; } = _ => { };

    /// <summary>An image from the user's install ("PA00.ASL", "A932.ASL"...), or null when missing.</summary>
    public Func<string, DibSurface?> Image { get; init; } = _ => null;

    /// <summary>Starts a timed animation. The default (tests) runs it to completion synchronously.</summary>
    public Action<IAnimation> StartAnimation { get; init; } = a => { while (a.Step()) { } };

    /// <summary>Colour picked for the current move event (clown colour or the current colour).</summary>
    public int SegmentColor { get; set; }

    /// <summary>Undo buffer A (the original's canvas snapshot bitmap), or null in tests that do not need it.</summary>
    public DibSurface? Backing { get; init; }

    /// <summary>Canvas → A (FUN_1028_0127).</summary>
    public void SnapshotCanvas() => Backing?.CopyFrom(Canvas.Surface);

    /// <summary>A → canvas (FUN_1028_0281).</summary>
    public void RestoreCanvas()
    {
        if (Backing != null) Canvas.Surface.CopyFrom(Backing);
    }

    /// <summary>Shows TextInpt modally; the callback runs after it closes (tests: immediately).</summary>
    public Action<Action> AskText { get; init; } = done => done();

    /// <summary>Shows the Rotator modally; the callback runs after it closes (tests: immediately).</summary>
    public Action<Action> AskRotation { get; init; } = done => done();

    /// <summary>A bitmap file by full path (clip art), or null. The caller owns the surface.</summary>
    public Func<string, DibSurface?> LoadPath { get; init; } = _ => null;
}

/// <summary>
/// A timed effect written as an iterator: the code up to the first <c>yield</c> runs at once when the
/// animation is created; each yielded value is the wait (ms, "more than") before the next part runs.
/// The code after the last yield is the cleanup and runs in the final <see cref="Step"/>.
/// </summary>
public sealed class StepAnimation : IAnimation
{
    readonly IEnumerator<int> _steps;
    bool _alive;

    public StepAnimation(IEnumerable<int> steps)
    {
        _steps = steps.GetEnumerator();
        _alive = _steps.MoveNext();
        IntervalMs = _alive ? _steps.Current : 0;
    }

    public int IntervalMs { get; private set; }

    public bool Step()
    {
        if (!_alive) return false;
        _alive = _steps.MoveNext();
        if (_alive) IntervalMs = _steps.Current;
        else _steps.Dispose();
        return _alive;
    }
}

/// <summary>
/// The box rubber band shared by the Text and Change It box tools (FUN_1018_2118): a 1-px white
/// R2_XORPEN outline from the anchor to the pointer, erased at the previous point and redrawn at
/// the current one. On release the band is erased and the action fires only when both X and Y differ
/// from the press point.
/// </summary>
public abstract class BoxTool : ITool
{
    /// <summary>XOR outline as one closed polyline (each corner pixel drawn once).</summary>
    public static void XorBox(CanvasPainter p, int x0, int y0, int x1, int y1)
    {
        IntPtr hdc = p.Hdc;
        Gdi.SetROP2(hdc, Gdi.R2_XORPEN);
        using (Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, 1, Gdi.Rgb(0xFFFFFF))))
        {
            Gdi.MoveToEx(hdc, x0, y0, IntPtr.Zero);
            Gdi.LineTo(hdc, x1, y0);
            Gdi.LineTo(hdc, x1, y1);
            Gdi.LineTo(hdc, x0, y1);
            Gdi.LineTo(hdc, x0, y0);
        }
        Gdi.SetROP2(hdc, Gdi.R2_COPYPEN);
    }

    public virtual void Down(ToolContext c, int x, int y) => XorBox(c.Canvas, c.State.AnchorX, c.State.AnchorY, x, y);

    public virtual void Move(ToolContext c, int x, int y)
    {
        var s = c.State;
        XorBox(c.Canvas, s.AnchorX, s.AnchorY, s.PrevX, s.PrevY);
        XorBox(c.Canvas, s.AnchorX, s.AnchorY, x, y);
    }

    public virtual void Up(ToolContext c, int x, int y)
    {
        var s = c.State;
        XorBox(c.Canvas, s.AnchorX, s.AnchorY, s.PrevX, s.PrevY);
        if (x != s.AnchorX && y != s.AnchorY)
            Action(c, Crayons.Core.Tools.ChangeItMath.Normalize(s.AnchorX, s.AnchorY, x, y));
    }

    /// <summary>The box action, with the normalised box.</summary>
    protected abstract void Action(ToolContext c, Crayons.Core.Tools.Box box);
}

/// <summary>A drawing mode. Coordinates are canvas client coordinates.</summary>
public interface ITool
{
    void Down(ToolContext c, int x, int y);
    void Move(ToolContext c, int x, int y);
    void Up(ToolContext c, int x, int y) { }
}

/// <summary>
/// The canvas mouse life cycle shared by every mode (tools-lines.md "Stroke life cycle"):
/// prev/anchor bookkeeping (C4), the mode's mouse-down sound, the per-move segment
/// colour (clown = table[Random(159)] once per move event) and the mouse-up sound.
/// </summary>
public sealed class ToolDispatcher
{
    /// <summary>Parallelogram step 2: tracks the mouse without a button and keeps its anchor.</summary>
    public const int TrackingMode = 0x37;

    readonly ToolContext _c;
    readonly IReadOnlyDictionary<int, ITool> _tools;

    public ToolDispatcher(ToolContext context, IReadOnlyDictionary<int, ITool>? tools = null)
    {
        _c = context;
        _tools = tools ?? ToolSet.Create();
    }

    StudioState S => _c.State;

    /// <summary>The shared tool context (ABCD keys and commits come from the studio, not the canvas).</summary>
    public ToolContext Context => _c;

    ITool? Current => _tools.TryGetValue(S.Mode, out var t) ? t : null;

    /// <summary>True when moves must reach the tool although no button is held.</summary>
    public bool TracksWithoutButton => S.Mode == TrackingMode;

    public void Down(int x, int y)
    {
        S.StrokeActive = true;
        if (S.Mode != TrackingMode)
        {
            S.PrevX = S.AnchorX = x;
            S.PrevY = S.AnchorY = y;
        }
        if (ModeSounds.Down(S.Mode) is int snd) _c.PlaySound(snd);
        _c.SegmentColor = S.CurrentColor;
        Current?.Down(_c, x, y);
    }

    public void Move(int x, int y)
    {
        if (!(S.StrokeActive || TracksWithoutButton) || S.Mode == 0x10) return;
        _c.SegmentColor = S.MultiColor ? ColorTable.Get(_c.Random.Next(159)) : S.CurrentColor;
        Current?.Move(_c, x, y);
        S.PrevX = x;
        S.PrevY = y;
    }

    public void Up(int x, int y)
    {
        if (!S.StrokeActive) return;
        S.StrokeActive = false;
        int mode = S.Mode; // a two-step tool may switch modes in Up; the sound belongs to the old mode
        Current?.Up(_c, x, y);
        if (ModeSounds.Up(mode, x != S.AnchorX, y != S.AnchorY) is int snd) _c.PlaySound(snd);
    }
}

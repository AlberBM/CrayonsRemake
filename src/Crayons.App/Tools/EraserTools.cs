using Crayons.App.Rendering;
using Crayons.Core;
using Crayons.Core.Tools;

namespace Crayons.App.Tools;

/// <summary>
/// The Eraser tab (behavior/tools-text-edit.md §4). Every effect except the square eraser ends with
/// the whole canvas white. The original drew these straight to the screen inside busy loops; the remake
/// runs them as timed <see cref="StepAnimation"/>s (spec §3.5) with the pacing listed in decisions B52.
/// </summary>
public static class EraserTools
{
    const int W = EraserMath.W, H = EraserMath.H;

    /// <summary>Host ticks a new-slate wipe and the window shade take: 2 (~31 ms; observed &lt; 40 ms, spec §9.1 R17).</summary>
    public const int NewSlateTicks = 2, WindowShadeTicks = 2;

    /// <summary>Crumple: one step per host tick ("more than 14 ms"), 9 steps plus the final white, about 140 ms (R17).</summary>
    public const int ShrinkStepMs = 40, CrumpleStepMs = 14;

    static void Whiteness(DibSurface canvas) => Gdi.PatBlt(canvas.Hdc, 0, 0, W, H, Gdi.WHITENESS);

    /// <summary>Slot 0, new slate: Random(8)+1 picks the wipe (SND65..72), then the canvas turns white.</summary>
    public static IAnimation NewSlate(ToolContext c) => new StepAnimation(NewSlateSteps(c, c.Random.Next(8) + 1));

    static IEnumerable<int> NewSlateSteps(ToolContext c, int variant)
    {
        c.PlaySound(EraserMath.NewSlateSound(variant));
        var steps = EraserMath.NewSlateSteps(variant);
        int perTick = Math.Max(1, (steps.Count + NewSlateTicks - 1) / NewSlateTicks);
        IntPtr hdc = c.Canvas.Hdc;
        bool lines = EraserMath.NewSlateUsesLines(variant);
        for (int i = 0; i < steps.Count; i++)
        {
            foreach (var b in steps[i])
            {
                if (lines) Gdi.Line(hdc, b.L, b.T, b.R, b.B, 0xFFFFFF);
                else Gdi.FillRect(hdc, b.L, b.T, b.R, b.B, 0xFFFFFF);
            }
            if ((i + 1) % perTick == 0) yield return 0;
        }
        Whiteness(c.Canvas.Surface);
    }

    /// <summary>Slot 1, explode (mode 0x2F): the A930 animation, 79 rings, white. Then Mode = 0.</summary>
    public sealed class Explode : ITool
    {
        public void Down(ToolContext c, int x, int y)
        {
            c.StartAnimation(new StepAnimation(Steps(c, x, y)));
            c.State.Mode = 0; // "on mouse-up the mode is set to 0": the option has to be chosen again
        }

        public void Move(ToolContext c, int x, int y) { }

        public static IEnumerable<int> Steps(ToolContext c, int x, int y)
        {
            var canvas = c.Canvas.Surface;
            c.PlaySound(62);
            var sheet = c.Image("A930.ASL");
            int px = x - 38, py = y - 25, rgb = c.State.CurrentColor;
            using var patch = new DibSurface(EraserMath.ExplodeW, EraserMath.ExplodeH, 0xFFFFFF);
            patch.DrawPart(canvas, 0, 0, px, py, patch.Width, patch.Height);
            for (int step = 1; step <= 8; step++)
            {
                if (step > 1) yield return EraserMath.ExplodeIntervalMs;
                Gdi.BitBlt(canvas.Hdc, px, py, patch.Width, patch.Height, patch.Hdc, 0, 0, Gdi.SRCCOPY);
                if (sheet != null)
                {
                    int sx = 1 + 80 * EraserMath.ExplodeFrame(step);
                    Gdi.BitBlt(canvas.Hdc, px, py, EraserMath.ExplodeW, EraserMath.ExplodeH, sheet.Hdc, sx, 54, Gdi.SRCAND);
                    Gdi.BitBlt(canvas.Hdc, px, py, EraserMath.ExplodeW, EraserMath.ExplodeH, sheet.Hdc, sx, 1, Gdi.SRCPAINT);
                }
            }
            yield return EraserMath.ExplodeIntervalMs;
            IntPtr hdc = canvas.Hdc;
            for (int k = 2; k <= 80; k++)
            {
                var r = EraserMath.ExplodeRing(k, x, y);
                using (Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, 8, Gdi.Rgb(rgb))))
                using (Gdi.Use(hdc, Gdi.CreateSolidBrush(Gdi.Rgb(0xFFFFFF))))
                    Gdi.Ellipse(hdc, r.L, r.T, r.R, r.B);
                if (k % 10 == 0) yield return 0;
            }
            Whiteness(canvas);
        }
    }

    /// <summary>Slot 2, shrink away: SND63, snapshot, 12 shrinking copies of the snapshot, white.</summary>
    public static IAnimation ShrinkAway(ToolContext c) => new StepAnimation(ShrinkSteps(c));

    static IEnumerable<int> ShrinkSteps(ToolContext c)
    {
        c.PlaySound(63);
        c.SnapshotCanvas();
        var backing = c.Backing ?? c.Canvas.Surface;
        foreach (var _ in WackyTools.BarnZoomSteps(c.Canvas.Surface, backing)) yield return ShrinkStepMs;
        Whiteness(c.Canvas.Surface);
    }

    /// <summary>
    /// Slot 3, crumple: 9 steps of a random 51-point polygon clip with the snapshot stretched into a
    /// shrinking inset; each step first whitens the previous step's region (the whole canvas at step 1).
    /// </summary>
    public static IAnimation Crumple(ToolContext c) => new StepAnimation(CrumpleSteps(c));

    static IEnumerable<int> CrumpleSteps(ToolContext c)
    {
        var canvas = c.Canvas.Surface;
        var backing = c.Backing ?? canvas;
        using var source = new DibSurface(canvas.Width, canvas.Height);
        source.CopyFrom(backing);
        IntPtr hdc = canvas.Hdc, previous = IntPtr.Zero;
        for (int i = 1; i <= 9; i++)
        {
            if (i == 1) c.PlaySound(61);
            var inset = EraserMath.CrumpleInset(i);
            var pts = EraserMath.CrumplePoints(inset, c.Random).Select(p => new Gdi.POINT(p.X, p.Y)).ToArray();
            Gdi.SelectClipRgn(hdc, previous); // null at step 1: the whole canvas
            Whiteness(canvas);
            if (previous != IntPtr.Zero) Gdi.DeleteObject(previous);
            previous = Gdi.CreatePolygonRgn(pts, pts.Length, Gdi.ALTERNATE);
            Gdi.SelectClipRgn(hdc, previous);
            Gdi.StretchBlt(hdc, inset.L, inset.T, inset.Width, inset.Height, source.Hdc, 0, 0, W - 2, H - 2, Gdi.SRCCOPY);
            Gdi.SelectClipRgn(hdc, IntPtr.Zero);
            yield return CrumpleStepMs;
        }
        if (previous != IntPtr.Zero) Gdi.DeleteObject(previous);
        Whiteness(canvas);
    }

    /// <summary>Slot 4, window shade: the A63 ring pulls the picture up into the top band (SND64), then white.</summary>
    public static IAnimation WindowShade(ToolContext c) => new StepAnimation(WindowShadeSteps(c));

    static IEnumerable<int> WindowShadeSteps(ToolContext c)
    {
        var canvas = c.Canvas.Surface;
        IntPtr hdc = canvas.Hdc;
        c.PlaySound(64);
        var ring = c.Image("A63.ASL");
        int band = EraserMath.WindowShadeBand();
        using var temp = new DibSurface(canvas.Width, canvas.Height);
        for (int i = 1; i <= 20; i++)
        {
            int e = EraserMath.WindowShadeEdge(i);
            Gdi.PatBlt(hdc, 0, e, W, H - e, Gdi.WHITENESS);
            if (ring != null)
            {
                Gdi.BitBlt(hdc, W / 2 - 40, e, 80, 150, ring.Hdc, 80, 0, Gdi.SRCAND);
                Gdi.BitBlt(hdc, W / 2 - 40, e, 80, 150, ring.Hdc, 0, 0, Gdi.SRCPAINT);
            }
            if (e > band)
            {
                temp.CopyFrom(canvas); // StretchBlt within one surface: read from a copy
                Gdi.StretchBlt(hdc, 0, 0, W, band, temp.Hdc, 0, band, W, e - band, Gdi.SRCCOPY);
            }
            if (i % (20 / WindowShadeTicks) == 0) yield return 0; // R17: the 20 steps in 2 host ticks
        }
        Whiteness(canvas);
    }

    /// <summary>Slot 5, square eraser (mode 0x39): a white 20x20 square on press and every move (SND3 from the mode table).</summary>
    public sealed class Square : ITool
    {
        public void Down(ToolContext c, int x, int y) => Erase(c, x, y);
        public void Move(ToolContext c, int x, int y) => Erase(c, x, y);

        static void Erase(ToolContext c, int x, int y)
        {
            var b = EraserMath.SquareEraser(x, y);
            Gdi.FillRect(c.Canvas.Hdc, b.L, b.T, b.R, b.B, 0xFFFFFF);
        }
    }
}

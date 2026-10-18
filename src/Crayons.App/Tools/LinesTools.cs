using Crayons.Core;
using Crayons.Core.Tools;

namespace Crayons.App.Tools;

/// <summary>
/// The Crayon (Lines) tool family, per behavior/tools-lines.md. Every random call
/// is made in the documented order, including the ones whose results are unused.
/// </summary>
public static class LinesTools
{
    /// <summary>Mode 1, Crayon: a dot on press, then prev→current with width = thickness.</summary>
    public sealed class Crayon : ITool
    {
        public void Down(ToolContext c, int x, int y) => c.Canvas.Segment(x, y, x + 1, y, c.State.Thickness, c.State.CurrentColor);
        public void Move(ToolContext c, int x, int y) => c.Canvas.Segment(c.State.PrevX, c.State.PrevY, x, y, c.State.Thickness, c.SegmentColor);
    }

    /// <summary>Mode 0x17, Radial Line: the Crayon dot, then anchor→current on every move.</summary>
    public sealed class Radial : ITool
    {
        public void Down(ToolContext c, int x, int y) => c.Canvas.Segment(x, y, x + 1, y, c.State.Thickness, c.State.CurrentColor);
        public void Move(ToolContext c, int x, int y) => c.Canvas.Segment(c.State.AnchorX, c.State.AnchorY, x, y, c.State.Thickness, c.SegmentColor);
    }

    /// <summary>Mode 0x0A, Chalk: a width-12 line in the current colour plus a speck burst in the trio colours.</summary>
    public sealed class Chalk : ITool
    {
        public void Down(ToolContext c, int x, int y)
        {
            c.Canvas.Segment(x, y, x, y, 12, c.State.CurrentColor);
            Specks(c, x, y);
        }

        public void Move(ToolContext c, int x, int y)
        {
            c.Canvas.Segment(c.State.PrevX, c.State.PrevY, x, y, 12, c.State.CurrentColor);
            Specks(c, x, y);
        }

        static void Specks(ToolContext c, int x, int y)
        {
            var (t0, t1, t2) = c.State.Trio;
            foreach (int colour in new[] { t0, t1, t2 })
                for (int i = 0; i < 7; i++)
                {
                    int dx = c.Random.Next(16) - 8;
                    int dy = c.Random.Next(16) - 8;
                    if (DMath.Dist(dx, dy) <= 8)
                        c.Canvas.Segment(x + dx, y + dy, x + dx + 1, y + dy + 1, 1, colour);
                }
        }
    }

    /// <summary>Mode 5, Crazy Three Color Pen: thickness forced to 2 during the stroke; six fixed-colour strands.</summary>
    public sealed class CrazyPen : ITool
    {
        public void Down(ToolContext c, int x, int y)
        {
            c.State.SavedThickness = c.State.Thickness;
            c.State.Thickness = 2;
            Strands(c, x, y, x + 1, y);
        }

        public void Move(ToolContext c, int x, int y) => Strands(c, c.State.PrevX, c.State.PrevY, x, y);

        public void Up(ToolContext c, int x, int y) => c.State.Thickness = c.State.SavedThickness;

        static void Strands(ToolContext c, int x0, int y0, int x1, int y1)
        {
            var offs = LinesMath.CrazyPenOffsets(c.State.Thickness);
            for (int i = 0; i < offs.Length; i++)
            {
                var (ox, oy) = offs[i];
                c.Canvas.Segment(x0 + ox, y0 + oy, x1 + ox, y1 + oy, 2, ColorTable.Get(LinesMath.CrazyPenColorIndex(i)));
            }
        }
    }

    /// <summary>Mode 2, Corner: lines from the point to the four canvas corners (W x H = 558 x 386).</summary>
    public sealed class Corner : ITool
    {
        public void Down(ToolContext c, int x, int y) => Lines(c, x, y, c.State.CurrentColor);
        public void Move(ToolContext c, int x, int y) => Lines(c, x, y, c.SegmentColor);

        static void Lines(ToolContext c, int x, int y, int colour)
        {
            foreach (var (cx, cy) in LinesMath.Corners)
                c.Canvas.Segment(x, y, cx, cy, c.State.Thickness, colour);
        }
    }

    /// <summary>Mode 0x0B, Symmetry: prev→current plus mirrored copies about the canvas centre lines.</summary>
    public sealed class Symmetry : ITool
    {
        public void Down(ToolContext c, int x, int y) => Draw(c, x, y, x + 1, y + 1, c.State.CurrentColor);
        public void Move(ToolContext c, int x, int y) => Draw(c, c.State.PrevX, c.State.PrevY, x, y, c.SegmentColor);

        static void Draw(ToolContext c, int px, int py, int x, int y, int colour)
        {
            foreach (var (x0, y0, x1, y1) in LinesMath.SymmetrySegments(c.State.Symmetry, px, py, x, y))
                c.Canvas.Segment(x0, y0, x1, y1, c.State.Thickness, colour);
        }
    }

    /// <summary>Mode 0x13, Caterpillar: hair bursts of 1-px lines from the point, radius 6 x thickness.</summary>
    public sealed class Caterpillar : ITool
    {
        public void Down(ToolContext c, int x, int y) => Burst(c, x, y);
        public void Move(ToolContext c, int x, int y) => Burst(c, x, y);

        static void Burst(ToolContext c, int x, int y)
        {
            // In clown mode the burst makes one extra Random(16) whose result is unused.
            if (c.State.MultiColor) c.Random.Next(16);
            var (t0, t1, t2) = c.State.Trio;
            int radius = 6 * c.State.Thickness;
            foreach (int colour in new[] { t0, t2, t1 })
            {
                int k = c.Random.Next(50) + 20;
                for (int i = 0; i <= k; i++)
                {
                    int dx = c.Random.Next(60) - 30;
                    int dy = c.Random.Next(60) - 30;
                    if (DMath.Dist(dx, dy) <= radius)
                        c.Canvas.Segment(x, y, x + dx, y + dy, 1, colour);
                }
            }
        }
    }

    /// <summary>Modes 0x0C / 0x0D, Thick/Thin: the width depends on how far the mouse moved since the last event.</summary>
    public sealed class ThickThin : ITool
    {
        public void Down(ToolContext c, int x, int y) =>
            c.Canvas.Segment(x, y, x, y, LinesMath.ThickThinWidth(c.State.Mode, 0), c.State.CurrentColor);

        public void Move(ToolContext c, int x, int y)
        {
            int d = LinesMath.ThickThinDistance(x - c.State.PrevX, y - c.State.PrevY);
            c.Canvas.Segment(c.State.PrevX, c.State.PrevY, x, y, LinesMath.ThickThinWidth(c.State.Mode, d), c.State.CurrentColor);
        }
    }

    /// <summary>Mode 0x26, Straight Line: XOR rubber band (white 1-px pen) while dragging, committed on release.</summary>
    public sealed class StraightLine : ITool
    {
        static readonly int BandColor = ColorTable.Get(ColorTable.White);

        public void Down(ToolContext c, int x, int y) => Band(c, x, y);
        public void Move(ToolContext c, int x, int y) => Band(c, x, y);

        static void Band(ToolContext c, int x, int y)
        {
            var s = c.State;
            c.Canvas.XorSegment(s.AnchorX, s.AnchorY, s.PrevX, s.PrevY, BandColor);
            c.Canvas.XorSegment(s.AnchorX, s.AnchorY, x, y, BandColor);
        }

        public void Up(ToolContext c, int x, int y)
        {
            var s = c.State;
            if (x == s.AnchorX && y == s.AnchorY) return;
            c.Canvas.XorSegment(s.AnchorX, s.AnchorY, s.PrevX, s.PrevY, BandColor);
            c.Canvas.Segment(s.AnchorX, s.AnchorY, x, y, s.Thickness, s.CurrentColor);
        }
    }
}

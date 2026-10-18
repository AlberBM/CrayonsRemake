using Crayons.App.Rendering;
using Crayons.Core;
using Crayons.Core.Tools;

namespace Crayons.App.Tools;

/// <summary>
/// The Paintbrush family (behavior/tools-paint.md). Every random call is made in the
/// documented order. The clown modifier does not change any paint colour (the dispatcher
/// still makes its per-move Random(159) call when the flag is on).
/// </summary>
public static class PaintTools
{
    static int[] TrioColors(ToolContext c)
    {
        var (t0, t1, t2) = c.State.Trio;
        return new[] { t0, t1, t2 };
    }

    /// <summary>Mode 0x16, Dripping Paint: a thick line that sometimes grows a three-colour drip and blob.</summary>
    public sealed class Dripping : ITool
    {
        public void Down(ToolContext c, int x, int y) =>
            c.Canvas.Segment(x, y, x + 1, y, c.State.Thickness, c.State.CurrentColor);

        public void Move(ToolContext c, int x, int y)
        {
            var s = c.State;
            int t = s.Thickness;
            int yEnd = y + c.Random.Next(10 * t) + 10;
            c.Canvas.Segment(s.PrevX, s.PrevY, x, y, t, s.CurrentColor);
            if (c.Random.Next(10) >= 2) return;
            var trio = TrioColors(c);
            for (int k = 0; k < 3; k++) c.Canvas.Segment(x + k, y, x + k, yEnd, 1, trio[k]);
            int cx = x + 1, cy = yEnd;
            int rx = c.Random.Next(t) + 5;
            int ry = c.Random.Next(t) + 5;
            c.Canvas.Ellipse(cx - rx, cy - ry, cx + rx, cy + ry, 1, trio[0], trio[0]);
            rx -= rx / 4; ry -= ry / 4;
            c.Canvas.Ellipse(cx - rx, cy - ry, cx + rx, cy + ry, 1, trio[1], trio[1]);
            rx -= rx / 2; ry -= ry / 2;
            c.Canvas.Ellipse(cx - rx, cy - ry, cx + rx, cy + ry, 1, trio[2], trio[2]);
        }
    }

    /// <summary>
    /// The nib engine shared by Oil (0x15), Ribbon Cable (0x08), Magic Brush (0x0F) and
    /// Calligraphy (0x0E): 3t width-2 stripes offset by the brush type.
    /// </summary>
    public sealed class Nib : ITool
    {
        int[] _stripes = Array.Empty<int>();

        /// <summary>The stripe colours of the current stroke (for tests).</summary>
        public IReadOnlyList<int> Stripes => _stripes;

        void BuildStripes(ToolContext c)
        {
            var s = c.State;
            int n = 3 * s.Thickness;
            var trio = TrioColors(c);
            _stripes = new int[n];
            for (int i = 0; i < n; i++)
                _stripes[i] = s.Mode switch
                {
                    0x15 or 0x0F => trio[c.Random.Next(3)],
                    0x08 => ColorTable.Get(c.Random.Next(160)),
                    _ => s.CurrentColor,
                };
        }

        void Draw(ToolContext c, int px, int py, int x, int y)
        {
            var offs = PaintMath.NibOffsets(c.State.BrushType, c.State.Thickness);
            for (int i = 0; i < offs.Length; i++)
            {
                var (dx, dy) = offs[i];
                c.Canvas.Segment(px + dx, py + dy, x + dx, y + dy, 2, _stripes[i]);
            }
        }

        public void Down(ToolContext c, int x, int y)
        {
            BuildStripes(c);
            Draw(c, x, y, x + 1, y);
        }

        public void Move(ToolContext c, int x, int y)
        {
            if (c.State.Mode == 0x0F || _stripes.Length != 3 * c.State.Thickness) BuildStripes(c); // Magic: marble re-roll
            Draw(c, c.State.PrevX, c.State.PrevY, x, y);
        }
    }

    /// <summary>
    /// Mode 0x3A, Fill Bucket: ExtFloodFill(FLOODFILLSURFACE) on the exact target colour with the
    /// style's brush. Normal mode skips a target equal to the current colour; Magic Color mode
    /// skips black (table 69) so outlines are never filled.
    /// </summary>
    public sealed class FillBucket : ITool
    {
        public void Down(ToolContext c, int x, int y) => Fill(c, x, y);
        public void Move(ToolContext c, int x, int y) { }

        public static void Fill(ToolContext c, int x, int y)
        {
            var s = c.State;
            int target = c.Canvas.Surface.GetRgb(x, y);
            if (target < 0) return;
            IntPtr hdc = c.Canvas.Hdc;
            using var brush = FillBrush.Create(s.FillPattern, s.CurrentColor, c.Image);
            if (brush.Transparent) Gdi.SetBkMode(hdc, Gdi.TRANSPARENT);
            bool fill = s.MagicColorActive ? target != ColorTable.Get(ColorTable.Black) : target != s.CurrentColor;
            if (fill)
                using (Gdi.Use(hdc, brush.Handle, delete: false))
                    Gdi.ExtFloodFill(hdc, x, y, Gdi.Rgb(target), Gdi.FLOODFILLSURFACE);
            Gdi.SetBkMode(hdc, Gdi.OPAQUE);
        }
    }

    /// <summary>Mode 0x19, Roller: a speckled solid rectangle stamped on every down/move event.</summary>
    public sealed class Roller : ITool
    {
        public void Down(ToolContext c, int x, int y) => Stamp(c, x, y);
        public void Move(ToolContext c, int x, int y) => Stamp(c, x, y);

        static void Stamp(ToolContext c, int x, int y)
        {
            var s = c.State;
            var (hw, hh) = PaintMath.RollerHalfSize(s.RollerDir, s.Thickness);
            IntPtr hdc = c.Canvas.Hdc;
            using (Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, 1, Gdi.Rgb(s.CurrentColor))))
            using (Gdi.Use(hdc, Gdi.CreateSolidBrush(Gdi.Rgb(s.CurrentColor))))
                Gdi.Rectangle(hdc, x - hw, y - hh, x + hw, y + hh);
            var (_, t1, t2) = s.Trio;
            for (int i = 0; i <= 30 * s.Thickness; i++)
            {
                int ax = x + c.Random.Next(2 * hw) - hw;
                int ay = y + c.Random.Next(2 * hh) - hh;
                c.Canvas.Pixel(ax, ay, t1);
                int bx = x + c.Random.Next(2 * hw) - hw;
                int by = y + c.Random.Next(2 * hh) - hh;
                c.Canvas.Pixel(bx, by, t2);
            }
        }
    }

    /// <summary>Mode 0x10, Splatter (click only): thick rays ending in blobs around a central blob.</summary>
    public sealed class Splatter : ITool
    {
        public void Move(ToolContext c, int x, int y) { }

        public void Down(ToolContext c, int x, int y)
        {
            var s = c.State;
            int t = s.Thickness;
            IntPtr hdc = c.Canvas.Hdc;
            using var pen = Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, t, Gdi.Rgb(s.CurrentColor)));
            using var br = Gdi.Use(hdc, Gdi.CreateSolidBrush(Gdi.Rgb(s.CurrentColor)));
            int count = c.Random.Next(30) + 5;
            for (int k = 0; k <= count; k++)
            {
                int ex = x + c.Random.Next(20 * t) - 10 * t;
                int ey = y + c.Random.Next(20 * t) - 10 * t;
                int erx = c.Random.Next(t);
                int ery = c.Random.Next(t);
                Gdi.MoveToEx(hdc, x, y, IntPtr.Zero);
                Gdi.LineTo(hdc, ex, ey);
                Gdi.Ellipse(hdc, ex - erx - 1, ey - ery - 1, ex + erx + 1, ey + ery + 1);
            }
            int crx = c.Random.Next(4 * t);
            int cry = c.Random.Next(4 * t);
            Gdi.Ellipse(hdc, x - crx - 1, y - cry - 1, x + crx + 1, y + cry + 1);
        }
    }

    /// <summary>Mode 9, Spray: one burst per down/move event, small (pixels) or big (tri-colour dots).</summary>
    public sealed class Spray : ITool
    {
        public void Down(ToolContext c, int x, int y) => Burst(c, x, y);
        public void Move(ToolContext c, int x, int y) => Burst(c, x, y);

        static void Burst(ToolContext c, int x, int y)
        {
            var s = c.State;
            int t = s.Thickness;
            int count = PaintMath.SprayCount(t), r = PaintMath.SprayRadius(t);
            if (s.SprayMode != 3)
            {
                for (int k = 0; k <= count; k++)
                {
                    int dx = c.Random.Next(2 * r) - r;
                    int dy = c.Random.Next(2 * r) - r;
                    if (PaintMath.SprayAccepts(dx, dy, r)) c.Canvas.Pixel(x + dx, y + dy, s.CurrentColor);
                }
                return;
            }
            foreach (int colour in TrioColors(c))
                for (int k = 0; k <= count / 3; k++)
                {
                    int dx = c.Random.Next(2 * r) - r;
                    int dy = c.Random.Next(2 * r) - r;
                    if (!PaintMath.SprayAccepts(dx, dy, r)) continue;
                    if (t < 2) c.Canvas.Segment(x + dx, y + dy, x + dx + 1, y + dy + 1, t, colour);
                    else Dot(c, x + dx, y + dy, t, colour);
                }
        }

        /// <summary>
        /// A zero-length width-t LineTo, assumed to draw a round dot (tools-paint.md §8, Inferred).
        /// Modern GDI draws nothing for widths below 3, so a 2-px dot is drawn as a 2x2 square.
        /// </summary>
        static void Dot(ToolContext c, int x, int y, int width, int rgb)
        {
            if (width >= 3) { c.Canvas.Segment(x, y, x, y, width, rgb); return; }
            c.Canvas.Log?.Add(new Segment(x, y, x, y, width, rgb));
            Gdi.FillRect(c.Canvas.Hdc, x - 1, y - 1, x + 1, y + 1, rgb);
        }
    }

    /// <summary>Mode 0x2A, Throw Rugs (click only): 30..89 concentric outlines in random colours.</summary>
    public sealed class ThrowRugs : ITool
    {
        public void Move(ToolContext c, int x, int y) { }

        public void Down(ToolContext c, int x, int y)
        {
            IntPtr hdc = c.Canvas.Hdc;
            var pens = new IntPtr[101];
            for (int i = 0; i < pens.Length; i++) pens[i] = Gdi.CreatePen(Gdi.PS_SOLID, 2, ColorTable.ColorRef(c.Random.Next(160)));
            IntPtr oldPen = Gdi.SelectObject(hdc, pens[0]);
            bool circle = c.State.RugShape == 1;
            try
            {
                using (Gdi.Use(hdc, Gdi.GetStockObject(Gdi.NULL_BRUSH), delete: false))
                {
                    int n = c.Random.Next(60) + 30;
                    for (int i = 1; i <= n; i++)
                    {
                        Gdi.SelectObject(hdc, pens[c.Random.Next(100)]);
                        if (circle) Gdi.Ellipse(hdc, x - i, y - i, x + i, y + i);
                        else Gdi.Rectangle(hdc, x - i, y - i, x + i, y + i);
                    }
                }
            }
            finally
            {
                Gdi.SelectObject(hdc, oldPen);
                foreach (var p in pens) Gdi.DeleteObject(p);
            }
            c.PlaySound(circle ? 77 : 78);
        }
    }
}

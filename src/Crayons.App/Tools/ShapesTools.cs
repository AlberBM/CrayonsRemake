using Crayons.App.Rendering;
using Crayons.Core;
using Crayons.Core.Studio;
using Crayons.Core.Tools;

namespace Crayons.App.Tools;

/// <summary>
/// The Shapes tab and the 3D tab (behavior/tools-shapes.md). Every shape previews with an
/// XOR rubber band (erase at the previous point, draw at the current one) and renders on
/// release only when X and Y both changed (the 3D line: either).
/// </summary>
public static class ShapesTools
{
    /// <summary>
    /// Final rendering of a Shapes-tab shape by fill style: styles below 16 use the current-colour
    /// pen (width = thickness) and the style brush (TRANSPARENT below 8); 16..23 a region-clipped
    /// gradient; 24..31 region-clipped PB tiles. Gradients and pictures draw no outline.
    /// </summary>
    public static void Render(ToolContext c, ShapeGeom g)
    {
        var s = c.State;
        IntPtr hdc = c.Canvas.Hdc;
        Gdi.SetROP2(hdc, Gdi.R2_COPYPEN);
        switch (PaintMath.BrushKind(s.FillPattern))
        {
            case FillBrushKind.Gradient:
            {
                IntPtr rgn = FillRenderer.CreateRegion(g);
                FillRenderer.Clipped(hdc, rgn, () => FillRenderer.Gradient(hdc, s.FillPattern, g.Box, s.ColorIndex));
                Gdi.DeleteObject(rgn);
                break;
            }
            case FillBrushKind.Picture:
            {
                IntPtr rgn = FillRenderer.CreateRegion(g);
                var pic = c.Image(PaintMath.PictureFile(s.FillPattern));
                FillRenderer.Clipped(hdc, rgn, () => FillRenderer.Picture(hdc, pic, g.Box));
                Gdi.DeleteObject(rgn);
                break;
            }
            default:
                using (var brush = FillBrush.Create(s.FillPattern, s.CurrentColor, c.Image))
                {
                    if (brush.Transparent) Gdi.SetBkMode(hdc, Gdi.TRANSPARENT);
                    using (Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, s.Thickness, Gdi.Rgb(s.CurrentColor))))
                    using (Gdi.Use(hdc, brush.Handle, delete: false))
                        CanvasPainter.Primitive(hdc, g);
                    Gdi.SetBkMode(hdc, Gdi.OPAQUE);
                }
                break;
        }
    }

    /// <summary>Base for drag shapes: XOR preview from the anchor, final render on release.</summary>
    public abstract class DragShape : ITool
    {
        protected abstract ShapeGeom Preview(StudioState s, int x, int y);
        protected virtual bool Commits(StudioState s, int x, int y) => x != s.AnchorX && y != s.AnchorY;
        protected abstract void Final(ToolContext c, int x, int y);

        public void Down(ToolContext c, int x, int y) => Band(c, x, y);
        public void Move(ToolContext c, int x, int y) => Band(c, x, y);

        void Band(ToolContext c, int x, int y)
        {
            c.Canvas.XorOutline(Preview(c.State, c.State.PrevX, c.State.PrevY));
            c.Canvas.XorOutline(Preview(c.State, x, y));
        }

        public void Up(ToolContext c, int x, int y)
        {
            var s = c.State;
            if (!Commits(s, x, y)) return;
            c.Canvas.XorOutline(Preview(s, s.PrevX, s.PrevY));
            Final(c, x, y);
        }
    }

    /// <summary>The nine Shapes-tab shapes (ellipse, rectangle, triangle, rounded square, trapezoid, diamond, hexagon, octagon, star).</summary>
    public sealed class Shape : DragShape
    {
        static int Variant(StudioState s) => s.Mode switch { 0x22 => s.TO, 0x23 => s.SO, 0x24 => s.O8, _ => 1 };

        public static ShapeGeom Geometry(StudioState s, int x, int y) =>
            ShapesMath.ForMode(s.Mode, Variant(s), s.AnchorX, s.AnchorY, x, y);

        protected override ShapeGeom Preview(StudioState s, int x, int y) => Geometry(s, x, y);
        protected override void Final(ToolContext c, int x, int y) => Render(c, Geometry(c.State, x, y));
    }

    /// <summary>
    /// Modes 0x36 / 0x37, the two-step parallelogram: drag a rectangle (0x36); on release the mode
    /// becomes 0x37 and the outline follows the mouse with no button held; the next release commits.
    /// Step 1's band is the two anchor polylines of <see cref="ShapesMath.ParallelogramStep1Band"/>.
    /// </summary>
    public sealed class Parallelogram : ITool
    {
        int _dx, _dy;

        ShapeGeom Geometry(StudioState s, int x, int y) => s.Mode == 0x37
            ? ShapesMath.Parallelogram(s.O9, s.AnchorX, s.AnchorY, _dx, _dy, x, y)
            : ShapesMath.ParallelogramStep1(s.AnchorX, s.AnchorY, x, y);

        void Band(ToolContext c, int x, int y)
        {
            var s = c.State;
            Outline(c, s.PrevX, s.PrevY);
            Outline(c, x, y);
        }

        /// <summary>One XOR band: step 1's two anchor polylines (R11), step 2's closed polygon.</summary>
        void Outline(ToolContext c, int x, int y)
        {
            var s = c.State;
            if (s.Mode == 0x37) { c.Canvas.XorOutline(Geometry(s, x, y)); return; }
            foreach (var (x0, y0, x1, y1) in ShapesMath.ParallelogramStep1Band(s.AnchorX, s.AnchorY, x, y))
                c.Canvas.XorSegment(x0, y0, x1, y1, 0xFFFFFF);
        }

        public void Down(ToolContext c, int x, int y)
        {
            Band(c, x, y);
            c.State.PrevX = x;
            c.State.PrevY = y;
        }

        public void Move(ToolContext c, int x, int y) => Band(c, x, y);

        public void Up(ToolContext c, int x, int y)
        {
            var s = c.State;
            bool both = x != s.AnchorX && y != s.AnchorY;
            if (s.PrevX != x || s.PrevY != y) Band(c, x, y);
            s.PrevX = x;
            s.PrevY = y;
            if (!both) return;
            if (s.Mode == 0x36)
            {
                _dx = x - s.AnchorX;
                _dy = y - s.AnchorY;
                s.Mode = 0x37;
                return;
            }
            c.Canvas.XorOutline(Geometry(s, x, y));
            // No clean-up pixel (R11, B33): what the bands left at (X0+dx', Y0+dy') stays and the fill covers it.
            Render(c, Geometry(s, x, y));
            s.Mode = 0x36;
        }
    }

    // ---- 3D tab ---------------------------------------------------------------------------

    /// <summary>Modes 0x2C sphere, 0x2D shaded box, 0x2E emerald: the shared shaded renderer with the light source.</summary>
    public sealed class Shaded3D : DragShape
    {
        static int Kind(StudioState s) => s.Mode switch { 0x2D => 2, 0x2E => 3, _ => 1 };

        protected override ShapeGeom Preview(StudioState s, int x, int y)
        {
            var (rx, ry, cx, cy) = ShapesMath.ShadedFromDrag(s.AnchorX, s.AnchorY, x, y);
            return Kind(s) switch
            {
                2 => new ShapeGeom(ShapeKind.Rectangle, cx - rx, cy - ry, cx + rx, cy + ry),
                3 => new ShapeGeom(ShapeKind.RoundRect, cx - rx, cy - ry, cx + rx, cy + ry, rx, ry),
                _ => new ShapeGeom(ShapeKind.Ellipse, cx - rx, cy - ry, cx + rx, cy + ry),
            };
        }

        protected override void Final(ToolContext c, int x, int y)
        {
            var s = c.State;
            var (rx, ry, cx, cy) = ShapesMath.ShadedFromDrag(s.AnchorX, s.AnchorY, x, y);
            FillRenderer.Shaded(c.Canvas.Hdc, s.ColorIndex, s.LightSource, Kind(s), rx, ry, cx, cy);
        }
    }

    /// <summary>Mode 0x2B, torus: a 20-px tube of 2-px shaded ellipse outlines outside the dragged ellipse.</summary>
    public sealed class Torus : DragShape
    {
        protected override ShapeGeom Preview(StudioState s, int x, int y) => ShapesMath.Ellipse(s.AnchorX, s.AnchorY, x, y);

        protected override void Final(ToolContext c, int x, int y)
        {
            var s = c.State;
            var (minX, minY, maxX, maxY) = ShapesMath.Ellipse(s.AnchorX, s.AnchorY, x, y).Box;
            IntPtr hdc = c.Canvas.Hdc;
            using var hollow = Gdi.Use(hdc, Gdi.GetStockObject(Gdi.NULL_BRUSH), delete: false);
            foreach (var (d, shade) in ShapesMath.TorusPasses())
                using (Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, 2, ColorTable.ColorRef(s.ColorIndex * 10 + shade))))
                    Gdi.Ellipse(hdc, minX - d, minY - d, maxX + d, maxY + d);
        }
    }

    /// <summary>Mode 0x3B, 3D line: t+2 passes of offset 2-px shade lines, darkest outside.</summary>
    public sealed class Line3D : ITool
    {
        static readonly int Band = ColorTable.Get(ColorTable.White);

        public void Down(ToolContext c, int x, int y) => Rubber(c, x, y);
        public void Move(ToolContext c, int x, int y) => Rubber(c, x, y);

        static void Rubber(ToolContext c, int x, int y)
        {
            var s = c.State;
            c.Canvas.XorSegment(s.AnchorX, s.AnchorY, s.PrevX, s.PrevY, Band);
            c.Canvas.XorSegment(s.AnchorX, s.AnchorY, x, y, Band);
        }

        public void Up(ToolContext c, int x, int y)
        {
            var s = c.State;
            if (x == s.AnchorX && y == s.AnchorY) return;
            c.Canvas.XorSegment(s.AnchorX, s.AnchorY, s.PrevX, s.PrevY, Band);
            int x0 = s.AnchorX, y0 = s.AnchorY;
            foreach (var p in ShapesMath.Line3DPasses(s.Thickness, x - x0, y - y0))
            {
                int rgb = ColorTable.Get(s.ColorIndex * 10 + p.Shade);
                c.Canvas.Segment(x0 + p.Dx1, y0 + p.Dy1, x + p.Dx1, y + p.Dy1, 2, rgb);
                c.Canvas.Segment(x0 + p.Dx2, y0 + p.Dy2, x + p.Dx2, y + p.Dy2, 2, rgb);
            }
        }
    }

    /// <summary>Mode 0x3C, cube: front face plus top/bottom and side faces toward the Box Direction corner, all gradients.</summary>
    public sealed class Cube : DragShape
    {
        protected override ShapeGeom Preview(StudioState s, int x, int y) => ShapesMath.Rectangle(s.AnchorX, s.AnchorY, x, y);

        protected override void Final(ToolContext c, int x, int y)
        {
            var s = c.State;
            IntPtr hdc = c.Canvas.Hdc;
            var faces = ShapesMath.CubeFaces(s.BoxDir, s.BoxDepth, s.AnchorX, s.AnchorY, x, y);
            for (int i = 0; i < faces.Length; i++)
            {
                var g = i == 0
                    ? new ShapeGeom(ShapeKind.Rectangle, faces[0].Points[0].X, faces[0].Points[0].Y, faces[0].Points[2].X, faces[0].Points[2].Y)
                    : ShapeGeom.Poly(faces[i].Points);
                IntPtr rgn = FillRenderer.CreateRegion(g);
                int style = faces[i].Style;
                FillRenderer.Clipped(hdc, rgn, () => FillRenderer.Gradient(hdc, style, g.Box, s.ColorIndex));
                Gdi.DeleteObject(rgn);
            }
        }
    }

    /// <summary>Mode 0x3D, pixel jumble: 10t+1 swaps of two random pixels within ±r.</summary>
    public sealed class Jumble : ITool
    {
        public void Down(ToolContext c, int x, int y) => Swap(c, x, y);
        public void Move(ToolContext c, int x, int y) => Swap(c, x, y);

        static void Swap(ToolContext c, int x, int y)
        {
            var surf = c.Canvas.Surface;
            int r = ShapesMath.BrushRadius(c.State.Thickness);
            for (int i = 0; i <= 10 * c.State.Thickness; i++)
            {
                int x1 = x + c.Random.Next(2 * r) - r;
                int y1 = y + c.Random.Next(2 * r) - r;
                int x2 = x + c.Random.Next(2 * r) - r;
                int y2 = y + c.Random.Next(2 * r) - r;
                int a = surf.GetRgb(x1, y1), b = surf.GetRgb(x2, y2);
                if (a < 0 || b < 0) continue; // GetPixel outside the canvas fails; SetPixel there is clipped
                surf.SetRgb(x1, y1, b);
                surf.SetRgb(x2, y2, a);
            }
        }
    }

    /// <summary>Mode 0x3E, smear: each row's 2r-wide strip left of the cursor is copied to a random nearby x.</summary>
    public sealed class Smear : ITool
    {
        public void Down(ToolContext c, int x, int y) => Rows(c, x, y);
        public void Move(ToolContext c, int x, int y) => Rows(c, x, y);

        static void Rows(ToolContext c, int x, int y)
        {
            int r = ShapesMath.BrushRadius(c.State.Thickness);
            if (x - 2 * r <= 0) return;
            IntPtr hdc = c.Canvas.Hdc;
            for (int row = y - r; row <= y + r; row++)
            {
                int dst = x - r + c.Random.Next(4 * r) - 2 * r;
                Gdi.BitBlt(hdc, dst, row, 2 * r, 1, hdc, x - 2 * r, row, Gdi.SRCCOPY);
            }
        }
    }
}

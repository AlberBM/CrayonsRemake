using Crayons.App.Rendering;
using Crayons.Core;
using Crayons.Core.Tools;

namespace Crayons.App.Tools;

/// <summary>
/// The Change It (Magic Hat) tab (behavior/tools-text-edit.md §5): Face mosaics, zoom, reverse, invert,
/// the eye dropper, and cut / copy / copy-flipper with the floating selection. The tunnel and squiggle
/// slots reuse <see cref="WackyTools.BarnZoomSteps"/> and <see cref="WackyTools.ShearSteps"/>.
/// </summary>
public static class ChangeItTools
{
    const int W = ChangeItMath.W, H = ChangeItMath.H;

    /// <summary>
    /// A mirrored copy of (sx, sy, w, h) to (x, y, w, h): left-right (negative source width) or upside down
    /// (negative source height). The source must not overlap the destination surface area.
    /// </summary>
    public static void Mirror(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, bool horizontal)
    {
        if (horizontal) Gdi.StretchBlt(dst, x, y, w, h, src, sx + w - 1, sy, -w, h, Gdi.SRCCOPY);
        else Gdi.StretchBlt(dst, x, y, w, h, src, sx, sy + h - 1, w, -h, Gdi.SRCCOPY);
    }

    /// <summary>Modes 0x3F / 0x40, Face: squares or circles mosaic over the box (sounds 48 / 49 from the mode table).</summary>
    public sealed class Face : BoxTool
    {
        protected override void Action(ToolContext c, Box box)
        {
            var surface = c.Canvas.Surface;
            surface.Flush();
            if (c.State.Mode == 0x40) ChangeItMath.MosaicCircles(surface.Pixels, surface.Width, surface.Height, box, c.Random);
            else ChangeItMath.MosaicSquares(surface.Pixels, surface.Width, surface.Height, box);
        }
    }

    /// <summary>
    /// Mode 0x41, zoom: Size 2 stretches the box to the whole canvas (0,0,W,H); Size 1 whitens the canvas
    /// and stretches the whole picture (0,0,W-2,H-2) into the box with BLACKONWHITE.
    /// </summary>
    public sealed class Zoom : BoxTool
    {
        protected override void Action(ToolContext c, Box box)
        {
            var canvas = c.Canvas.Surface;
            using var copy = new DibSurface(canvas.Width, canvas.Height);
            copy.CopyFrom(canvas);
            if (c.State.Size == 2)
                Gdi.StretchBlt(canvas.Hdc, 0, 0, W, H, copy.Hdc, box.L, box.T, box.Width, box.Height, Gdi.SRCCOPY);
            else
            {
                Gdi.PatBlt(canvas.Hdc, 0, 0, W, H, Gdi.WHITENESS);
                // Shrinking ANDs the dropped pixels together (BLACKONWHITE), so 1-px dark lines stay solid (R5).
                int oldMode = Gdi.SetStretchBltMode(canvas.Hdc, Gdi.BLACKONWHITE);
                Gdi.StretchBlt(canvas.Hdc, box.L, box.T, box.Width, box.Height, copy.Hdc, 0, 0, W - 2, H - 2, Gdi.SRCCOPY);
                Gdi.SetStretchBltMode(canvas.Hdc, oldMode);
            }
        }
    }

    /// <summary>Mode 0x42, reverse: the clamped box mirrored in place (Flip 4 left-right, 3 upside down), column x from l + r - x (R4); SND50.</summary>
    public sealed class Reverse : BoxTool
    {
        protected override void Action(ToolContext c, Box box)
        {
            var b = ChangeItMath.ReverseClamp(box);
            if (b.Width <= 0 || b.Height <= 0) return;
            var canvas = c.Canvas.Surface;
            bool horizontal = c.State.Flip != 3;
            var (sx, sy) = ChangeItMath.MirrorSourceOrigin(b, horizontal);
            using var copy = new DibSurface(b.Width, b.Height);
            copy.DrawPart(canvas, 0, 0, sx, sy, b.Width, b.Height);
            Mirror(canvas.Hdc, b.L, b.T, b.Width, b.Height, copy.Hdc, 0, 0, horizontal);
        }
    }

    /// <summary>Mode 0x43, Plus: DSTINVERT over the box (RGB inversion, spec §3.2); SND51.</summary>
    public sealed class Invert : BoxTool
    {
        protected override void Action(ToolContext c, Box box) =>
            Gdi.PatBlt(c.Canvas.Hdc, box.L, box.T, box.Width, box.Height, Gdi.DSTINVERT);
    }

    /// <summary>Mode 0x44, eye dropper: a click replaces every pixel of the clicked colour with the current colour.</summary>
    public sealed class EyeDropper : ITool
    {
        public void Down(ToolContext c, int x, int y)
        {
            var surface = c.Canvas.Surface;
            surface.Flush();
            ChangeItMath.ReplaceColor(surface.Pixels, surface.Width, surface.Height, x, y, c.State.CurrentColor);
        }

        public void Move(ToolContext c, int x, int y) { }
    }

    /// <summary>
    /// Cut (0x45), copy (0x46) and copy flipper (0x47). Select: drag a box; on release snapshot and draw the
    /// blue frame; the mode becomes 0x48/0x49/0x4A with a pending selection. The next press outside the box
    /// restores and goes idle (mode 0); inside it grabs the piece (cut whitens the box), snapshots, and the
    /// drag moves the piece over the snapshot. The release (sound 58/59/60) returns to 0x45/0x46/0x47.
    /// </summary>
    public sealed class Selection : BoxTool
    {
        Box _box;
        DibSurface? _piece;
        int _offX, _offY, _lastX, _lastY;
        bool _dragging;

        public override void Down(ToolContext c, int x, int y)
        {
            var s = c.State;
            if (s.Mode is >= 0x45 and <= 0x47) { base.Down(c, x, y); return; }
            // 0x48-0x4A: the selection is pending.
            c.RestoreCanvas();
            s.FloatingSelection = false;
            if (!_box.Contains(x, y))
            {
                s.Mode = 0;
                return;
            }
            var canvas = c.Canvas.Surface;
            _piece?.Dispose();
            _piece = new DibSurface(Math.Max(1, _box.Width), Math.Max(1, _box.Height));
            // The flipper mirrors column x to l + r - x (R4), so its piece starts one column (row) further in.
            var (sx, sy) = s.Mode == 0x4A ? ChangeItMath.MirrorSourceOrigin(_box, s.Flip != 3) : (_box.L, _box.T);
            _piece.DrawPart(canvas, 0, 0, sx, sy, _box.Width, _box.Height);
            if (s.Mode == 0x48) Gdi.FillRect(canvas.Hdc, _box.L, _box.T, _box.R, _box.B, 0xFFFFFF);
            _offX = x - _box.L;
            _offY = y - _box.T;
            _lastX = _box.L;
            _lastY = _box.T;
            c.SnapshotCanvas();
            _dragging = true;
        }

        public override void Move(ToolContext c, int x, int y)
        {
            if (c.State.Mode is >= 0x45 and <= 0x47) { base.Move(c, x, y); return; }
            if (_dragging) Place(c, x, y);
        }

        public override void Up(ToolContext c, int x, int y)
        {
            var s = c.State;
            if (s.Mode is >= 0x45 and <= 0x47) { base.Up(c, x, y); return; }
            if (!_dragging) return;
            Place(c, x, y);
            _dragging = false;
            s.Mode -= 3; // the release sound (58/59/60) belongs to 0x48/0x49/0x4A
        }

        void Place(ToolContext c, int x, int y)
        {
            if (_piece == null) return;
            var canvas = c.Canvas.Surface;
            if (c.Backing is { } a)
                Gdi.BitBlt(canvas.Hdc, _lastX, _lastY, _piece.Width, _piece.Height, a.Hdc, _lastX, _lastY, Gdi.SRCCOPY);
            int px = x - _offX, py = y - _offY;
            if (c.State.Mode == 0x4A) Mirror(canvas.Hdc, px, py, _piece.Width, _piece.Height, _piece.Hdc, 0, 0, c.State.Flip != 3);
            else Gdi.BitBlt(canvas.Hdc, px, py, _piece.Width, _piece.Height, _piece.Hdc, 0, 0, Gdi.SRCCOPY);
            _lastX = px;
            _lastY = py;
        }

        protected override void Action(ToolContext c, Box box)
        {
            var s = c.State;
            c.SnapshotCanvas();
            _box = ChangeItMath.SelectionClamp(box);
            DrawSelectionFrame(c.Canvas.Surface, _box);
            s.Mode += 3;
            s.FloatingSelection = true;
        }
    }

    /// <summary>The pending-selection marquee (R6): blue/white diagonal stripes, see <see cref="ChangeItMath.SelectionMarquee"/>.</summary>
    public static void DrawSelectionFrame(DibSurface canvas, Box box)
    {
        canvas.Flush();
        foreach (var (x, y, blue) in ChangeItMath.SelectionMarquee(box))
            canvas.SetRgb(x, y, blue ? ChangeItMath.SelectionRgb : 0xFFFFFF);
    }
    /// <summary>Slot 3, tunnel ("far horizon"): 12 nested copies of the snapshot, no white at the end.</summary>
    public static IAnimation Tunnel(ToolContext c) =>
        new StepAnimation(WackyTools.BarnZoomSteps(c.Canvas.Surface, c.Backing ?? c.Canvas.Surface));

    /// <summary>Slot 4, squiggle: 10 passes of shifted rows of the snapshot.</summary>
    public static IAnimation Squiggle(ToolContext c) =>
        new StepAnimation(WackyTools.ShearSteps(c.Canvas.Surface, c.Backing ?? c.Canvas.Surface, c.Random));
}

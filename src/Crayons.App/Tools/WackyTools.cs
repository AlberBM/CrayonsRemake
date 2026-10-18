using Crayons.App.Rendering;
using Crayons.Core;
using Crayons.Core.Tools;

namespace Crayons.App.Tools;

/// <summary>
/// The Clown ("wacky") family (behavior/tools-wacky.md), including the Mr. Brush and Train
/// animations and the Younger Barn Zoom / Shear effects. Every random call is made in the
/// documented order; the dispatcher's per-move Random(159) comes first when clown mode is on.
/// </summary>
public static class WackyTools
{
    const int W = LinesMath.W, H = LinesMath.H;

    static int[] TrioColors(ToolContext c)
    {
        var (t0, t1, t2) = c.State.Trio;
        return new[] { t0, t1, t2 };
    }

    /// <summary>Mode 7, Mini Shapes: one random-size triangle, square or circle per event.</summary>
    public sealed class MiniShapes : ITool
    {
        public void Down(ToolContext c, int x, int y) => Draw(c, x, y, c.State.CurrentColor);
        public void Move(ToolContext c, int x, int y) => Draw(c, x, y, c.SegmentColor);

        static void Draw(ToolContext c, int x, int y, int colour)
        {
            int r = c.Random.Next(20) + 5;
            int v = c.Random.Next(3) + 1; // drawn for every shape
            IntPtr hdc = c.Canvas.Hdc;
            using var pen = Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, 1, Gdi.Rgb(colour)));
            using var br = Gdi.Use(hdc, Gdi.CreateSolidBrush(Gdi.Rgb(colour)));
            switch (c.State.MiniShape)
            {
                case 1: Gdi.Rectangle(hdc, x - r, y - r, x + r, y + r); break;
                case 2: Gdi.Ellipse(hdc, x - r, y - r, x + r, y + r); break;
                default:
                    var pts = WackyMath.MiniTriangle(v, x, y, r).Select(p => new Gdi.POINT(p.X, p.Y)).ToArray();
                    Gdi.Polygon(hdc, pts, pts.Length);
                    break;
            }
        }
    }

    /// <summary>Mode 6, Spider Splatter: 8 random lines of width t within ±10t.</summary>
    public sealed class Spider : ITool
    {
        public void Down(ToolContext c, int x, int y) => Draw(c, x, y, c.State.CurrentColor); // SND7 comes from the mode table
        public void Move(ToolContext c, int x, int y) => Draw(c, x, y, c.SegmentColor);

        static void Draw(ToolContext c, int x, int y, int colour)
        {
            int k = c.State.Thickness * 10;
            for (int i = 0; i < 8; i++)
            {
                int ex = x + c.Random.Next(2 * k) - k;
                int ey = y + c.Random.Next(2 * k) - k;
                c.Canvas.Segment(x, y, ex, ey, c.State.Thickness, colour);
            }
        }
    }

    /// <summary>Mode 3, Crosshair: four lines to the canvas edges; moves use three shades of the current hue.</summary>
    public sealed class Crosshair : ITool
    {
        static (int X, int Y)[] Targets(int x, int y) => new[] { (x, H), (x, 0), (0, y), (W, y) };

        public void Down(ToolContext c, int x, int y)
        {
            foreach (var (ex, ey) in Targets(x, y)) c.Canvas.Segment(x, y, ex, ey, c.State.Thickness, c.State.CurrentColor);
        }

        public void Move(ToolContext c, int x, int y)
        {
            c.Random.Next(3); // drawn and discarded
            var trio = TrioColors(c);
            foreach (var (ex, ey) in Targets(x, y)) c.Canvas.Segment(x, y, ex, ey, c.State.Thickness, trio[c.Random.Next(3)]);
        }
    }

    /// <summary>Mode 0x1B, shaded Fish Bubbles: shaded-renderer circles, light 5.</summary>
    public sealed class ShadedBubbles : ITool
    {
        public void Down(ToolContext c, int x, int y) => Draw(c, x, y);
        public void Move(ToolContext c, int x, int y) => Draw(c, x, y);

        static void Draw(ToolContext c, int x, int y)
        {
            int ox = c.Random.Next(30) - 15;
            int oy = c.Random.Next(30) - 15;
            int s = c.Random.Next(15) + 3;
            int hue = c.SegmentColor == c.State.CurrentColor ? c.State.ColorIndex : c.Random.Next(15);
            FillRenderer.Shaded(c.Canvas.Hdc, hue, 5, 1, s, s, x + ox, y + oy);
        }
    }

    /// <summary>Mode 0x1A, outline Fish Bubbles: hollow 1-px circles in the stroke colour.</summary>
    public sealed class OutlineBubbles : ITool
    {
        public void Down(ToolContext c, int x, int y) => Draw(c, x, y);
        public void Move(ToolContext c, int x, int y) => Draw(c, x, y);

        static void Draw(ToolContext c, int x, int y)
        {
            int r = c.Random.Next(10) + 5;
            int dx = c.Random.Next(30) - 15;
            int dy = c.Random.Next(30) - 15;
            c.Canvas.Ellipse(x + dx - r, y + dy - r, x + dx + r, y + dy + r, 1, c.SegmentColor, null);
        }
    }

    /// <summary>Mode 0x11, Christmas Tree Ornaments: a garland line with a shaded ball every 40 px.</summary>
    public sealed class Ornaments : ITool
    {
        public void Down(ToolContext c, int x, int y) =>
            c.Canvas.Segment(x, y, x + 1, y, c.State.Thickness, c.State.CurrentColor);

        public void Move(ToolContext c, int x, int y)
        {
            var s = c.State;
            int t = s.Thickness;
            c.Canvas.Segment(s.PrevX, s.PrevY, x, y, t, s.CurrentColor);
            if (DMath.Dist(x - s.AnchorX, y - s.AnchorY) <= 40) return;
            c.Canvas.Segment(x, y, x, y + 5 * t, t, s.CurrentColor);
            int hue = s.MultiColor ? c.Random.Next(16) : s.ColorIndex;
            c.PlaySound(17);
            FillRenderer.Shaded(c.Canvas.Hdc, hue, 1, 1, 3 * t, 3 * t, x, y + 5 * t);
            s.AnchorX = x;
            s.AnchorY = y;
        }
    }

    /// <summary>Mode 0x38, Hair: one random arc per event.</summary>
    public sealed class Hair : ITool
    {
        public void Down(ToolContext c, int x, int y) => Draw(c, x, y, c.State.CurrentColor);

        public void Move(ToolContext c, int x, int y)
        {
            var s = c.State;
            int colour = s.MultiColor ? ColorTable.Get(s.ColorIndex * 10 + c.Random.Next(10)) : s.CurrentColor;
            Draw(c, x, y, colour);
        }

        static void Draw(ToolContext c, int x, int y, int colour)
        {
            int t = c.State.Thickness;
            int r = c.Random.Next(5 * t) + 2;
            int o1 = WackyMath.HairSigned(c.Random.Next(r));
            int o2 = WackyMath.HairSigned(c.Random.Next(r));
            int o3 = WackyMath.HairSigned(c.Random.Next(r));
            int o4 = WackyMath.HairSigned(c.Random.Next(r));
            IntPtr hdc = c.Canvas.Hdc;
            using var pen = Gdi.Use(hdc, Gdi.CreatePen(Gdi.PS_SOLID, t, Gdi.Rgb(colour)));
            Gdi.Arc(hdc, x - r, y - r, x + r, y + r, x + o1, y + o2, x + o3, y + o4);
        }
    }

    /// <summary>Mode 4, Grass (Older): a tuft of 4..9 1-px blades in the tri-colour.</summary>
    public sealed class Grass : ITool
    {
        public void Down(ToolContext c, int x, int y) => Tuft(c, x, y);
        public void Move(ToolContext c, int x, int y) => Tuft(c, x, y);

        static void Tuft(ToolContext c, int x, int y)
        {
            var (a, sp) = WackyMath.GrassSize(c.State.Grass);
            var trio = TrioColors(c);
            int count = c.Random.Next(6) + 4;
            for (int i = 0; i < count; i++)
            {
                int dx = c.Random.Next(sp) - sp / 2;
                int dy = c.Random.Next(a);
                int s = c.Random.Next(3);
                c.Canvas.Segment(x, y, x + dx, y - dy, 1, trio[s]);
            }
        }
    }

    // ---- Animations ----------------------------------------------------------------------

    /// <summary>A canvas-sized background save buffer for a sprite (clipped like BitBlt).</summary>
    sealed class SaveBuffer : IDisposable
    {
        readonly DibSurface _buf;
        public SaveBuffer(int w, int h) => _buf = new DibSurface(w, h, 0xFFFFFF);
        public void Save(DibSurface canvas, int x, int y) => _buf.DrawPart(canvas, 0, 0, x, y, _buf.Width, _buf.Height);
        public void Restore(DibSurface canvas, int x, int y) => Gdi.BitBlt(canvas.Hdc, x, y, _buf.Width, _buf.Height, _buf.Hdc, 0, 0, Gdi.SRCCOPY);
        public void Dispose() => _buf.Dispose();
    }

    /// <summary>Sprite blit: mask (SRCAND) then image (SRCPAINT).</summary>
    static void Sprite(DibSurface canvas, DibSurface? sheet, int x, int y, int w, int h, int sx, int maskY, int imageY)
    {
        if (sheet == null) return;
        Gdi.BitBlt(canvas.Hdc, x, y, w, h, sheet.Hdc, sx, maskY, Gdi.SRCAND);
        Gdi.BitBlt(canvas.Hdc, x, y, w, h, sheet.Hdc, sx, imageY, Gdi.SRCPAINT);
    }

    /// <summary>Mode 0x32, Mr. Brush: a walking brush that paints a wavy line down the canvas.</summary>
    public sealed class MrBrush : ITool
    {
        public void Move(ToolContext c, int x, int y) { }

        public void Down(ToolContext c, int x, int y) => c.StartAnimation(new Animation(c, x, y));

        public sealed class Animation : IAnimation
        {
            readonly ToolContext _c;
            readonly DibSurface _canvas;
            readonly DibSurface? _sheet;
            readonly SaveBuffer _save = new(WackyMath.MrBrushSize, WackyMath.MrBrushSize);
            readonly int _width, _rgb;
            int _bx, _by, _tipX, _tipY, _runLen, _drift, _n, _phase = 1;
            bool _first = true;

            public Animation(ToolContext c, int x, int y)
            {
                _c = c;
                _canvas = c.Canvas.Surface;
                _sheet = c.Image("A932.ASL");
                _width = c.State.Thickness;
                _rgb = c.State.CurrentColor;
                _bx = x - 25;
                _by = y - 25;
                _tipX = _bx + 8;
                _tipY = _by + 43;
                _save.Save(_canvas, _bx, _by);
                _runLen = c.Random.Next(15) + 2;
                _drift = -(c.Random.Next(8) + 1);
            }

            /// <summary>About 1.4 s before the first frame, then more than 100 ms per frame (~110 ms with the host tick), R16.</summary>
            public int IntervalMs => _first ? WackyMath.MrBrushStartDelayMs : WackyMath.MrBrushIntervalMs;

            public bool Step()
            {
                _save.Restore(_canvas, _bx, _by);
                _by += 4;
                _n++;
                int prevX = _tipX, prevY = _tipY;
                _tipY += 4;
                _bx += _drift;
                _tipX = _bx + WackyMath.MrBrushTipOffsets[_phase - 1];
                _c.Canvas.Segment(_tipX, _tipY, prevX, prevY, _width, _rgb);
                if (_n >= _runLen)
                {
                    _drift = WackyMath.FlipDrift(_drift, _c.Random.Next(8) + 1);
                    _n = 0;
                    _runLen = _c.Random.Next(15) + 2;
                }
                _save.Save(_canvas, _bx, _by);
                Sprite(_canvas, _sheet, _bx, _by, WackyMath.MrBrushSize, WackyMath.MrBrushSize,
                    WackyMath.MrBrushFrameX[_phase - 1], WackyMath.MrBrushMaskY, WackyMath.MrBrushImageY);
                // First tick: the original asked for "<appdir>snd44.asl", which does not exist: play nothing (spec §3.5).
                _first = false;
                if (_phase == 5) _c.PlaySound(74);
                else if (_phase == 1) _c.PlaySound(75);
                _phase = _phase % 8 + 1;
                if (_by < H) return true;
                _c.PlaySound(76);
                _save.Restore(_canvas, _bx, _by); // off-screen restore
                _save.Dispose();
                return false;
            }

            /// <summary>For tests: the first tick has run.</summary>
            public bool Started => !_first;
        }
    }

    /// <summary>Mode 0x30, Train: a train that lays rails and ties as it drives left.</summary>
    public sealed class Train : ITool
    {
        public void Move(ToolContext c, int x, int y) { }

        public void Down(ToolContext c, int x, int y) => c.StartAnimation(new Animation(c, x, y));

        public sealed class Animation : IAnimation
        {
            readonly ToolContext _c;
            readonly DibSurface _canvas;
            readonly DibSurface? _sheet;
            readonly SaveBuffer _save = new(WackyMath.TrainW, WackyMath.TrainH);
            readonly int _rgb, _d;
            int _tx, _ty, _p1x, _p1y, _p2x, _p2y, _y1, _y2, _lastTieX, _frame = 1;

            public Animation(ToolContext c, int x, int y)
            {
                _c = c;
                _canvas = c.Canvas.Surface;
                c.PlaySound(73);
                _rgb = c.State.CurrentColor;
                _sheet = c.Image("A931.ASL");
                _tx = x - 38;
                _ty = y - 25;
                _p1x = x + 25; _p1y = _y1 = y + 10;
                _p2x = x + 20; _p2y = _y2 = y + 16;
                _lastTieX = x + 25;
                _d = WackyMath.TrainDrift(c.Random.Next(3) + 1);
                _save.Save(_canvas, _tx, _ty);
            }

            public int IntervalMs => WackyMath.TrainIntervalMs;

            public int TrainX => _tx;
            public int TrainY => _ty;

            public bool Step()
            {
                _save.Restore(_canvas, _tx, _ty);
                int a = _tx + 58, b = _tx + 68;
                _c.Canvas.Segment(_p1x, _p1y, a, _y1, 2, _rgb);
                _c.Canvas.Segment(_p2x, _p2y, b, _y2, 2, _rgb);
                if (Math.Abs(_lastTieX - a) >= 10)
                {
                    _c.Canvas.Segment(a, _y1 - 3, b, _y2 + 3, 2, _rgb);
                    _lastTieX = a;
                }
                _tx -= 5;
                (_p1x, _p1y, _p2x, _p2y) = (a, _y1, b, _y2);
                _ty += _d; _y1 += _d; _y2 += _d;
                _save.Save(_canvas, _tx, _ty);
                Sprite(_canvas, _sheet, _tx, _ty, WackyMath.TrainW, WackyMath.TrainH,
                    WackyMath.TrainFrameX[_frame - 1], WackyMath.TrainMaskY, WackyMath.TrainImageY);
                _frame = _frame % 4 + 1;
                if (_tx <= -70)
                {
                    _save.Restore(_canvas, _tx, _ty);
                    _save.Dispose();
                    return false;
                }
                if (_ty >= H || _ty <= -49)
                {
                    _save.Dispose(); // the original returned without its final restore (and leaked GDI objects)
                    return false;
                }
                return true;
            }
        }
    }

    // ---- Younger Barn Zoom and Shear (also Older Change It slots 3 and 4) -----------------

    /// <summary>Animation pacing of Barn Zoom / tunnel copies and Shear passes (decisions B52).</summary>
    public const int BarnZoomStepMs = 40, ShearPassMs = 40;

    /// <summary>Barn Zoom / tunnel: 12 nested shrinking StretchBlts of the backing bitmap (0, 0, W-2, H-2).</summary>
    public static void BarnZoom(DibSurface canvas, DibSurface backing)
    {
        foreach (var _ in BarnZoomSteps(canvas, backing)) { }
    }

    /// <summary>Barn Zoom one copy at a time: yields after each of the 12 copies.</summary>
    public static IEnumerable<int> BarnZoomSteps(DibSurface canvas, DibSurface backing)
    {
        for (int k = 1; k <= 12; k++)
        {
            var (x, y, w, h) = WackyMath.BarnZoomRect(k, W, H);
            Gdi.StretchBlt(canvas.Hdc, x, y, w, h, backing.Hdc, 0, 0, W - 2, H - 2, Gdi.SRCCOPY);
            yield return BarnZoomStepMs;
        }
    }

    /// <summary>Shear / squiggle: 10 passes of row-by-row shifted copies of the backing bitmap.</summary>
    public static void Shear(DibSurface canvas, DibSurface backing, DelphiRandom random)
    {
        foreach (var _ in ShearSteps(canvas, backing, random)) { }
    }

    /// <summary>Shear one pass at a time: yields after each of the 10 passes (random calls unchanged).</summary>
    public static IEnumerable<int> ShearSteps(DibSurface canvas, DibSurface backing, DelphiRandom random)
    {
        for (int pass = 0; pass < 10; pass++)
        {
            bool dir = random.Next(10) >= 5;
            int seg = random.Next(50) + 10;
            int off = random.Next(seg) + 7;
            for (int y = 1; y <= H; y++)
            {
                if (y < backing.Height)
                    Gdi.BitBlt(canvas.Hdc, dir ? off : -off, y, W - 2, 1, backing.Hdc, 0, y, Gdi.SRCCOPY);
                off++;
                if (off >= seg)
                {
                    dir = !dir;
                    off = 0;
                    seg = random.Next(50) + 10;
                }
            }
            yield return ShearPassMs;
        }
    }
}

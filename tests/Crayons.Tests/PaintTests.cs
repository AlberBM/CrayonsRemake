using Crayons.App.Rendering;
using Crayons.App.Tools;
using Crayons.App.Ui;
using Crayons.Core;
using Crayons.Core.Studio;
using Crayons.Core.Tools;

namespace Crayons.Tests;

/// <summary>A canvas, state, logs and a fixed-seed dispatcher for tool tests.</summary>
public abstract class ToolTestBase : IDisposable
{
    protected const uint Seed = 12345;
    protected readonly DibSurface Canvas = new(556, 384, 0xFFFFFF);
    protected readonly StudioState S = new();
    protected readonly List<Segment> Log = new();
    protected readonly List<int> Sounds = new();
    protected readonly ToolContext Ctx;
    protected readonly ToolDispatcher D;
    protected readonly Dictionary<string, DibSurface> Images = new(StringComparer.OrdinalIgnoreCase);

    protected ToolTestBase()
    {
        Ctx = new ToolContext
        {
            State = S,
            Random = new DelphiRandom(Seed),
            Canvas = new CanvasPainter(Canvas) { Log = Log },
            PlaySound = Sounds.Add,
            Image = n => Images.TryGetValue(n, out var d) ? d : null,
        };
        D = new ToolDispatcher(Ctx);
    }

    public virtual void Dispose()
    {
        Canvas.Dispose();
        foreach (var i in Images.Values) i.Dispose();
    }

    protected int CountNot(int rgb) => Count(c => c != rgb);

    /// <summary>Counts canvas pixels (0xRRGGBB) matching a predicate, reading the DIB once.</summary>
    protected int Count(Func<int, bool> match)
    {
        Canvas.Flush();
        int n = 0;
        foreach (uint p in Canvas.Pixels) if (match((int)(p & 0xffffff))) n++;
        return n;
    }

    protected static DelphiRandom Replay() => new(Seed);
}

public class PaintMathTests
{
    [Fact]
    public void NibOffsets_PerBrushType()
    {
        Assert.Equal(new[] { (1, -1), (0, 0), (-1, 1) }, PaintMath.NibOffsets(1, 1));
        Assert.Equal(new[] { (-1, -1), (0, 0), (1, 1) }, PaintMath.NibOffsets(2, 1));
        Assert.Equal(new[] { (0, -1), (0, 0), (0, 1) }, PaintMath.NibOffsets(3, 1));
        Assert.Equal(new[] { (-1, 0), (0, 0), (1, 0) }, PaintMath.NibOffsets(4, 1));
        // N = 6, h = 2: the extra stripe falls on the far side.
        Assert.Equal(new[] { (2, -2), (1, -1), (0, 0), (-1, 1), (-2, 2), (-3, 3) }, PaintMath.NibOffsets(1, 2));
        Assert.Equal(30, PaintMath.NibOffsets(3, 10).Length);
        Assert.Equal((0, -14), PaintMath.NibOffsets(3, 10)[0]);
    }

    [Theory]
    [InlineData(3, 0, 32, 31, 30)]
    [InlineData(3, 2, 32, 31, 30)]
    [InlineData(9, 5, 95, 94, 93)]
    [InlineData(15, 9, 159, 158, 157)]
    public void TriColour_UsesMaxShadeTwo(int colour, int shade, int i0, int i1, int i2)
    {
        var s = new StudioState { ColorIndex = colour, ShadeIndex = shade };
        Assert.Equal((ColorTable.Get(i0), ColorTable.Get(i1), ColorTable.Get(i2)), s.Trio);
    }

    [Fact]
    public void Roller_AndSpraySizes()
    {
        Assert.Equal((6, 12), PaintMath.RollerHalfSize(3, 2));
        Assert.Equal((12, 6), PaintMath.RollerHalfSize(4, 2));
        Assert.Equal(20, PaintMath.SprayCount(1));
        Assert.Equal(20, PaintMath.SprayCount(2));
        Assert.Equal(50, PaintMath.SprayCount(5));
        Assert.Equal(16, PaintMath.SprayRadius(2));
    }

    [Theory]
    [InlineData(16, 0, 16, true)]
    [InlineData(8, 8, 16, true)]     // 11.31 -> 11
    [InlineData(12, 12, 16, false)]  // 16.97 -> 17
    [InlineData(-11, 11, 16, true)]  // 15.56 -> 16
    [InlineData(7, 4, 8, true)]      // 8.06 -> 8
    [InlineData(6, 6, 8, true)]      // 8.49 -> 8
    [InlineData(5, 7, 8, false)]     // 8.60 -> 9
    public void SprayAcceptance(int dx, int dy, int r, bool kept) => Assert.Equal(kept, PaintMath.SprayAccepts(dx, dy, r));

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(31, 33, 0)]
    [InlineData(32, 1, 1)]
    [InlineData(254, 10, 7)]
    [InlineData(256, 10, 7)]   // beyond the picture: clamped to the last cell
    [InlineData(0, 34, 8)]
    [InlineData(100, 67, 11)]
    [InlineData(0, 68, 16)]
    [InlineData(250, 93, 23)]
    [InlineData(0, 94, 24)]
    [InlineData(64, 159, 25)]
    [InlineData(200, 160, 31)]
    [InlineData(254, 224, 31)]
    [InlineData(10, -1, -1)]
    public void FillStyleHitMap(int ix, int iy, int style) => Assert.Equal(style, PaintMath.FillStyleAt(ix, iy));

    [Fact]
    public void FillStylesPlacement_BucketVsShapes()
    {
        Assert.Equal((280, 68), PaintMath.FillStylesPlacement(bucket: true));
        Assert.Equal((94, 222), PaintMath.FillStylesPlacement(bucket: false));
    }

    [Fact]
    public void FillStylesOverlay_FrameToStyle()
    {
        var picks = new List<int>();
        var shapes = new FillStylesOverlay(null, 0, -1, bucket: false, picks.Add);
        Assert.Equal(new Rectangle(82, 94, 257, 222), shapes.Bounds);
        Assert.Equal(0, shapes.StyleAt(new Point(82, 94)));        // image (0, 1)
        Assert.Equal(9, shapes.StyleAt(new Point(82 + 40, 94 + 35)));
        Assert.Equal(26, shapes.StyleAt(new Point(82 + 130, 94 + 100)));
        Assert.Equal(-1, shapes.StyleAt(new Point(81, 100)));
        Assert.Equal(-1, shapes.StyleAt(new Point(100, 316)));
        var bucket = new FillStylesOverlay(null, 0, -1, bucket: true, picks.Add);
        Assert.Equal(new Rectangle(82, 280, 257, 68), bucket.Bounds);
        Assert.Equal(15, bucket.StyleAt(new Point(82 + 250, 280 + 66)));
        Assert.Equal(16, bucket.StyleAt(new Point(82, 347))); // the last visible row is image row 68
        bucket.MouseDown(new Point(82 + 70, 290), MouseButtons.Right);
        Assert.Empty(picks);
        bucket.MouseDown(new Point(82 + 70, 290), MouseButtons.Left);
        Assert.Equal(new[] { 2 }, picks);
    }

    [Fact]
    public void BrushKinds_AndFiles()
    {
        Assert.Equal(FillBrushKind.Null, PaintMath.BrushKind(0));
        Assert.Equal(FillBrushKind.Solid, PaintMath.BrushKind(1));
        Assert.Equal(new[] { 0, 1, 4, 3, 2, 5 }, Enumerable.Range(2, 6).Select(PaintMath.HatchStyle));
        Assert.Equal("PA00.ASL", PaintMath.PatternFile(8));
        Assert.Equal("PA07.ASL", PaintMath.PatternFile(15));
        Assert.Equal("PB1.ASL", PaintMath.PictureFile(24));
        Assert.Equal("PB8.ASL", PaintMath.PictureFile(31));
        Assert.Equal(FillBrushKind.Gradient, PaintMath.BrushKind(16));
        Assert.Equal(FillBrushKind.Picture, PaintMath.BrushKind(31));
    }

    [Fact]
    public void PaintCanPolygon_Has26ClosedPoints()
    {
        Assert.Equal(26, PaintMath.PaintCanPolygon.Length);
        Assert.Equal(PaintMath.PaintCanPolygon[0], PaintMath.PaintCanPolygon[^1]);
        Assert.All(PaintMath.PaintCanPolygon, p => Assert.InRange(p.Y, 280, 329));
    }
}

public class PaintToolTests : ToolTestBase
{
    [Fact]
    public void Dripping_RandomOrderAndDrip()
    {
        S.Mode = 0x16;
        D.Down(100, 100);
        Assert.Equal(new Segment(100, 100, 101, 100, 2, 0xFF0000), Log[0]);
        var r = Replay();
        int prevX = 100;
        // Move events until a drip happens, checking the documented draw order each time.
        for (int k = 0; k < 40; k++)
        {
            Log.Clear();
            int x = 110 + k, y = 100;
            D.Move(x, y);
            int len = r.Next(20) + 10;
            bool drip = r.Next(10) < 2;
            Assert.Equal(new Segment(prevX, 100, x, y, 2, 0xFF0000), Log[0]);
            prevX = x;
            if (!drip) { Assert.Single(Log); continue; }
            var (t0, t1, t2) = S.Trio;
            Assert.Equal(new[] { new Segment(x, y, x, y + len, 1, t0), new Segment(x + 1, y, x + 1, y + len, 1, t1), new Segment(x + 2, y, x + 2, y + len, 1, t2) },
                Log.Skip(1).Take(3));
            r.Next(2); // rx
            int ry = r.Next(2) + 5;
            Assert.Equal(t0, Canvas.GetRgb(x + 1, y + len + ry - 1)); // outer (T0) ring below the inner blobs
            Assert.Equal(t2, Canvas.GetRgb(x + 1, y + len));
            Assert.Equal(r.Seed, Ctx.Random.Seed);
            return;
        }
        Assert.Fail("no drip in 40 moves");
    }

    [Theory]
    [InlineData(0x15)]
    [InlineData(0x08)]
    [InlineData(0x0F)]
    [InlineData(0x0E)]
    public void Nib_StripeColoursPerOption(int mode)
    {
        S.Mode = mode;
        S.BrushType = 3;
        D.Down(200, 200);
        var r = Replay();
        var (t0, t1, t2) = S.Trio;
        var trio = new[] { t0, t1, t2 };
        int[] expected = Enumerable.Range(0, 6).Select(_ => mode switch
        {
            0x15 or 0x0F => trio[r.Next(3)],
            0x08 => ColorTable.Get(r.Next(160)),
            _ => S.CurrentColor,
        }).ToArray();
        Assert.Equal(expected, Log.Select(l => l.Rgb));
        Assert.All(Log, l => Assert.Equal(2, l.Width));
        Assert.Equal(new[] { 198, 199, 200, 201, 202, 203 }, Log.Select(l => l.Y0)); // vertical nib, h = 2
        Assert.All(Log, l => Assert.Equal((200, 201), (l.X0, l.X1)));
        Log.Clear();
        D.Move(210, 205);
        bool reroll = mode == 0x0F;
        if (reroll) expected = Enumerable.Range(0, 6).Select(_ => trio[r.Next(3)]).ToArray();
        Assert.Equal(expected, Log.Select(l => l.Rgb));
        Assert.Equal(r.Seed, Ctx.Random.Seed);
        Assert.Equal(ModeSounds.Down(mode), Sounds.Single());
    }

    [Fact]
    public void Nib_BrushTypeOneIsSlash()
    {
        S.Mode = 0x0E;
        S.Thickness = 1;
        D.Down(50, 50);
        Assert.Equal(new[] { (51, 49), (50, 50), (49, 51) }, Log.Select(l => (l.X0, l.Y0)));
    }

    [Fact]
    public void Fill_FloodsExactRegionWithSolid()
    {
        S.Mode = 0x3A;
        Gdi.FrameRect(Canvas.Hdc, 10, 10, 50, 40, 0x000000);
        D.Down(20, 20);
        Assert.Equal(new[] { 5 }, Sounds);
        Assert.Equal(0xFF0000, Canvas.GetRgb(11, 11));
        Assert.Equal(0xFF0000, Canvas.GetRgb(48, 38));
        Assert.Equal(0x000000, Canvas.GetRgb(10, 10));
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(5, 5));
        Assert.Equal(38 * 28, Count(c => c == 0xFF0000));
    }

    [Fact]
    public void Fill_GuardSkipsCurrentColour_AndMagicColourSkipsBlack()
    {
        S.Mode = 0x3A;
        Canvas.Fill(0xFF0000);
        S.ColorIndex = 3; S.ShadeIndex = 0; // blue
        Canvas.Fill(0x0000FF);
        D.Down(5, 5);
        Assert.Equal(0, CountNot(0x0000FF));
        Canvas.Fill(0x000000);
        S.MagicColorActive = true;
        D.Down(5, 5);
        Assert.Equal(0, CountNot(0x000000)); // black is never filled in Magic Color mode
        S.MagicColorActive = false;
        D.Down(5, 5);
        Assert.Equal(0, CountNot(0x0000FF));
    }

    [Fact]
    public void Fill_NullStyleAndTransparentHatch()
    {
        S.Mode = 0x3A;
        S.FillPattern = 0;
        D.Down(5, 5);
        Assert.Equal(0, CountNot(0xFFFFFF));
        S.FillPattern = 2; // horizontal hatch: row 3 of every 8, gaps keep the old colour
        Canvas.Fill(0x00FF00);
        D.Down(5, 5);
        Assert.Equal(0xFF0000, Canvas.GetRgb(0, 3));
        Assert.Equal(0xFF0000, Canvas.GetRgb(100, 11));
        Assert.Equal(0x00FF00, Canvas.GetRgb(0, 4));
        Assert.Equal(0x00FF00, Canvas.GetRgb(7, 0));
    }

    [Fact]
    public void Fill_PatternRecoloursBlack()
    {
        var pa = new DibSurface(8, 8, 0xFFFFFF);
        pa.SetRgb(0, 0, 0x000000);
        pa.SetRgb(3, 5, 0x000000);
        Images["PA00.ASL"] = pa;
        S.Mode = 0x3A;
        S.FillPattern = 8;
        Canvas.Fill(0x00FF00);
        D.Down(20, 20);
        Assert.Equal(0xFF0000, Canvas.GetRgb(8, 8));
        Assert.Equal(0xFF0000, Canvas.GetRgb(11, 13));
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(9, 8)); // opaque: white stays white
    }

    [Fact]
    public void Roller_StampAndSpecks()
    {
        S.Mode = 0x19;
        S.Thickness = 1;
        S.RollerDir = 3; // tall: hw 3, hh 6
        D.Down(100, 100);
        Assert.Equal(new[] { 26 }, Sounds);
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(103, 100));
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(100, 106));
        var r = Replay();
        var (_, t1, t2) = S.Trio;
        var expected = new Dictionary<(int, int), int>();
        for (int i = 0; i <= 30; i++)
        {
            int ax = 100 + r.Next(6) - 3, ay = 100 + r.Next(12) - 6;
            expected[(ax, ay)] = t1;
            int bx = 100 + r.Next(6) - 3, by = 100 + r.Next(12) - 6;
            expected[(bx, by)] = t2;
        }
        foreach (var ((x, y), c) in expected) Assert.Equal(c, Canvas.GetRgb(x, y));
        Assert.Equal(r.Seed, Ctx.Random.Seed);
    }

    [Fact]
    public void Splatter_ClickOnly_RandomConsumption()
    {
        S.Mode = 0x10;
        S.Thickness = 2;
        D.Down(200, 200);
        D.Move(220, 220); // never moves
        var r = Replay();
        int count = r.Next(30) + 5;
        for (int k = 0; k <= count; k++) { r.Next(40); r.Next(40); r.Next(2); r.Next(2); }
        r.Next(8); r.Next(8);
        Assert.Equal(r.Seed, Ctx.Random.Seed);
        Assert.Equal(new[] { 23 }, Sounds);
        Assert.Equal(0xFF0000, Canvas.GetRgb(200, 200));
    }

    [Fact]
    public void Spray_Small_SetsAcceptedSamples()
    {
        S.Mode = 9;
        S.Thickness = 1;
        D.Down(200, 200);
        var r = Replay();
        var hits = new HashSet<(int, int)>();
        for (int k = 0; k <= 20; k++)
        {
            int dx = r.Next(16) - 8, dy = r.Next(16) - 8;
            if (DMath.Dist(dx, dy) <= 8) hits.Add((200 + dx, 200 + dy));
        }
        Assert.Equal(hits.Count, CountNot(0xFFFFFF));
        Assert.All(hits, h => Assert.Equal(0xFF0000, Canvas.GetRgb(h.Item1, h.Item2)));
        Assert.Equal(new[] { 10 }, Sounds);
    }

    [Fact]
    public void Spray_Big_TrioDotsInOrder()
    {
        S.Mode = 9;
        S.SprayMode = 3;
        S.Thickness = 2;
        D.Down(200, 200);
        var r = Replay();
        var (t0, t1, t2) = S.Trio;
        var expected = new List<Segment>();
        foreach (int c in new[] { t0, t1, t2 })
            for (int k = 0; k <= 20 / 3; k++)
            {
                int dx = r.Next(32) - 16, dy = r.Next(32) - 16;
                if (DMath.Dist(dx, dy) <= 16) expected.Add(new Segment(200 + dx, 200 + dy, 200 + dx, 200 + dy, 2, c));
            }
        Assert.Equal(expected, Log);
        var last = expected[^1];
        Assert.Equal(t2, Canvas.GetRgb(last.X0 - 1, last.Y0 - 1)); // the 2-px dot is drawn
    }

    [Theory]
    [InlineData(1, 77)]
    [InlineData(2, 78)]
    public void ThrowRugs_PensShapesAndSound(int shape, int snd)
    {
        S.Mode = 0x2A;
        S.RugShape = shape;
        D.Down(200, 200);
        var r = Replay();
        var pens = Enumerable.Range(0, 101).Select(_ => r.Next(160)).ToArray();
        int n = r.Next(60) + 30;
        var picks = Enumerable.Range(0, n).Select(_ => r.Next(100)).ToArray();
        Assert.Equal(r.Seed, Ctx.Random.Seed);
        Assert.Equal(new[] { snd }, Sounds);
        // The outermost outline is i = n in pen picks[n-1].
        int outer = ColorTable.Get(pens[picks[n - 1]]);
        Assert.Equal(outer, Canvas.GetRgb(200, 200 - n));
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(200, 200 - n - 3));
    }
}

public class YoungerPaintTests
{
    [Fact]
    public void Younger_PaintRestrictions()
    {
        var y = LevelDefinition.Younger;
        Assert.Equal(OptionKind.None, y.Option(2, 8)!.Kind); // no Magic Brush: the click removes the marker (R12)
        Assert.Equal(OptionKind.None, y.Option(2, 9)!.Kind); // no Calligraphy
        Assert.DoesNotContain(PanelId.BrushType, y.ClickablePanels);
        Assert.DoesNotContain(PanelId.Roller, y.ClickablePanels);
        Assert.Contains(PanelId.Spray, y.ClickablePanels);
        Assert.Contains(PanelId.Rug, y.ClickablePanels);
        Assert.Contains(PanelId.PaintPatterns, y.ClickablePanels);
        var s = new StudioState { BrushType = 2 };
        y.Option(2, 4)!.Setup!(s);
        Assert.Equal(3, s.BrushType);
        Assert.Equal(0x08, y.Option(2, 4)!.ModeOf!(s));
    }
}

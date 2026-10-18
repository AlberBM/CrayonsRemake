using Crayons.App.Rendering;
using Crayons.App.Tools;
using Crayons.Core;
using Crayons.Core.Tools;

namespace Crayons.Tests;

public class ShadedRendererTests
{
    [Fact]
    public void KTable()
    {
        Assert.Equal((11, 11, 11, 11), ShapesMath.LightK(1));
        Assert.Equal((14, 8, 14, 8), ShapesMath.LightK(2));
        Assert.Equal((14, 8, 8, 14), ShapesMath.LightK(5));
        Assert.Equal((14, 8, 11, 11), ShapesMath.LightK(9));
    }

    [Fact]
    public void Light1_ConcentricRings()
    {
        var p = ShapesMath.ShadedPasses(1, 20, 20, 100, 100);
        Assert.Equal((80, 80, 120, 120), p[0]);
        // i*20/11: 1.82 2, 3.64 4, 5.45 5, 7.27 7, 9.09 9, 10.9 11, 12.7 13, 14.5 15, 16.4 16
        Assert.Equal(new[] { 0, 2, 4, 5, 7, 9, 11, 13, 15, 16 }, p.Select(r => r.L - 80));
        Assert.All(p, r => Assert.Equal(200, r.L + r.R));
        Assert.All(p, r => Assert.Equal((r.L, r.R), (r.T, r.B)));
    }

    [Fact]
    public void Light2_HalfEvenRounding()
    {
        // sL = 20/14, sR = 2.5 (exact in float32): 2.5 -> 2, 7.5 -> 8, 12.5 -> 12, 22.5 -> 22.
        var p = ShapesMath.ShadedPasses(2, 20, 20, 100, 100);
        Assert.Equal((81, 81, 118, 118), p[1]);
        Assert.Equal((84, 84, 112, 112), p[3]);
        Assert.Equal((87, 87, 108, 108), p[5]);
        Assert.Equal((93, 93, 98, 98), p[9]);
    }

    [Fact]
    public void Light5_LowerLeftHighlight()
    {
        var p = ShapesMath.ShadedPasses(5, 20, 20, 100, 100);
        Assert.Equal((93, 102, 98, 107), p[9]);
        Assert.Equal(9, ShapesMath.ShadedPassShade(0));
        Assert.Equal(0, ShapesMath.ShadedPassShade(9));
    }

    [Fact]
    public void ShadedFromDrag()
    {
        Assert.Equal((25, 10, 125, 60), ShapesMath.ShadedFromDrag(150, 70, 100, 50));
        Assert.Equal((2, 0, 12, 7), ShapesMath.ShadedFromDrag(10, 7, 15, 8));
    }

    [Fact]
    public void Shaded_DrawsOutermostDarkest()
    {
        using var s = new DibSurface(200, 200, 0xFFFFFF);
        FillRenderer.Shaded(s.Hdc, 3, 1, 2, 20, 20, 100, 100);
        Assert.Equal(ColorTable.Get(39), s.GetRgb(80, 100));
        Assert.Equal(ColorTable.Get(30), s.GetRgb(100, 100));
        Assert.Equal(0xFFFFFF, s.GetRgb(79, 100));
    }
}

public class WackyMathTests
{
    [Fact]
    public void MiniTriangles()
    {
        Assert.Equal(new[] { (50, 40), (40, 60), (60, 60) }, WackyMath.MiniTriangle(1, 50, 50, 10));
        Assert.Equal(new[] { (40, 40), (60, 60), (40, 60) }, WackyMath.MiniTriangle(2, 50, 50, 10));
        Assert.Equal(new[] { (40, 40), (40, 60), (60, 50) }, WackyMath.MiniTriangle(3, 50, 50, 10));
    }

    [Fact]
    public void Tables()
    {
        Assert.Equal((15, 20), WackyMath.GrassSize(1));
        Assert.Equal((25, 35), WackyMath.GrassSize(2));
        Assert.Equal((45, 55), WackyMath.GrassSize(3));
        Assert.Equal(-4, WackyMath.HairSigned(4));
        Assert.Equal(3, WackyMath.HairSigned(3));
        Assert.Equal(new[] { 1, 0, 1 }, new[] { 1, 2, 3 }.Select(WackyMath.TrainDrift));
        Assert.Equal(5, WackyMath.FlipDrift(-3, 5));
        Assert.Equal(-5, WackyMath.FlipDrift(3, 5));
        Assert.Equal(new[] { 1, 54, 107, 160, 213, 160, 107, 54 }, WackyMath.MrBrushFrameX);
        Assert.Equal((22, 15, 514, 356), WackyMath.BarnZoomRect(1, 558, 386));
        Assert.Equal((264, 180, 30, 26), WackyMath.BarnZoomRect(12, 558, 386));
    }
}

public class WackyToolTests : ToolTestBase
{
    [Fact]
    public void MiniShapes_DrawsVForEveryShape()
    {
        S.Mode = 7;
        S.MiniShape = 1;
        D.Down(100, 100);
        var r = Replay();
        int rad = r.Next(20) + 5;
        r.Next(3);
        Assert.Equal(r.Seed, Ctx.Random.Seed);
        Assert.Equal(0xFF0000, Canvas.GetRgb(100 - rad, 100 - rad));
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(100 - rad - 1, 100));
        Assert.Equal(new[] { 8 }, Sounds);
    }

    [Fact]
    public void Spider_EightLinesXFirst()
    {
        S.Mode = 6;
        D.Down(200, 200);
        var r = Replay();
        var expected = Enumerable.Range(0, 8).Select(_ =>
        {
            int ex = 200 + r.Next(40) - 20;
            int ey = 200 + r.Next(40) - 20;
            return new Segment(200, 200, ex, ey, 2, 0xFF0000);
        });
        Assert.Equal(expected, Log);
        Assert.Equal(new[] { 7 }, Sounds);
    }

    [Fact]
    public void Crosshair_MoveDiscardsOneThenPicksTrio()
    {
        S.Mode = 3;
        D.Down(100, 50);
        Assert.Equal(new[] { (100, 386), (100, 0), (0, 50), (558, 50) }, Log.Select(l => (l.X1, l.Y1)));
        Log.Clear();
        S.MultiColor = true;
        D.Move(110, 60);
        var r = Replay();
        r.Next(159);
        r.Next(3);
        var (t0, t1, t2) = S.Trio;
        var trio = new[] { t0, t1, t2 };
        Assert.Equal(Enumerable.Range(0, 4).Select(_ => trio[r.Next(3)]), Log.Select(l => l.Rgb));
    }

    [Fact]
    public void ShadedBubble_UsesCurrentHueOnDown()
    {
        S.Mode = 0x1B;
        D.Down(200, 200);
        var r = Replay();
        int ox = r.Next(30) - 15, oy = r.Next(30) - 15, s = r.Next(15) + 3;
        Assert.Equal(r.Seed, Ctx.Random.Seed);
        int centre = Canvas.GetRgb(200 + ox, 200 + oy);
        Assert.Contains(centre, Enumerable.Range(90, 10).Select(i => ColorTable.Get(i))); // a shade of the current hue (9)
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(200 + ox - s - 1, 200 + oy));
        Assert.Equal(new[] { 28 }, Sounds);
    }

    [Fact]
    public void OutlineBubble_RandomOrder()
    {
        S.Mode = 0x1A;
        D.Down(200, 200);
        var r = Replay();
        int rad = r.Next(10) + 5, dx = r.Next(30) - 15, dy = r.Next(30) - 15;
        Assert.Equal(0xFF0000, Canvas.GetRgb(200 + dx - rad, 200 + dy));
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(200 + dx, 200 + dy));
    }

    [Fact]
    public void Ornaments_BallEvery40Px()
    {
        S.Mode = 0x11;
        D.Down(100, 100);
        Assert.Empty(Sounds);
        D.Move(130, 100);
        Assert.Empty(Sounds);
        D.Move(141, 100);
        Assert.Equal(new[] { 17 }, Sounds);
        Assert.Equal((141, 100), (S.AnchorX, S.AnchorY));
        Assert.Equal(new Segment(141, 100, 141, 110, 2, 0xFF0000), Log[^1]);
        Assert.Equal(ColorTable.Get(99), Canvas.GetRgb(141, 110 - 6)); // ball rx = 3t = 6, shade 9 outside
        D.Move(170, 100);
        Assert.Single(Sounds);
    }

    [Fact]
    public void Hair_ClownColourThenRadiusThenOffsets()
    {
        S.Mode = 0x38;
        S.MultiColor = true;
        D.Down(100, 100);
        D.Move(120, 100);
        var r = Replay();
        // down: r, o1..o4
        int rad = r.Next(10) + 2;
        for (int i = 0; i < 4; i++) r.Next(rad);
        r.Next(159);   // dispatcher clown colour (unused by Hair)
        r.Next(10);    // hair shade
        rad = r.Next(10) + 2;
        for (int i = 0; i < 4; i++) r.Next(rad);
        Assert.Equal(r.Seed, Ctx.Random.Seed);
    }

    [Fact]
    public void Grass_BladesInTrio()
    {
        S.Mode = 4;
        S.Grass = 1;
        D.Down(200, 200);
        var r = Replay();
        var (t0, t1, t2) = S.Trio;
        var trio = new[] { t0, t1, t2 };
        int n = r.Next(6) + 4;
        var expected = Enumerable.Range(0, n).Select(_ =>
        {
            int dx = r.Next(20) - 10, dy = r.Next(15), s = r.Next(3);
            return new Segment(200, 200, 200 + dx, 200 - dy, 1, trio[s]);
        });
        Assert.Equal(expected, Log);
    }

    [Fact]
    public void MrBrush_WalksDownAndPaints()
    {
        S.Mode = 0x32;
        D.Down(100, 100);
        var r = Replay();
        int runLen = r.Next(15) + 2;
        int drift = -(r.Next(8) + 1);
        // bx 75, by 75; the tip starts at (83, 118); first tick: by 79, bx 75 + drift, tip (bx + 8, 122).
        Assert.Equal(new Segment(75 + drift + 8, 122, 83, 118, 2, 0xFF0000), Log[0]);
        Assert.Equal(78, Log.Count); // by: 75 -> 387 in steps of 4
        Assert.Equal(75, Sounds[0]);
        Assert.Equal(76, Sounds[^1]);
        Assert.Equal(10, Sounds.Count(s => s == 75));
        Assert.Equal(10, Sounds.Count(s => s == 74));
        Assert.Equal(runLen, runLen);
        Assert.All(Log.Skip(1).Zip(Log), p => Assert.Equal((p.First.X1, p.First.Y1), (p.Second.X0, p.Second.Y0)));
    }

    [Fact]
    public void Train_LaysRailsAndTies()
    {
        S.Mode = 0x30;
        D.Down(300, 100);
        Assert.Equal(73, Sounds[0]);
        // tick 1: tx 262 -> a 320, b 330; rails from (325,110) and (320,116); |325-320| < 10: no tie.
        Assert.Equal(new Segment(325, 110, 320, 110, 2, 0xFF0000), Log[0]);
        Assert.Equal(new Segment(320, 116, 330, 116, 2, 0xFF0000), Log[1]);
        // tick 2: a 315: |325 - 315| >= 10 -> tie (315, y1-3) -> (325, y2+3).
        var r = Replay();
        int d = WackyMath.TrainDrift(r.Next(3) + 1);
        Assert.Equal(new Segment(315, 110 + d - 3, 325, 116 + d + 3, 2, 0xFF0000), Log[4]);
        int ticks = Log.Count(l => l.Y0 == l.Y1) / 2; // two rail segments per tick
        Assert.Equal(67, ticks); // tx 262 -> -73
    }

    [Fact]
    public void BarnZoom_And_Shear()
    {
        using var backing = new DibSurface(556, 384, 0x00FF00);
        WackyTools.BarnZoom(Canvas, backing);
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(21, 14));   // outside the first copy
        Assert.Equal(0x00FF00, Canvas.GetRgb(22, 15));   // first copy's corner
        Assert.Equal(0x00FF00, Canvas.GetRgb(293, 205)); // inside the twelfth copy (264,180) 30x26
        Assert.Equal(0x00FF00, Canvas.GetRgb(535, 370));   // first copy covers x 22..535, y 15..370
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(536, 371));
        var rnd = new DelphiRandom(7);
        WackyTools.Shear(Canvas, backing, rnd);
        var r = new DelphiRandom(7);
        for (int pass = 0; pass < 10; pass++)
        {
            r.Next(10); int seg = r.Next(50) + 10; int off = r.Next(seg) + 7;
            for (int y = 1; y <= 386; y++) { off++; if (off >= seg) { off = 0; seg = r.Next(50) + 10; } }
        }
        Assert.Equal(r.Seed, rnd.Seed);
    }
}

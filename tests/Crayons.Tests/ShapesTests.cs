using Crayons.App.Tools;
using Crayons.Core;
using Crayons.Core.Studio;
using Crayons.Core.Tools;

namespace Crayons.Tests;

public class ShapesMathTests
{
    [Fact]
    public void Triangles()
    {
        Assert.Equal(new[] { (10, 50), (31, 50), (20, 10) }, ShapesMath.Triangle(1, 10, 10, 31, 50).Points);
        Assert.Equal(new[] { (31, 50), (10, 50), (21, 10) }, ShapesMath.Triangle(1, 31, 10, 10, 50).Points); // toward X
        Assert.Equal(new[] { (10, 10), (31, 50), (10, 50) }, ShapesMath.Triangle(2, 10, 10, 31, 50).Points);
        Assert.Equal(new[] { (10, 10), (10, 51), (31, 30) }, ShapesMath.Triangle(3, 10, 10, 31, 51).Points);
    }

    [Fact]
    public void Trapezoids()
    {
        Assert.Equal(new[] { (0, 30), (52, 30), (40, 0), (10, 0) }, ShapesMath.Trapezoid(1, 0, 0, 52, 30).Points);
        Assert.Equal(new[] { (0, 0), (0, 52), (30, 40), (30, 10) }, ShapesMath.Trapezoid(2, 0, 0, 30, 52).Points);
        Assert.Equal(new[] { (52, 30), (0, 30), (12, 0), (42, 0) }, ShapesMath.Trapezoid(1, 52, 0, 0, 30).Points);
    }

    [Fact]
    public void Diamond_Hexagon_Octagon()
    {
        Assert.Equal(new[] { (0, 15), (25, 31), (51, 15), (25, 0) }, ShapesMath.Diamond(0, 0, 51, 31).Points);
        Assert.Equal(new[] { (0, 16), (10, 31), (42, 31), (52, 16), (42, 0), (10, 0) }, ShapesMath.Hexagon(0, 0, 52, 31).Points);
        // half-even: dy/2 = 2.5 -> 2
        Assert.Equal(new[] { (0, 2), (5, 5), (20, 5), (25, 2), (20, 0), (5, 0) }, ShapesMath.Hexagon(0, 0, 25, 5).Points);
        Assert.Equal(new[] { (0, 10), (0, 20), (10, 31), (20, 31), (30, 20), (30, 10), (20, 0), (10, 0) },
            ShapesMath.Octagon(0, 0, 30, 31).Points);
    }

    [Fact]
    public void Stars()
    {
        Assert.Equal(new[] { (0, 25), (40, 30), (50, 50), (60, 30), (100, 25), (60, 20), (50, 0), (40, 20) },
            ShapesMath.Star(1, 0, 0, 100, 50).Points);
        Assert.Equal(new[] { (0, 0), (40, 25), (0, 50), (50, 30), (100, 50), (60, 25), (100, 0), (50, 20) },
            ShapesMath.Star(2, 0, 0, 100, 50).Points);
    }

    [Fact]
    public void Parallelograms()
    {
        Assert.Equal(new[] { (10, 10), (50, 10), (30, 50), (-10, 50) }, ShapesMath.Parallelogram(4, 10, 10, 40, 20, 30, 50).Points);
        Assert.Equal(new[] { (10, 10), (10, 30), (30, 50), (30, 30) }, ShapesMath.Parallelogram(3, 10, 10, 40, 20, 30, 50).Points);
        // The first step-2 erase (at the step-1 corner) equals the step-1 polygon.
        Assert.Equal(ShapesMath.ParallelogramStep1(10, 10, 50, 30).Points, ShapesMath.Parallelogram(4, 10, 10, 40, 20, 50, 30).Points);
        Assert.Equal(ShapesMath.ParallelogramStep1(10, 10, 50, 30).Points!.ToHashSet(), ShapesMath.Parallelogram(3, 10, 10, 40, 20, 50, 30).Points!.ToHashSet());
        Assert.Equal((-10, 10, 50, 50), ShapesMath.Parallelogram(4, 10, 10, 40, 20, 30, 50).Box);
    }

    [Fact]
    public void RoundedSquare_Corners() =>
        Assert.Equal((10, 7), (ShapesMath.RoundedSquare(50, 50, 10, 21).CornerW, ShapesMath.RoundedSquare(50, 50, 10, 21).CornerH));

    [Fact]
    public void Gradient_BandsOverlapByOneLine()
    {
        var lines = ShapesMath.GradientLines(16, 0, 0, 25, 10);
        // s = 2.5: band ends Round(2.5)=2, Round(5)=5, Round(7.5)=8, 10, Round(12.5)=12, ...
        var band0 = lines.Where(l => l.Shade == 9).Select(l => l.X0);
        var band1 = lines.Where(l => l.Shade == 8).Select(l => l.X0);
        var band2 = lines.Where(l => l.Shade == 7).Select(l => l.X0);
        Assert.Equal(new[] { 0, 1, 2 }, band0);
        Assert.Equal(new[] { 2, 3, 4, 5 }, band1);
        Assert.Equal(new[] { 5, 6, 7, 8 }, band2);
        Assert.Equal(25, lines[^1].X0);
        Assert.Equal(0, lines[^1].Shade);
        Assert.All(lines, l => Assert.Equal((0, 10), (l.Y0, l.Y1)));
    }

    [Fact]
    public void Gradient_Directions()
    {
        var h = ShapesMath.GradientLines(19, 0, 0, 10, 20);
        Assert.Equal(new GradientLine(0, 0, 0, 10, 0), h[0]); // light first, horizontal from minY
        Assert.Equal(9, h[^1].Shade);
        var slash = ShapesMath.GradientLines(20, 10, 0, 30, 10); // w 20, h 10, s 3
        Assert.Equal(new GradientLine(9, 10, 0, 0, 10), slash[0]);
        Assert.Equal(40, slash[^1].X0); // v runs from minX to minX + w + h
        var back = ShapesMath.GradientLines(21, 10, 0, 30, 10);
        Assert.Equal(new GradientLine(0, 10, 10, 0, 0), back[0]);
        Assert.Equal(new GradientLine(9, 10, 10, 0, 0), ShapesMath.GradientLines(22, 10, 0, 30, 10)[0]);
        Assert.Equal(new GradientLine(0, 10, 0, 0, 10), ShapesMath.GradientLines(23, 10, 0, 30, 10)[0]);
    }

    [Fact]
    public void Cube_OffsetsAndFaces()
    {
        Assert.Equal((30, 15), ShapesMath.CubeOffsets(100, 50, 3));
        Assert.Equal((2, 2), ShapesMath.CubeOffsets(25, 25, 1)); // 2.5 -> 2
        Assert.Equal((8, 8), ShapesMath.CubeOffsets(25, 25, 3)); // 7.5 -> 8
        Assert.Equal((2, 2), ShapesMath.CubeOffsets(-15, 15, 1)); // 1.5 -> 2
        var f2 = ShapesMath.CubeFaces(2, 3, 200, 150, 100, 100);
        Assert.Equal(new[] { (100, 100), (200, 100), (200, 150), (100, 150) }, f2[0].Points);
        Assert.Equal(23, f2[0].Style);
        Assert.Equal(new[] { (70, 85), (170, 85), (200, 100), (100, 100) }, f2[1].Points);
        Assert.Equal(18, f2[1].Style);
        Assert.Equal(new[] { (70, 85), (70, 135), (100, 150), (100, 100) }, f2[2].Points);
        Assert.Equal(16, f2[2].Style);
        var f7 = ShapesMath.CubeFaces(7, 3, 100, 100, 200, 150);
        Assert.Equal(20, f7[0].Style);
        Assert.Equal(new[] { (130, 165), (230, 165), (200, 150), (100, 150) }, f7[1].Points);
        Assert.Equal(19, f7[1].Style);
        Assert.Equal(new[] { (200, 100), (230, 115), (230, 165), (200, 150) }, f7[2].Points);
        Assert.Equal(17, f7[2].Style);
        var f4 = ShapesMath.CubeFaces(4, 3, 100, 100, 200, 150);
        Assert.Equal((22, 18, 17), (f4[0].Style, f4[1].Style, f4[2].Style));
        Assert.Equal(new[] { (230, 85), (200, 100), (100, 100), (130, 85) }, f4[1].Points);
        var f5 = ShapesMath.CubeFaces(5, 3, 100, 100, 200, 150);
        Assert.Equal((21, 19, 16), (f5[0].Style, f5[1].Style, f5[2].Style));
        Assert.Equal(new[] { (70, 115), (70, 165), (100, 150), (100, 100) }, f5[2].Points);
    }

    [Fact]
    public void Torus_PassList()
    {
        var p = ShapesMath.TorusPasses();
        Assert.Equal(20, p.Length);
        Assert.Equal((19, 9), p[0]);
        Assert.Equal((10, 0), p[9]);
        Assert.Equal((9, 0), p[10]);
        Assert.Equal((0, 9), p[19]);
    }

    [Fact]
    public void Line3D_PassLists()
    {
        Assert.Equal(new[] { new Line3DPass(9, 0, 2, 0, -2), new(6, 0, 1, 0, -1), new(3, 0, 0, 0, 0) }, ShapesMath.Line3DPasses(1, 10, 0));
        Assert.Equal(new[] { 9, 7, 5, 3 }, ShapesMath.Line3DPasses(2, 0, 10).Select(p => p.Shade));
        Assert.Equal(new Line3DPass(9, 3, 0, -3, 0), ShapesMath.Line3DPasses(2, 0, 10)[0]);   // vertical: x ± o
        Assert.Equal(new Line3DPass(9, 3, 0, -3, 0), ShapesMath.Line3DPasses(2, 0, 0)[0]);    // vertical wins over horizontal
        Assert.Equal(new Line3DPass(9, 4, 4, -4, -4), ShapesMath.Line3DPasses(3, 10, -5)[0]); // opposite signs
        Assert.Equal(new Line3DPass(9, -4, 4, 4, -4), ShapesMath.Line3DPasses(3, -10, -5)[0]); // same signs
        Assert.Equal(new[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 0 }, ShapesMath.Line3DPasses(10, 5, 5).Select(p => p.Shade));
        Assert.Equal(new[] { 9, 7, 5, 3, 1 }, ShapesMath.Line3DPasses(3, 5, 5).Select(p => p.Shade));
    }

    [Fact]
    public void BrushRadius_Table() =>
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 8, 10, 12, 14, 16 }, Enumerable.Range(1, 10).Select(ShapesMath.BrushRadius));

    [Fact]
    public void ShapeUpSounds_NeedBothAxes()
    {
        Assert.Null(ModeSounds.Up(0x2C, true, false));
        Assert.Equal(44, ModeSounds.Up(0x2C, true, true));
        Assert.Null(ModeSounds.Up(0x3C, false, true));
        Assert.Equal(39, ModeSounds.Up(0x3B, false, true));
        Assert.Null(ModeSounds.Up(0x37, true, false));
    }
}

public class ShapesToolTests : ToolTestBase
{
    [Fact]
    public void Rectangle_XorPreviewLeavesOnlyFinalOutline()
    {
        S.Mode = 0x1F;
        S.Thickness = 1;
        S.FillPattern = 0;
        D.Down(10, 10);
        D.Move(50, 40);
        Assert.Equal(0x000000, Canvas.GetRgb(30, 10)); // white XOR white
        D.Move(60, 50);
        D.Up(60, 50);
        Assert.Equal(new[] { 31 }, Sounds);
        Assert.Equal(0xFF0000, Canvas.GetRgb(30, 10));
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(30, 30));
        Assert.Equal(2 * 50 + 2 * 38, CountNot(0xFFFFFF));
    }

    [Fact]
    public void Ellipse_ZeroHeightDragRendersNothing()
    {
        S.Mode = 0x21;
        D.Down(10, 10);
        D.Move(50, 10);
        D.Up(50, 10);
        Assert.Empty(Sounds);
        Assert.Equal(0, Canvas.GetRgb(30, 10) == 0xFF0000 ? 1 : 0);
    }

    [Fact]
    public void Gradient16_DarkLeftLightRight()
    {
        S.Mode = 0x1F;
        S.FillPattern = 16;
        S.ColorIndex = 3;
        D.Down(100, 100);
        D.Move(200, 150);
        D.Up(200, 150);
        Assert.Equal(ColorTable.Get(39), Canvas.GetRgb(101, 120));
        Assert.Equal(ColorTable.Get(30), Canvas.GetRgb(198, 120));
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(99, 120));  // clipped to the rectangle region
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(150, 150)); // the region excludes the bottom edge
    }

    [Fact]
    public void PictureFill_TilesClippedToEllipse()
    {
        var pb = new Crayons.App.Rendering.DibSurface(54, 54, 0x00FF00);
        pb.SetRgb(0, 0, 0x0000FF);
        Images["PB1.ASL"] = pb;
        S.Mode = 0x21;
        S.FillPattern = 24;
        D.Down(100, 100);
        D.Move(300, 200);
        D.Up(300, 200);
        Assert.Equal(0x00FF00, Canvas.GetRgb(200, 150));
        Assert.Equal(0x0000FF, Canvas.GetRgb(154, 154)); // tile origin repeats every 54 px from (100,100)
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(101, 101)); // outside the ellipse
    }

    [Fact]
    public void Parallelogram_TwoSteps()
    {
        S.Mode = 0x36;
        S.FillPattern = 1;
        S.Thickness = 1;
        D.Down(100, 100);
        D.Move(140, 120);
        D.Up(140, 120);
        Assert.Equal(0x37, S.Mode);
        Assert.Empty(Sounds);
        Assert.True(D.TracksWithoutButton);
        D.Move(160, 160);  // no button held
        D.Move(170, 180);
        D.Down(170, 180);
        Assert.Equal((100, 100), (S.AnchorX, S.AnchorY)); // the press keeps the anchor
        D.Up(170, 180);
        Assert.Equal(0x36, S.Mode);
        Assert.Equal(new[] { 56 }, Sounds);
        // variant 4: (100,100) (140,100) (170,180) (130,180), filled red; no XOR residue elsewhere.
        Assert.Equal(0xFF0000, Canvas.GetRgb(135, 140));
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(100, 120)); // the step-1 rectangle's left edge was erased
        Assert.Equal(0xFF0000, Canvas.GetRgb(140, 120)); // R11: no white clean-up pixel at the stored corner (B33)
        Assert.Equal(0, Count(c => c is not (0xFFFFFF or 0xFF0000))); // no XOR residue
    }

    [Fact]
    public void Parallelogram_Step1Band_LeavesAnchorAndOppositeCornerWhite()
    {
        // R11: the step-1 band is two polylines from the anchor; the anchor is XORed twice, the far corner never.
        S.Mode = 0x36;
        D.Down(100, 100);
        D.Move(130, 115);
        D.Move(140, 120);
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(100, 100));
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(140, 120));
        Assert.Equal(0x000000, Canvas.GetRgb(140, 100));
        Assert.Equal(0x000000, Canvas.GetRgb(100, 120));
        Assert.Equal(0x000000, Canvas.GetRgb(120, 100));
        Assert.Equal(0x000000, Canvas.GetRgb(140, 110));
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(130, 115)); // the previous band was erased
        Assert.Equal(new[] { (100, 100, 140, 100), (140, 100, 140, 120), (100, 100, 100, 120), (100, 120, 140, 120) },
            ShapesMath.ParallelogramStep1Band(100, 100, 140, 120));
    }

    [Fact]
    public void Sphere_UsesLightSourceAndColourGroup()
    {
        S.Mode = 0x2C;
        S.LightSource = 1;
        S.ColorIndex = 11;
        D.Down(100, 100);
        D.Move(140, 140);
        D.Up(140, 140);
        Assert.Equal(new[] { 44 }, Sounds);
        Assert.Equal(ColorTable.Get(110), Canvas.GetRgb(120, 120));
        Assert.Equal(ColorTable.Get(119), Canvas.GetRgb(100, 120));
    }

    [Fact]
    public void Torus_TubeOutsideTheEllipse()
    {
        S.Mode = 0x2B;
        D.Down(200, 150);
        D.Move(300, 250);
        D.Up(300, 250);
        Assert.Equal(new[] { 43 }, Sounds);
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(250, 200)); // inside untouched
        Assert.NotEqual(0xFFFFFF, Canvas.GetRgb(190, 200)); // tube on the left
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(175, 200));
    }

    [Fact]
    public void Line3D_DrawsPassPairs()
    {
        S.Mode = 0x3B;
        S.Thickness = 1;
        D.Down(100, 100);
        D.Move(200, 100);
        D.Up(200, 100);
        Assert.Equal(new[] { 39 }, Sounds);
        var final = Log.Where(l => !l.Xor).ToList();
        Assert.Equal(6, final.Count);
        Assert.Equal(new Segment(100, 102, 200, 102, 2, ColorTable.Get(99)), final[0]);
        Assert.Equal(new Segment(100, 98, 200, 98, 2, ColorTable.Get(99)), final[1]);
        Assert.Equal(ColorTable.Get(93), final[5].Rgb);
    }

    [Fact]
    public void Cube_DrawsThreeGradientFaces()
    {
        S.Mode = 0x3C;
        S.BoxDir = 2;
        S.BoxDepth = 3;
        S.ColorIndex = 3;
        D.Down(100, 100);
        D.Move(200, 150);
        D.Up(200, 150);
        Assert.Equal(new[] { 46 }, Sounds);
        Assert.NotEqual(0xFFFFFF, Canvas.GetRgb(150, 125)); // front
        Assert.NotEqual(0xFFFFFF, Canvas.GetRgb(150, 95));  // top
        Assert.NotEqual(0xFFFFFF, Canvas.GetRgb(85, 120));  // left side
        Assert.Equal(0xFFFFFF, Canvas.GetRgb(210, 90));
        Assert.Equal(1, S.FillPattern); // the user's own fill style is untouched
    }

    [Fact]
    public void Jumble_SwapsPixelsWithinRadius()
    {
        S.Mode = 0x3D;
        S.Thickness = 1;
        Gdi_FillLeftHalfBlack();
        D.Down(100, 100);
        Assert.Equal(new[] { 29 }, Sounds);
        var r = Replay();
        for (int i = 0; i <= 10; i++) for (int k = 0; k < 4; k++) r.Next(4);
        Assert.Equal(r.Seed, Ctx.Random.Seed);
        int black = 0;
        for (int y = 90; y < 110; y++) for (int x = 90; x < 110; x++) if (Canvas.GetRgb(x, y) == 0) black++;
        Assert.Equal(10 * 20, black); // swaps preserve the pixel count
    }

    [Fact]
    public void Smear_SkippedNearLeftEdge()
    {
        S.Mode = 0x3E;
        S.Thickness = 1;
        D.Down(4, 100); // X - 2r <= 0
        Assert.Equal(Seed, Ctx.Random.Seed);
        D.Move(50, 100);
        var r = Replay();
        for (int row = 98; row <= 102; row++) r.Next(8);
        Assert.Equal(r.Seed, Ctx.Random.Seed);
    }

    void Gdi_FillLeftHalfBlack() => Crayons.App.Rendering.Gdi.FillRect(Canvas.Hdc, 0, 0, 100, 384, 0x000000);
}

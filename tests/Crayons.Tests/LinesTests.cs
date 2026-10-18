using Crayons.App.Rendering;
using Crayons.App.Tools;
using Crayons.Core;
using Crayons.Core.Studio;
using Crayons.Core.Tools;

namespace Crayons.Tests;

public class LinesMathTests
{
    [Fact]
    public void CrazyPen_SixStrandsAsDocumented()
    {
        Assert.Equal(new[] { (2, -2), (1, -1), (0, 0), (-1, 1), (-2, 2), (-3, 3) }, LinesMath.CrazyPenOffsets(2));
        Assert.Equal(new[] { 95, 95, 115, 115, 30, 30 }, Enumerable.Range(0, 6).Select(LinesMath.CrazyPenColorIndex));
    }

    [Theory]
    [InlineData(0x0C, 0, 8)]
    [InlineData(0x0C, 3, 8)]
    [InlineData(0x0C, 4, 6)]
    [InlineData(0x0C, 8, 6)]
    [InlineData(0x0C, 9, 4)]
    [InlineData(0x0C, 15, 4)]
    [InlineData(0x0C, 16, 2)]
    [InlineData(0x0C, 100, 2)]
    [InlineData(0x0C, 500, 2)]
    [InlineData(0x0D, 0, 2)]
    [InlineData(0x0D, 5, 4)]
    [InlineData(0x0D, 10, 6)]
    [InlineData(0x0D, 101, 8)]
    public void ThickThinWidths(int mode, int d, int width) => Assert.Equal(width, LinesMath.ThickThinWidth(mode, d));

    [Fact]
    public void ThickThinDistance_IsHalfManhattan() => Assert.Equal(3, LinesMath.ThickThinDistance(-4, 3));

    [Fact]
    public void Mirrors_UseOuterCanvasSize()
    {
        Assert.Equal(558, LinesMath.MirrorX(0));
        Assert.Equal(458, LinesMath.MirrorX(100));
        Assert.Equal(386, LinesMath.MirrorY(0));
        Assert.Equal(186, LinesMath.MirrorY(200));
    }

    [Fact]
    public void SymmetrySegments_PerMode()
    {
        Assert.Equal(new[] { (10, 20, 30, 40), (548, 20, 528, 40) }, LinesMath.SymmetrySegments(2, 10, 20, 30, 40));
        Assert.Equal(new[] { (10, 20, 30, 40), (10, 366, 30, 346) }, LinesMath.SymmetrySegments(1, 10, 20, 30, 40));
        Assert.Equal(new[] { (10, 20, 30, 40), (548, 20, 528, 40), (10, 366, 30, 346), (548, 366, 528, 346) },
            LinesMath.SymmetrySegments(3, 10, 20, 30, 40));
    }
}

public class ToolbarLayoutTests
{
    const string PixelMap = @"E:\Programs\Reverse Engineering Lab\investigations\crayons\spec\reference\toolbar-pixelmap.txt";

    [Fact]
    public void TabEdges()
    {
        Assert.Equal(85, ToolbarLayout.TabLeftAt(0, 62));
        Assert.Equal(90, ToolbarLayout.TabLeftAt(0, 91));
        Assert.Equal(130, ToolbarLayout.TabRightAt(0, 62));
        Assert.Equal(125, ToolbarLayout.TabRightAt(0, 91));
        Assert.Equal(540, ToolbarLayout.TabRightAt(10, 62));
        Assert.Equal((95, 60), ToolbarLayout.IconAt(0));
        Assert.Equal((382, 60), ToolbarLayout.IconAt(7));
    }

    /// <summary>
    /// Compares the procedural strip with the observed pixel map (Older, tab 2 selected),
    /// ignoring the icon rectangles. Skipped when the lab's reference file is absent.
    /// </summary>
    [SkippableFact]
    public void MatchesObservedPixelMap()
    {
        Skip.IfNot(File.Exists(PixelMap), "reference pixel map not available");
        var rows = File.ReadAllLines(PixelMap).Where(l => l.StartsWith("y=")).ToDictionary(l => int.Parse(l[2..5]), l => l[6..]);
        var px = ToolbarLayout.Render(11, 2);
        var icons = Enumerable.Range(0, 11).Select(ToolbarLayout.IconAt).ToArray();
        int compared = 0;
        var diffs = new List<string>();
        for (int y = 60; y <= 91; y++)
            for (int x = 82; x < 640; x++)
            {
                if (icons.Any(i => x >= i.X && x < i.X + 32 && y >= i.Y && y < i.Y + 32)) continue;
                char want = rows[y][x - 82];
                if (want == '?') continue;
                char got = px[y - 60, x - 82] switch { TabPixel.White => 'W', TabPixel.Black => 'K', TabPixel.Dark => 'D', _ => 'L' };
                compared++;
                if (want != got) diffs.Add($"({x},{y}) want {want} got {got}");
            }
        Assert.True(compared > 4000, $"only {compared} pixels compared");
        Assert.True(diffs.Count == 0, string.Join("; ", diffs.Take(20)));
    }
}

public class ToolbarViewTests
{
    const string PixelMap = @"E:\Programs\Reverse Engineering Lab\investigations\crayons\spec\reference\toolbar-pixelmap.txt";

    /// <summary>The rendered strip with the EXE's icons, against every non-icon-coloured cell of the observed map.</summary>
    [SkippableFact]
    public void WithIcons_MatchesObservedPixelMap()
    {
        var root = Crayons.Core.Assets.AssetStore.FindInstall();
        Skip.If(root == null || !File.Exists(PixelMap), "install or reference pixel map not available");
        var rows = File.ReadAllLines(PixelMap).Where(l => l.StartsWith("y=")).ToDictionary(l => int.Parse(l[2..5]), l => l[6..]);
        using var images = new ImageCache(new Crayons.Core.Assets.AssetStore(root!));
        using var view = new Crayons.App.Studio.ToolbarView();
        view.Draw(LevelDefinition.Older, images, 2);
        var codes = new Dictionary<int, char> { [0xFFFFFF] = 'W', [0x000000] = 'K', [0x646464] = 'D', [0xA0A0A0] = 'L' };
        var diffs = new List<string>();
        int iconCells = 0, iconColoured = 0;
        for (int y = 60; y <= 91; y++)
            for (int x = 82; x < 640; x++)
            {
                int rgb = view.Surface.GetRgb(x - 82, y - 60);
                char got = codes.TryGetValue(rgb, out var g) ? g : '?';
                char want = rows[y][x - 82];
                if (want == '?') { iconCells++; if (got == '?') iconColoured++; continue; }
                if (want != got) diffs.Add($"({x},{y}) want {want} got {rgb:X6}");
            }
        Assert.True(diffs.Count == 0, string.Join("; ", diffs.Take(20)));
        Assert.Equal(iconCells, iconColoured);
    }
}

public class LinesToolTests : IDisposable
{
    readonly DibSurface _canvas = new(556, 384, 0xFFFFFF);
    readonly StudioState _s = new();
    readonly List<Segment> _log = new();
    readonly List<int> _sounds = new();
    readonly ToolDispatcher _d;
    const uint Seed = 12345;

    public LinesToolTests()
    {
        var painter = new CanvasPainter(_canvas) { Log = _log };
        _d = new ToolDispatcher(new ToolContext { State = _s, Random = new DelphiRandom(Seed), Canvas = painter, PlaySound = _sounds.Add });
    }

    public void Dispose() => _canvas.Dispose();

    [Fact]
    public void Crayon_DotThenSegments()
    {
        _s.Mode = 1;
        _d.Down(10, 10);
        _d.Move(20, 15);
        _d.Move(30, 15);
        _d.Up(30, 15);
        Assert.Equal(new[] { 1 }, _sounds);
        Assert.Equal(new Segment(10, 10, 11, 10, 2, 0xFF0000), _log[0]);
        Assert.Equal(new Segment(10, 10, 20, 15, 2, 0xFF0000), _log[1]);
        Assert.Equal(new Segment(20, 15, 30, 15, 2, 0xFF0000), _log[2]);
        Assert.Equal(0xFF0000, _canvas.GetRgb(25, 15));
    }

    [Fact]
    public void Clown_PicksRandom159OncePerMoveEvent()
    {
        _s.Mode = 2; // corner: four lines per event share one colour
        _s.MultiColor = true;
        _d.Down(100, 100);
        _d.Move(110, 100);
        _d.Move(120, 100);
        var r = new DelphiRandom(Seed);
        int c1 = ColorTable.Get(r.Next(159)), c2 = ColorTable.Get(r.Next(159));
        Assert.All(_log.Take(4), s => Assert.Equal(0xFF0000, s.Rgb)); // the press uses the current colour
        Assert.All(_log.Skip(4).Take(4), s => Assert.Equal(c1, s.Rgb));
        Assert.All(_log.Skip(8).Take(4), s => Assert.Equal(c2, s.Rgb));
        Assert.Equal(new[] { (0, 0), (558, 0), (558, 386), (0, 386) }, _log.Take(4).Select(s => (s.X1, s.Y1)));
    }

    [Fact]
    public void Radial_DrawsFromAnchor()
    {
        _s.Mode = 0x17;
        _d.Down(50, 50);
        _d.Move(60, 60);
        _d.Move(70, 50);
        Assert.Equal(new[] { 24 }, _sounds);
        Assert.Equal((50, 50, 70, 50), (_log[2].X0, _log[2].Y0, _log[2].X1, _log[2].Y1));
    }

    [Fact]
    public void Chalk_RandomOrderAndTrio()
    {
        _s.Mode = 0x0A;
        _s.ColorIndex = 3; _s.ShadeIndex = 0; // trio uses max(shade, 2)
        _d.Down(200, 200);
        var expected = new List<Segment> { new(200, 200, 200, 200, 12, ColorTable.Get(30)) };
        var r = new DelphiRandom(Seed);
        foreach (int c in new[] { 32, 31, 30 })
            for (int i = 0; i < 7; i++)
            {
                int dx = r.Next(16) - 8, dy = r.Next(16) - 8;
                if (DMath.Dist(dx, dy) <= 8) expected.Add(new(200 + dx, 200 + dy, 201 + dx, 201 + dy, 1, ColorTable.Get(c)));
            }
        Assert.Equal(expected, _log);
        Assert.Equal(new[] { 11 }, _sounds);
    }

    [Fact]
    public void Caterpillar_PenOrderAndClownExtraCall()
    {
        _s.Mode = 0x13;
        _s.MultiColor = true;
        _s.Thickness = 3;
        _d.Down(100, 100);
        var r = new DelphiRandom(Seed);
        r.Next(16); // the unused clown-mode call
        var (t0, t1, t2) = _s.Trio;
        var expected = new List<Segment>();
        foreach (int c in new[] { t0, t2, t1 })
        {
            int k = r.Next(50) + 20;
            for (int i = 0; i <= k; i++)
            {
                int dx = r.Next(60) - 30, dy = r.Next(60) - 30;
                if (DMath.Dist(dx, dy) <= 18) expected.Add(new(100, 100, 100 + dx, 100 + dy, 1, c));
            }
        }
        Assert.Equal(expected, _log);
    }

    [Fact]
    public void CrazyPen_ForcesThicknessTwoAndRestores()
    {
        _s.Mode = 5;
        _s.Thickness = 7;
        _d.Down(50, 50);
        Assert.Equal(2, _s.Thickness);
        Assert.Equal(6, _log.Count);
        Assert.Equal(new Segment(52, 48, 53, 48, 2, 0xFF0000), _log[0]);
        Assert.Equal(new Segment(47, 53, 48, 53, 2, 0x0000FF), _log[5]);
        _d.Move(60, 50);
        Assert.Equal(new Segment(50, 50, 60, 50, 2, 0x00C300), _log[8]);
        _d.Up(60, 50);
        Assert.Equal(7, _s.Thickness);
    }

    [Fact]
    public void Symmetry_FourWay()
    {
        _s.Mode = 0x0B;
        _s.Symmetry = 3;
        _d.Down(10, 20);
        Assert.Equal(new[] { (10, 20, 11, 21), (548, 20, 547, 21), (10, 366, 11, 365), (548, 366, 547, 365) },
            _log.Select(s => (s.X0, s.Y0, s.X1, s.Y1)));
    }

    [Fact]
    public void ThickThin_WidthFromSpeed()
    {
        _s.Mode = 0x0C;
        _d.Down(10, 10);
        _d.Move(12, 10);   // d = 1 -> 8
        _d.Move(32, 20);   // d = 15 -> 4
        _d.Move(132, 120); // d = 100 -> 2
        Assert.Equal(new[] { 8, 8, 4, 2 }, _log.Select(s => s.Width));
        Assert.Equal(new[] { 13 }, _sounds);
    }

    [Fact]
    public void StraightLine_XorBandLeavesOnlyFinalLine()
    {
        _s.Mode = 0x26;
        _s.Thickness = 1;
        _d.Down(10, 10);
        _d.Move(100, 50);
        Assert.Equal(0x000000, _canvas.GetRgb(55, 30)); // white XOR white band on white = black
        _d.Move(100, 10);
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(55, 30)); // old band erased
        _d.Up(100, 10);
        Assert.Equal(0xFF0000, _canvas.GetRgb(50, 10));
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(55, 30));
        Assert.Equal(new[] { 38 }, _sounds);
        var count = 0;
        for (int y = 0; y < 384; y++) for (int x = 0; x < 556; x++) if (_canvas.GetRgb(x, y) != 0xFFFFFF) count++;
        Assert.Equal(90, count); // (10,10)..(99,10): LineTo excludes the end pixel
    }

    [Fact]
    public void StraightLine_ClickWithoutMoveDrawsNothing()
    {
        _s.Mode = 0x26;
        _d.Down(10, 10);
        _d.Up(10, 10);
        Assert.Empty(_sounds);
        Assert.DoesNotContain(_log, s => !s.Xor);
    }

    [Fact]
    public void ModeZero_DrawsNothing()
    {
        _s.Mode = 0;
        _d.Down(1, 1);
        _d.Move(5, 5);
        _d.Up(5, 5);
        Assert.Empty(_log);
        Assert.Empty(_sounds);
    }
}

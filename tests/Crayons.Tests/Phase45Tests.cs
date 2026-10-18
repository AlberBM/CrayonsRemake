using Crayons.App.Rendering;
using Crayons.App.Tools;
using Crayons.Core;
using Crayons.Core.Assets;
using Crayons.Core.Content;
using Crayons.Core.Studio;
using Crayons.Core.Tools;

namespace Crayons.Tests;

public class TextMathTests
{
    [Fact]
    public void PointsToPixels_At96Ppi()
    {
        Assert.Equal(13, TextMath.PointsToPixels(10)); // 13.33
        Assert.Equal(103, TextMath.PointsToPixels(77)); // 102.67
        Assert.Equal(19, TextMath.ThicknessFontSize(2)); // 2*7*96/72 = 18.67
        Assert.Equal(9, TextMath.ThicknessFontSize(1));
    }

    [Fact]
    public void FitSize_BinarySearch_Sequence()
    {
        // width = 3 * size: target 120 -> exact hit at s = 40 (mid 30).
        var tried = new List<int>();
        int s = TextMath.FitSize(sz => { tried.Add(sz); return 3 * sz; }, 120, 2);
        // mids: 77 (s103, wider) -> hi 77; 39 (s52) -> hi 39; 20 (s27) -> lo 20; 29 (s39 -> 117 narrower) -> lo 29;
        // 34 (s45 -> wider) -> hi 34; 31 (s41 -> 123 wider) -> hi 31; 30 (s40 -> 120 equal) stop.
        Assert.Equal(new[] { 103, 52, 27, 39, 45, 41, 40 }, tried);
        Assert.Equal(40, s);
    }

    [Fact]
    public void FitSize_GapStopsEarlier()
    {
        var two = new List<int>(); var three = new List<int>();
        TextMath.FitSize(sz => { two.Add(sz); return 1000; }, 10, 2);
        TextMath.FitSize(sz => { three.Add(sz); return 1000; }, 10, 3);
        // always wider: hi shrinks 152 -> 77 -> 39 -> 20 -> 11 -> 6 -> 4 -> 3 (|3-2| < 2 stops)
        Assert.Equal(new[] { 103, 52, 27, 15, 8, 5, 4 }, two);
        Assert.Equal(new[] { 103, 52, 27, 15, 8, 5 }, three); // |4-2| < 3 stops one step earlier
        Assert.Equal(3, TextMath.FitGap(0x4C));
        Assert.Equal(3, TextMath.FitGap(0x4D));
        Assert.Equal(2, TextMath.FitGap(0x4B));
        Assert.Equal(2, TextMath.FitGap(0x4E));
    }

    [Fact]
    public void ShadowOffsets()
    {
        Assert.Equal((-4, -4), TextMath.ShadowOffset(2, 4));
        Assert.Equal((0, -4), TextMath.ShadowOffset(3, 4));
        Assert.Equal((4, -4), TextMath.ShadowOffset(4, 4));
        Assert.Equal((-4, 4), TextMath.ShadowOffset(5, 4));
        Assert.Equal((0, 4), TextMath.ShadowOffset(6, 4));
        Assert.Equal((4, 4), TextMath.ShadowOffset(7, 4));
    }

    [Theory]
    [InlineData(2, 10, 10, 13, 13, 14, 14)]
    [InlineData(3, 10, 10, 10, 13, 10, 14)]
    [InlineData(4, 10, 10, 7, 13, 6, 14)]
    [InlineData(5, 10, 14, 13, 11, 14, 10)]
    [InlineData(6, 10, 14, 10, 11, 10, 10)]
    [InlineData(7, 14, 14, 11, 11, 10, 10)]
    public void ThreeD_Layers(int dir, int fx0, int fy0, int lxLast, int lyLast, int faceX, int faceY)
    {
        // s = 9 -> d = 3: layers k = 0..3.
        var (layers, face) = TextMath.ThreeDLayout(dir, 9, 10, 10);
        Assert.Equal(4, layers.Length);
        Assert.Equal((fx0, fy0), layers[0]);
        Assert.Equal((lxLast, lyLast), layers[3]);
        Assert.Equal((faceX, faceY), face);
    }

    [Fact]
    public void Perspective_Copies()
    {
        // s = 5, w = 64 -> d = 8; heights s-d+k > 0 for k = 4..8.
        var (copies, face) = TextMath.PerspectiveLayout(5, 64, 200);
        Assert.Equal(new[] { (1, 196), (2, 195), (3, 194), (4, 193), (5, 192) }, copies);
        Assert.Equal((5, 192), face);
        var (all, f2) = TextMath.PerspectiveLayout(40, 80, 100);
        Assert.Equal(11, all.Length);
        Assert.Equal((30, 100), all[0]);
        Assert.Equal((40, 90), f2);
    }

    [Fact]
    public void AbcdKeys()
    {
        string t = "";
        Assert.Equal(TextMath.KeyResult.Edited, TextMath.ApplyKey(ref t, 'a'));
        TextMath.ApplyKey(ref t, 'b');
        TextMath.ApplyKey(ref t, '\b');
        Assert.Equal("a", t);
        Assert.Equal(TextMath.KeyResult.Commit, TextMath.ApplyKey(ref t, '\r'));
        string longText = new('x', 254);
        Assert.Equal(TextMath.KeyResult.Ignored, TextMath.ApplyKey(ref longText, 'y'));
        Assert.Equal(TextMath.KeyResult.Ignored, TextMath.ApplyKey(ref longText, '\r')); // > 253: Enter ignored too
        Assert.Equal(TextMath.KeyResult.Edited, TextMath.ApplyKey(ref longText, '\b'));
        Assert.Equal(253, longText.Length);
    }

    [Fact]
    public void TextSounds_AfterDialog()
    {
        Assert.Equal(53, ModeSounds.TextSound(0x4B));
        Assert.Equal(52, ModeSounds.TextSound(0x4C));
        Assert.Equal(54, ModeSounds.TextSound(0x4D));
        Assert.Equal(55, ModeSounds.TextSound(0x4E));
        Assert.Null(ModeSounds.Up(0x4B, true, true));
        Assert.Null(ModeSounds.Up(0x3F, true, false)); // box actions need both
        Assert.Equal(48, ModeSounds.Up(0x3F, true, true));
        Assert.Equal(58, ModeSounds.Up(0x48, false, false));
    }
}

public class EraserMathTests
{
    [Fact]
    public void NewSlate_LineVariants()
    {
        var v1 = EraserMath.NewSlateSteps(1);
        Assert.Equal(558 / 2 + 2, v1.Count);
        Assert.Equal(new[] { new Box(0, 0, 0, 386), new Box(558, 0, 558, 386) }, v1[0]);
        Assert.Equal(new Box(280, 0, 280, 386), v1[^1][0]);
        var v2 = EraserMath.NewSlateSteps(2);
        Assert.Equal(new[] { new Box(279, 0, 279, 386), new Box(279, 0, 279, 386) }, v2[0]);
        Assert.Equal(new Box(-1, 0, -1, 386), v2[^1][0]);
        var v3 = EraserMath.NewSlateSteps(3);
        Assert.Equal(386 / 2 + 2, v3.Count);
        Assert.Equal(new Box(0, 386, 558, 386), v3[0][1]);
        var v4 = EraserMath.NewSlateSteps(4);
        Assert.Equal(new Box(0, 193 - 5, 558, 193 - 5), v4[5][0]);
    }

    [Fact]
    public void NewSlate_GridVariants()
    {
        var v5 = EraserMath.NewSlateSteps(5);
        Assert.Equal(97, v5.Count); // ceil(386/4) rows
        Assert.Equal(140, v5[0].Length); // ceil(558/4) columns
        Assert.Equal(new Box(0, 0, 4, 4), v5[0][0]);
        Assert.Equal(new Box(0, 384, 4, 388), EraserMath.NewSlateSteps(6)[0][0]);
        var v7 = EraserMath.NewSlateSteps(7);
        Assert.Equal(140, v7.Count);
        Assert.Equal(new Box(0, 4, 4, 8), v7[0][1]);
        Assert.Equal(new Box(556, 0, 560, 4), EraserMath.NewSlateSteps(8)[0][0]);
        Assert.Equal(65, EraserMath.NewSlateSound(1));
        Assert.Equal(72, EraserMath.NewSlateSound(8));
    }

    [Fact]
    public void Insets_And_Shade()
    {
        Assert.Equal((22, 15, 514, 356), EraserMath.ShrinkRect(1));
        Assert.Equal(new Box(27, 19, 531, 367), EraserMath.CrumpleInset(1));
        Assert.Equal(new Box(251, 173, 307, 213), EraserMath.CrumpleInset(9));
        Assert.Equal(371, EraserMath.WindowShadeEdge(1));
        Assert.Equal(86, EraserMath.WindowShadeEdge(20));
        Assert.Equal(38, EraserMath.WindowShadeBand());
        Assert.Equal(new[] { 0, 0, 1, 1, 2, 2, 3, 3 }, Enumerable.Range(1, 8).Select(EraserMath.ExplodeFrame));
        Assert.Equal(new Box(92, 92, 108, 108), EraserMath.ExplodeRing(2, 100, 100));
        Assert.Equal(new Box(90, 40, 110, 60), EraserMath.SquareEraser(100, 50));
    }

    [Fact]
    public void CrumplePoints_XThenY()
    {
        var inset = EraserMath.CrumpleInset(3);
        var pts = EraserMath.CrumplePoints(inset, new DelphiRandom(5));
        var r = new DelphiRandom(5);
        Assert.Equal(51, pts.Length);
        foreach (var p in pts)
        {
            Assert.Equal(inset.L + r.Next(inset.Width), p.X);
            Assert.Equal(inset.T + r.Next(inset.Height), p.Y);
        }
    }
}

public class ChangeItMathTests
{
    static uint[] Gradient(int w, int h)
    {
        var px = new uint[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = 0xFF000000u | (uint)i;
        return px;
    }

    [Fact]
    public void MosaicSquares_SamplesCentres()
    {
        int w = 20, h = 20;
        var px = Gradient(w, h);
        ChangeItMath.MosaicSquares(px, w, h, new Box(0, 0, 12, 9));
        // centres (3,3), (8,3), (3,8), (8,8); x from 3 while < 12 -> 3, 8; y 3, 8.
        Assert.Equal(new[] { (3, 3), (8, 3), (3, 8), (8, 8) }, ChangeItMath.SquareCentres(new Box(0, 0, 12, 9)));
        uint c = 0xFF000000u | (uint)(3 * w + 3);
        for (int y = 1; y <= 5; y++) for (int x = 1; x <= 5; x++) Assert.Equal(c, px[y * w + x]);
        Assert.Equal(0xFF000000u | (uint)(8 * w + 8), px[10 * w + 10]);
        Assert.Equal(0xFF000000u, px[0]); // outside every block
        Assert.Equal(0xFF000000u | (uint)(11 * w + 11), px[11 * w + 11]);
    }

    [Fact]
    public void MosaicCircles_RandomPerRow()
    {
        int w = 40, h = 20;
        var px = Gradient(w, h);
        var box = new Box(0, 0, 30, 10);
        ChangeItMath.MosaicCircles(px, w, h, box, new DelphiRandom(11));
        var r = new DelphiRandom(11);
        int r0 = r.Next(6) + 1, r1 = r.Next(6) + 1; // rows y = 3, 8
        // first circle of row 0 at (3,3): the pixel r0 above takes the centre colour; the next circle starts at x = 3 + r0 + 1
        Assert.Equal(0xFF000000u | (uint)(3 * w + 4 + r0), px[3 * w + 4 + r0]);
        Assert.Equal(0xFF000000u | (uint)(3 * w + 3), px[(3 - r0) * w + 3]);
        Assert.True(r1 >= 1);
    }

    [Fact]
    public void EyeDropper_GlobalReplace()
    {
        int w = 4, h = 2;
        var px = new uint[] { 0xFF111111, 0xFF222222, 0xFF111111, 0xFF333333, 0xFF111111, 0xFF222222, 0xFF444444, 0xFF111111 };
        Assert.Equal(4, ChangeItMath.ReplaceColor(px, w, h, 0, 0, 0xABCDEF));
        Assert.Equal(0xFFABCDEF, px[7]);
        Assert.Equal(0xFF222222, px[1]);
        Assert.Equal(0, ChangeItMath.ReplaceColor(px, w, h, 0, 0, 0xABCDEF)); // same as current: nothing
        Assert.Equal(0, ChangeItMath.ReplaceColor(px, w, h, 9, 0, 0x123456));
    }

    [Fact]
    public void Clamps_And_Frame()
    {
        Assert.Equal(new Box(1, 1, 555, 383), ChangeItMath.ReverseClamp(new Box(0, -5, 600, 500)));
        Assert.Equal(new Box(10, 20, 557, 385), ChangeItMath.SelectionClamp(new Box(10, 20, 900, 900)));
        var f = ChangeItMath.SelectionFrame(new Box(10, 10, 20, 20));
        Assert.Equal(5, f.Length);
        Assert.Equal(new Box(6, 6, 24, 24), f[4]);
        Assert.Equal(new Box(5, 2, 9, 7), ChangeItMath.Normalize(9, 7, 5, 2));
    }
}

public class ContentMathTests
{
    [Fact]
    public void Pager_DownAndWrap()
    {
        var p = new ThumbPager(10); // last = 9
        Assert.Equal((0, 3, "1 - 4 of 10"), (p.First, p.PageLast, p.Label));
        p.Down(); Assert.Equal((4, 7), (p.First, p.PageLast));
        p.Down(); Assert.Equal((6, 9), (p.First, p.PageLast)); // last < pageLast+4 -> final page
        p.Down(); Assert.Equal((0, 3), (p.First, p.PageLast)); // pageLast == last -> wrap
    }

    [Fact]
    public void Pager_UpAndWrap()
    {
        var p = new ThumbPager(10);
        p.Up(); Assert.Equal((6, 9), (p.First, p.PageLast)); // first-3 < 0 with > 4 pictures -> last page
        p.Up(); Assert.Equal((3, 6), (p.First, p.PageLast));
        p.Up(); Assert.Equal((0, 3), (p.First, p.PageLast));
        Assert.Equal(new[] { 0, 1, 2, 3 }, Enumerable.Range(0, 4).Select(p.EntryAt));
        p.Down(); p.Up(); Assert.Equal((1, 4), (p.First, p.PageLast)); // up 3 / down 4 asymmetry
        Assert.Equal(4, p.EntryAt(3));
    }

    [Fact]
    public void Pager_SmallLists()
    {
        var p = new ThumbPager(3);
        Assert.Equal((0, 2, "1 - 3 of 3"), (p.First, p.PageLast, p.Label));
        Assert.Equal(-1, p.EntryAt(3));
        p.Up(); Assert.Equal((0, 2), (p.First, p.PageLast));
        p.Down(); Assert.Equal((0, 2), (p.First, p.PageLast));
        var five = new ThumbPager(5);
        five.Down(); Assert.Equal((1, 4), (five.First, five.PageLast));
        five.Up(); Assert.Equal((1, 4), (five.First, five.PageLast)); // first-3 < 0, 5 > 4 -> last page
    }

    [Fact]
    public void SaveName_Format_And_RandomOrder()
    {
        var a = SaveNames.Generate("UP", new DelphiRandom(123));
        Assert.True(SaveNames.IsValid(a, "UP"), a);
        Assert.Equal(12, a.Length);
        var r = new DelphiRandom(123);
        var expect = "UP";
        for (int i = 0; i < 6; i++) expect += r.Next(10) < 5 ? (char)('A' + r.Next(26)) : (char)('0' + r.Next(10));
        Assert.Equal(expect + ".BMP", a);
        Assert.True(SaveNames.IsValid(SaveNames.Generate("YP", new DelphiRandom(9)), "YP"));
        Assert.False(SaveNames.IsValid("UPabc123.BMP", "UP"));
    }

    [Fact]
    public void MagicColor_KeyTable()
    {
        Assert.Equal(48, MagicColorKeys.Table.Length);
        for (int k = 0; k < 48; k++)
        {
            var (key, idx) = MagicColorKeys.Table[k];
            Assert.Equal(ColorTable.Get(160 + k), key); // keys are table entries 160..207
            Assert.Equal(10 * (k / 3) + 3 + 2 * (k % 3), idx); // shades 3, 5, 7
        }
        Assert.Equal(0xCA5FFF, MagicColorKeys.FillFor(0xFFFFFE)); // 202,95,255
        Assert.Equal(0x78D200, MagicColorKeys.FillFor(0xFFFBFB)); // 120,210,0
        Assert.Null(MagicColorKeys.FillFor(0xFFFCFA)); // in the table but never tested
        Assert.Null(MagicColorKeys.FillFor(0xFFFFFF));
    }

    [Fact]
    public void Jukebox_Transitions()
    {
        var s = new StudioState { MusicPlaying = true };
        var j = new JukeboxState(s);
        j.ToggleMusic();
        Assert.Equal(MusicTransition.Stop, j.Close());
        var j2 = new JukeboxState(s); // music off at open
        j2.ToggleMusic();
        Assert.Equal(MusicTransition.Restart, j2.Close());
        Assert.False(s.MusicPlaying);
        var j3 = new JukeboxState(s);
        j3.ToggleMusic(); j3.ToggleMusic(); j3.ToggleSounds(); j3.ToggleAudioHelp();
        Assert.Equal(MusicTransition.None, j3.Close());
        Assert.False(s.SoundOn);
        Assert.False(s.AudioHelpOn);
    }

    [Fact]
    public void PictureKinds_Tables()
    {
        Assert.Equal((82, 1, 450), PictureKinds.Placement(PictureKind.ClipArtColor));
        Assert.Equal((82, 94, 330), PictureKinds.Placement(PictureKind.Open));
        Assert.Equal(new[] { (1, 1), (202, 1), (1, 202), (202, 202) }, PictureKinds.CellOrigins(PictureKind.ClipArtBlackWhite));
        Assert.Equal(3, PictureKinds.Orientation(@"C:\x\MV05.BMP"));
        Assert.Equal(4, PictureKinds.Orientation("FH01.BMP"));
        Assert.Equal((50, 100), PictureKinds.StampHalf(3));
        Assert.Equal((200, 100), PictureKinds.ClipArtCell(4));
        Assert.Equal(new[] { 100, 100, 102, 104, 103, 105 },
            new[] { PictureKind.ClipArtColor, PictureKind.ClipArtBlackWhite, PictureKind.Background, PictureKind.Open, PictureKind.Trash, PictureKind.ColoringBook }.Select(PictureKinds.ShowSound));
        Assert.Equal(new[] { "a.bmp", "B.bmp", "c.bmp" }, PictureKinds.Order(new[] { "c.bmp", "B.bmp", "a.bmp" }));
        Assert.Equal(250, PictureKinds.Order(Enumerable.Range(0, 300).Select(i => $"{i:D3}.bmp")).Count);
    }

    [Fact]
    public void Bmp24_RoundTrip()
    {
        var px = new uint[] { 0xFF102030, 0xFF405060, 0xFF708090, 0xFFFFFFFF, 0xFF000000, 0xFFABCDEF };
        var bmp = BmpWriter.Write24(px, 3, 2);
        Assert.Equal(54 + 2 * 12, bmp.Length);
        using var s = DibSurface.FromBmp(bmp);
        Assert.Equal((3, 2), (s.Width, s.Height));
        Assert.Equal(0x102030, s.GetRgb(0, 0));
        Assert.Equal(0xABCDEF, s.GetRgb(2, 1));
    }

    [Fact]
    public void ActivityLists_Parallel()
    {
        var l = ActivityLists.From(new[] { "Cat", "", "Dog" }, new[] { "d0.bmp", "d0.bmp" }, new[] { "a1400.asl", " a1401.asl " });
        Assert.Equal(3, l.Titles.Length);
        Assert.Equal("a1401.asl", l.Picture(1));
        Assert.Equal("", l.Picture(2));
        Assert.Equal("", l.Preview(5));
    }
}

public class Phase45ToolTests : IDisposable
{
    readonly StudioState _s = new();
    readonly DibSurface _canvas = new(556, 384, 0xFFFFFF);
    readonly DibSurface _backing = new(556, 384, 0xFFFFFF);
    readonly List<int> _sounds = new();
    readonly ToolDispatcher _d;
    readonly ToolContext _c;

    public Phase45ToolTests()
    {
        _c = new ToolContext
        {
            State = _s, Random = new DelphiRandom(42), Canvas = new CanvasPainter(_canvas), PlaySound = _sounds.Add,
            Backing = _backing,
        };
        _d = new ToolDispatcher(_c);
    }

    public void Dispose()
    {
        _canvas.Dispose();
        _backing.Dispose();
    }

    void Drag(int x0, int y0, int x1, int y1)
    {
        _d.Down(x0, y0);
        _d.Move(x1, y1);
        _d.Up(x1, y1);
    }

    [Fact]
    public void Mirror_IsExact()
    {
        using var src = new DibSurface(5, 3, 0);
        for (int x = 0; x < 5; x++) for (int y = 0; y < 3; y++) src.SetRgb(x, y, x * 16 + y);
        using var dst = new DibSurface(5, 3, 0xFFFFFF);
        ChangeItTools.Mirror(dst.Hdc, 0, 0, 5, 3, src.Hdc, 0, 0, horizontal: true);
        for (int x = 0; x < 5; x++) for (int y = 0; y < 3; y++) Assert.Equal((4 - x) * 16 + y, dst.GetRgb(x, y));
        ChangeItTools.Mirror(dst.Hdc, 0, 0, 5, 3, src.Hdc, 0, 0, horizontal: false);
        for (int x = 0; x < 5; x++) for (int y = 0; y < 3; y++) Assert.Equal(x * 16 + 2 - y, dst.GetRgb(x, y));
    }

    [Fact]
    public void BoxBand_LeavesNoTrace()
    {
        _s.Mode = 0x43; // invert, but release with an unchanged Y: no action
        Drag(10, 10, 50, 10);
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(30, 10));
        Assert.Empty(_sounds);
        Drag(10, 10, 50, 40);
        Assert.Equal(0x000000, _canvas.GetRgb(30, 20)); // inverted white
        Assert.Equal(0x000000, _canvas.GetRgb(10, 10));
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(50, 40)); // box is l..r-1
        Assert.Equal(new[] { 51 }, _sounds);
    }

    [Fact]
    public void EyeDropper_ReplacesClickedColour()
    {
        _s.ColorIndex = 9; _s.ShadeIndex = 5; // red
        _canvas.SetRgb(5, 5, 0x00FF00);
        _canvas.SetRgb(300, 200, 0x00FF00);
        _s.Mode = 0x44;
        _d.Down(5, 5); _d.Up(5, 5);
        Assert.Equal(0xFF0000, _canvas.GetRgb(300, 200));
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(6, 6));
    }

    [Fact]
    public void Cut_SelectGrabDrop()
    {
        Gdi.FillRect(_canvas.Hdc, 20, 20, 40, 40, 0x00FF00);
        _s.Mode = 0x45;
        Drag(20, 20, 40, 40);
        Assert.Equal(0x48, _s.Mode);
        Assert.True(_s.FloatingSelection);
        Assert.Equal(0x0000FF, _canvas.GetRgb(16, 30)); // blue frame grows outward to 4 px
        Assert.Equal(0x00FF00, _backing.GetRgb(30, 30)); // snapshot without the frame
        _d.Down(25, 25);                                  // inside: grab, cut whitens the box
        _d.Move(125, 25);
        _d.Up(125, 25);
        Assert.Equal(0x45, _s.Mode);
        Assert.False(_s.FloatingSelection);
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(30, 30));
        Assert.Equal(0x00FF00, _canvas.GetRgb(130, 30));
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(16, 30));   // frame gone
        Assert.Equal(58, _sounds[^1]);
    }

    [Fact]
    public void Copy_PressOutside_GoesIdle()
    {
        _s.Mode = 0x46;
        Drag(20, 20, 40, 40);
        _d.Down(200, 200);
        Assert.Equal(0, _s.Mode);
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(16, 30)); // restored from the snapshot
    }

    [Fact]
    public void FaceSquares_FromDispatcher()
    {
        _canvas.SetRgb(13, 13, 0x123456);
        _s.Mode = 0x3F;
        Drag(10, 10, 30, 30);
        Assert.Equal(0x123456, _canvas.GetRgb(11, 15));
        Assert.Equal(48, _sounds[^1]);
    }

    [Fact]
    public void StyledText_DrawsInBox_AndPlaysAfterDialog()
    {
        _s.Mode = 0x4B;
        _s.TextBuffer = "Hi";
        bool asked = false;
        var c = new ToolContext
        {
            State = _s, Random = new DelphiRandom(1), Canvas = new CanvasPainter(_canvas), PlaySound = _sounds.Add,
            Backing = _backing, AskText = done => { asked = true; done(); },
        };
        var d = new ToolDispatcher(c);
        d.Down(50, 50); d.Move(250, 120); d.Up(250, 120);
        Assert.True(asked);
        Assert.Equal(new[] { 53 }, _sounds);
        _canvas.Flush();
        int painted = 0;
        for (int y = 0; y < 384; y++) for (int x = 0; x < 556; x++) if (_canvas.GetRgb(x, y) != 0xFFFFFF) painted++;
        Assert.True(painted > 100);
        Assert.True(_s.RotationAngle > 10); // plain-text quirk: the fitted size lands in the angle
    }

    [Fact]
    public void Abcd_TypingAndCommit()
    {
        _s.Mode = 0x51;
        _s.TextFontName = "Arial";
        _s.Thickness = 3;
        _d.Down(100, 100); _d.Up(100, 100);
        Assert.True(_s.TypingActive);
        Assert.Equal((100, 84), (_s.TextOriginX, _s.TextOriginY));
        Assert.Equal(_s.CurrentColor, _canvas.GetRgb(101, 90)); // caret 3 x 27
        TextTools.Abcd.Key(_c, 'W');
        Assert.Equal("W", _s.TextBuffer);
        TextTools.Abcd.Key(_c, '\r');
        Assert.False(_s.TypingActive);
        // caret gone after the commit: column x = origin + width has no caret rectangle at its bottom
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(99, 110));
    }

    [Fact]
    public void NewSlate_UsesRandom8_AndEndsWhite()
    {
        _canvas.SetRgb(10, 10, 0);
        var r = new DelphiRandom(42);
        int v = r.Next(8) + 1;
        var a = EraserTools.NewSlate(_c);
        while (a.Step()) { }
        Assert.Equal(new[] { 64 + v }, _sounds);
        Assert.Equal(r.Seed, _c.Random.Seed);
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(10, 10));
    }

    [Fact]
    public void Explode_ModeZero_WhiteAtEnd()
    {
        _canvas.SetRgb(10, 10, 0);
        _s.Mode = 0x2F;
        _d.Down(100, 100); _d.Up(100, 100);
        Assert.Equal(0, _s.Mode);
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(10, 10));
        Assert.Equal(62, _sounds[0]);
    }

    [Fact]
    public void Crumple_Makes51RandomCallsPairsPerStep()
    {
        var a = EraserTools.Crumple(_c);
        while (a.Step()) { }
        var r = new DelphiRandom(42);
        for (int i = 1; i <= 9; i++) EraserMath.CrumplePoints(EraserMath.CrumpleInset(i), r);
        Assert.Equal(r.Seed, _c.Random.Seed);
        Assert.Equal(new[] { 61 }, _sounds);
    }

    [Fact]
    public void SquareEraser()
    {
        Gdi.FillRect(_canvas.Hdc, 0, 0, 100, 100, 0);
        _s.Mode = 0x39;
        _d.Down(50, 50); _d.Up(50, 50);
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(40, 40));
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(59, 59));
        Assert.Equal(0x000000, _canvas.GetRgb(60, 60));
        Assert.Equal(new[] { 3 }, _sounds);
    }

    [Fact]
    public void MagicColor_FloodsKeyColour()
    {
        Gdi.FillRect(_canvas.Hdc, 10, 10, 30, 30, 0xFFFFFE);
        _s.Mode = 0x50;
        _d.Down(15, 15);
        Assert.Equal(0xCA5FFF, _canvas.GetRgb(20, 20));
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(5, 5));
        _d.Down(5, 5); // white is not a key colour
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(5, 5));
        Assert.Equal(new[] { 5, 5 }, _sounds);
    }

    [Fact]
    public void Stamp_CancelledByRightPress()
    {
        _s.Mode = 0x33;
        _s.PictureOrientation = 4;
        using var art = new DibSurface(200, 200, 0x000000); // black image, white mask on the bottom half
        Gdi.FillRect(art.Hdc, 0, 0, 200, 100, 0xFF0000);
        Gdi.FillRect(art.Hdc, 0, 100, 200, 200, 0x000000);
        var c = new ToolContext
        {
            State = _s, Random = new DelphiRandom(1), Canvas = new CanvasPainter(_canvas), PlaySound = _sounds.Add,
            Backing = _backing, LoadPath = _ => { var s = new DibSurface(200, 200); s.CopyFrom(art); return s; },
        };
        var d = new ToolDispatcher(c);
        d.Down(200, 200); d.Up(200, 200);
        Assert.Equal(0xFF0000, _canvas.GetRgb(200, 200));
        Assert.Equal(new[] { 57 }, _sounds);
        d.Down(400, 300);
        _s.CancelPlace = true;
        d.Up(400, 300);
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(400, 300));
        Assert.Equal(0x33, _s.Mode);
        Assert.Single(_sounds);
    }
}

public class DialogFormTests
{
    [SkippableFact]
    public void JukeboxIcons_Decode()
    {
        var root = AssetStore.FindInstall();
        Skip.If(root == null, "No Crayons install on this machine");
        var exe = NeResources.Load(Path.Combine(root!, "CRAYON2.EXE"));
        var form = exe.Form("TJUKEBOX")!;
        var icon = form.Child("Image2")!.PictureIcon();
        Assert.NotNull(icon);
        Assert.Equal((32, 32), (icon!.Width, icon.Height));
        Assert.NotNull(form.Child("Image8")!.PictureBitmap());
        foreach (var f in new[] { "TTHUMBS", "TACTIVITY", "TROTATOR", "TTEXTINPT", "TFONTFORM", "TABOUTME" })
            Assert.NotNull(exe.Form(f));
    }
}

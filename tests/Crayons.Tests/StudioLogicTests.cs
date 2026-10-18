using Crayons.Core;
using Crayons.Core.Studio;

namespace Crayons.Tests;

public class HotspotTests
{
    [Theory]
    [InlineData(50, 10, 9)]   // red ball (startup colour)
    [InlineData(70, 60, 1)]   // 63..88 x 55..70 -> magenta
    [InlineData(0, 30, 8)]
    [InlineData(70, 125, 2)]
    [InlineData(35, 50, 15)]  // overlaps nothing else
    public void BallAt_UsesHalfOpenRects(int x, int y, int ball) => Assert.Equal(ball, Hotspots.BallAt(x, y));

    [Fact]
    public void BallAt_LastMatchWins_AndMissesReturnNull()
    {
        // (35,62) is inside ball 12 (32..47 x 62..77) and ball 15 (27..43 x 47..63): the later one wins.
        Assert.Equal(15, Hotspots.BallAt(35, 62));
        Assert.Equal(12, Hotspots.BallAt(35, 63));
        Assert.Null(Hotspots.BallAt(79, 0));
        // Right/bottom edges are exclusive: ball 9 is 45..59 x 3..17.
        Assert.Equal(9, Hotspots.BallAt(59, 17));
        Assert.Null(Hotspots.BallAt(59, 18));
        Assert.Equal(11, Hotspots.BallAt(60, 17));
    }

    [Theory]
    [InlineData(0, 180, 0)]   // R14: y = 180 selects a shade (Q1)
    [InlineData(0, 181, 0)]
    [InlineData(79, 196, 4)]
    [InlineData(0, 197, 5)]
    [InlineData(16, 212, 6)]
    [InlineData(79, 212, 9)]
    public void ShadeAt_FollowsInputMd(int x, int y, int shade)
    {
        Assert.True(Hotspots.IsShadeArea(x, y));
        Assert.Equal(shade, Hotspots.ShadeAt(x, y));
    }

    [Theory]
    [InlineData(0, 179)]
    [InlineData(0, 213)]
    [InlineData(80, 190)]
    public void ShadeArea_Bounds(int x, int y) => Assert.False(Hotspots.IsShadeArea(x, y));

    [Theory]
    [InlineData(100, 0)]
    [InlineData(139, 0)]
    [InlineData(140, 1)]
    [InlineData(499, 9)]
    public void OptionSlot(int x, int slot) => Assert.Equal(slot, Hotspots.OptionSlotAt(x));

    [Theory]
    [InlineData(82, 0)]
    [InlineData(126, 0)]   // localX 44
    [InlineData(127, 0)]   // localX 45 -> (45-10) div 40 = 0
    [InlineData(132, 1)]   // localX 50
    [InlineData(171, 1)]
    [InlineData(172, 2)]
    [InlineData(530, 10)]
    [InlineData(560, 11)]  // past the last Older tab
    public void TabIndexFormula(int x, int tab) => Assert.Equal(tab, Hotspots.TabIndexAt(x));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(26, 0)]
    [InlineData(27, -1)]
    [InlineData(28, 1)]
    [InlineData(53, 1)]
    [InlineData(54, -1)]
    [InlineData(55, 2)]
    [InlineData(79, 2)]
    public void Thirds_HaveDeadPixels(int x, int third) => Assert.Equal(third, Hotspots.ThirdAt(x));

    [Fact]
    public void FixedHotspots()
    {
        Assert.True(Hotspots.Exit.Contains(598, 0));
        Assert.False(Hotspots.Exit.Contains(638, 0));
        Assert.True(Hotspots.Undo.Contains(42, 156));
        Assert.False(Hotspots.Undo.Contains(43, 140));
        Assert.False(Hotspots.Palette.Contains(10, 133));
        Assert.True(Hotspots.Help.Contains(0, 479));
        Assert.True(Hotspots.CanvasClient.Contains(83, 93));
        Assert.False(Hotspots.CanvasClient.Contains(639, 200)); // right border
        Assert.True(Hotspots.CanvasOuter.Contains(639, 477));
    }
}

public class PanelTests
{
    static bool Click(StudioState s, PanelId id, int x, int y) => Panels.Get(id).Click(s, x, y);

    [Fact]
    public void Thickness_ArrowsClamp()
    {
        var s = new StudioState();
        Assert.Equal(2, s.Thickness);
        Click(s, PanelId.Thickness, 10, 260);
        Click(s, PanelId.Thickness, 10, 260);
        Assert.Equal(1, s.Thickness);
        for (int i = 0; i < 15; i++) Click(s, PanelId.Thickness, 60, 270);
        Assert.Equal(10, s.Thickness);
        Assert.False(Click(s, PanelId.Thickness, 40, 260)); // between the arrows
        Assert.Equal(10, s.Thickness);
    }

    [Theory]
    [InlineData(1, "A29")]
    [InlineData(2, "A28")]
    [InlineData(10, "A20")]
    public void Thickness_Art(int t, string file) => Assert.Equal(file, Panels.Get(PanelId.Thickness).Art(new StudioState { Thickness = t }));

    [Fact]
    public void Symmetry_ThirdsAndArt()
    {
        var s = new StudioState();
        var p = Panels.Get(PanelId.Symmetry);
        p.Click(s, 30, 300); Assert.Equal(1, s.Symmetry); Assert.Equal("A59", p.Art(s));
        p.Click(s, 60, 300); Assert.Equal(3, s.Symmetry); Assert.Equal("A60", p.Art(s));
        p.Click(s, 54, 300); Assert.Equal(3, s.Symmetry); // dead pixel
        p.Click(s, 0, 300); Assert.Equal(2, s.Symmetry); Assert.Equal("A58", p.Art(s));
        Assert.Equal(280, p.RowY);
    }

    [Fact]
    public void ColorMode_Halves()
    {
        var s = new StudioState();
        var p = Panels.Get(PanelId.ColorMode);
        Assert.Equal("A18", p.Art(s));
        p.Click(s, 39, 400); Assert.True(s.MultiColor); Assert.Equal("A17", p.Art(s));
        p.Click(s, 40, 400); Assert.False(s.MultiColor);
        Assert.Equal(new HitRect(0, 380, 80, 430), p.Outer);
    }

    [Fact]
    public void ThickThin_SetsVariantAndMode()
    {
        var s = new StudioState();
        var p = Panels.Get(PanelId.ThickThin);
        p.Click(s, 10, 240); Assert.Equal(2, s.LineShape); Assert.Equal(0x0D, s.Mode); Assert.Equal("A36", p.Art(s));
        p.Click(s, 50, 240); Assert.Equal(1, s.LineShape); Assert.Equal(0x0C, s.Mode); Assert.Equal("A37", p.Art(s));
    }

    [Fact]
    public void ArtTable_MatchesSpec83()
    {
        Assert.Equal("A79", Panels.Get(PanelId.BoxDepth).Art(new StudioState { BoxDepth = 1 }));
        Assert.Equal("A13", Panels.Get(PanelId.BrushType).Art(new StudioState { BrushType = 3 }));
        Assert.Equal("A7", Panels.Get(PanelId.Roller).Art(new StudioState { RollerDir = 4 }));
        Assert.Equal("A42", Panels.Get(PanelId.Spray).Art(new StudioState { SprayMode = 3 }));
        Assert.Equal("A8", Panels.Get(PanelId.Rug).Art(new StudioState { RugShape = 2 }));
        Assert.Equal("A30", Panels.Get(PanelId.MiniShape).Art(new StudioState { MiniShape = 3 }));
        Assert.Equal("A46", Panels.Get(PanelId.LightSource).Art(new StudioState { LightSource = 8 }));
        Assert.Equal("A50", Panels.Get(PanelId.LightSource).Art(new StudioState { LightSource = 9 }));
        Assert.Equal("A83", Panels.Get(PanelId.BoxDirection).Art(new StudioState { BoxDir = 7 }));
        Assert.Equal("A95", Panels.Get(PanelId.Shadow).Art(new StudioState { Shadow = 7 }));
        Assert.Equal("A62", Panels.Get(PanelId.O9).Art(new StudioState { O9 = 4 }));
        Assert.Equal("A65", Panels.Get(PanelId.Size).Art(new StudioState { Size = 1 }));
        Assert.Equal("A67", Panels.Get(PanelId.Flip).Art(new StudioState { Flip = 3 }));
    }

    [Fact]
    public void LightSource_MiddleRowHasNoCentre()
    {
        var s = new StudioState();
        var p = Panels.Get(PanelId.LightSource);
        p.Click(s, 10, 270); Assert.Equal(9, s.LightSource);
        p.Click(s, 70, 270); Assert.Equal(8, s.LightSource);
        Assert.False(p.Click(s, 40, 270));
        p.Click(s, 40, 300); Assert.Equal(6, s.LightSource);
    }
}

public class LevelDefinitionTests
{
    [Fact]
    public void Older_LinesRow()
    {
        var d = LevelDefinition.Older;
        var s = new StudioState();
        Assert.Equal(1, d.Option(1, 0)!.ModeOf!(s));
        Assert.Equal(new[] { PanelId.Thickness, PanelId.ColorMode, PanelId.Symmetry }, d.Option(1, 5)!.Panels);
        Assert.Equal(0x0C, d.Option(1, 7)!.ModeOf!(s));
        s.LineShape = 2;
        Assert.Equal(0x0D, d.Option(1, 7)!.ModeOf!(s));
        Assert.Equal(OptionKind.None, d.Option(1, 9)!.Kind);
        Assert.Equal(0x26, d.Option(1, 8)!.ModeOf!(s));
        d.Option(1, 3)!.Setup!(s);
        Assert.Equal(1, s.BrushType);
    }

    [Fact]
    public void Younger_LinesRow()
    {
        var d = LevelDefinition.Younger;
        var s = new StudioState { Symmetry = 3 };
        d.Option(1, 5)!.Setup!(s);
        Assert.Equal(2, s.Symmetry);
        Assert.Equal(new[] { PanelId.Thickness, PanelId.ColorMode }, d.Option(1, 5)!.Panels);
        Assert.Equal(0x26, d.Option(1, 7)!.ModeOf!(s));
        Assert.Equal(OptionKind.None, d.Option(1, 8)!.Kind);
        Assert.Equal(OptionKind.None, d.Option(1, 9)!.Kind); // R12: empty slot removes the marker
    }

    [Fact]
    public void Tabs()
    {
        Assert.Equal(11, LevelDefinition.Older.Tabs.Count);
        Assert.Equal(9, LevelDefinition.Younger.Tabs.Count);
        Assert.Equal(new[] { "ICON_1", "ICON_2", "ICON_3", "ICON_5", "ICON_12", "ICON_7", "ICON_9", "ICON_10", "ICON_11" },
            LevelDefinition.Younger.Tabs.Select(t => t.Icon));
        Assert.Equal("A914", LevelDefinition.Older.Tabs[6].Sheet);
        Assert.Equal("A950", LevelDefinition.Younger.InitialSheet);
        Assert.Equal("A939.ASL", LevelDefinition.Younger.CaptionFile);
        Assert.Equal("UP", LevelDefinition.Older.SavePrefix);
    }

    [Fact]
    public void Younger_ShapesFixValues_AndPaintFixes()
    {
        var d = LevelDefinition.Younger;
        var s = new StudioState { TO = 3, SO = 2, O8 = 2, O9 = 4, BrushType = 1, RollerDir = 4 };
        Assert.Null(d.Option(4, 0)!.Setup); // the ellipse resets nothing
        d.Option(4, 2)!.Setup!(s);
        d.Option(4, 4)!.Setup!(s);
        d.Option(4, 8)!.Setup!(s);
        d.Option(4, 9)!.Setup!(s);
        Assert.Equal((1, 1, 1, 3), (s.TO, s.SO, s.O8, s.O9));
        Assert.Null(LevelDefinition.Older.Option(5, 2)!.Setup); // Older keeps the last choice
        d.Option(2, 1)!.Setup!(s); Assert.Equal(3, s.BrushType);
        d.Option(2, 3)!.Setup!(s); Assert.Equal(3, s.RollerDir);
        Assert.False(d.DrawnPanels.Contains(PanelId.SO));
        Assert.True(d.ClickablePanels.Contains(PanelId.SO));
    }

    [Fact]
    public void PaintFill_ResetsLargePattern()
    {
        var s = new StudioState { FillPattern = 20 };
        LevelDefinition.Older.Option(2, 2)!.Setup!(s);
        Assert.Equal(1, s.FillPattern);
    }
}

public class TooltipMapTests
{
    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(1, 6, 6)]
    [InlineData(1, 7, 111)]
    [InlineData(1, 8, 7)]
    [InlineData(1, 9, 300)]
    [InlineData(2, 9, 17)]
    [InlineData(3, 8, 26)]
    [InlineData(3, 9, 300)]
    [InlineData(9, 0, 66)]
    [InlineData(11, 5, 84)]
    [InlineData(11, 6, 300)]
    public void Older_OptionIndices(int tool, int slot, int index) => Assert.Equal(index, LevelDefinition.Older.OptionTooltip(tool, slot));

    [Theory]
    [InlineData(1, 7, 7)]
    [InlineData(3, 8, 159)]
    [InlineData(3, 9, 160)]
    [InlineData(8, 0, 161)]
    [InlineData(8, 1, 162)]
    [InlineData(9, 2, 81)]
    public void Younger_OptionIndices(int tool, int slot, int index) => Assert.Equal(index, LevelDefinition.Younger.OptionTooltip(tool, slot));

    [Fact]
    public void FrameSamples()
    {
        var s = new StudioState();
        s.ShowPanel(PanelId.ColorMode);
        var older = LevelDefinition.Older.FrameTooltip;
        Assert.Equal(85, older(s, 10, 10));      // colours
        Assert.Equal(109, older(s, 20, 145));    // undo
        Assert.Equal(86, older(s, 20, 200));     // shades
        Assert.Equal(87, older(s, 20, 240));     // thickness (visible at startup)
        Assert.Equal(88, older(s, 20, 400));     // clown
        Assert.Equal(115, older(s, 60, 400));    // businessman
        Assert.Equal(89, older(s, 20, 450));     // help
        Assert.Equal(143, older(s, 610, 50));    // exit
        Assert.Equal(2, older(s, 190, 20));      // tool 1, slot 2
        s.ClearPanels();
        s.ShowPanel(PanelId.Symmetry);
        Assert.Equal(141, older(s, 30, 300));
        Assert.Equal(300, older(s, 20, 240));
        // R3: band D is not gated by the colour-mode panel (seen on Paint s0 in the original).
        Assert.Equal(88, older(s, 20, 400));
        Assert.Equal(115, older(s, 60, 400));
        s.ClearPanels();
        Assert.Equal(88, LevelDefinition.Younger.FrameTooltip(s, 20, 400));
        Assert.Equal(300, older(s, 20, 300)); // bands A/B/C stay gated
        Assert.Equal(130 + 3, LevelDefinition.Older.TabTooltipBase + Hotspots.TabIndexAt(215));
        Assert.Equal(163, LevelDefinition.Younger.TabTooltipBase + Hotspots.TabIndexAt(90));
    }
}

public class CursorRuleTests
{
    [Theory]
    [InlineData(1, 36)]
    [InlineData(0x17, 1)]
    [InlineData(0x0A, 12)]
    [InlineData(5, 37)]
    [InlineData(2, 1)]
    [InlineData(0x0B, 2)]
    [InlineData(0x13, 1)]
    [InlineData(0x0C, 14)]
    [InlineData(0x0D, 2)]
    [InlineData(0x26, 1)]
    [InlineData(0, 0)]
    [InlineData(0x44, 34)]
    [InlineData(0x48, 20)]
    public void ByMode(int mode, int index) => Assert.Equal(index, CursorRules.CursorIndex(new StudioState { Mode = mode }));

    [Fact]
    public void PanelRulesComeFirst()
    {
        var s = new StudioState { Mode = 1, BrushType = 4 };
        s.ShowPanel(PanelId.BrushType);
        Assert.Equal(8, CursorRules.CursorIndex(s));
        s.ClearPanels();
        s.ShowPanel(PanelId.Roller);
        s.RollerDir = 3;
        Assert.Equal(4, CursorRules.CursorIndex(s));
        s.RollerDir = 4;
        Assert.Equal(21, CursorRules.CursorIndex(s));
    }

    [Theory]
    [InlineData(1, 24)]
    [InlineData(2, 24)]
    [InlineData(3, 25)]
    [InlineData(9, 31)]
    [InlineData(10, 32)]
    public void SquaresByThickness(int t, int index) => Assert.Equal(index, CursorRules.CursorIndex(new StudioState { Mode = 0x3D, Thickness = t }));

    [Theory]
    [InlineData(1, "CURSOR_1")]
    [InlineData(18, "CURSOR_18")]
    [InlineData(19, "CURSOR_21")]
    [InlineData(23, "CURSOR_25")]
    [InlineData(24, "CURSOR_26")]
    [InlineData(31, "CURSOR_33")]
    [InlineData(32, "CURSOR_20")]
    [InlineData(36, "CURSOR_36")]
    [InlineData(33, null)]
    public void ResourceNames(int index, string? name) => Assert.Equal(name, CursorRules.ResourceName(index));
}

public class ModeSoundTests
{
    [Fact]
    public void LinesSounds()
    {
        Assert.Equal(new int?[] { 1, 24, 11, 15, 21, 12, 18, 13, 14 },
            new[] { 1, 0x17, 0x0A, 5, 2, 0x0B, 0x13, 0x0C, 0x0D }.Select(ModeSounds.Down));
        Assert.Null(ModeSounds.Down(0x26));
        Assert.Equal(38, ModeSounds.Up(0x26, true, false));
        Assert.Null(ModeSounds.Up(0x26, false, false));
        Assert.Null(ModeSounds.Up(0x21, true, false)); // shapes need both
        Assert.Equal(33, ModeSounds.Up(0x21, true, true));
    }
}

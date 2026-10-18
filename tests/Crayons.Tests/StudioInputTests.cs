using Crayons.Core.Studio;

namespace Crayons.Tests;

public class StudioInputTests
{
    sealed class FakeEffects : IStudioEffects
    {
        public readonly List<string> Log = new();
        public void PlaySound(int snd) => Log.Add($"snd{snd}");
        public void Snapshot() => Log.Add("snapshot");
        public void RestoreUndo() => Log.Add("undo");
        public void DrawOptionStrip(int slot) => Log.Add($"strip{slot}");
        public void RedrawShadeGrid() => Log.Add("grid");
        public void RedrawColumn() => Log.Add("tail");
        public void TabChanged(int tab, string sheet) => Log.Add($"tab{tab}:{sheet}");
        public void RunAction(string name) => Log.Add($"action:{name}");
        public void OpenHelp() => Log.Add("help");
        public void AskQuit() => Log.Add("quit?");
    }

    static (StudioInput In, StudioState S, FakeEffects Fx) Make(Level level = Level.Older)
    {
        var s = new StudioState();
        s.ShowPanel(PanelId.ColorMode); // set at first paint
        var fx = new FakeEffects();
        return (new StudioInput(LevelDefinition.For(level), s, fx), s, fx);
    }

    [Fact]
    public void OptionClick_CommonStepsThenEntry()
    {
        var (input, s, fx) = Make();
        input.FormClick(300 + 5, 20); // slot 5 = Symmetry
        Assert.Equal(new[] { "snd94", "snapshot", "strip5", "tail" }, fx.Log);
        Assert.Equal(0x0B, s.Mode);
        Assert.Equal(5, s.LastOptionSlot);
        Assert.Equal(new[] { PanelId.ColorMode, PanelId.Symmetry, PanelId.Thickness }.OrderBy(p => p), s.VisiblePanels.OrderBy(p => p));
    }

    [Fact]
    public void NoneEntry_RemovesMarker_KeepsMode_ClearsPanels()
    {
        var (input, s, fx) = Make();
        input.OptionClick(8); // straight line
        fx.Log.Clear();
        input.OptionClick(9); // none
        Assert.Equal(new[] { "snd94", "snapshot", "strip9", "strip12" }, fx.Log);
        Assert.Equal(0x26, s.Mode);
        Assert.Equal(12, s.LastOptionSlot);
        Assert.Empty(s.VisiblePanels);
    }

    [Fact]
    public void TabClick_ModeZero_PanelsKept_NoSnapshot()
    {
        var (input, s, fx) = Make();
        Assert.True(input.TabClick(2));
        Assert.Equal(new[] { "snd93", "tab2:A902", "strip12" }, fx.Log);
        Assert.Equal(0, s.Mode);
        Assert.Equal(3, s.CurrentTool);
        Assert.Contains(PanelId.Thickness, s.VisiblePanels);
        Assert.Contains(PanelId.ColorMode, s.VisiblePanels);
        Assert.False(input.TabClick(11)); // past the last Older tab
        Assert.False(LevelDefinition.Younger.Tabs.Count > 9);
    }

    [Fact]
    public void Palette_AlwaysResetsShade_EvenOnMiss()
    {
        var (input, s, fx) = Make();
        s.ShadeIndex = 2;
        input.FormClick(79, 0); // no ball there
        Assert.Equal(new[] { "snd98", "snapshot", "grid" }, fx.Log);
        Assert.Equal(9, s.ColorIndex);
        Assert.Equal(5, s.ShadeIndex);
        input.FormClick(65, 20); // ball 11 (green)
        Assert.Equal(11, s.ColorIndex);
        Assert.Equal(0x00C300, s.CurrentColor);
    }

    [Fact]
    public void Shade_Undo_Help_Exit()
    {
        var (input, s, fx) = Make();
        input.FormClick(40, 185);
        Assert.Equal(2, s.ShadeIndex);
        Assert.Equal(new[] { "snd95", "grid", "snapshot" }, fx.Log);
        fx.Log.Clear();
        input.FormClick(10, 140);
        Assert.Equal(new[] { "snd97", "undo" }, fx.Log);
        fx.Log.Clear();
        input.FormClick(10, 450);
        Assert.Equal(new[] { "snapshot", "help", "tail" }, fx.Log);
        fx.Log.Clear();
        input.FormClick(600, 10);
        Assert.Equal(new[] { "quit?" }, fx.Log);
    }

    [Fact]
    public void Panels_SoundAndValue_OnlyWhenVisible()
    {
        var (input, s, fx) = Make();
        input.FormClick(10, 400); // colour mode, left = clown
        Assert.True(s.MultiColor);
        Assert.Equal(new[] { "snd96", "tail" }, fx.Log);
        fx.Log.Clear();
        input.FormClick(10, 300); // band B: no panel visible
        Assert.Equal(new[] { "tail" }, fx.Log);
        fx.Log.Clear();
        input.FormClick(10, 240); // band A but outside the thickness outer rect (y 256..276)
        Assert.Equal(new[] { "tail" }, fx.Log);
        input.FormClick(60, 260);
        Assert.Equal(3, s.Thickness);
    }

    [Fact]
    public void Symmetry_DeadPixelPlaysSoundButKeepsValue()
    {
        var (input, s, fx) = Make();
        input.OptionClick(5);
        fx.Log.Clear();
        input.FormClick(27, 300);
        Assert.Equal(new[] { "snd96", "tail" }, fx.Log);
        Assert.Equal(2, s.Symmetry);
        input.FormClick(60, 300);
        Assert.Equal(3, s.Symmetry);
    }

    [Fact]
    public void ThickThinOption_FollowsVariant()
    {
        var (input, s, _) = Make();
        input.OptionClick(7);
        Assert.Equal(0x0C, s.Mode);
        Assert.Equal(new[] { PanelId.ThickThin }, s.VisiblePanels);
        input.FormClick(10, 240); // thin->thick
        Assert.Equal(0x0D, s.Mode);
        input.OptionClick(0);
        input.OptionClick(7);
        Assert.Equal(0x0D, s.Mode); // the variant is remembered
    }

    [Fact]
    public void Younger_PanelsNotClickableAreIgnored()
    {
        var (input, s, fx) = Make(Level.Younger);
        s.ShowPanel(PanelId.Symmetry); // never shown in Younger, and has no click handler
        input.FormClick(60, 300);
        Assert.Equal(2, s.Symmetry);
        Assert.DoesNotContain("snd96", fx.Log);
        input.OptionClick(5);
        Assert.Equal(new[] { PanelId.ColorMode, PanelId.Thickness }.OrderBy(p => p), s.VisiblePanels.OrderBy(p => p));
    }

    [Fact]
    public void ActionEntries_AreForwarded()
    {
        var (input, s, fx) = Make();
        input.TabClick(10); // PC
        input.OptionClick(0);
        Assert.Contains("action:Save", fx.Log);
        input.OptionClick(6); // empty slot after the speaker: common steps only
        Assert.Equal(6, s.LastOptionSlot);
        Assert.Equal("strip6", fx.Log[^1]);
    }
}

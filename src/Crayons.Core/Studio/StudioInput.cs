namespace Crayons.Core.Studio;

/// <summary>Side effects the studio's click state machine asks the UI layer to perform.</summary>
public interface IStudioEffects
{
    void PlaySound(int snd);
    /// <summary>Canvas → undo buffer A.</summary>
    void Snapshot();
    /// <summary>Undo buffer A → canvas.</summary>
    void RestoreUndo();
    /// <summary>A922 strip plus the red triangle under <paramref name="slot"/> (12 = none).</summary>
    void DrawOptionStrip(int slot);
    void RedrawShadeGrid();
    /// <summary>The click tail: column, enabled panels, shade grid.</summary>
    void RedrawColumn();
    /// <summary>Load the tab's options sheet, restart the animation, redraw the toolbar.</summary>
    void TabChanged(int tab, string sheet);
    /// <summary>An immediate option action or dialog (Save, eraser effects, pictures...).</summary>
    void RunAction(string name);
    void OpenHelp();
    void AskQuit();
}

/// <summary>
/// The studio's left-click state machine (behavior/input.md §2, spec §8.3): fixed
/// hotspots, palette and shades, option clicks with the (tool, slot) table, tab clicks,
/// and the modifier panels. Coordinates are 640x480 frame pixels outside the canvas.
/// </summary>
public sealed class StudioInput
{
    readonly LevelDefinition _level;
    readonly StudioState _s;
    readonly IStudioEffects _fx;

    public StudioInput(LevelDefinition level, StudioState state, IStudioEffects effects)
    {
        _level = level;
        _s = state;
        _fx = effects;
    }

    /// <summary>A left click on the form (not on the toolbar or the canvas).</summary>
    public void FormClick(int x, int y)
    {
        if (Hotspots.Help.Contains(x, y))
        {
            _fx.Snapshot();
            _fx.OpenHelp();
            _fx.RedrawColumn();
        }
        else if (Hotspots.Exit.Contains(x, y))
            _fx.AskQuit();
        else if (Hotspots.Palette.Contains(x, y))
        {
            _fx.PlaySound(98);
            if (Hotspots.BallAt(x, y) is int ball) _s.ColorIndex = ball;
            _fx.Snapshot();
            _s.ShadeIndex = 5; // even when no ball was hit
            _fx.RedrawShadeGrid();
        }
        else if (Hotspots.Undo.Contains(x, y))
        {
            _fx.PlaySound(97);
            _fx.RestoreUndo();
        }
        else if (Hotspots.IsShadeArea(x, y))
        {
            _s.ShadeIndex = Hotspots.ShadeAt(x, y);
            _fx.PlaySound(95);
            _fx.RedrawShadeGrid();
            _fx.Snapshot();
        }
        else if (Hotspots.OptionsRow.Contains(x, y))
        {
            OptionClick(Hotspots.OptionSlotAt(x));
            _fx.RedrawColumn();
        }
        else
        {
            PanelClick(x, y);
            _fx.RedrawColumn();
        }
    }

    /// <summary>Visible, clickable panel hit: SND96 (unless silent), then its value change.</summary>
    public void PanelClick(int x, int y)
    {
        foreach (var panel in Panels.All)
        {
            if (!_s.IsPanelVisible(panel.Id) || !_level.ClickablePanels.Contains(panel.Id)) continue;
            if (!panel.Outer.Contains(x, y)) continue;
            if (!panel.Silent) _fx.PlaySound(96);
            panel.Click(_s, x, y);
            if (panel.OpensDialog) _fx.RunAction(panel.Id == PanelId.Font ? "FontDialog" : "FillStylesDialog");
            return;
        }
    }

    /// <summary>Option click (input.md §2.4): SND94, snapshot, clear panels, marker, apply the entry.</summary>
    public void OptionClick(int slot)
    {
        _fx.PlaySound(94);
        _fx.Snapshot();
        _s.ClearPanels();
        _s.MagicColorActive = false;
        SetOptionStrip(slot);
        var entry = _level.Option(_s.CurrentTool, slot);
        if (entry == null) return;
        switch (entry.Kind)
        {
            case OptionKind.None:
                SetOptionStrip(12); // marker removed, Mode keeps its old value
                break;
            case OptionKind.Mode:
                entry.Setup?.Invoke(_s);
                _s.Mode = entry.ModeOf!(_s);
                foreach (var p in entry.Panels ?? Array.Empty<PanelId>()) _s.ShowPanel(p);
                break;
            case OptionKind.Action:
                entry.Setup?.Invoke(_s);
                _fx.RunAction(entry.ActionName!);
                break;
        }
    }

    /// <summary>Tab click: SND93, Mode = 0, the tab's sheet, tool = tab + 1, marker removed. Panels stay.</summary>
    public bool TabClick(int tab)
    {
        if (tab < 0 || tab >= _level.Tabs.Count) return false; // no tab under the pointer
        if (tab == _s.CurrentTool - 1) return false;           // the selected tab: nothing happens (R13, Q5)
        _fx.PlaySound(93);
        _s.Mode = 0;
        _s.CurrentTool = tab + 1;
        _fx.TabChanged(tab, _level.Tabs[tab].Sheet);
        SetOptionStrip(12);
        return true;
    }

    public void SetOptionStrip(int slot)
    {
        _s.LastOptionSlot = slot;
        _fx.DrawOptionStrip(slot);
    }
}

namespace Crayons.Core.Studio;

/// <summary>The two age levels. The original shipped them as two executables.</summary>
public enum Level { Younger, Older }

/// <summary>
/// The studio's global state (behavior/input.md §3), with descriptive names and the
/// documented FormCreate defaults. Shared by both levels.
/// </summary>
public sealed class StudioState
{
    // ---- Tool and colour -------------------------------------------------
    /// <summary>Main tool = toolbar tab + 1 (1-based).</summary>
    public int CurrentTool { get; set; } = 1;
    /// <summary>Sub-tool / drawing mode (0 = nothing).</summary>
    public int Mode { get; set; } = 1;
    /// <summary>Slot under the option marker; 12 = no marker.</summary>
    public int LastOptionSlot { get; set; } = 0;
    public int ColorIndex { get; set; } = 9;
    public int ShadeIndex { get; set; } = 5;
    public int Thickness { get; set; } = 2;
    public int SavedThickness { get; set; } = 2;
    /// <summary>Clown ("Color Crazy") mode: random segment colours.</summary>
    public bool MultiColor { get; set; }

    // ---- Modifier values (defaults per input.md §3) ----------------------
    public int BrushType { get; set; } = 1;
    public int RollerDir { get; set; } = 4;
    public int RugShape { get; set; } = 2;
    public int Bubble { get; set; } = 1;
    public int MiniShape { get; set; } = 3;
    public int MiniShapeAlt { get; set; } = 1;
    /// <summary>Thick/Thin variant: 1 = thick-to-thin (mode 0x0C), 2 = thin-to-thick (0x0D).</summary>
    public int LineShape { get; set; } = 1;
    public int Grass { get; set; } = 2;
    public int SprayMode { get; set; } = 1;
    public int FillPattern { get; set; } = 1;
    public int LightSource { get; set; } = 2;
    public int O8 { get; set; } = 1;
    public int SO { get; set; } = 1;
    public int TO { get; set; } = 1;
    /// <summary>Symmetry: 2 = left-right, 1 = top-bottom, 3 = four-way.</summary>
    public int Symmetry { get; set; } = 2;
    public int O9 { get; set; } = 4;
    public int BoxDir { get; set; } = 2;
    public int BoxDepth { get; set; } = 3;
    public int Size { get; set; } = 1;
    public int MoveShape { get; set; } = 1;
    public int Flip { get; set; } = 4;
    public int Shadow { get; set; } = 2;
    public int LoadMode { get; set; } = 1;
    public int RotationAngle { get; set; } = 45;

    // ---- Text ---------------------------------------------------------------
    public string TextFontName { get; set; } = "";
    public string ShadowTextFontName { get; set; } = "Arial";
    /// <summary>The shared 256-byte text buffer (TextInpt, Rotator and ABCD typing).</summary>
    public string TextBuffer { get; set; } = "";
    /// <summary>ABCD text origin (click X, click Y - 16).</summary>
    public int TextOriginX { get; set; }
    public int TextOriginY { get; set; }

    // ---- Pictures --------------------------------------------------------------
    /// <summary>Path picked in the thumbnail browser / activity form.</summary>
    public string PicturePath { get; set; } = "";
    /// <summary>Clip-art orientation of the picked file: 4 = H (200x100), 3 = V (100x200).</summary>
    public int PictureOrientation { get; set; } = 4;
    /// <summary>A right-button press during a clip-art stamp drag cancels the drop.</summary>
    public bool CancelPlace { get; set; }

    // ---- Audio (never persisted; on at every launch) ----------------------
    public bool SoundOn { get; set; } = true;
    public bool MusicOn { get; set; } = true;
    public bool AudioHelpOn { get; set; } = true;
    public bool MusicPlaying { get; set; }
    /// <summary>Last started track 1..20 (0 = none yet).</summary>
    public int MusicTrack { get; set; }

    // ---- Tooltip -------------------------------------------------------------
    public bool TooltipVisible { get; set; }
    public bool TooltipDismissable { get; set; }

    // ---- Canvas interaction --------------------------------------------------
    public bool StrokeActive { get; set; }
    /// <summary>Previous point: press point on mouse-down, then the current point after every move.</summary>
    public int PrevX { get; set; }
    public int PrevY { get; set; }
    /// <summary>Anchor: set only on mouse-down.</summary>
    public int AnchorX { get; set; }
    public int AnchorY { get; set; }
    public bool FloatingSelection { get; set; }
    public bool TypingActive { get; set; }
    public bool MagicColorActive { get; set; }

    // ---- Modifier panel visibility flags ------------------------------------
    readonly HashSet<PanelId> _panels = new() { PanelId.Thickness };

    public bool IsPanelVisible(PanelId p) => _panels.Contains(p);
    public void ShowPanel(PanelId p) => _panels.Add(p);
    public void HidePanel(PanelId p) => _panels.Remove(p);
    public void ClearPanels() => _panels.Clear();
    public IReadOnlyCollection<PanelId> VisiblePanels => _panels;

    // ---- Derived colours -----------------------------------------------------
    public int CurrentColorIndex => ColorIndex * 10 + ShadeIndex;
    /// <summary>Current colour as 0xRRGGBB.</summary>
    public int CurrentColor => ColorTable.Get(CurrentColorIndex);

    /// <summary>The "tri-colour": s' = max(shade,2); T0 = [c*10+s'], T1 = s'-1, T2 = s'-2.</summary>
    public (int T0, int T1, int T2) Trio
    {
        get
        {
            int b = ColorIndex * 10 + Math.Max(ShadeIndex, 2);
            return (ColorTable.Get(b), ColorTable.Get(b - 1), ColorTable.Get(b - 2));
        }
    }
}

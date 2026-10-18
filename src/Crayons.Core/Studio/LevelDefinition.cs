namespace Crayons.Core.Studio;

/// <summary>A toolbar tab: its icon group in the EXE, its options sheet, and its tooltip index.</summary>
public sealed record TabDef(string Icon, string Sheet, int TooltipIndex, string Name);

public enum OptionKind
{
    /// <summary>Sets a mode (plus optional panels and setup).</summary>
    Mode,
    /// <summary>"none": removes the marker (518d(12)); Mode keeps its old value.</summary>
    None,
    /// <summary>An immediate action or dialog (named), e.g. Save, an eraser effect.</summary>
    Action,
}

/// <summary>One cell of the (tool, slot) option table of input.md §2.4 / §9.</summary>
public sealed record OptionEntry(
    OptionKind Kind,
    Func<StudioState, int>? ModeOf = null,
    PanelId[]? Panels = null,
    Action<StudioState>? Setup = null,
    string? ActionName = null);

/// <summary>
/// Everything that differs between the Younger and Older studios (spec §3.4):
/// tabs, option table, panels, caption file, tooltip maps, save prefix.
/// </summary>
public sealed class LevelDefinition
{
    public required Level Level { get; init; }
    public required string Exe { get; init; }
    public required string CaptionFile { get; init; }
    public required string SavePrefix { get; init; }
    public required IReadOnlyList<TabDef> Tabs { get; init; }
    /// <summary>Option table indexed [tool-1][slot]; null = no entry.</summary>
    public required OptionEntry?[][] Options { get; init; }
    /// <summary>Panels that react to clicks.</summary>
    public required IReadOnlySet<PanelId> ClickablePanels { get; init; }
    /// <summary>Panels that are drawn (first paint and the click tail).</summary>
    public required IReadOnlySet<PanelId> DrawnPanels { get; init; }
    /// <summary>Caption index for an option slot of a tool, or 300 (nothing).</summary>
    public required Func<int, int, int> OptionTooltip { get; init; }
    /// <summary>Caption index for a right-click on the frame (not the toolbar), or 300.</summary>
    public required Func<StudioState, int, int, int> FrameTooltip { get; init; }
    public required int TabTooltipBase { get; init; }

    public string InitialSheet => Tabs[0].Sheet;

    public OptionEntry? Option(int tool, int slot)
    {
        if (tool < 1 || tool > Options.Length || slot < 0) return null;
        var row = Options[tool - 1];
        return slot < row.Length ? row[slot] : null;
    }

    public static LevelDefinition For(Level level) => level == Level.Older ? Older : Younger;

    // ---- Table helpers ---------------------------------------------------------
    const PanelId Th = PanelId.Thickness, CM = PanelId.ColorMode, BT = PanelId.BrushType, RD = PanelId.Roller,
        PP = PanelId.PaintPatterns, SD = PanelId.Spray, RS = PanelId.Rug, MS = PanelId.MiniShape, BB = PanelId.Bubble,
        GR = PanelId.Grass, LS = PanelId.LightSource, BD = PanelId.BoxDepth, BX = PanelId.BoxDirection,
        SY = PanelId.Symmetry, TT = PanelId.ThickThin, TO = PanelId.TO, SO = PanelId.SO, O8 = PanelId.O8,
        O9 = PanelId.O9, FN = PanelId.Font, SH = PanelId.Shadow, MV = PanelId.MoveShape, SZ = PanelId.Size,
        FL = PanelId.Flip;

    static OptionEntry M(int mode, params PanelId[] panels) => new(OptionKind.Mode, _ => mode, panels);
    static OptionEntry M(int mode, Action<StudioState> setup, params PanelId[] panels) => new(OptionKind.Mode, _ => mode, panels, setup);
    static OptionEntry ModeBy(Func<StudioState, int> mode, params PanelId[] panels) => new(OptionKind.Mode, mode, panels);
    static OptionEntry Act(string name) => new(OptionKind.Action, ActionName: name);
    static readonly OptionEntry None = new(OptionKind.None);

    static void ResetFillPattern(StudioState s) { if (s.FillPattern > 15) s.FillPattern = 1; }

    static OptionEntry Font(string name) => M(0x51, s => s.TextFontName = name, Th);

    static OptionEntry?[] Row(params OptionEntry?[] e)
    {
        var r = new OptionEntry?[10];
        Array.Copy(e, r, e.Length);
        return r;
    }

    // Shared rows ---------------------------------------------------------------
    static OptionEntry?[] WackyRow(OptionEntry? s8, OptionEntry? s9) => Row(
        M(7, CM, MS), M(6, Th, CM), M(3, Th), ModeBy(s => s.Bubble == 1 ? 0x1B : 0x1A, CM, BB),
        M(0x11, Th, CM), M(0x32, Th), M(0x30), M(0x38, Th, CM), s8, s9);

    /// <summary>
    /// The Shapes row. Younger resets the variant when its shape is selected (tools-shapes.md:
    /// triangle TO = 1, trapezoid SO = 1, star O8 = 1, parallelogram O9 = 3); Older keeps the last choice.
    /// </summary>
    static OptionEntry?[] ShapesRow(bool younger) => Row(
        M(0x21, Th, PP),
        M(0x1F, Th, PP),
        younger ? M(0x22, s => s.TO = 1, Th, PP, TO) : M(0x22, Th, PP, TO),
        M(0x20, Th, PP),
        younger ? M(0x23, s => s.SO = 1, Th, PP, SO) : M(0x23, Th, PP, SO),
        M(0x29, Th, PP),
        M(0x28, Th, PP),
        M(0x25, Th, PP),
        younger ? M(0x24, s => s.O8 = 1, Th, PP, O8) : M(0x24, Th, PP, O8),
        younger ? M(0x36, s => s.O9 = 3, Th, PP, O9) : M(0x36, Th, PP, O9));

    static OptionEntry?[] AbcdRow() => Row(Font("Creeper"), Font("Walters"), Font("Crayon"), Font("Mexacali"), Font("Treehouse"),
        None, None, None, None, None);

    static OptionEntry?[] EraserRow() => Row(Act("NewSlate"), M(0x2F), Act("ShrinkAway"), Act("Crumple"), Act("WindowShade"), M(0x39),
        None, None, None, None);

    static OptionEntry?[] DrawingsRow() => Row(Act("PicturesColor"), Act("PicturesBlackWhite"), Act("Backgrounds"));

    static OptionEntry?[] PcRow() => Row(Act("Save"), Act("Open"), Act("Print"), Act("Trash"), Act("About"), Act("Jukebox"));

    static int Slots(int slot, int count, int baseIndex) => slot >= 0 && slot < count ? baseIndex + slot : 300;

    // ---- Older (CRAYON2) ------------------------------------------------------------
    public static readonly LevelDefinition Older = new()
    {
        Level = Level.Older,
        Exe = "CRAYON2.EXE",
        CaptionFile = "A938.ASL",
        SavePrefix = "UP",
        TabTooltipBase = 130,
        Tabs = new TabDef[]
        {
            new("ICON_1", "A900", 130, "Lines"), new("ICON_2", "A901", 131, "Paint"), new("ICON_3", "A902", 132, "Wacky"),
            new("ICON_4", "A903", 133, "3D Shapes"), new("ICON_5", "A904", 134, "Shapes"), new("ICON_6", "A905", 135, "Text with Shadows"),
            new("ICON_12", "A914", 136, "Text Again"), new("ICON_7", "A906", 137, "Eraser"), new("ICON_8", "A907", 138, "Change It"),
            new("ICON_9", "A908", 139, "Drawings"), new("ICON_11", "A910", 140, "Computer Activities"),
        },
        Options = new[]
        {
            Row(M(1, Th, CM), M(0x17, Th, CM), M(0x0A), M(5, s => s.BrushType = 1), M(2, Th, CM), M(0x0B, Th, CM, SY),
                M(0x13, Th), ModeBy(s => s.LineShape == 1 ? 0x0C : 0x0D, TT), M(0x26, Th), None),
            Row(M(0x16, Th), M(0x15, Th, BT), M(0x3A, ResetFillPattern, PP), M(0x19, Th, RD), M(8, Th, BT), M(0x10, Th),
                M(9, Th, SD), M(0x2A, RS), M(0x0F, Th, BT), M(0x0E, Th, BT)),
            WackyRow(M(4, GR), None),
            Row(M(0x2C, LS), M(0x2D, LS), M(0x2E, LS), M(0x2B), M(0x3B, Th), M(0x3C, BD, BX), M(0x3D, Th), M(0x3E, Th), None, None),
            ShapesRow(younger: false),
            Row(M(0x4B, FN), M(0x4D, FN, SH), M(0x4E, FN), M(0x4F, FN, Th), M(0x4C, FN, SH), None, None, None, None, None),
            AbcdRow(),
            EraserRow(),
            Row(M(0x45), M(0x46), ModeBy(s => s.MoveShape == 1 ? 0x3F : 0x40, MV), Act("Reduce"), Act("MoveAround"),
                M(0x41, SZ), M(0x42, FL), M(0x43), M(0x44), M(0x47, FL)),
            DrawingsRow(),
            PcRow(),
        },
        ClickablePanels = Enum.GetValues<PanelId>().Where(p => p != PanelId.Unused).ToHashSet(),
        DrawnPanels = Enum.GetValues<PanelId>().ToHashSet(),
        OptionTooltip = (tool, slot) => tool switch
        {
            1 => slot switch { >= 0 and <= 6 => slot, 7 => 111, 8 => 7, _ => 300 },
            2 => Slots(slot, 10, 8),
            3 => Slots(slot, 9, 18),
            4 => Slots(slot, 8, 27),
            5 => Slots(slot, 10, 35),
            6 => Slots(slot, 5, 45),
            7 => Slots(slot, 10, 50),
            8 => Slots(slot, 6, 60),
            9 => Slots(slot, 10, 66),
            10 => Slots(slot, 3, 76),
            11 => Slots(slot, 6, 79),
            _ => 300,
        },
        FrameTooltip = OlderFrameTooltip,
    };

    // ---- Younger (CRAYON1) ----------------------------------------------------------
    public static readonly LevelDefinition Younger = new()
    {
        Level = Level.Younger,
        Exe = "CRAYON1.EXE",
        CaptionFile = "A939.ASL",
        SavePrefix = "YP",
        TabTooltipBase = 163,
        Tabs = new TabDef[]
        {
            new("ICON_1", "A950", 163, "Lines"), new("ICON_2", "A951", 164, "Paint"), new("ICON_3", "A952", 165, "Wacky"),
            new("ICON_5", "A904", 166, "Shapes"), new("ICON_12", "A914", 167, "Letters"), new("ICON_7", "A953", 168, "Eraser"),
            new("ICON_9", "A908", 169, "Drawings"), new("ICON_10", "A954", 170, "Fun Projects"), new("ICON_11", "A910", 171, "Computer Activities"),
        },
        Options = new[]
        {
            Row(M(1, Th, CM), M(0x17, Th, CM), M(0x0A), M(5, s => s.BrushType = 1), M(2, Th, CM),
                M(0x0B, s => s.Symmetry = 2, Th, CM), M(0x13, Th), M(0x26, Th), None, None), // s8/s9 empty: no marker (R12)
            Row(M(0x16, Th), M(0x15, s => s.BrushType = 3, Th, BT), M(0x3A, ResetFillPattern, PP), M(0x19, s => s.RollerDir = 3, Th, RD),
                M(8, s => s.BrushType = 3, Th, BT), M(0x10, Th), M(9, Th, SD), M(0x2A, RS), None, None), // s8/s9 empty (R12)
            WackyRow(Act("BarnZoom"), Act("Shear")),
            ShapesRow(younger: true),
            AbcdRow(),
            EraserRow(),
            DrawingsRow(),
            Row(Act("ColoringBook"), Act("MagicColor")),
            PcRow(),
        },
        ClickablePanels = new HashSet<PanelId> { Th, PP, BB, RS, FL, MV, SD, SZ, SO, MS, CM },
        DrawnPanels = new HashSet<PanelId> { Th, SZ, MV, RS, FL, CM, BB, MS, SD, PP },
        OptionTooltip = (tool, slot) => tool switch
        {
            1 => Slots(slot, 8, 0),
            2 => Slots(slot, 8, 8),
            3 => slot switch { 8 => 159, 9 => 160, _ => Slots(slot, 8, 18) },
            4 => Slots(slot, 10, 35),
            5 => Slots(slot, 10, 50),
            6 => Slots(slot, 6, 60),
            7 => Slots(slot, 3, 76),
            8 => Slots(slot, 2, 161),
            9 => Slots(slot, 6, 79),
            _ => 300,
        },
        FrameTooltip = YoungerFrameTooltip,
    };

    // ---- Frame tooltip maps (input.md §5 and §9.5); last match wins -------------------
    static int Half(int x, int left, int right) => Hotspots.HalfAt(x) == 0 ? left : right;

    static int CommonLeftColumn(int idx, int x, int y)
    {
        if (new HitRect(0, 0, 80, 180).Contains(x, y)) idx = 85;
        if (new HitRect(0, 132, 44, 156).Contains(x, y)) idx = 109;
        if (new HitRect(0, 180, 80, 215).Contains(x, y)) idx = 86;
        return idx;
    }

    static int OlderFrameTooltip(StudioState s, int x, int y)
    {
        int idx = 300;
        if (Hotspots.OptionsRow.Contains(x, y)) idx = Older.OptionTooltip(s.CurrentTool, Hotspots.OptionSlotAt(x));
        idx = CommonLeftColumn(idx, x, y);
        bool V(PanelId p) => s.IsPanelVisible(p);
        int third = Hotspots.ThirdAt(x);
        if (Hotspots.Band('A').Contains(x, y))
        {
            if (V(Th)) idx = 87;
            if (V(BD)) idx = 100;
            if (V(MS)) idx = 96;
            if (V(GR) && third >= 0) idx = third switch { 0 => 98, 1 => 123, _ => 124 };
            if (V(BB)) idx = Half(x, 97, 119);
            if (V(TT)) idx = Half(x, 111, 122);
            if (V(RS)) idx = Half(x, 95, 117);
            if (V(MV)) idx = 110;
            if (V(FL)) idx = s.Mode == 0x42 ? Half(x, 112, 120) : Half(x, 113, 121);
        }
        if (Hotspots.Band('B').Contains(x, y))
        {
            if (V(FN)) idx = 106;
            if (V(BX)) idx = 101;
            if (V(SZ)) idx = Half(x, 108, 118);
            if (V(SY) && third >= 0) idx = third switch { 0 => 91, 1 => 141, _ => 142 };
            if (V(SD)) idx = Half(x, 94, 116);
            if (V(PP)) idx = 92;
            if (V(BT)) idx = 90;
            if (V(RD)) idx = 93;
        }
        if (Hotspots.Band('C').Contains(x, y))
        {
            if (V(O8)) idx = 104;
            if (V(O9)) idx = 105;
            if (V(SO)) idx = 103;
            if (V(TO)) idx = 102;
            if (V(SH)) idx = s.Mode == 0x4C ? 114 : 107;
        }
        if (V(LS) && new HitRect(0, 230, 80, 310).Contains(x, y)) idx = 99;
        // Band D is not gated by the colour-mode panel (spec §9.1 R3: it shows even on Paint s0).
        if (Hotspots.Band('D').Contains(x, y)) idx = Half(x, 88, 115);
        if (Hotspots.Help.Contains(x, y)) idx = 89;
        if (new HitRect(Hotspots.CanvasW + 40, 0, Hotspots.CanvasW + 80, 60).Contains(x, y)) idx = 143;
        return idx;
    }

    static int YoungerFrameTooltip(StudioState s, int x, int y)
    {
        int idx = 300;
        if (Hotspots.OptionsRow.Contains(x, y)) idx = Younger.OptionTooltip(s.CurrentTool, Hotspots.OptionSlotAt(x));
        idx = CommonLeftColumn(idx, x, y);
        bool V(PanelId p) => s.IsPanelVisible(p);
        if (Hotspots.Band('A').Contains(x, y))
        {
            if (V(Th)) idx = 87;
            if (V(MS)) idx = 96;
            if (V(BB)) idx = Half(x, 97, 119);
            if (V(RS)) idx = Half(x, 95, 117);
        }
        if (Hotspots.Band('B').Contains(x, y))
        {
            if (V(SD)) idx = Half(x, 94, 116);
            if (V(PP)) idx = 92;
        }
        // Band D is not gated by the colour-mode panel (spec §9.1 R3: it shows even on Paint s0).
        if (Hotspots.Band('D').Contains(x, y)) idx = Half(x, 88, 115);
        if (Hotspots.Help.Contains(x, y)) idx = 89;
        if (new HitRect(Hotspots.CanvasW + 40, 0, Hotspots.CanvasW + 80, 60).Contains(x, y)) idx = 143;
        return idx;
    }
}

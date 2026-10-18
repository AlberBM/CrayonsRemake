namespace Crayons.Core.Studio;

/// <summary>Canvas cursor selection (behavior/input.md §7, spec §8.4).</summary>
public static class CursorRules
{
    /// <summary>
    /// Screen.Cursors index for the canvas, first matching rule wins; 0 = the default arrow.
    /// </summary>
    public static int CursorIndex(StudioState s)
    {
        if (s.IsPanelVisible(PanelId.BrushType))
            return s.BrushType switch { 1 => 3, 2 => 5, 3 => 10, _ => 8 };
        if (s.IsPanelVisible(PanelId.Roller))
            return s.RollerDir == 3 ? 4 : 21;
        if (s.Mode == 0x39) return 30;
        if (s.Mode is 0x3D or 0x3E) return s.Thickness <= 2 ? 24 : 22 + Math.Min(s.Thickness, 10);
        return s.Mode switch
        {
            0 => 0,
            1 => 36,
            4 => 7,
            5 => 37,
            6 => 18,
            7 => 19,
            9 => 16,
            0x0A => 12,
            0x0B or 0x0D => 2,
            0x0C => 14,
            0x1A or 0x1B => 17,
            0x27 => 13,
            0x2A => 23,
            0x2F => 22,
            0x33 or (>= 0x48 and <= 0x4A) => 20,
            0x3A or 0x50 => 11,
            (>= 0x3F and <= 0x43) or (>= 0x45 and <= 0x47) => 15,
            0x44 => 34,
            0x4F or 0x51 => 9,
            _ => 1,
        };
    }

    /// <summary>
    /// Cursor index → GROUP_CURSOR name, per the FormCreate registration list:
    /// 1-18 → CURSOR_1-18, 19-31 → CURSOR_21-33, 32 → CURSOR_20, 34/36/37 → same number.
    /// Null for unregistered indices.
    /// </summary>
    public static string? ResourceName(int index) => index switch
    {
        >= 1 and <= 18 => $"CURSOR_{index}",
        >= 19 and <= 31 => $"CURSOR_{index + 2}",
        32 => "CURSOR_20",
        34 or 36 or 37 => $"CURSOR_{index}",
        _ => null,
    };
}

/// <summary>Sound effects per canvas mouse-down / mouse-up mode (input.md §8).</summary>
public static class ModeSounds
{
    public static int? Down(int mode) => mode switch
    {
        1 => 1, 2 => 21, 3 => 22, 4 => 4, 5 => 15, 6 => 7, 7 => 8, 8 => 9, 9 => 10,
        0x0A => 11, 0x0B => 12, 0x0C => 13, 0x0D => 14, 0x0E => 19, 0x0F => 20, 0x10 => 23,
        0x13 => 18, 0x15 => 16, 0x16 => 2, 0x17 => 24, 0x18 => 25, 0x19 => 26, 0x1A => 27, 0x1B => 28,
        0x38 => 6, 0x39 => 3, 0x3A => 5, 0x3D => 29, 0x3E => 30, 0x50 => 5,
        _ => null,
    };

    /// <summary>
    /// Mouse-up sound. Shapes (Shapes and 3D tabs, including the parallelogram commit 0x37 and
    /// the cube) need both X and Y to differ from the anchor; modes 0x26 and 0x3B need only one.
    /// Modes 0x27 and 0x33 have extra conditions handled by their tools.
    /// </summary>
    public static int? Up(int mode, bool xChanged, bool yChanged)
    {
        int? snd = mode switch
        {
            0x1F => 31, 0x20 => 32, 0x21 => 33, 0x22 => 34, 0x23 => 35, 0x24 => 36, 0x25 => 37, 0x26 => 38,
            0x28 => 41, 0x29 => 42, 0x2B => 43, 0x2C => 44, 0x2D => 47, 0x2E => 45, 0x37 => 56, 0x3B => 39,
            0x3C => 46, 0x3F => 48, 0x40 => 49, 0x42 => 50, 0x43 => 51, 0x48 => 58, 0x49 => 59, 0x4A => 60,
            // Text 0x4B-0x4E (53/52/54/55) play after the TextInpt dialog closes: see TextSound.
            _ => null,
        };
        if (snd == null) return null;
        // Shape routines only render on release when X and Y both changed (tools-shapes.md "Mouse protocol");
        // the Change It box actions (Face, Reverse, Invert) follow the same rule (tools-text-edit.md §0).
        bool shape = mode is >= 0x1F and <= 0x25 or 0x28 or 0x29 or (>= 0x2B and <= 0x2E) or 0x37 or 0x3C
            or 0x3F or 0x40 or 0x42 or 0x43;
        if (shape && !(xChanged && yChanged)) return null;
        if (mode is 0x26 or 0x3B && !(xChanged || yChanged)) return null;
        return snd;
    }

    /// <summary>Older Text sounds, played after the TextInpt dialog: 4B 53, 4C 52, 4D 54, 4E 55.</summary>
    public static int? TextSound(int mode) => mode switch { 0x4B => 53, 0x4C => 52, 0x4D => 54, 0x4E => 55, _ => null };
}

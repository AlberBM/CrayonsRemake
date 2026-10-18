using System.Text;

namespace Crayons.Core.Content;

/// <summary>The thumbnail browser kinds (content-files.md §3; PictureDialogKind).</summary>
public enum PictureKind
{
    ColoringBook = 1,
    Background = 4,
    Open = 5,
    ClipArtColor = 6,
    ClipArtBlackWhite = 7,
    Trash = 8,
}

/// <summary>Per-kind constants of the Tthumbs form (FormShow).</summary>
public static class PictureKinds
{
    public static int ShowSound(PictureKind k) => k switch
    {
        PictureKind.ColoringBook => 105,
        PictureKind.Background => 102,
        PictureKind.Open => 104,
        PictureKind.Trash => 103,
        _ => 100,
    };

    public static string Caption(PictureKind k) => k switch
    {
        PictureKind.ColoringBook => "Coloring Book Pictures",
        PictureKind.Background => "Background Images",
        PictureKind.Open => "My Pictures -- Choose Picture to Open",
        PictureKind.ClipArtColor => "Clip Art Pictures -- Color",
        PictureKind.ClipArtBlackWhite => "Clip Art Pictures -- Black / White",
        _ => "Choose a Picture to Delete",
    };

    public static bool IsClipArt(PictureKind k) => k is PictureKind.ClipArtColor or PictureKind.ClipArtBlackWhite;

    /// <summary>Form placement: Left 82; Top 94 / Height 330, or Top 1 / Height 450 for clip art.</summary>
    public static (int Left, int Top, int Height) Placement(PictureKind k) => IsClipArt(k) ? (82, 1, 450) : (82, 94, 330);

    /// <summary>Cell origins: (1,1), (202,1), (1,152|202), (202,152|202).</summary>
    public static (int X, int Y)[] CellOrigins(PictureKind k)
    {
        int y2 = IsClipArt(k) ? 202 : 152;
        return new[] { (1, 1), (202, 1), (1, y2), (202, y2) };
    }

    /// <summary>
    /// Clip-art orientation from the second letter of the file name: H → 4 (200x100), V → 3 (100x200).
    /// </summary>
    public static int Orientation(string fileName)
    {
        var name = Path.GetFileName(fileName);
        return name.Length > 1 && char.ToUpperInvariant(name[1]) == 'V' ? 3 : 4;
    }

    /// <summary>Clip-art cell size: H (4) = 200x100, V (3) = 100x200.</summary>
    public static (int W, int H) ClipArtCell(int orientation) => orientation == 3 ? (100, 200) : (200, 100);

    /// <summary>Stamp half-sizes: H = (100, 50), V = (50, 100).</summary>
    public static (int HalfW, int HalfH) StampHalf(int orientation) => orientation == 3 ? (50, 100) : (100, 50);

    public const int MaxEntries = 250;

    /// <summary>The browser list: case-insensitive alphabetical by file name (spec §8.8), capped at 250.</summary>
    public static List<string> Order(IEnumerable<string> paths) =>
        paths.OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
             .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
             .Take(MaxEntries).ToList();
}

/// <summary>
/// The thumbnail paging state (FUN_1020_072e/1a23/1aba): four cells per page, up moves by 3
/// and down by 4 with the documented wrap rules.
/// </summary>
public sealed class ThumbPager
{
    public int Count { get; }
    public int Last => Count - 1;
    public int First { get; private set; }
    public int PageLast { get; private set; }

    public ThumbPager(int count)
    {
        Count = count;
        FirstPage();
    }

    void FirstPage()
    {
        First = 0;
        PageLast = Math.Min(3, Last);
    }

    /// <summary>Up (sound 106).</summary>
    public void Up()
    {
        if (First - 3 < 0)
        {
            if (Count <= 4) FirstPage();
            else { PageLast = Last; First = Last - 3; }
        }
        else
        {
            First -= 3;
            PageLast -= 3;
        }
    }

    /// <summary>Down (sound 107).</summary>
    public void Down()
    {
        if (Last < PageLast + 4 && PageLast < Last)
        {
            PageLast = Last;
            First = Math.Max(0, Last - 3);
        }
        else if (PageLast == Last) FirstPage();
        else
        {
            First += 4;
            PageLast += 4;
        }
    }

    /// <summary>The entry shown in cell 0..3, or -1 for a blank cell (entries first..first+3 that exist).</summary>
    public int EntryAt(int cell) => First + cell < Count && First + cell >= 0 ? First + cell : -1;

    /// <summary>Label1: "first+1 - pageLast+1 of count".</summary>
    public string Label => $"{First + 1} - {PageLast + 1} of {Count}";
}

/// <summary>The Save picture name generator (FUN_1008_6734).</summary>
public static class SaveNames
{
    /// <summary>
    /// prefix + 6 characters + ".BMP": per character, Random(10) &lt; 5 gives 'A' + Random(26),
    /// otherwise '0' + Random(10).
    /// </summary>
    public static string Generate(string prefix, DelphiRandom random)
    {
        var sb = new StringBuilder(prefix);
        for (int i = 0; i < 6; i++)
            sb.Append(random.Next(10) < 5 ? (char)('A' + random.Next(26)) : (char)('0' + random.Next(10)));
        return sb.Append(".BMP").ToString();
    }

    /// <summary>True for names the generator can produce for this prefix.</summary>
    public static bool IsValid(string name, string prefix) =>
        System.Text.RegularExpressions.Regex.IsMatch(name, "^" + prefix + "[A-Z0-9]{6}\\.BMP$");
}

/// <summary>Magic Color reveal (FUN_1028_79f2): 48 near-white key colours, each flood-filled with a table colour.</summary>
public static class MagicColorKeys
{
    /// <summary>(key RGB, colour-table index) in test order (content-files.md §6).</summary>
    public static readonly (int Key, int Index)[] Table =
    {
        (0xFFFFFE, 3), (0xFEFFFE, 5), (0xFFFFFD, 7), (0xFEFFFD, 13), (0xFFFFFC, 15), (0xFEFFFC, 17),
        (0xFFFFFB, 23), (0xFEFFFB, 25), (0xFFFFFA, 27), (0xFEFFFA, 33), (0xFDFFFE, 35), (0xFCFFFE, 37),
        (0xFDFFFD, 43), (0xFCFFFD, 45), (0xFDFFFC, 47), (0xFCFFFC, 53), (0xFDFFFB, 55), (0xFCFFFB, 57),
        (0xFDFFFA, 63), (0xFCFFFA, 65), (0xFBFFFE, 67), (0xFAFFFE, 73), (0xFBFFFD, 75), (0xFAFFFD, 77),
        (0xFBFFFC, 83), (0xFAFFFC, 85), (0xFBFFFB, 87), (0xFAFFFB, 93), (0xFBFFFA, 95), (0xFAFFFA, 97),
        (0xFFFEFE, 103), (0xFFFDFE, 105), (0xFFFEFD, 107), (0xFFFDFD, 113), (0xFFFEFC, 115), (0xFFFDFC, 117),
        (0xFFFEFB, 123), (0xFFFDFB, 125), (0xFFFEFA, 127), (0xFFFDFA, 133), (0xFFFCFE, 135), (0xFFFBFE, 137),
        (0xFFFCFD, 143), (0xFFFBFD, 145), (0xFFFCFC, 147), (0xFFFBFC, 153), (0xFFFCFB, 155), (0xFFFBFB, 157),
    };

    /// <summary>The fill colour (0xRRGGBB) for a clicked pixel, or null when it is not a key colour.</summary>
    public static int? FillFor(int rgb)
    {
        foreach (var (key, idx) in Table)
            if (key == rgb) return ColorTable.Get(idx);
        return null;
    }
}

/// <summary>What the Jukebox's close does to the music (shell.md §4).</summary>
public enum MusicTransition { None, Stop, Restart }

/// <summary>The Tjukebox toggles and their close-time music transition.</summary>
public sealed class JukeboxState
{
    readonly Studio.StudioState _s;
    readonly bool _musicAtOpen;

    public JukeboxState(Studio.StudioState state)
    {
        _s = state;
        _musicAtOpen = state.MusicOn;
    }

    public void ToggleSounds() => _s.SoundOn = !_s.SoundOn;
    public void ToggleMusic() => _s.MusicOn = !_s.MusicOn;
    public void ToggleAudioHelp() => _s.AudioHelpOn = !_s.AudioHelpOn;

    /// <summary>On close: on → off stops MIDI; off → on clears MusicPlaying and plays the next track.</summary>
    public MusicTransition Close()
    {
        if (_musicAtOpen && !_s.MusicOn) return MusicTransition.Stop;
        if (!_musicAtOpen && _s.MusicOn)
        {
            _s.MusicPlaying = false;
            return MusicTransition.Restart;
        }
        return MusicTransition.None;
    }
}

/// <summary>The Tactivity form's three parallel lists (titles, preview files, picture files).</summary>
public sealed record ActivityLists(string[] Titles, string[] Previews, string[] Pictures)
{
    /// <summary>Lines of the three list files; blank lines become empty items.</summary>
    public static ActivityLists From(string[] titles, string[] previews, string[] pictures) =>
        new(TrimEnd(titles), TrimEnd(previews), TrimEnd(pictures));

    // A trailing newline does not make an extra item (TStrings.LoadFromFile).
    static string[] TrimEnd(string[] lines) => lines.Select(l => l.TrimEnd('\r', '\n')).ToArray();

    public string Preview(int i) => i >= 0 && i < Previews.Length ? Previews[i].Trim() : "";
    public string Picture(int i) => i >= 0 && i < Pictures.Length ? Pictures[i].Trim() : "";
}

/// <summary>Writes 24-bit bottom-up BMP files.</summary>
public static class BmpWriter
{
    /// <summary>A BMP file image of <paramref name="width"/> x <paramref name="height"/> pixels given as 0x??RRGGBB, top-down.</summary>
    public static byte[] Write24(ReadOnlySpan<uint> pixels, int width, int height)
    {
        int stride = (width * 3 + 3) & ~3;
        int imageSize = stride * height;
        var b = new byte[54 + imageSize];
        b[0] = (byte)'B'; b[1] = (byte)'M';
        BitConverter.TryWriteBytes(b.AsSpan(2), 54 + imageSize);
        BitConverter.TryWriteBytes(b.AsSpan(10), 54);
        BitConverter.TryWriteBytes(b.AsSpan(14), 40);
        BitConverter.TryWriteBytes(b.AsSpan(18), width);
        BitConverter.TryWriteBytes(b.AsSpan(22), height);
        BitConverter.TryWriteBytes(b.AsSpan(26), (short)1);
        BitConverter.TryWriteBytes(b.AsSpan(28), (short)24);
        BitConverter.TryWriteBytes(b.AsSpan(34), imageSize);
        BitConverter.TryWriteBytes(b.AsSpan(38), 3780); // 96 dpi
        BitConverter.TryWriteBytes(b.AsSpan(42), 3780);
        for (int y = 0; y < height; y++)
        {
            int row = 54 + (height - 1 - y) * stride;
            for (int x = 0; x < width; x++)
            {
                uint p = pixels[y * width + x];
                b[row + x * 3] = (byte)p;
                b[row + x * 3 + 1] = (byte)(p >> 8);
                b[row + x * 3 + 2] = (byte)(p >> 16);
            }
        }
        return b;
    }
}

namespace Crayons.Core.Assets;

/// <summary>
/// Reads the original Crayons Color &amp; Paint Studio data files from the user's own
/// install. Nothing from the original product is bundled with this program; every
/// image, sound and song is loaded from <see cref="Root"/> at runtime.
/// </summary>
public sealed class AssetStore
{
    /// <summary>Files whose presence identifies a full Crayons install.</summary>
    static readonly string[] Markers = { "A1.ASL", "A900.ASL", Path.Combine("SND", "PC1.MID") };

    public string Root { get; }

    public AssetStore(string root)
    {
        if (!IsInstall(root))
            throw new DirectoryNotFoundException($"No Crayons install found at '{root}'.");
        Root = root;
    }

    public static bool IsInstall(string? dir) =>
        !string.IsNullOrWhiteSpace(dir) && Markers.All(m => File.Exists(Path.Combine(dir, m)));

    /// <summary>Searches the usual install locations; returns null when none is found.</summary>
    public static string? FindInstall(IEnumerable<string>? extraCandidates = null)
    {
        var candidates = new List<string>();
        if (extraCandidates != null) candidates.AddRange(extraCandidates);
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            candidates.Add(Path.Combine(drive.Name, "XCRAYON"));
            candidates.Add(Path.Combine(drive.Name, "Programs", "XCRAYON"));
            candidates.Add(Path.Combine(drive.Name, "Program Files", "Expert Software", "Crayons"));
        }
        return candidates.FirstOrDefault(IsInstall);
    }

    /// <summary>Full path of an asset given its path relative to the install (case-insensitive on Windows).</summary>
    public string PathOf(string relative) => Path.Combine(Root, relative);

    public bool Exists(string relative) => File.Exists(PathOf(relative));

    public byte[] ReadBytes(string relative) => File.ReadAllBytes(PathOf(relative));

    public string[] ReadLines(string relative) =>
        File.ReadAllLines(PathOf(relative), System.Text.Encoding.Latin1);

    /// <summary>Lists files in an install subfolder matching a DOS wildcard, sorted by numeric suffix.</summary>
    public IReadOnlyList<string> List(string folder, string pattern)
    {
        var dir = PathOf(folder);
        if (!Directory.Exists(dir)) return Array.Empty<string>();
        return Directory.GetFiles(dir, pattern)
            .Select(f => Path.Combine(folder, Path.GetFileName(f)))
            .OrderBy(NaturalKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static string NaturalKey(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s, @"\d+", m => m.Value.PadLeft(6, '0'));
}

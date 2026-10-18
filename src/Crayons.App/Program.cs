using Crayons.Core.Assets;
using Crayons.Core.Studio;

namespace Crayons.App;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => LogError(e.Exception);
        var root = FindOrAskForInstall(args.FirstOrDefault(a => !a.StartsWith("--")));
        if (root == null) return;
        var host = new GameHost(new AssetStore(root), windowed: args.Contains("--windowed"));
        // Developer shortcut: --level=younger|older skips the splash and the age picker.
        var level = args.FirstOrDefault(a => a.StartsWith("--level="))?["--level=".Length..];
        if (level != null) host.StartLevel(level.Equals("younger", StringComparison.OrdinalIgnoreCase) ? Level.Younger : Level.Older);
        Application.Run(host);
    }

    /// <summary>Appends an unexpected error to %LOCALAPPDATA%\CrayonsRemake\error.log and keeps running.</summary>
    static void LogError(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrayonsRemake");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), $"{DateTime.Now:O} {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException) { }
    }

    /// <summary>
    /// The original game files are required: they hold all the art, sounds and music.
    /// Uses the command-line path, the remembered path, a known location, or asks the user.
    /// </summary>
    static string? FindOrAskForInstall(string? argPath)
    {
        var settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrayonsRemake", "install-path.txt");
        string? remembered = File.Exists(settingsFile) ? File.ReadAllText(settingsFile).Trim() : null;
        var found = AssetStore.FindInstall(new[] { argPath, remembered }.OfType<string>());
        while (found == null)
        {
            MessageBox.Show(
                "Crayons needs the files from your original Crayons Color & Paint Studio installation " +
                "(the folder containing CRAYON1.EXE, A1.ASL and the SND, ART and ACTIVITY folders).\n\nPlease select that folder.",
                "Crayons Studio Remake", MessageBoxButtons.OK, MessageBoxIcon.Information);
            using var dlg = new FolderBrowserDialog { Description = "Select your original Crayons folder", UseDescriptionForTitle = true };
            if (dlg.ShowDialog() != DialogResult.OK) return null;
            if (AssetStore.IsInstall(dlg.SelectedPath)) found = dlg.SelectedPath;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!);
        File.WriteAllText(settingsFile, found);
        return found;
    }
}

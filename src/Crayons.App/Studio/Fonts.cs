using System.Runtime.InteropServices;
using Crayons.App.Rendering;

namespace Crayons.App.Studio;

/// <summary>
/// The original's TrueType fonts (Creeper, Walters, Crayon, Mexacali, Treehouse) loaded privately for this
/// process from the user's install (spec §8.6), and the installed-font list for the FontForm dialog.
/// </summary>
public static class Fonts
{
    static readonly object Gate = new();
    static readonly HashSet<string> Loaded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// AddFontResourceEx(FR_PRIVATE) for every *.TTF in the install folder, or in its FONTS\ subfolder when
    /// that exists. Returns the number of font files added. Nothing is installed system-wide.
    /// </summary>
    public static int LoadPrivate(string installRoot)
    {
        var sub = Path.Combine(installRoot, "FONTS");
        var dir = Directory.Exists(sub) ? sub : installRoot;
        int n = 0;
        lock (Gate)
        {
            foreach (var ttf in Directory.EnumerateFiles(dir, "*.TTF"))
            {
                if (!Loaded.Add(ttf)) continue;
                if (Gdi.AddFontResourceEx(ttf, Gdi.FR_PRIVATE, IntPtr.Zero) > 0) n++;
            }
        }
        return n;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct LOGFONT
    {
        public int lfHeight, lfWidth, lfEscapement, lfOrientation, lfWeight;
        public byte lfItalic, lfUnderline, lfStrikeOut, lfCharSet, lfOutPrecision, lfClipPrecision, lfQuality, lfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string lfFaceName;
    }

    delegate int EnumProc(ref LOGFONT lf, IntPtr tm, uint type, IntPtr lParam);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    static extern int EnumFontFamiliesEx(IntPtr hdc, ref LOGFONT lf, EnumProc proc, IntPtr lParam, uint flags);

    /// <summary>
    /// Every installed font family (Screen.Fonts), including the private ones, sorted case-insensitively.
    /// Vertical "@" families are left out; Win16 had none.
    /// </summary>
    public static List<string> Installed()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var probe = new DibSurface(1, 1);
        var lf = new LOGFONT { lfCharSet = 1 /* DEFAULT_CHARSET */, lfFaceName = "" };
        EnumProc proc = (ref LOGFONT f, IntPtr _, uint _, IntPtr _) =>
        {
            if (!string.IsNullOrEmpty(f.lfFaceName) && f.lfFaceName[0] != '@') names.Add(f.lfFaceName);
            return 1;
        };
        EnumFontFamiliesEx(probe.Hdc, ref lf, proc, IntPtr.Zero, 0);
        GC.KeepAlive(proc);
        return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

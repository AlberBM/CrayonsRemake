using System.Diagnostics;
using System.Drawing.Printing;
using Crayons.App.Rendering;
using Crayons.App.Tools;
using Crayons.App.Ui;
using Crayons.Core.Content;
using Crayons.Core.Studio;

namespace Crayons.App.Studio;

/// <summary>
/// The studio's dialogs and content actions: TextInpt / FontForm / Rotator for the text tools, the
/// thumbnail browser, Magic Color, Save / Open / Print / Trash, About and the Jukebox
/// (tools-text-edit.md §2, content-files.md §3-7, spec §3.6 and §3.9).
/// </summary>
public sealed partial class StudioScreen
{
    readonly string _mediaRoot;

    /// <summary>Saved pictures live here (spec §3.9), never in the original install.</summary>
    public static string SaveFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Crayons Pictures");

    FormLayout Layout(string formClass) => new(Host.Images, _level.Exe, formClass);

    string Caption(int index) => index >= 0 && index < 291 && index < _captions.Length ? _captions[index] : "";

    void OpenOverlay(IOverlay overlay)
    {
        _overlay = overlay;
        Host.RequestRedraw();
    }

    void CloseOverlay()
    {
        (_overlay as IDisposable)?.Dispose();
        _overlay = null;
        Host.RequestRedraw();
    }

    /// <summary>A BMP (or BMP-format .ASL) by full path, or null when missing or unreadable.</summary>
    static DibSurface? LoadBitmap(string path)
    {
        try { return File.Exists(path) ? DibSurface.FromBmp(File.ReadAllBytes(path)) : null; }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Picture {path}: {ex.Message}");
            return null;
        }
    }

    // ---------------------------------------------------------------- text dialogs

    /// <summary>Ttextinpt for the Older Text box styles; Screen.Cursor is the default arrow meanwhile.</summary>
    void OpenTextInput(Action done) =>
        OpenOverlay(new TextInputOverlay(Layout("TTEXTINPT"), _s, _audio.Effect, () =>
        {
            CloseOverlay();
            done();
        }));

    /// <summary>Trotator for the rotated text option.</summary>
    void OpenRotator(Action done) =>
        OpenOverlay(new RotatorOverlay(Layout("TROTATOR"), _s, _audio.Effect, () =>
        {
            CloseOverlay();
            done();
        }));

    /// <summary>The Font panel (no sound) opens TFontForm modally.</summary>
    void OpenFontForm() =>
        OpenOverlay(new FontFormOverlay(Layout("TFONTFORM"), _s, Fonts.Installed(), _audio.Effect, () =>
        {
            CloseOverlay();
            RestoreUndo(); // the canvas is repainted from A after a dialog
        }));

    // ---------------------------------------------------------------- pictures

    /// <summary>The files a picture kind browses (content-files.md §3; spec §3.9 for Open/Trash).</summary>
    List<string> PictureFiles(PictureKind kind)
    {
        IEnumerable<string> Glob(string dir, string mask) =>
            Directory.Exists(dir) ? Directory.EnumerateFiles(dir, mask) : Enumerable.Empty<string>();
        string art = Path.Combine(_mediaRoot, "ART");
        var files = kind switch
        {
            PictureKind.ClipArtColor => Glob(art, "F*.bmp"),
            PictureKind.ClipArtBlackWhite => Glob(art, "M*.bmp"),
            PictureKind.Background => Glob(art, "level*.bmp"),
            PictureKind.ColoringBook => Glob(art, "cb*.bmp"),
            _ => Glob(SaveFolder, _level.SavePrefix + "*.bmp")
                .Concat(Glob(Host.Assets.PathOf("USER"), _level.SavePrefix + "*.bmp")),
        };
        return PictureKinds.Order(files);
    }

    /// <summary>
    /// The pre-check ("There are no pictures to see.") and the Tthumbs browser. After the pick the canvas
    /// is repainted from A and the kind's action runs; a cancel leaves Mode 0 for the Drawings kinds.
    /// </summary>
    void OpenPictures(PictureKind kind)
    {
        var files = PictureFiles(kind);
        if (files.Count == 0)
        {
            ShowBox("Important!", "There are no pictures to see.", MessageBoxOverlay.Icon.Information, new[] { "OK" }, _ => AfterPicture(kind, null));
            return;
        }
        _audio.Effect(PictureKinds.ShowSound(kind));
        var blank = Host.Images.TryFile("A920.BMP");
        OpenOverlay(new ThumbsOverlay(Layout("TTHUMBS"), kind, files, LoadBitmap, blank, _audio.Effect, Caption, _audio.Voice, index =>
        {
            CloseOverlay();
            RestoreUndo();
            AfterPicture(kind, index >= 0 ? files[index] : null);
        }));
    }

    void AfterPicture(PictureKind kind, string? path)
    {
        switch (kind)
        {
            case PictureKind.ClipArtColor or PictureKind.ClipArtBlackWhite:
                if (path == null) { _s.Mode = 0; break; }
                _s.PicturePath = path;
                _s.PictureOrientation = PictureKinds.Orientation(path);
                _s.Mode = 0x33; // stamp
                break;
            case PictureKind.Background:
                _s.Mode = 0; // backgrounds never enter stamp mode (spec §8.8)
                if (path != null) StretchToCanvas(path);
                break;
            case PictureKind.Open:
                if (path != null) StretchToCanvas(path);
                break;
            case PictureKind.ColoringBook:
                if (path == null) { _s.Mode = 0; break; }
                StretchToCanvas(path);
                SelectPaintFill(0x3A);
                break;
            case PictureKind.Trash:
                if (path != null) Trash(path);
                break;
        }
        Host.RequestRedraw();
    }

    /// <summary>StretchBlt a picture to (0, 0, W, H) of the canvas, then snapshot (FUN_1008_6978).</summary>
    void StretchToCanvas(string path)
    {
        using var pic = LoadBitmap(path);
        if (pic == null) return;
        Gdi.StretchBlt(_canvas.Hdc, 0, 0, Hotspots.CanvasW, Hotspots.CanvasH, pic.Hdc, 0, 0, pic.Width, pic.Height, Gdi.SRCCOPY);
        Snapshot();
    }

    /// <summary>
    /// Coloring Book / Magic Color pick (spec §8.8): the Paint tab as a tab click, the Fill option (slot 2)
    /// as an option click, then the mode override.
    /// </summary>
    void SelectPaintFill(int mode)
    {
        _input.TabClick(1);
        _input.OptionClick(2);
        RedrawColumn();
        _s.Mode = mode;
    }

    /// <summary>Trash: delete a saved picture at once (no confirmation); install files are read-only.</summary>
    void Trash(string path)
    {
        bool ours = Path.GetFullPath(path).StartsWith(Path.GetFullPath(SaveFolder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        try
        {
            if (!ours) throw new UnauthorizedAccessException("Install files are read-only");
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Trash {path}: {ex.Message}");
            ShowBox("There is a problem...", "This picture can't be deleted.", MessageBoxOverlay.Icon.Warning, new[] { "OK" }, _ => { });
        }
    }

    // ---------------------------------------------------------------- Magic Color

    /// <summary>Tactivity with Z102/Z202/Z302; OK whitens the canvas, blits the A14xx picture at (0,0), snapshots and selects Paint/0x50.</summary>
    void OpenMagicColor()
    {
        string dir = Host.Assets.PathOf("ACTIVITY");
        string[] Lines(string name)
        {
            var p = Path.Combine(dir, name);
            return File.Exists(p) ? File.ReadAllLines(p, System.Text.Encoding.Latin1) : Array.Empty<string>();
        }
        var lists = ActivityLists.From(Lines("Z102.ASL"), Lines("Z202.ASL"), Lines("Z302.ASL"));
        OpenOverlay(new ActivityOverlay(Layout("TACTIVITY"), "MagiColor Pictures", lists, f => LoadActivityFile(dir, f), _audio.Effect, index =>
        {
            CloseOverlay();
            RestoreUndo();
            string file = lists.Picture(index);
            if (index < 0 || file.Length == 0) { _s.Mode = 0; return; }
            using var pic = LoadActivityFile(dir, file);
            Gdi.PatBlt(_canvas.Hdc, 0, 0, Hotspots.CanvasW, Hotspots.CanvasH, Gdi.WHITENESS);
            if (pic != null) _canvas.Draw(pic, 0, 0);
            Snapshot();
            SelectPaintFill(0x50);
        }));
    }

    /// <summary>activity\ + a list entry; an entry without an extension is tried as .ASL then .BMP.</summary>
    static DibSurface? LoadActivityFile(string dir, string name)
    {
        if (name.Length == 0) return null;
        var p = Path.Combine(dir, name);
        if (!File.Exists(p) && !Path.HasExtension(name))
            p = new[] { p + ".ASL", p + ".BMP" }.FirstOrDefault(File.Exists) ?? p;
        return LoadBitmap(p);
    }

    // ---------------------------------------------------------------- PC: save, print, about, jukebox

    /// <summary>
    /// Save (FUN_1008_6734): the 558x386 canvas (client at (0,0), the 2 px beyond white) as a 24-bit BMP named
    /// prefix + 6 random characters; an existing name fails with the documented box (no retry).
    /// </summary>
    void SavePicture()
    {
        string name = SaveNames.Generate(_level.SavePrefix, _random);
        string path = Path.Combine(SaveFolder, name);
        try
        {
            Directory.CreateDirectory(SaveFolder);
            if (File.Exists(path)) throw new IOException("exists");
            File.WriteAllBytes(path, CanvasBmp());
            ShowBox("Picture Saved!", "It is done!", MessageBoxOverlay.Icon.Information, new[] { "OK" }, _ => { });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Save {path}: {ex.Message}");
            ShowBox("There is a problem...", "Error -- Picture NOT Saved.", MessageBoxOverlay.Icon.Warning, new[] { "OK" }, _ => { });
        }
    }

    /// <summary>The canvas as a W x H (558x386) 24-bit BMP file image.</summary>
    public byte[] CanvasBmp()
    {
        using var full = new DibSurface(Hotspots.CanvasW, Hotspots.CanvasH, 0xFFFFFF);
        full.Draw(_canvas, 0, 0);
        full.Flush();
        return BmpWriter.Write24(full.Pixels, full.Width, full.Height);
    }

    void AskPrint() =>
        ShowBox("Making Sure!", "Do you really want to print?", MessageBoxOverlay.Icon.Question, new[] { "Yes", "No" }, b =>
        {
            if (b == 0) PrintCanvas();
        });

    /// <summary>
    /// Prints buffer A (equal to the canvas right after the option click) on the default printer, scaled
    /// to fit the printable area with the aspect ratio kept, centred (spec §3.9).
    /// </summary>
    void PrintCanvas()
    {
        var settings = new PrinterSettings();
        if (PrinterSettings.InstalledPrinters.Count == 0 || !settings.IsValid)
        {
            ShowBox("Crayons Studio Remake", "Printer driver is missing.", MessageBoxOverlay.Icon.Warning, new[] { "OK" }, _ => { });
            return;
        }
        try
        {
            using var bmp = _undoA.ToBitmap();
            using var doc = new PrintDocument { PrinterSettings = settings, DocumentName = "CRAYONS", PrintController = new StandardPrintController() };
            doc.PrintPage += (_, e) =>
            {
                var area = e.Graphics!.VisibleClipBounds;
                float scale = Math.Min(area.Width / bmp.Width, area.Height / bmp.Height);
                float w = bmp.Width * scale, h = bmp.Height * scale;
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                e.Graphics.DrawImage(bmp, area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
                e.HasMorePages = false;
            };
            doc.Print();
        }
        catch (Exception ex) when (ex is InvalidPrinterException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Debug.WriteLine($"Print: {ex.Message}");
            ShowBox("Printer Problem", "Cannot communicate with the printer. Make sure it is on.", MessageBoxOverlay.Icon.Warning, new[] { "OK" }, _ => { });
        }
    }

    void OpenAbout()
    {
        _audio.Effect(108);
        OpenOverlay(new AboutOverlay(Layout("TABOUTME"), Host.Images.TryFile("A912.ASL"), _audio.Effect, () =>
        {
            CloseOverlay();
            RestoreUndo();
        }));
    }

    void OpenJukebox() =>
        OpenOverlay(new JukeboxOverlay(Layout("TJUKEBOX"), _s, _audio.Effect, Caption, _audio.Voice, transition =>
        {
            CloseOverlay();
            RestoreUndo();
            if (transition == MusicTransition.Stop) _audio.StopMusic();
            else if (transition == MusicTransition.Restart) _audio.PlayNextSong();
        }));
}

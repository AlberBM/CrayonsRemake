using Crayons.App.Rendering;
using Crayons.Core.Assets;
using Crayons.Core.Content;
using Crayons.Core.Studio;

namespace Crayons.App.Ui;

/// <summary>
/// Tthumbs (content-files.md §3): four picture cells per page, the up / down / killme buttons, Label1
/// "a - b of n" in yellow, and right-click hints 125/126/147 with their voice clips. Picking a filled cell
/// returns its index; an empty cell or killme (SND92) returns -1.
/// </summary>
public sealed class ThumbsOverlay : DialogOverlay, IDisposable
{
    readonly PictureKind _kind;
    readonly IReadOnlyList<string> _paths;
    readonly Func<string, DibSurface?> _load;
    readonly DibSurface? _blank;
    readonly Action<int> _sound;
    readonly Func<int, string> _caption;
    readonly Action<int> _voice;
    readonly Action<int> _onClose;
    readonly ImageButton _up, _down, _kill;
    readonly Point _labelAt;
    readonly Func<IntPtr> _labelFont;
    readonly int _labelColor;
    readonly DibSurface?[] _cells = new DibSurface?[4];
    readonly int[] _orientation = { 4, 4, 4, 4 };
    bool _closed;

    public ThumbPager Pager { get; }

    /// <param name="paths">Ordered full paths (already capped at 250).</param>
    /// <param name="blank">The 1x1 a920.bmp that blanks every cell before a page loads.</param>
    /// <param name="onClose">The picked entry index, or -1.</param>
    public ThumbsOverlay(FormLayout form, PictureKind kind, IReadOnlyList<string> paths, Func<string, DibSurface?> load,
        DibSurface? blank, Action<int> sound, Func<int, string> caption, Action<int> voice, Action<int> onClose)
        : base(PictureKinds.Caption(kind), PictureKinds.Placement(kind).Left, PictureKinds.Placement(kind).Top,
            form.Int("ClientWidth", 526), PictureKinds.Placement(kind).Height - 2 * Win95.Border - Win95.CaptionH)
    {
        _kind = kind;
        _paths = paths;
        _load = load;
        _blank = blank;
        _sound = sound;
        _caption = caption;
        _voice = voice;
        _onClose = onClose;
        _up = form.Button("upbutton");
        _down = form.Button("downbutton");
        _kill = form.Button("killme");
        var (lx, ly, _, _) = form.Bounds("Label1", (420, 3, 94, 53));
        _labelAt = new Point(lx, ly);
        _labelFont = form.FontOf("Label1");
        _labelColor = form.Color("Label1", "Font.Color", 0xFFFF00);
        HintDelayMs = form.Child("Timer2")?.Int("Interval", 500) ?? 500;
        Pager = new ThumbPager(paths.Count);
        LoadPage();
    }

    /// <summary>The page loader: blank all four cells, then load first..first+3 that exist.</summary>
    void LoadPage()
    {
        for (int i = 0; i < 4; i++)
        {
            _cells[i]?.Dispose();
            _cells[i] = null;
            int e = Pager.EntryAt(i);
            if (e < 0) continue;
            _cells[i] = _load(_paths[e]);
            if (PictureKinds.IsClipArt(_kind)) _orientation[i] = PictureKinds.Orientation(_paths[e]);
        }
    }

    /// <summary>Cell i's bounds: 200x150 (stretched) or the clip-art H/V size.</summary>
    public Rectangle Cell(int i)
    {
        var (x, y) = PictureKinds.CellOrigins(_kind)[i];
        var (w, h) = PictureKinds.IsClipArt(_kind) ? PictureKinds.ClipArtCell(_orientation[i]) : (200, 150);
        return new Rectangle(x, y, w, h);
    }

    void Close(int result)
    {
        if (_closed) return;
        _closed = true;
        _onClose(result);
        Dispose();
    }

    protected override void DrawClient(DibSurface frame, IntPtr hdc)
    {
        Gdi.FillRect(hdc, 0, 0, ClientSize.Width, ClientSize.Height, 0x000000);
        for (int i = 0; i < 4; i++)
        {
            var r = Cell(i);
            var pic = _cells[i] ?? _blank;
            if (pic == null) continue;
            if (!PictureKinds.IsClipArt(_kind))
                Gdi.StretchBlt(hdc, r.X, r.Y, r.Width, r.Height, pic.Hdc, 0, 0, pic.Width, pic.Height, Gdi.SRCCOPY);
            else
                Gdi.BitBlt(hdc, r.X, r.Y, Math.Min(r.Width, pic.Width), Math.Min(r.Height, pic.Height), pic.Hdc, 0, 0, Gdi.SRCCOPY);
        }
        _up.Draw(hdc);
        _down.Draw(hdc);
        _kill.Draw(hdc);
        Win95.Text(hdc, _labelFont, _labelAt.X, _labelAt.Y, Pager.Label, _labelColor);
    }

    void Hint(int index, Point p)
    {
        if (ShowHint(_caption(index), p)) _voice(index);
    }

    protected override void ClientDown(Point p, MouseButtons button)
    {
        bool right = button == MouseButtons.Right;
        if (_up.Hit(p))
        {
            if (right) Hint(125, p);
            else { _sound(106); Pager.Up(); LoadPage(); }
            return;
        }
        if (_down.Hit(p))
        {
            if (right) Hint(126, p);
            else { _sound(107); Pager.Down(); LoadPage(); }
            return;
        }
        if (_kill.Hit(p))
        {
            if (right) Hint(147, p);
            else _kill.Down(p, button);
            return;
        }
        if (right) return;
        for (int i = 0; i < 4; i++)
            if (Cell(i).Contains(p)) { _pressedCell = i; return; }
    }

    int _pressedCell = -1;

    protected override void ClientUp(Point p)
    {
        if (_kill.Up(p)) { _sound(92); Close(-1); return; }
        int cell = _pressedCell;
        _pressedCell = -1;
        if (cell < 0 || !Cell(cell).Contains(p)) return;
        Close(Pager.EntryAt(cell)); // an empty cell closes with result 0 (-1 here)
    }

    public void Dispose()
    {
        for (int i = 0; i < 4; i++) { _cells[i]?.Dispose(); _cells[i] = null; }
    }
}

/// <summary>
/// Tactivity in Magic Color mode (content-files.md §6): ListBox1 shows the titles; Image3 previews
/// activity\ + ListBox2[i]; a list click plays SND34; OK (Image1) returns the index, Cancel (Image2) SND36.
/// </summary>
public sealed class ActivityOverlay : DialogOverlay, IDisposable
{
    readonly ActivityLists _lists;
    readonly Func<string, DibSurface?> _loadPreview;
    readonly Action<int> _sound;
    readonly Action<int> _onClose;
    readonly ImageButton _ok, _cancel;
    readonly Point _previewAt;
    readonly int _back;
    DibSurface? _preview;
    bool _closed;

    public ListBox List { get; }

    public ActivityOverlay(FormLayout form, string caption, ActivityLists lists, Func<string, DibSurface?> loadPreview,
        Action<int> sound, Action<int> onClose)
        : base(caption, form.Int("Left", 251), form.Int("Top", 103), form.Int("ClientWidth", 354), form.Int("ClientHeight", 291))
    {
        _lists = lists;
        _loadPreview = loadPreview;
        _sound = sound;
        _onClose = onClose;
        _ok = form.Button("Image1");
        _cancel = form.Button("Image2");
        _back = form.Color(null, "Color", 0x000000);
        var (px, py, _, _) = form.Bounds("Image3", (234, 46, 90, 90));
        _previewAt = new Point(px, py);
        var (lx, ly, lw, lh) = form.Bounds("ListBox1", (9, 8, 204, 273));
        List = new ListBox(new Rectangle(lx, ly, lw, lh), lists.Titles, form.FontOf("ListBox1"), form.Child("ListBox1")?.Int("ItemHeight", 16) ?? 16,
            form.Color("ListBox1", "Font.Color", 0xFFFFFF), form.Color("ListBox1", "Color", 0x000000), form.Ctl3D("ListBox1"));
        if (lists.Titles.Length > 0) List.Select(0, false);
        List.ItemPicked += (_, _) =>
        {
            _sound(34);
            Refresh();
        };
        Refresh();
    }

    void Refresh()
    {
        _preview?.Dispose();
        _preview = List.ItemIndex >= 0 ? _loadPreview(_lists.Preview(List.ItemIndex)) : null;
    }

    void Close(int result)
    {
        if (_closed) return;
        _closed = true;
        _onClose(result);
        Dispose();
    }

    protected override void DrawClient(DibSurface frame, IntPtr hdc)
    {
        Gdi.FillRect(hdc, 0, 0, ClientSize.Width, ClientSize.Height, _back);
        List.Draw(hdc);
        Picture(hdc, _preview, _previewAt.X, _previewAt.Y);
        _ok.Draw(hdc);
        _cancel.Draw(hdc);
    }

    protected override void ClientDown(Point p, MouseButtons button)
    {
        _ok.Down(p, button);
        _cancel.Down(p, button);
        List.MouseDown(p, button);
    }

    protected override void ClientMove(Point p) => List.MouseMove(p);

    protected override void ClientUp(Point p)
    {
        List.MouseUp();
        if (_ok.Up(p)) Close(List.ItemIndex);
        else if (_cancel.Up(p)) { _sound(36); Close(-1); }
    }

    public override void KeyDown(Keys key) => List.KeyDown(key);
    protected override void OnTick(int ms) => List.Tick(ms);

    public void Dispose()
    {
        _preview?.Dispose();
        _preview = null;
    }
}

/// <summary>
/// Tjukebox (content-files.md §7, shell.md §4): three toggles (sounds, music, audio help) showing their
/// on / off TIcon pictures, right-click hints 128/127/129 (blank in A938) and 155 on the exit Image8.
/// Exit plays SND92; the close reports the music transition.
/// </summary>
public sealed class JukeboxOverlay : DialogOverlay
{
    readonly JukeboxState _jukebox;
    readonly StudioState _s;
    readonly Action<int> _sound;
    readonly Func<int, string> _caption;
    readonly Action<int> _voice;
    readonly Action<MusicTransition> _onClose;
    readonly DibSurface? _background;
    readonly Point _backgroundAt;
    readonly ImageButton _exit;
    readonly (Rectangle Bounds, DibImage? On, DibImage? Off, Func<bool> State, Action Toggle, int Hint)[] _toggles;
    readonly (Rectangle Bounds, string Text, Func<IntPtr> Font, int Color, int Back)[] _labels;
    readonly int _back;
    bool _closed;

    public JukeboxOverlay(FormLayout form, StudioState state, Action<int> sound, Func<int, string> caption, Action<int> voice,
        Action<MusicTransition> onClose)
        : base(form.Caption(null, "jukebox"), form.Int("Left", 172), form.Int("Top", 98), form.Int("ClientWidth", 321), form.Int("ClientHeight", 200))
    {
        _s = state;
        _jukebox = new JukeboxState(state);
        _sound = sound;
        _caption = caption;
        _voice = voice;
        _onClose = onClose;
        _back = form.Color(null, "Color", 0xC0C0C0);
        _background = form.Picture("Image1");
        var (bx, by, _, _) = form.Bounds("Image1", (1, 0, 320, 200));
        _backgroundAt = new Point(bx, by);
        _exit = form.Button("Image8");
        HintDelayMs = form.Child("Timer2")?.Int("Interval", 250) ?? 250;
        Rectangle B(string n, (int, int, int, int) f) { var (x, y, w, h) = form.Bounds(n, f); return new Rectangle(x, y, Math.Max(w, 32), Math.Max(h, 32)); }
        _toggles = new (Rectangle, DibImage?, DibImage?, Func<bool>, Action, int)[]
        {
            (B("Image4", (258, 8, 32, 32)), form.Icon("Image2"), form.Icon("Image3"), () => _s.SoundOn, _jukebox.ToggleSounds, 128),
            (B("Image7", (258, 50, 32, 32)), form.Icon("Image5"), form.Icon("Image6"), () => _s.MusicOn, _jukebox.ToggleMusic, 127),
            (B("Image11", (257, 92, 32, 32)), form.Icon("Image9"), form.Icon("Image10"), () => _s.AudioHelpOn, _jukebox.ToggleAudioHelp, 129),
        };
        _labels = new[] { "Label1", "Label2", "Label3" }.Where(n => form.Child(n) != null).Select(n =>
        {
            var (x, y, w, h) = form.Bounds(n, (0, 0, 0, 0));
            return (new Rectangle(x, y, w, h), form.Caption(n, ""), form.FontOf(n), form.Color(n, "Font.Color", 0x00FFFF), form.Color(n, "Color", 0x000000));
        }).ToArray();
    }

    void Close()
    {
        if (_closed) return;
        _closed = true;
        _onClose(_jukebox.Close());
    }

    protected override void DrawClient(DibSurface frame, IntPtr hdc)
    {
        Gdi.FillRect(hdc, 0, 0, ClientSize.Width, ClientSize.Height, _back);
        Picture(hdc, _background, _backgroundAt.X, _backgroundAt.Y);
        foreach (var (r, text, font, color, back) in _labels)
        {
            Gdi.FillRect(hdc, r.Left, r.Top, r.Right, r.Bottom, back);
            using (Win95.Clip(hdc, r)) Win95.Text(hdc, font, r.X, r.Y, text, color);
        }
        _exit.Draw(hdc);
    }

    protected override void DrawOverClient(DibSurface frame)
    {
        foreach (var t in _toggles)
            Win95.Icon(frame, t.State() ? t.On : t.Off, ClientOrigin.X + t.Bounds.X, ClientOrigin.Y + t.Bounds.Y);
    }

    protected override void ClientDown(Point p, MouseButtons button)
    {
        bool right = button == MouseButtons.Right;
        foreach (var t in _toggles)
        {
            if (!t.Bounds.Contains(p)) continue;
            if (right) { if (ShowHint(_caption(t.Hint), p)) _voice(t.Hint); }
            else if (button == MouseButtons.Left) t.Toggle();
            return;
        }
        if (_exit.Hit(p) && right) { if (ShowHint(_caption(155), p)) _voice(155); return; }
        _exit.Down(p, button);
    }

    protected override void ClientUp(Point p)
    {
        if (_exit.Up(p))
        {
            _sound(92);
            Close();
        }
    }
}

/// <summary>
/// Taboutme (content-files.md §7): SND108 on show, A912 drawn at (0,0) clipped to the 277x313 client; a
/// left click closes it (SND92) only within the bottom 40 px and ±20 px of the horizontal centre, or the
/// caption's X button closes it (spec §9.1 R7).
/// </summary>
public sealed class AboutOverlay : DialogOverlay
{
    readonly DibSurface? _picture;
    readonly Action<int> _sound;
    readonly Action _onClose;
    readonly int _back;
    bool _closed;

    public AboutOverlay(FormLayout form, DibSurface? picture, Action<int> sound, Action onClose)
        : base(form.Caption(null, "About This Program"), form.Int("Left", 260), form.Int("Top", 119),
            form.Int("ClientWidth", 277), form.Int("ClientHeight", 313))
    {
        _picture = picture;
        _sound = sound;
        _onClose = onClose;
        _back = form.Color(null, "Color", 0x000000);
        SystemMenu = true; // R7 (Q40): the caption has the system-menu icon and a working X
        CloseBox = true;
    }

    /// <summary>The caption X (SC_CLOSE) closes the box without the hotspot's SND92.</summary>
    protected override void CloseBoxClicked()
    {
        if (_closed) return;
        _closed = true;
        _onClose();
    }

    /// <summary>The closing hotspot: bottom 40 px, within 20 px of the horizontal centre.</summary>
    public bool IsCloseSpot(Point p) =>
        p.Y >= ClientSize.Height - 40 && p.Y < ClientSize.Height && Math.Abs(p.X - ClientSize.Width / 2) <= 20;

    protected override void DrawClient(DibSurface frame, IntPtr hdc)
    {
        Gdi.FillRect(hdc, 0, 0, ClientSize.Width, ClientSize.Height, _back);
        Picture(hdc, _picture, 0, 0);
    }

    protected override void ClientDown(Point p, MouseButtons button)
    {
        if (button != MouseButtons.Left || !IsCloseSpot(p) || _closed) return;
        _closed = true;
        _sound(92);
        _onClose();
    }
}

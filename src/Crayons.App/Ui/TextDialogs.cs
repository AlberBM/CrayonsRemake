using Crayons.App.Rendering;
using Crayons.Core.Studio;
using Crayons.Core.Tools;

namespace Crayons.App.Ui;

/// <summary>
/// Ttextinpt (tools-text-edit.md §2.5): "Enter Text to be Displayed:", Edit1 shows the previous buffer
/// with all text selected; OK (Image1, or Enter) stores the edit text, Cancel (Image2) plays SND92 and
/// clears the buffer. Both close the dialog.
/// </summary>
public sealed class TextInputOverlay : DialogOverlay
{
    readonly StudioState _s;
    readonly Action<int> _sound;
    readonly Action _onClose;
    readonly ImageButton _ok, _cancel;
    readonly int _back;
    bool _closed;

    public EditBox Edit { get; }

    public TextInputOverlay(FormLayout form, StudioState state, Action<int> sound, Action onClose)
        : base(form.Caption(null, "Enter Text to be Displayed:"), form.Int("Left", 153), form.Int("Top", 111),
            form.Int("ClientWidth", 408), form.Int("ClientHeight", 88))
    {
        _s = state;
        _sound = sound;
        _onClose = onClose;
        _ok = form.Button("Image1");
        _cancel = form.Button("Image2");
        _back = form.Color(null, "Color", 0x000000);
        var (x, y, w, h) = form.Bounds("Edit1", (9, 30, 321, 23));
        Edit = new EditBox(new Rectangle(x, y, w, h), form.FontOf("Edit1"), form.Ctl3D("Edit1"));
        Edit.SetText(state.TextBuffer);
        Edit.SelectAll();
        Edit.Enter += Ok;
    }

    void Close()
    {
        if (_closed) return;
        _closed = true;
        _onClose();
    }

    public void Ok()
    {
        _s.TextBuffer = Edit.Text;
        Close();
    }

    public void Cancel()
    {
        _sound(92);
        _s.TextBuffer = "";
        Edit.SetText("");
        Close();
    }

    protected override void DrawClient(DibSurface frame, IntPtr hdc)
    {
        Gdi.FillRect(hdc, 0, 0, ClientSize.Width, ClientSize.Height, _back);
        _ok.Draw(hdc);
        _cancel.Draw(hdc);
        Edit.Draw(hdc);
    }

    protected override void ClientDown(Point p, MouseButtons button)
    {
        _ok.Down(p, button);
        _cancel.Down(p, button);
        if (button == MouseButtons.Left && Edit.Bounds.Contains(p)) Edit.MouseDown(p);
    }

    protected override void ClientUp(Point p)
    {
        if (_ok.Up(p)) Ok();
        else if (_cancel.Up(p)) Cancel();
    }

    public override void KeyPress(char c) => Edit.KeyPress(c);
    public override void KeyDown(Keys key) => Edit.KeyDown(key);
    protected override void OnTick(int ms) => Edit.Tick(ms);
}

/// <summary>
/// TFontForm (tools-text-edit.md §2.6): the installed fonts, sorted, white on black, with the current face
/// selected; Label1 shows "Hello" in that face at height -40. A left click on an item plays SND89 and
/// changes the Older Text face at once (no cancel). OK (Image1) plays SND91 and closes.
/// </summary>
public sealed class FontFormOverlay : DialogOverlay
{
    readonly StudioState _s;
    readonly Action<int> _sound;
    readonly Action _onClose;
    readonly ImageButton _ok;
    readonly Rectangle _label;
    readonly int _labelHeight, _labelColor, _back;
    readonly string _labelText;

    public ListBox List { get; }

    public FontFormOverlay(FormLayout form, StudioState state, IReadOnlyList<string> fonts, Action<int> sound, Action onClose)
        : base(form.Caption(null, "FontForm"), form.Int("Left", 268), form.Int("Top", 143),
            form.Int("ClientWidth", 394), form.Int("ClientHeight", 161))
    {
        _s = state;
        _sound = sound;
        _onClose = onClose;
        _ok = form.Button("Image1");
        _back = form.Color(null, "Color", 0x000000);
        var (lx, ly, lw, lh) = form.Bounds("ListBox1", (4, 2, 225, 157));
        List = new ListBox(new Rectangle(lx, ly, lw, lh), fonts, form.FontOf("ListBox1"), form.Child("ListBox1")?.Int("ItemHeight", 16) ?? 16,
            form.Color("ListBox1", "Font.Color", 0xFFFFFF), form.Color("ListBox1", "Color", 0x000000), form.Ctl3D("ListBox1"));
        var (x, y, w, h) = form.Bounds("Label1", (233, 3, 160, 80));
        _label = new Rectangle(x, y, w, h);
        _labelHeight = form.Child("Label1")?.Int("Font.Height", -40) ?? -40;
        _labelColor = form.Color("Label1", "Font.Color", 0xFFFFFF);
        _labelText = form.Caption("Label1", "Hello");
        int match = fonts.ToList().FindIndex(f => string.Equals(f, state.ShadowTextFontName, StringComparison.OrdinalIgnoreCase));
        if (match >= 0) List.Select(match);
        List.ItemPicked += (i, mouse) =>
        {
            if (!mouse) return; // the original reacted to ListBox1MouseDown only
            _sound(89);
            _s.ShadowTextFontName = List.Items[i];
        };
    }

    protected override void DrawClient(DibSurface frame, IntPtr hdc)
    {
        Gdi.FillRect(hdc, 0, 0, ClientSize.Width, ClientSize.Height, _back);
        List.Draw(hdc);
        // Label1: centred "Hello" in the current face (Alignment taCenter, AutoSize off).
        Gdi.FillRect(hdc, _label.Left, _label.Top, _label.Right, _label.Bottom, 0x000000);
        string face = _s.ShadowTextFontName;
        Func<IntPtr> font = () => Win95.Font(face, _labelHeight);
        int tw = Win95.Measure(font, _labelText).Width;
        using (Win95.Clip(hdc, _label))
            Win95.Text(hdc, font, _label.X + (_label.Width - tw) / 2, _label.Y, _labelText, _labelColor);
        _ok.Draw(hdc);
    }

    protected override void ClientDown(Point p, MouseButtons button)
    {
        _ok.Down(p, button);
        List.MouseDown(p, button);
    }

    protected override void ClientMove(Point p) => List.MouseMove(p);

    protected override void ClientUp(Point p)
    {
        List.MouseUp();
        if (_ok.Up(p))
        {
            _sound(91);
            _onClose();
        }
    }

    public override void KeyDown(Keys key) => List.KeyDown(key);
    protected override void OnTick(int ms) => List.Tick(ms);
}

/// <summary>
/// Trotator (tools-text-edit.md §2.7). On its third 200-ms Timer1 tick it loads the buffer into Edit1
/// (selected), sets the scroll bar to the angle and draws the preview (caption "Text Rotation", white
/// panel, black cross, "Example" in Arial at the angle, "&lt;n&gt; Degrees"). Scrolling sets the angle at once.
/// OK (Image1 or Enter): SND91, buffer := Edit1, angle := position. Cancel (Image2): SND92, clear the buffer.
/// </summary>
public sealed class RotatorOverlay : DialogOverlay
{
    readonly StudioState _s;
    readonly Action<int> _sound;
    readonly Action _onClose;
    readonly ImageButton _ok, _cancel;
    readonly Rectangle _panel, _label;
    readonly Func<IntPtr> _labelFont;
    readonly int _labelColor, _back;
    string _labelText;
    int _timer1Ms, _timer1Ticks;
    bool _previewReady, _closed;

    public EditBox Edit { get; }
    public VScrollBar Bar { get; }

    public RotatorOverlay(FormLayout form, StudioState state, Action<int> sound, Action onClose)
        : base(form.Caption(null, "rotator"), form.Int("Left", 112), form.Int("Top", 128),
            form.Int("ClientWidth", 377), form.Int("ClientHeight", 158))
    {
        _s = state;
        _sound = sound;
        _onClose = onClose;
        _ok = form.Button("Image1");
        _cancel = form.Button("Image2");
        _back = form.Color(null, "Color", 0x000000);
        var (px, py, pw, ph) = form.Bounds("Panel1", (6, 5, 125, 100));
        _panel = new Rectangle(px, py, pw, ph);
        var (lx, ly, lw, lh) = form.Bounds("Label1", (157, 52, 107, 16));
        _label = new Rectangle(lx, ly, lw, lh);
        _labelFont = form.FontOf("Label1");
        _labelColor = form.Color("Label1", "Font.Color", 0xFFFF00);
        _labelText = form.Caption("Label1", "45 Degrees");
        var (ex, ey, ew, eh) = form.Bounds("Edit1", (10, 124, 357, 24));
        Edit = new EditBox(new Rectangle(ex, ey, ew, eh), form.FontOf("Edit1"), form.Ctl3D("Edit1"));
        Edit.Enter += Ok;
        var (sx, sy, sw, sh) = form.Bounds("ScrollBar1", (133, 5, 17, 100));
        var sb = form.Child("ScrollBar1");
        Bar = new VScrollBar(new Rectangle(sx, sy, sw, sh), sb?.Int("Min", 0) ?? 0, sb?.Int("Max", 359) ?? 359, sb?.Int("LargeChange", 10) ?? 10);
        Bar.Scroll += pos =>
        {
            _s.RotationAngle = pos;
            DrawPreviewState();
        };
    }

    /// <summary>The Timer1 work done on its third tick (FormShow enabled it).</summary>
    public void Initialise()
    {
        Edit.SetText(_s.TextBuffer);
        Edit.SelectAll();
        Bar.SetPosition(_s.RotationAngle);
        DrawPreviewState();
    }

    void DrawPreviewState()
    {
        Caption = "Text Rotation";
        _labelText = $"{_s.RotationAngle} Degrees";
        _previewReady = true;
    }

    void Close()
    {
        if (_closed) return;
        _closed = true;
        _onClose();
    }

    public void Ok()
    {
        _sound(91);
        _s.TextBuffer = Edit.Text;
        _s.RotationAngle = Bar.Position;
        Close();
    }

    public void Cancel()
    {
        _sound(92);
        _s.TextBuffer = "";
        Close();
    }

    protected override void OnTick(int ms)
    {
        Edit.Tick(ms);
        Bar.Tick(ms);
        if (_timer1Ticks >= 3) return;
        _timer1Ms += ms;
        while (_timer1Ms >= 200 && _timer1Ticks < 3)
        {
            _timer1Ms -= 200;
            if (++_timer1Ticks == 3) Initialise();
        }
    }

    protected override void DrawClient(DibSurface frame, IntPtr hdc)
    {
        Gdi.FillRect(hdc, 0, 0, ClientSize.Width, ClientSize.Height, _back);
        // Panel1: a white TPanel with a raised 1-px bevel until the preview paints over it.
        Gdi.FillRect(hdc, _panel.Left, _panel.Top, _panel.Right, _panel.Bottom, 0xFFFFFF);
        if (!_previewReady) Win95.Bevel(hdc, _panel, 0xFFFFFF, Win95.Shadow);
        else
            using (Win95.Clip(hdc, _panel))
            {
                int ox = _panel.X, oy = _panel.Y;
                Gdi.Line(hdc, ox + 57, oy + 50, ox + 67, oy + 50, 0x000000);
                Gdi.Line(hdc, ox + 62, oy + 45, ox + 62, oy + 55, 0x000000);
                Gdi.SetBkMode(hdc, Gdi.TRANSPARENT);
                using (Gdi.Use(hdc, Gdi.Font(-TextMath.PointsToPixels(10), "Arial", _s.RotationAngle * 10)))
                {
                    Gdi.SetTextAlign(hdc, Gdi.TA_CENTER | Gdi.TA_BASELINE);
                    Gdi.SetTextColor(hdc, Gdi.Rgb(_s.CurrentColor));
                    Gdi.TextOut(hdc, ox + 62, oy + 50, "Example", 7);
                    Gdi.SetTextAlign(hdc, Gdi.TA_LEFT);
                }
            }
        int tw = Win95.Measure(_labelFont, _labelText).Width;
        Win95.Text(hdc, _labelFont, _label.X + (_label.Width - tw) / 2, _label.Y, _labelText, _labelColor);
        Bar.Draw(hdc);
        Edit.Draw(hdc);
        _ok.Draw(hdc);
        _cancel.Draw(hdc);
    }

    protected override void ClientDown(Point p, MouseButtons button)
    {
        _ok.Down(p, button);
        _cancel.Down(p, button);
        if (button != MouseButtons.Left) return;
        if (Bar.MouseDown(p)) return;
        if (Edit.Bounds.Contains(p)) Edit.MouseDown(p);
    }

    protected override void ClientMove(Point p) => Bar.MouseMove(p);

    protected override void ClientUp(Point p)
    {
        Bar.MouseUp();
        if (_ok.Up(p)) Ok();
        else if (_cancel.Up(p)) Cancel();
    }

    public override void KeyPress(char c) => Edit.KeyPress(c);
    public override void KeyDown(Keys key) => Edit.KeyDown(key);
}

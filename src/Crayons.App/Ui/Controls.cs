using Crayons.App.Rendering;

namespace Crayons.App.Ui;

/// <summary>
/// A single-line edit box (TEdit) for the dialog overlays: caret, insertion, Backspace/Delete,
/// Left/Right/Home/End, click-to-place, select-all and a blinking caret. Coordinates are client coordinates.
/// </summary>
public sealed class EditBox
{
    public const int MaxLength = 255; // the text goes into a 256-byte string buffer
    const int BlinkMs = 530;

    readonly Func<IntPtr> _font;
    readonly bool _ctl3d;
    int _blink;
    int _scroll;

    public EditBox(Rectangle bounds, Func<IntPtr> font, bool ctl3d)
    {
        Bounds = bounds;
        _font = font;
        _ctl3d = ctl3d;
    }

    public Rectangle Bounds { get; }
    public string Text { get; private set; } = "";
    public int Caret { get; private set; }
    public int SelStart { get; private set; }
    public int SelLength { get; private set; }
    public bool CaretVisible { get; private set; } = true;

    /// <summary>Raised by Enter.</summary>
    public event Action? Enter;

    public void SetText(string text)
    {
        Text = text.Length > MaxLength ? text[..MaxLength] : text;
        Caret = Text.Length;
        SelStart = SelLength = 0;
    }

    public void SelectAll()
    {
        SelStart = 0;
        SelLength = Text.Length;
        Caret = Text.Length;
    }

    void DeleteSelection()
    {
        if (SelLength == 0) return;
        Text = Text.Remove(SelStart, SelLength);
        Caret = SelStart;
        SelLength = 0;
    }

    void Touch()
    {
        _blink = 0;
        CaretVisible = true;
    }

    /// <summary>WM_CHAR: Backspace, Enter, printable characters (replacing the selection).</summary>
    public void KeyPress(char c)
    {
        Touch();
        if (c == '\r') { Enter?.Invoke(); return; }
        if (c == '\b')
        {
            if (SelLength > 0) DeleteSelection();
            else if (Caret > 0) { Text = Text.Remove(Caret - 1, 1); Caret--; }
            return;
        }
        if (c < ' ') return;
        DeleteSelection();
        if (Text.Length >= MaxLength) return;
        Text = Text.Insert(Caret, c.ToString());
        Caret++;
    }

    /// <summary>Navigation keys.</summary>
    public void KeyDown(Keys key)
    {
        Touch();
        switch (key)
        {
            case Keys.Left: SelLength = 0; Caret = Math.Max(0, Caret - 1); break;
            case Keys.Right: SelLength = 0; Caret = Math.Min(Text.Length, Caret + 1); break;
            case Keys.Home: SelLength = 0; Caret = 0; break;
            case Keys.End: SelLength = 0; Caret = Text.Length; break;
            case Keys.Delete:
                if (SelLength > 0) DeleteSelection();
                else if (Caret < Text.Length) Text = Text.Remove(Caret, 1);
                break;
        }
    }

    int Inset => _ctl3d ? 3 : 2;

    int TextWidth(string s) => Win95.Measure(_font, s).Width;

    /// <summary>A click places the caret at the nearest character boundary.</summary>
    public void MouseDown(Point p)
    {
        Touch();
        SelLength = 0;
        int x = p.X - Bounds.X - Inset + _scroll;
        int best = 0, bestD = int.MaxValue;
        for (int i = 0; i <= Text.Length; i++)
        {
            int d = Math.Abs(TextWidth(Text[..i]) - x);
            if (d < bestD) { bestD = d; best = i; }
        }
        Caret = best;
    }

    public void Tick(int ms)
    {
        _blink += ms;
        if (_blink >= BlinkMs) { _blink = 0; CaretVisible = !CaretVisible; }
    }

    public void Draw(IntPtr hdc, bool focused = true)
    {
        var r = Bounds;
        Gdi.FillRect(hdc, r.Left, r.Top, r.Right, r.Bottom, 0xFFFFFF);
        if (_ctl3d) Win95.Sunken(hdc, r);
        else Gdi.FrameRect(hdc, r.Left, r.Top, r.Right, r.Bottom, 0x000000);
        int inner = r.Width - 2 * Inset;
        int caretX = TextWidth(Text[..Caret]);
        if (caretX - _scroll > inner - 1) _scroll = caretX - inner + 1;
        if (caretX - _scroll < 0) _scroll = caretX;
        int lineH = Win95.Measure(_font, "Wg").Height;
        int tx = r.X + Inset - _scroll, ty = r.Y + Math.Max(Inset - 1, (r.Height - lineH) / 2);
        using var clip = Win95.Clip(hdc, Rectangle.FromLTRB(r.X + Inset - 1, r.Y + 1, r.Right - Inset + 1, r.Bottom - 1));
        Win95.Text(hdc, _font, tx, ty, Text, 0x000000);
        if (SelLength > 0 && focused)
        {
            int sx = TextWidth(Text[..SelStart]), sw = TextWidth(Text.Substring(SelStart, SelLength));
            Gdi.FillRect(hdc, tx + sx, ty, tx + sx + sw, ty + lineH, Win95.SelectionRgb);
            Win95.Text(hdc, _font, tx + sx, ty, Text.Substring(SelStart, SelLength), 0xFFFFFF);
        }
        else if (focused && CaretVisible)
            Gdi.Line(hdc, tx + caretX, ty, tx + caretX, ty + lineH, 0x000000);
    }
}

/// <summary>
/// A vertical scroll bar (TScrollBar or a list box's bar): arrows step by SmallChange, the trough pages by
/// LargeChange, the thumb drags; holding an arrow or the trough repeats.
/// </summary>
public sealed class VScrollBar
{
    readonly bool _proportional;
    int _repeat, _dragOffset = -1, _held;

    public VScrollBar(Rectangle bounds, int min, int max, int largeChange, bool proportional = false)
    {
        Bounds = bounds;
        Min = min;
        Max = max;
        LargeChange = largeChange;
        _proportional = proportional;
    }

    public Rectangle Bounds { get; set; }
    public int Min { get; set; }
    public int Max { get; set; }
    public int SmallChange { get; set; } = 1;
    public int LargeChange { get; set; }
    /// <summary>For proportional thumbs: the visible page size.</summary>
    public int PageSize { get; set; } = 1;
    public int Position { get; private set; }

    /// <summary>Raised when the user changes the position.</summary>
    public event Action<int>? Scroll;

    public void SetPosition(int v) => Position = Math.Clamp(v, Min, Max);

    int Arrow => Math.Min(Bounds.Width, Bounds.Height / 2);

    Rectangle Thumb
    {
        get
        {
            int track = Bounds.Height - 2 * Arrow;
            int th = _proportional ? Math.Max(8, track * PageSize / Math.Max(1, Max - Min + PageSize)) : Math.Min(Bounds.Width, track);
            int range = Math.Max(1, Max - Min);
            int y = Bounds.Y + Arrow + (track - th) * (Position - Min) / range;
            return new Rectangle(Bounds.X, y, Bounds.Width, th);
        }
    }

    void Change(int delta)
    {
        int old = Position;
        SetPosition(Position + delta);
        if (Position != old) Scroll?.Invoke(Position);
    }

    /// <summary>1 = up arrow, 2 = down arrow, 3 = page up, 4 = page down, 5 = thumb.</summary>
    int Part(Point p)
    {
        if (!Bounds.Contains(p)) return 0;
        if (p.Y < Bounds.Y + Arrow) return 1;
        if (p.Y >= Bounds.Bottom - Arrow) return 2;
        var t = Thumb;
        if (p.Y < t.Y) return 3;
        if (p.Y >= t.Bottom) return 4;
        return 5;
    }

    public bool MouseDown(Point p)
    {
        int part = Part(p);
        if (part == 0) return false;
        _held = part;
        _repeat = -350; // first repeat after the usual keyboard delay
        if (part == 5) _dragOffset = p.Y - Thumb.Y;
        else Act(part);
        return true;
    }

    void Act(int part)
    {
        switch (part)
        {
            case 1: Change(-SmallChange); break;
            case 2: Change(SmallChange); break;
            case 3: Change(-LargeChange); break;
            case 4: Change(LargeChange); break;
        }
    }

    public void MouseMove(Point p)
    {
        if (_held != 5) return;
        int track = Bounds.Height - 2 * Arrow - Thumb.Height;
        if (track <= 0) return;
        int y = p.Y - _dragOffset - Bounds.Y - Arrow;
        int v = Min + (int)Math.Round((double)y * (Max - Min) / track);
        int old = Position;
        SetPosition(v);
        if (Position != old) Scroll?.Invoke(Position);
    }

    public void MouseUp()
    {
        _held = 0;
        _dragOffset = -1;
    }

    public void Tick(int ms)
    {
        if (_held is 0 or 5) return;
        _repeat += ms;
        while (_repeat >= 50) { _repeat -= 50; Act(_held); }
    }

    public void Draw(IntPtr hdc)
    {
        var r = Bounds;
        // Win95 trough: a light dither; drawn as a flat light grey here.
        Gdi.FillRect(hdc, r.Left, r.Top, r.Right, r.Bottom, 0xE0E0E0);
        var up = new Rectangle(r.X, r.Y, r.Width, Arrow);
        var down = new Rectangle(r.X, r.Bottom - Arrow, r.Width, Arrow);
        foreach (var (b, dir) in new[] { (up, -1), (down, 1) })
        {
            Gdi.FillRect(hdc, b.Left, b.Top, b.Right, b.Bottom, Win95.Face);
            Win95.Raised(hdc, b);
            int cx = b.X + b.Width / 2, cy = b.Y + b.Height / 2;
            for (int i = 0; i < 4; i++)
            {
                int y = dir < 0 ? cy - 2 + i : cy + 1 - i;
                Gdi.Line(hdc, cx - i, y, cx + i + 1, y, 0x000000);
            }
        }
        var t = Thumb;
        Gdi.FillRect(hdc, t.Left, t.Top, t.Right, t.Bottom, Win95.Face);
        Win95.Raised(hdc, t);
    }
}

/// <summary>A list box (TListBox) with a proportional scroll bar when the items do not fit.</summary>
public sealed class ListBox
{
    readonly Func<IntPtr> _font;
    readonly int _fg, _bg, _border;
    readonly VScrollBar _bar;

    /// <param name="ctl3d">2-px sunken border (true) or a 1-px black border.</param>
    public ListBox(Rectangle bounds, IReadOnlyList<string> items, Func<IntPtr> font, int itemHeight, int fg, int bg, bool ctl3d)
    {
        Bounds = bounds;
        Items = items;
        _font = font;
        ItemHeight = itemHeight;
        _fg = fg;
        _bg = bg;
        _border = ctl3d ? 2 : 1;
        Ctl3D = ctl3d;
        int visible = Visible;
        _bar = new VScrollBar(new Rectangle(bounds.Right - _border - 17, bounds.Y + _border, 17, bounds.Height - 2 * _border),
            0, Math.Max(0, items.Count - visible), Math.Max(1, visible - 1), proportional: true) { PageSize = visible };
        _bar.Scroll += v => TopIndex = v;
    }

    public Rectangle Bounds { get; }
    public IReadOnlyList<string> Items { get; }
    public int ItemHeight { get; }
    public bool Ctl3D { get; }
    public int ItemIndex { get; private set; } = -1;
    public int TopIndex { get; private set; }

    int Visible => Math.Max(1, (Bounds.Height - 2 * _border) / ItemHeight);
    bool HasBar => Items.Count > Visible;

    /// <summary>Raised when an item is picked: (index, true) with the mouse, (index, false) with the keyboard.</summary>
    public event Action<int, bool>? ItemPicked;

    public void Select(int index, bool scrollIntoView = true)
    {
        ItemIndex = Items.Count == 0 ? -1 : Math.Clamp(index, 0, Items.Count - 1);
        if (!scrollIntoView || ItemIndex < 0) return;
        if (ItemIndex < TopIndex) TopIndex = ItemIndex;
        if (ItemIndex >= TopIndex + Visible) TopIndex = ItemIndex - Visible + 1;
        TopIndex = Math.Clamp(TopIndex, 0, Math.Max(0, Items.Count - Visible));
        _bar.SetPosition(TopIndex);
    }

    /// <summary>The item under a client point, or -1.</summary>
    public int ItemAt(Point p)
    {
        var inner = Win95.Inflate(Bounds, _border);
        if (!inner.Contains(p) || (HasBar && _bar.Bounds.Contains(p))) return -1;
        int i = TopIndex + (p.Y - inner.Y) / ItemHeight;
        return i < Items.Count ? i : -1;
    }

    /// <summary>Left press: the scroll bar or an item. Returns true when the press was inside the list box.</summary>
    public bool MouseDown(Point p, MouseButtons button)
    {
        if (!Bounds.Contains(p)) return false;
        if (button == MouseButtons.Left && HasBar && _bar.MouseDown(p)) return true;
        int i = ItemAt(p);
        if (i >= 0 && button == MouseButtons.Left)
        {
            Select(i);
            ItemPicked?.Invoke(i, true);
        }
        return true;
    }

    public void MouseMove(Point p) => _bar.MouseMove(p);
    public void MouseUp() => _bar.MouseUp();
    public void Tick(int ms) => _bar.Tick(ms);

    public void KeyDown(Keys key)
    {
        int i = key switch
        {
            Keys.Up => ItemIndex - 1,
            Keys.Down => ItemIndex + 1,
            Keys.PageUp => ItemIndex - (Visible - 1),
            Keys.PageDown => ItemIndex + (Visible - 1),
            Keys.Home => 0,
            Keys.End => Items.Count - 1,
            _ => int.MinValue,
        };
        if (i == int.MinValue || Items.Count == 0) return;
        int old = ItemIndex;
        Select(i);
        if (ItemIndex != old) ItemPicked?.Invoke(ItemIndex, false);
    }

    public void Draw(IntPtr hdc)
    {
        var r = Bounds;
        Gdi.FillRect(hdc, r.Left, r.Top, r.Right, r.Bottom, _bg);
        if (Ctl3D) Win95.Sunken(hdc, r);
        else Gdi.FrameRect(hdc, r.Left, r.Top, r.Right, r.Bottom, 0x000000);
        var inner = Win95.Inflate(r, _border);
        int right = HasBar ? _bar.Bounds.X : inner.Right;
        using (Win95.Clip(hdc, Rectangle.FromLTRB(inner.X, inner.Y, right, inner.Bottom)))
        for (int row = 0; row < Visible + 1; row++)
        {
            int i = TopIndex + row;
            if (i >= Items.Count) break;
            int y = inner.Y + row * ItemHeight;
            if (y >= inner.Bottom) break;
            int bottom = Math.Min(y + ItemHeight, inner.Bottom);
            bool sel = i == ItemIndex;
            if (sel) Gdi.FillRect(hdc, inner.X, y, right, bottom, Win95.SelectionRgb);
            Win95.Text(hdc, _font, inner.X + 2, y, Items[i], sel ? 0xFFFFFF : _fg);
        }
        if (HasBar) _bar.Draw(hdc);
    }
}

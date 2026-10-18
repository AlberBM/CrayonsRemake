using Crayons.App.Rendering;

namespace Crayons.App.Ui;

/// <summary>
/// An in-frame modal message box in the Windows 95 style (spec §3.6): navy caption bar
/// with the original caption, grey face, an optional system icon, the message text and
/// push buttons. It is drawn into the 640x480 frame so it scales with the UI; while it is
/// open, input goes only to it.
/// </summary>
public sealed class MessageBoxOverlay : IOverlay, IDisposable
{
    void IOverlay.MouseDown(Point p, MouseButtons button) => MouseDown(p);

    public enum Icon { None, Question, Information, Warning }

    const int Face = 0xC0C0C0, Shadow = 0x808080, Highlight = 0xFFFFFF, Dark = 0x000000, Caption = 0x000080;
    const int CaptionH = 18, BtnW = 75, BtnH = 23, Margin = 12, BtnGap = 6;

    readonly string _caption, _text;
    readonly Icon _icon;
    readonly string[] _buttons;
    readonly Action<int> _onClose;
    readonly Rectangle _bounds;
    readonly Rectangle[] _buttonRects;
    readonly Size _textSize;
    int _pressed = -1;
    bool _pressedInside;

    /// <summary>Creates a box; <paramref name="onClose"/> receives the index of the button that was clicked.</summary>
    public MessageBoxOverlay(string caption, string text, Icon icon, string[] buttons, Action<int> onClose)
    {
        _caption = caption; _text = text; _icon = icon; _buttons = buttons; _onClose = onClose;
        using (var probe = new DibSurface(1, 1))
        using (Gdi.Use(probe.Hdc, UiFont(false)))
        {
            Gdi.GetTextExtentPoint32(probe.Hdc, text, text.Length, out var sz);
            _textSize = new Size(sz.cx, sz.cy);
        }
        int iconW = icon == Icon.None ? 0 : 32 + Margin;
        int contentW = iconW + _textSize.Width;
        int buttonsW = buttons.Length * BtnW + (buttons.Length - 1) * BtnGap;
        int clientW = Math.Max(contentW, buttonsW) + 2 * Margin;
        int contentH = Math.Max(icon == Icon.None ? 0 : 32, _textSize.Height);
        int clientH = Margin + contentH + Margin + BtnH + Margin;
        int w = clientW + 6, h = clientH + CaptionH + 6;
        _bounds = new Rectangle((640 - w) / 2, (480 - h) / 2, w, h);
        int bx = _bounds.X + (w - buttonsW) / 2, by = _bounds.Bottom - 3 - Margin - BtnH;
        _buttonRects = buttons.Select((_, i) => new Rectangle(bx + i * (BtnW + BtnGap), by, BtnW, BtnH)).ToArray();
    }

    static IntPtr UiFont(bool bold) =>
        Gdi.CreateFont(-11, 0, 0, 0, bold ? Gdi.FW_BOLD : Gdi.FW_NORMAL, 0, 0, 0, 1, 0, 0, Gdi.NONANTIALIASED_QUALITY, 0, "MS Sans Serif");

    public Rectangle Bounds => _bounds;

    static void Bevel(IntPtr hdc, Rectangle r, int topLeft, int bottomRight)
    {
        Gdi.Line(hdc, r.Left, r.Bottom - 1, r.Left, r.Top, topLeft);
        Gdi.Line(hdc, r.Left, r.Top, r.Right - 1, r.Top, topLeft);
        Gdi.Line(hdc, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1, bottomRight);
        Gdi.Line(hdc, r.Right - 1, r.Bottom - 1, r.Left - 1, r.Bottom - 1, bottomRight);
    }

    static Rectangle Inflate(Rectangle r, int d) => new(r.X + d, r.Y + d, r.Width - 2 * d, r.Height - 2 * d);

    public void Draw(DibSurface frame)
    {
        IntPtr hdc = frame.Hdc;
        var r = _bounds;
        Gdi.FillRect(hdc, r.Left, r.Top, r.Right, r.Bottom, Face);
        Bevel(hdc, r, Face, Dark);
        Bevel(hdc, Inflate(r, 1), Highlight, Shadow);
        var cap = new Rectangle(r.X + 3, r.Y + 3, r.Width - 6, CaptionH);
        Gdi.FillRect(hdc, cap.Left, cap.Top, cap.Right, cap.Bottom, Caption);
        Gdi.SetBkMode(hdc, Gdi.TRANSPARENT);
        using (Gdi.Use(hdc, UiFont(true)))
        {
            Gdi.SetTextColor(hdc, Gdi.Rgb(0xFFFFFF));
            Gdi.TextOut(hdc, cap.X + 3, cap.Y + 3, _caption, _caption.Length);
        }
        int cy = cap.Bottom + Margin;
        int tx = r.X + 3 + Margin;
        if (_icon != Icon.None)
        {
            var sys = _icon switch { Icon.Question => SystemIcons.Question, Icon.Warning => SystemIcons.Warning, _ => SystemIcons.Information };
            Gdi.DrawIconEx(hdc, tx, cy, sys.Handle, 32, 32, 0, IntPtr.Zero, Gdi.DI_NORMAL);
            tx += 32 + Margin;
        }
        using (Gdi.Use(hdc, UiFont(false)))
        {
            Gdi.SetTextColor(hdc, 0);
            int ty = cy + Math.Max(0, (32 - _textSize.Height) / 2);
            Gdi.TextOut(hdc, tx, ty, _text, _text.Length);
            for (int i = 0; i < _buttons.Length; i++)
            {
                var b = _buttonRects[i];
                bool down = i == _pressed && _pressedInside;
                Gdi.FillRect(hdc, b.Left, b.Top, b.Right, b.Bottom, Face);
                if (i == 0) Gdi.FrameRect(hdc, b.Left, b.Top, b.Right, b.Bottom, Dark); // default button
                var inner = i == 0 ? Inflate(b, 1) : b;
                if (down) Bevel(hdc, inner, Shadow, Shadow);
                else
                {
                    Bevel(hdc, inner, Highlight, Dark);
                    Bevel(hdc, Inflate(inner, 1), Face, Shadow);
                }
                Gdi.GetTextExtentPoint32(hdc, _buttons[i], _buttons[i].Length, out var ts);
                int off = down ? 1 : 0;
                Gdi.TextOut(hdc, b.X + (b.Width - ts.cx) / 2 + off, b.Y + (b.Height - ts.cy) / 2 + off, _buttons[i], _buttons[i].Length);
            }
        }
    }

    int ButtonAt(Point p) => Array.FindIndex(_buttonRects, b => b.Contains(p));

    public void MouseDown(Point p)
    {
        _pressed = ButtonAt(p);
        _pressedInside = _pressed >= 0;
    }

    public void MouseMove(Point p)
    {
        if (_pressed >= 0) _pressedInside = ButtonAt(p) == _pressed;
    }

    public void MouseUp(Point p)
    {
        int b = _pressed;
        _pressed = -1;
        if (b >= 0 && ButtonAt(p) == b) _onClose(b);
    }

    /// <summary>Keyboard: Enter picks the default (first) button; a button's first letter picks it.</summary>
    public void KeyDown(Keys key)
    {
        if (key == Keys.Enter) { _onClose(0); return; }
        for (int i = 0; i < _buttons.Length; i++)
            if (_buttons[i].Length > 0 && char.ToUpperInvariant(_buttons[i][0]) == (char)key) { _onClose(i); return; }
    }

    public void Dispose() { }
}

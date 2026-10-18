using Crayons.App.Rendering;
using Crayons.App.Studio;
using Crayons.Core.Assets;

namespace Crayons.App.Ui;

/// <summary>Windows 95 look shared by the dialog overlays: frames, bevels, fonts and text.</summary>
public static class Win95
{
    public const int Face = 0xC0C0C0, Shadow = 0x808080, Highlight = 0xFFFFFF, Dark = 0x000000, CaptionRgb = 0x000080,
        SelectionRgb = 0x000080;

    /// <summary>Fixed dialog frame width (SM_CXFIXEDFRAME) and caption height.</summary>
    public const int Border = 3, CaptionH = 18;

    static readonly DibSurface Probe = new(1, 1);

    public static void Bevel(IntPtr hdc, Rectangle r, int topLeft, int bottomRight)
    {
        Gdi.Line(hdc, r.Left, r.Bottom - 1, r.Left, r.Top, topLeft);
        Gdi.Line(hdc, r.Left, r.Top, r.Right - 1, r.Top, topLeft);
        Gdi.Line(hdc, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1, bottomRight);
        Gdi.Line(hdc, r.Right - 1, r.Bottom - 1, r.Left - 1, r.Bottom - 1, bottomRight);
    }

    public static Rectangle Inflate(Rectangle r, int d) => new(r.X + d, r.Y + d, r.Width - 2 * d, r.Height - 2 * d);

    /// <summary>A 2-px sunken edge (Ctl3D controls) around <paramref name="r"/>'s inside.</summary>
    public static void Sunken(IntPtr hdc, Rectangle r)
    {
        Bevel(hdc, r, Shadow, Highlight);
        Bevel(hdc, Inflate(r, 1), Dark, Face);
    }

    /// <summary>A 2-px raised edge (buttons, scroll-bar arrows and thumbs).</summary>
    public static void Raised(IntPtr hdc, Rectangle r)
    {
        Bevel(hdc, r, Face, Dark);
        Bevel(hdc, Inflate(r, 1), Highlight, Shadow);
    }

    /// <summary>The caption font: MS Sans Serif 8 bold.</summary>
    public static IntPtr CaptionFont() =>
        Gdi.CreateFont(-11, 0, 0, 0, Gdi.FW_BOLD, 0, 0, 0, 1, 0, 0, Gdi.NONANTIALIASED_QUALITY, 0, "MS Sans Serif");

    /// <summary>A Delphi Font (Name, Height, Style) as a GDI font.</summary>
    public static IntPtr Font(string face, int height, bool bold = false) =>
        Gdi.CreateFont(height, 0, 0, 0, bold ? Gdi.FW_BOLD : Gdi.FW_NORMAL, 0, 0, 0, 1, 0, 0, Gdi.NONANTIALIASED_QUALITY, 0, face);

    /// <summary>Text extent in a font created by <paramref name="font"/> (deleted afterwards).</summary>
    public static Size Measure(Func<IntPtr> font, string text)
    {
        using (Gdi.Use(Probe.Hdc, font()))
        {
            Gdi.GetTextExtentPoint32(Probe.Hdc, text.Length == 0 ? " " : text, Math.Max(1, text.Length), out var sz);
            return new Size(text.Length == 0 ? 0 : sz.cx, sz.cy);
        }
    }

    /// <summary>Transparent text at (x, y), top-left aligned.</summary>
    public static void Text(IntPtr hdc, Func<IntPtr> font, int x, int y, string text, int rgb)
    {
        using (Gdi.Use(hdc, font()))
        {
            Gdi.SetBkMode(hdc, Gdi.TRANSPARENT);
            Gdi.SetTextAlign(hdc, Gdi.TA_LEFT);
            Gdi.SetTextColor(hdc, Gdi.Rgb(rgb));
            Gdi.TextOut(hdc, x, y, text, text.Length);
        }
    }

    /// <summary>
    /// The outer window: grey face, 3-px raised frame and the navy caption bar with its bold white text.
    /// </summary>
    public static void Window(IntPtr hdc, Rectangle r, string caption, bool systemMenu = false, bool closeBox = false, bool closePressed = false)
    {
        Gdi.FillRect(hdc, r.Left, r.Top, r.Right, r.Bottom, Face);
        Bevel(hdc, r, Face, Dark);
        Bevel(hdc, Inflate(r, 1), Highlight, Shadow);
        var cap = CaptionRect(r);
        Gdi.FillRect(hdc, cap.Left, cap.Top, cap.Right, cap.Bottom, CaptionRgb);
        int textX = cap.X + 3;
        if (systemMenu)
        {
            // The system-menu icon: Windows' generic application icon at 16x16 (the window has no icon of its own).
            IntPtr icon = Gdi.LoadImage(IntPtr.Zero, (IntPtr)Gdi.IDI_APPLICATION, Gdi.IMAGE_ICON, 16, 16, Gdi.LR_SHARED);
            if (icon != IntPtr.Zero) Gdi.DrawIconEx(hdc, cap.X + 1, cap.Y, icon, 16, 16, 0, IntPtr.Zero, Gdi.DI_NORMAL);
            textX = cap.X + 20;
        }
        Text(hdc, CaptionFont, textX, cap.Y + 2, caption, 0xFFFFFF);
        if (closeBox) CloseButton(hdc, CloseButtonRect(r), closePressed);
    }

    /// <summary>The navy caption bar inside the frame.</summary>
    public static Rectangle CaptionRect(Rectangle outer) => new(outer.X + Border, outer.Y + Border, outer.Width - 2 * Border, CaptionH - 1);

    /// <summary>The Win95 caption close button: 16x14, 2 px from the caption's right edge.</summary>
    public static Rectangle CloseButtonRect(Rectangle outer)
    {
        var cap = CaptionRect(outer);
        return new Rectangle(cap.Right - 2 - 16, cap.Top + 2, 16, 14);
    }

    /// <summary>A raised (or pressed) grey caption button with the black X glyph.</summary>
    public static void CloseButton(IntPtr hdc, Rectangle b, bool pressed)
    {
        Gdi.FillRect(hdc, b.Left, b.Top, b.Right, b.Bottom, Face);
        if (pressed)
        {
            Bevel(hdc, b, Dark, Highlight);
            Bevel(hdc, Inflate(b, 1), Shadow, Face);
        }
        else Raised(hdc, b);
        int ox = b.X + 4 + (pressed ? 1 : 0), oy = b.Y + 3 + (pressed ? 1 : 0);
        // 8x7 X, two pixels wide per row (the Marlett close glyph at 8 pt).
        for (int i = 0; i < 7; i++)
        {
            int a = i, z = 6 - i;
            foreach (int x in new[] { ox + a, ox + a + 1, ox + z, ox + z + 1 })
                Gdi.SetPixel(hdc, x, oy + i, Gdi.Rgb(Dark));
        }
    }

    /// <summary>
    /// Intersects the clip region with a rectangle given in logical (viewport-relative) coordinates for
    /// the scope's lifetime, then restores the previous clip.
    /// </summary>
    public static ClipScope Clip(IntPtr hdc, Rectangle r) => new(hdc, r);

    public readonly struct ClipScope : IDisposable
    {
        readonly IntPtr _hdc, _saved;
        readonly bool _had;

        public ClipScope(IntPtr hdc, Rectangle r)
        {
            _hdc = hdc;
            _saved = Gdi.CreateRectRgn(0, 0, 0, 0);
            _had = Gdi.GetClipRgn(hdc, _saved) == 1;
            Gdi.GetViewportOrgEx(hdc, out var vp);
            IntPtr rgn = Gdi.CreateRectRgn(vp.X + r.Left, vp.Y + r.Top, vp.X + r.Right, vp.Y + r.Bottom);
            if (_had) Gdi.ExtSelectClipRgn(hdc, rgn, Gdi.RGN_AND);
            else Gdi.SelectClipRgn(hdc, rgn);
            Gdi.DeleteObject(rgn);
        }

        public void Dispose()
        {
            Gdi.SelectClipRgn(_hdc, _had ? _saved : IntPtr.Zero);
            Gdi.DeleteObject(_saved);
        }
    }

    /// <summary>Draws a TIcon image with the DrawIcon rule at (x, y).</summary>
    public static void Icon(DibSurface frame, DibImage? icon, int x, int y)
    {
        if (icon != null) frame.DrawIcon(icon, x, y);
    }
}

/// <summary>
/// Base of the in-frame modal dialogs (spec §3.6): a Windows 95 frame at the form's Left/Top (clamped
/// into 640x480) around a client area of the form's ClientWidth x ClientHeight, laid out from the
/// original form data read at runtime. Subclasses work in client coordinates.
/// </summary>
public abstract class DialogOverlay : IOverlay
{
    protected DialogOverlay(string caption, int left, int top, int clientW, int clientH)
    {
        Caption = caption;
        int w = clientW + 2 * Win95.Border, h = clientH + 2 * Win95.Border + Win95.CaptionH;
        left = Math.Clamp(left, 0, Math.Max(0, 640 - w));
        top = Math.Clamp(top, 0, Math.Max(0, 480 - h));
        Outer = new Rectangle(left, top, w, h);
        ClientOrigin = new Point(left + Win95.Border, top + Win95.Border + Win95.CaptionH);
        ClientSize = new Size(clientW, clientH);
    }

    public Rectangle Outer { get; }

    /// <summary>The caption shows the system-menu icon.</summary>
    protected bool SystemMenu { get; init; }

    /// <summary>The caption has a working X close button (<see cref="CloseBoxClicked"/>).</summary>
    protected bool CloseBox { get; init; }

    bool _closePressed, _closeTracking;

    /// <summary>The caption close button was clicked (pressed and released on it).</summary>
    protected virtual void CloseBoxClicked() { }
    public Point ClientOrigin { get; }
    public Size ClientSize { get; }
    protected string Caption { get; set; }

    /// <summary>The client rectangle in frame coordinates.</summary>
    public Rectangle ClientBounds => new(ClientOrigin, ClientSize);

    /// <summary>Frame point → client point.</summary>
    public Point ToClient(Point p) => new(p.X - ClientOrigin.X, p.Y - ClientOrigin.Y);

    public void Draw(DibSurface frame)
    {
        IntPtr hdc = frame.Hdc;
        Win95.Window(hdc, Outer, Caption, SystemMenu, CloseBox, _closePressed);
        var c = ClientBounds;
        IntPtr rgn = Gdi.CreateRectRgn(c.Left, c.Top, c.Right, c.Bottom);
        Gdi.SelectClipRgn(hdc, rgn);
        Gdi.SetViewportOrgEx(hdc, c.X, c.Y, IntPtr.Zero);
        try { DrawClient(frame, hdc); }
        finally
        {
            Gdi.SetViewportOrgEx(hdc, 0, 0, IntPtr.Zero);
            Gdi.SelectClipRgn(hdc, IntPtr.Zero);
            Gdi.DeleteObject(rgn);
        }
        DrawOverClient(frame);
        if (_hintVisible) _hint.Draw(frame);
    }

    // ---- Right-click hints (the forms' own yellow Panel1 + Timer2) -----------------------
    readonly Studio.TooltipView _hint = new();
    bool _hintVisible, _hintDismissable;
    int _hintMs;

    /// <summary>The form's Timer2 interval: how long a hint stays before a mouse move may hide it.</summary>
    protected int HintDelayMs { get; init; } = 500;

    /// <summary>Shows a hint panel at a client point (nothing for empty captions or ones starting with a space).</summary>
    protected bool ShowHint(string text, Point client)
    {
        if (text.Length == 0 || text[0] == ' ') return false;
        _hint.Build(text, ClientOrigin.X + client.X, ClientOrigin.Y + client.Y);
        _hintVisible = true;
        _hintDismissable = false;
        _hintMs = 0;
        return true;
    }

    void HideHint() => _hintVisible = false;

    /// <summary>Draws the client area with GDI on <paramref name="hdc"/>, whose origin is the client's top-left.</summary>
    protected abstract void DrawClient(DibSurface frame, IntPtr hdc);

    /// <summary>Drawing in frame coordinates after the client (icons drawn by pixel, tooltips).</summary>
    protected virtual void DrawOverClient(DibSurface frame) { }

    public void MouseDown(Point p, MouseButtons button)
    {
        HideHint(); // any click hides a hint at once
        if (CloseBox && button == MouseButtons.Left && Win95.CloseButtonRect(Outer).Contains(p))
        {
            _closeTracking = _closePressed = true;
            return;
        }
        ClientDown(ToClient(p), button);
    }

    public void MouseMove(Point p)
    {
        if (_hintVisible && _hintDismissable) HideHint();
        if (_closeTracking)
        {
            _closePressed = Win95.CloseButtonRect(Outer).Contains(p); // the button pops up while the pointer is off it
            return;
        }
        ClientMove(ToClient(p));
    }

    public void MouseUp(Point p)
    {
        if (_closeTracking)
        {
            _closeTracking = _closePressed = false;
            if (Win95.CloseButtonRect(Outer).Contains(p)) CloseBoxClicked();
            return;
        }
        ClientUp(ToClient(p));
    }

    public void Tick(int elapsedMs)
    {
        if (_hintVisible && !_hintDismissable && (_hintMs += elapsedMs) >= HintDelayMs) _hintDismissable = true;
        OnTick(elapsedMs);
    }

    protected virtual void ClientDown(Point p, MouseButtons button) { }
    protected virtual void ClientMove(Point p) { }
    protected virtual void ClientUp(Point p) { }
    public virtual void KeyDown(Keys key) { }
    public virtual void KeyPress(char c) { }
    protected virtual void OnTick(int elapsedMs) { }

    /// <summary>Draws a picture 1:1 at client (x, y) inside <see cref="DrawClient"/> (the viewport is at the client origin).</summary>
    protected static void Picture(IntPtr hdc, DibSurface? pic, int x, int y)
    {
        if (pic != null) Gdi.BitBlt(hdc, x, y, pic.Width, pic.Height, pic.Hdc, 0, 0, Gdi.SRCCOPY);
    }
}

/// <summary>
/// A TImage used as a button: its DFM bounds (AutoSize: the picture's size) and picture. Click fires on a
/// left release inside after a left press inside (Delphi OnClick); <see cref="Hit"/> serves OnMouseDown handlers.
/// </summary>
public sealed class ImageButton
{
    public ImageButton(DfmObject? component, DibSurface? picture)
    {
        Picture = picture;
        var (x, y, w, h) = component?.Bounds ?? (0, 0, 0, 0);
        if (picture != null && (component?.Properties.TryGetValue("AutoSize", out var a) ?? false) && a is true)
            (w, h) = (picture.Width, picture.Height);
        Bounds = new Rectangle(x, y, w, h);
    }

    public DibSurface? Picture { get; }
    public Rectangle Bounds { get; }
    bool _pressed;

    public bool Hit(Point p) => Bounds.Contains(p);

    public void Down(Point p, MouseButtons b) => _pressed = b == MouseButtons.Left && Hit(p);

    /// <summary>True when this release completes a click.</summary>
    public bool Up(Point p)
    {
        bool click = _pressed && Hit(p);
        _pressed = false;
        return click;
    }

    /// <summary>Draws the picture clipped to the image bounds, in client coordinates (inside DrawClient).</summary>
    public void Draw(IntPtr hdc)
    {
        if (Picture == null) return;
        Gdi.BitBlt(hdc, Bounds.X, Bounds.Y, Math.Min(Picture.Width, Bounds.Width), Math.Min(Picture.Height, Bounds.Height),
            Picture.Hdc, 0, 0, Gdi.SRCCOPY);
    }
}

/// <summary>Loads a form's layout and pictures from the user's install (spec §3.6), tolerating missing parts.</summary>
public sealed class FormLayout
{
    readonly ImageCache _images;
    readonly string _exe, _form;

    public FormLayout(ImageCache images, string exe, string formClass)
    {
        _images = images;
        _exe = exe;
        _form = formClass;
        try { Form = images.Exe(exe).Form(formClass); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"{formClass}: {ex.Message}");
        }
    }

    public DfmObject? Form { get; }

    public DfmObject? Child(string name) => Form?.Child(name);

    public int Int(string prop, int fallback) => Form?.Int(prop, fallback) ?? fallback;

    public string Text(string prop, string fallback) =>
        Form?.Properties.TryGetValue(prop, out var v) == true && v is string s ? s : fallback;

    public (int X, int Y, int W, int H) Bounds(string child, (int, int, int, int) fallback) => Child(child)?.Bounds ?? fallback;

    /// <summary>A TImage's bitmap, or null when it cannot be read.</summary>
    public DibSurface? Picture(string child)
    {
        if (Child(child)?.PictureBitmap() == null) return null;
        try { return _images.FormPicture(_exe, _form, child); }
        catch (InvalidDataException) { return null; }
    }

    /// <summary>A TImage's icon (TIcon picture), or null.</summary>
    public DibImage? Icon(string child)
    {
        try { return Child(child)?.PictureIcon(); }
        catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException or ArgumentOutOfRangeException) { return null; }
    }

    public ImageButton Button(string child) => new(Child(child), Picture(child));

    static string? Str(DfmObject? o, string p) => o != null && o.Properties.TryGetValue(p, out var v) && v is string s ? s : null;

    /// <summary>A component's font (its own unless ParentFont), else the form's: a GDI font factory.</summary>
    public Func<IntPtr> FontOf(string? child, string fallbackFace = "System", int fallbackHeight = -13)
    {
        var c = child == null ? null : Child(child);
        bool own = c != null && Str(c, "Font.Name") != null;
        var src = own ? c : Form;
        string face = Str(src, "Font.Name") ?? fallbackFace;
        int height = src?.Int("Font.Height", fallbackHeight) ?? fallbackHeight;
        bool bold = src != null && src.Properties.TryGetValue("Font.Style", out var st) && st is List<string> l && l.Contains("fsBold");
        return () => Win95.Font(face, height, bold);
    }

    /// <summary>Ctl3D of a component (default: the form's, default true).</summary>
    public bool Ctl3D(string child)
    {
        var c = Child(child);
        if (c != null && c.Properties.TryGetValue("Ctl3D", out var v) && v is bool b) return b;
        return Form == null || !Form.Properties.TryGetValue("Ctl3D", out var f) || f is not bool fb || fb;
    }

    /// <summary>A Delphi colour property as 0xRRGGBB (named colours or $00BBGGRR integers).</summary>
    public int Color(string? child, string prop, int fallback)
    {
        var o = child == null ? Form : Child(child);
        if (o == null || !o.Properties.TryGetValue(prop, out var v)) return fallback;
        if (v is int i) return ((i & 0xFF) << 16) | (i & 0xFF00) | ((i >> 16) & 0xFF);
        return v is string s ? NamedColor(s, fallback) : fallback;
    }

    public static int NamedColor(string name, int fallback) => name switch
    {
        "clBlack" or "clWindowText" or "clBtnText" => 0x000000,
        "clWhite" or "clWindow" => 0xFFFFFF,
        "clYellow" => 0xFFFF00,
        "clAqua" => 0x00FFFF,
        "clGreen" => 0x008000,
        "clLime" => 0x00FF00,
        "clRed" => 0xFF0000,
        "clBlue" => 0x0000FF,
        "clNavy" or "clHighlight" => 0x000080,
        "clGray" or "clBtnShadow" => 0x808080,
        "clSilver" or "clBtnFace" => 0xC0C0C0,
        "clMaroon" => 0x800000,
        "clPurple" => 0x800080,
        "clTeal" => 0x008080,
        "clOlive" => 0x808000,
        "clFuchsia" => 0xFF00FF,
        _ => fallback,
    };

    /// <summary>A component's Caption (Delphi stores it only when it differs from the Name).</summary>
    public string Caption(string? child, string fallback)
    {
        var o = child == null ? Form : Child(child);
        return Str(o, "Caption") ?? o?.Name ?? fallback;
    }
}

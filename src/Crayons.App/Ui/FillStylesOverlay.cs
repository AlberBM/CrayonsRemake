using Crayons.App.Rendering;
using Crayons.Core.Tools;

namespace Crayons.App.Ui;

/// <summary>An in-frame modal overlay: while one is open, input goes only to it (spec §3.6).</summary>
public interface IOverlay
{
    void Draw(DibSurface frame);
    void MouseDown(Point p, MouseButtons button);
    void MouseMove(Point p);
    void MouseUp(Point p);
    void KeyDown(Keys key);
    /// <summary>A typed character (WM_CHAR), including '\b' and '\r'.</summary>
    void KeyPress(char c) { }
    /// <summary>Host tick (about every 15 ms) for dialog timers and caret blinking.</summary>
    void Tick(int elapsedMs) { }
}

/// <summary>
/// The borderless TFillStyles dialog (tools-paint.md §5): a black 257-px-wide form at Left 82,
/// Top/Height 280/68 for the Fill Bucket or 94/222 for the shape tools, showing the form's own
/// Image1 picture (read at runtime from the EXE) at its DFM position. A left click on the image
/// selects a style by the documented row/cell map and closes the dialog, with no sound.
/// Clicks outside the form are ignored (modal).
/// </summary>
public sealed class FillStylesOverlay : IOverlay
{
    readonly DibSurface? _picture;
    readonly Rectangle _form;
    readonly Point _imageOrigin;
    readonly Action<int> _onPick;

    /// <param name="picture">TFILLSTYLES.Image1's bitmap, or null when it cannot be read.</param>
    /// <param name="imageLeft">Image1.Left from the DFM.</param>
    /// <param name="imageTop">Image1.Top from the DFM (-1 in the original).</param>
    public FillStylesOverlay(DibSurface? picture, int imageLeft, int imageTop, bool bucket, Action<int> onPick)
    {
        _picture = picture;
        var (top, height) = PaintMath.FillStylesPlacement(bucket);
        _form = new Rectangle(PaintMath.FillStylesLeft, top, PaintMath.FillStylesWidth, height);
        _imageOrigin = new Point(_form.X + imageLeft, _form.Y + imageTop);
        _onPick = onPick;
    }

    public Rectangle Bounds => _form;

    /// <summary>The style for a frame point, or -1 when the point is not on the form.</summary>
    public int StyleAt(Point p) =>
        _form.Contains(p) ? PaintMath.FillStyleAt(p.X - _imageOrigin.X, p.Y - _imageOrigin.Y) : -1;

    public void Draw(DibSurface frame)
    {
        IntPtr hdc = frame.Hdc;
        Gdi.FillRect(hdc, _form.Left, _form.Top, _form.Right, _form.Bottom, 0x000000);
        if (_picture == null) return;
        // The picture is clipped to the borderless form's client area.
        int sx = Math.Max(0, _form.X - _imageOrigin.X), sy = Math.Max(0, _form.Y - _imageOrigin.Y);
        int w = Math.Min(_picture.Width - sx, _form.Right - (_imageOrigin.X + sx));
        int h = Math.Min(_picture.Height - sy, _form.Bottom - (_imageOrigin.Y + sy));
        if (w > 0 && h > 0) frame.DrawPart(_picture, _imageOrigin.X + sx, _imageOrigin.Y + sy, sx, sy, w, h);
    }

    public void MouseDown(Point p, MouseButtons button)
    {
        if (button != MouseButtons.Left) return;
        int style = StyleAt(p);
        if (style >= 0) _onPick(style);
    }

    public void MouseMove(Point p) { }
    public void MouseUp(Point p) { }
    public void KeyDown(Keys key) { }
}

namespace Crayons.App.Rendering;

/// <summary>
/// A fixed 640x480 framebuffer (the original's design resolution, a 32-bpp DIB so all
/// UI and tool drawing uses real GDI) presented scaled to fit the window with
/// nearest-neighbour sampling (StretchBlt COLORONCOLOR) and letterboxing.
/// Converts window coordinates back to logical 640x480 coordinates for input.
/// </summary>
public sealed class ScaledSurface : Control
{
    public const int LogicalWidth = 640, LogicalHeight = 480;

    public DibSurface Frame { get; } = new(LogicalWidth, LogicalHeight, 0);

    public ScaledSurface()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
        BackColor = Color.Black;
    }

    /// <summary>Destination rectangle of the frame inside the control (largest fit, integer scale when possible).</summary>
    public Rectangle FrameBounds
    {
        get
        {
            double s = Math.Min((double)ClientSize.Width / LogicalWidth, (double)ClientSize.Height / LogicalHeight);
            if (s >= 1) s = Math.Floor(s * 4) / 4; // keep pixels evenly sized at common scales
            if (s <= 0) s = 1;
            int w = (int)(LogicalWidth * s), h = (int)(LogicalHeight * s);
            return new Rectangle((ClientSize.Width - w) / 2, (ClientSize.Height - h) / 2, w, h);
        }
    }

    /// <summary>Integer display scale used for cursors: round(frame scale), at least 1 (spec §8.4).</summary>
    public int DisplayScale => Math.Max(1, (int)Math.Round(FrameBounds.Width / (double)LogicalWidth, MidpointRounding.AwayFromZero));

    /// <summary>Maps a point in control coordinates to logical frame coordinates (may fall outside 0..639/0..479).</summary>
    public Point ToLogical(Point p)
    {
        var b = FrameBounds;
        return new Point((int)Math.Floor((p.X - b.X) * (double)LogicalWidth / b.Width),
                         (int)Math.Floor((p.Y - b.Y) * (double)LogicalHeight / b.Height));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var b = FrameBounds;
        IntPtr hdc = e.Graphics.GetHdc();
        try
        {
            Gdi.PatBlt(hdc, 0, 0, ClientSize.Width, b.Top, Gdi.BLACKNESS);
            Gdi.PatBlt(hdc, 0, b.Bottom, ClientSize.Width, ClientSize.Height - b.Bottom, Gdi.BLACKNESS);
            Gdi.PatBlt(hdc, 0, b.Top, b.Left, b.Height, Gdi.BLACKNESS);
            Gdi.PatBlt(hdc, b.Right, b.Top, ClientSize.Width - b.Right, b.Height, Gdi.BLACKNESS);
            Frame.Flush();
            Gdi.SetStretchBltMode(hdc, Gdi.COLORONCOLOR);
            Gdi.StretchBlt(hdc, b.X, b.Y, b.Width, b.Height, Frame.Hdc, 0, 0, LogicalWidth, LogicalHeight, Gdi.SRCCOPY);
        }
        finally { e.Graphics.ReleaseHdc(hdc); }
    }

    /// <summary>Arrow keys reach KeyDown (dialog edit boxes and list boxes use them).</summary>
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Frame.Dispose();
        base.Dispose(disposing);
    }
}

using Crayons.App.Rendering;

namespace Crayons.App.Screens;

/// <summary>
/// One full-screen state of the program (splash, age picker, Younger studio, Older studio).
/// All coordinates are logical 640x480 pixels.
/// </summary>
public abstract class GameScreen
{
    protected GameScreen(GameHost host) => Host = host;

    protected GameHost Host { get; }

    /// <summary>Renders the screen into the 640x480 frame (a GDI DIB surface).</summary>
    public abstract void Render(DibSurface frame);

    public virtual void MouseDown(Point p, MouseButtons button) { }
    public virtual void MouseMove(Point p, MouseButtons buttons) { }
    public virtual void MouseUp(Point p, MouseButtons button) { }
    public virtual void KeyDown(KeyEventArgs e) { }
    public virtual void KeyPress(KeyPressEventArgs e) { }

    /// <summary>A WM_SYSCOMMAND reached the main window.</summary>
    public virtual void SysCommand() { }

    /// <summary>Called every host tick (~15 ms) with the elapsed milliseconds.</summary>
    public virtual void Tick(int elapsedMs) { }

    public virtual Cursor Cursor => Cursors.Default;

    public virtual void Enter() { }
    public virtual void Leave() { }
}

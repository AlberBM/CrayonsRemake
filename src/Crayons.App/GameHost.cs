using Crayons.App.Audio;
using Crayons.App.Rendering;
using Crayons.App.Screens;
using Crayons.App.Studio;
using Crayons.Core.Assets;
using Crayons.Core.Studio;

namespace Crayons.App;

/// <summary>
/// The single top-level window. Owns the 640x480 surface, the current screen,
/// shared services (assets, images, audio) and the frame timer.
/// Fullscreen by default like the original (borderless, maximized); F11 toggles a window.
/// </summary>
public sealed class GameHost : Form
{
    const int WM_SYSCOMMAND = 0x0112;

    readonly ScaledSurface _surface = new() { Dock = DockStyle.Fill };
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    long _lastTick;
    bool _dirty;
    GameScreen? _screen;

    public AssetStore Assets { get; }
    public ImageCache Images { get; }
    public WinmmAudio Audio { get; } = new();

    /// <summary>Milliseconds since the host started (monotonic).</summary>
    public long Now => _clock.ElapsedMilliseconds;

    /// <summary>Integer UI scale used to scale cursors.</summary>
    public int DisplayScale => _surface.DisplayScale;

    public GameHost(AssetStore assets, bool windowed = false)
    {
        Assets = assets;
        Images = new ImageCache(assets);
        Text = "Crayons Studio Remake";
        SetWindowIcon("CRAYONST.EXE");
        BackColor = Color.Black;
        KeyPreview = true;
        SetFullscreen(!windowed);
        Controls.Add(_surface);

        _surface.MouseDown += (_, e) => { _screen?.MouseDown(_surface.ToLogical(e.Location), e.Button); UpdateCursor(); };
        _surface.MouseMove += (_, e) => { _screen?.MouseMove(_surface.ToLogical(e.Location), e.Button); UpdateCursor(); };
        _surface.MouseUp += (_, e) => { _screen?.MouseUp(_surface.ToLogical(e.Location), e.Button); UpdateCursor(); };
        _surface.Resize += (_, _) => UpdateCursor();
        KeyDown += OnKeyDownInternal;
        KeyPress += (_, e) => _screen?.KeyPress(e);
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();

        Show(new SplashScreen(this));
    }

    public void Show(GameScreen next)
    {
        _screen?.Leave();
        _screen = next;
        _screen.Enter();
        UpdateCursor();
        Redraw();
    }

    public void StartLevel(Level level)
    {
        var def = LevelDefinition.For(level);
        SetWindowIcon(def.Exe);
        Show(new StudioScreen(this, def));
    }

    /// <summary>
    /// Uses an original program's MAINICON as the window and taskbar icon, as the original did:
    /// the launcher's crayon bundle (CRAYONST.EXE), then the level program's own icon.
    /// </summary>
    public void SetWindowIcon(string exe)
    {
        try
        {
            if (Images.Exe(exe).IconFile("MAINICON") is not { } ico) return;
            var old = Icon;
            using (var ms = new MemoryStream(ico)) Icon = new Icon(ms);
            old?.Dispose();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException) { }
    }

    /// <summary>Re-renders the current screen into the frame and presents it.</summary>
    public void Redraw()
    {
        _dirty = false;
        if (_screen == null) return;
        _screen.Render(_surface.Frame);
        _surface.Invalidate();
    }

    /// <summary>Marks the frame dirty; it is re-rendered on the next tick (caps presentation at the tick rate).</summary>
    public void RequestRedraw() => _dirty = true;

    void OnTick()
    {
        long now = _clock.ElapsedMilliseconds;
        int dt = (int)(now - _lastTick);
        _lastTick = now;
        _screen?.Tick(dt);
        if (_dirty) Redraw();
    }

    void UpdateCursor() => _surface.Cursor = _screen?.Cursor ?? Cursors.Default;

    void OnKeyDownInternal(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F11) { SetFullscreen(FormBorderStyle != FormBorderStyle.None); e.Handled = true; return; }
        _screen?.KeyDown(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_SYSCOMMAND) _screen?.SysCommand();
        base.WndProc(ref m);
    }

    void SetFullscreen(bool on)
    {
        if (on)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Normal;
            ClientSize = new Size(ScaledSurface.LogicalWidth * 2, ScaledSurface.LogicalHeight * 2);
            CenterToScreen();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _screen?.Leave();
        _screen = null;
        Audio.Dispose();
        Images.Dispose();
        base.OnFormClosed(e);
    }
}

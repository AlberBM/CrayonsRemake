using System.Diagnostics;
using Crayons.App.Rendering;
using Crayons.App.Screens;
using Crayons.App.Tools;
using Crayons.App.Ui;
using Crayons.Core;
using Crayons.Core.Assets;
using Crayons.Core.Content;
using Crayons.Core.Studio;

namespace Crayons.App.Studio;

/// <summary>
/// The paint studio for one level (spec §3.4). It keeps a 640x480 "form" surface that is
/// updated exactly where the original drew (FormPaint, panel redraws, the options-row
/// timer), a separate toolbar strip, the canvas client (556x384), undo buffer A and
/// tooltip buffer B. Each presentation composites form, toolbar, canvas and overlays.
/// The click state machine itself lives in <see cref="StudioInput"/>; this class supplies
/// its side effects (<see cref="IStudioEffects"/>).
/// </summary>
public sealed partial class StudioScreen : GameScreen, IStudioEffects
{
    const int Timer2Ms = 250, MoveCoalesceMs = 20, MusicPollMs = 250;

    readonly LevelDefinition _level;
    readonly StudioState _s = new();
    readonly DelphiRandom _random = DelphiRandom.FromClock(); // Randomize at studio start
    readonly StudioArt _art;
    readonly StudioInput _input;
    readonly ToolbarView _toolbar = new();
    readonly TooltipView _tooltip = new();
    readonly StudioAudio _audio;
    readonly Dictionary<(int Index, int Scale), Cursor?> _cursors = new();
    readonly DibSurface _form = new(640, 480, 0);
    readonly DibSurface _canvas = new(Hotspots.CanvasClientW, Hotspots.CanvasClientH, 0xFFFFFF);
    readonly DibSurface _undoA = new(Hotspots.CanvasClientW, Hotspots.CanvasClientH, 0xFFFFFF);
    readonly DibSurface _tooltipB = new(Hotspots.CanvasClientW, Hotspots.CanvasClientH, 0xFFFFFF);
    readonly ToolDispatcher _tools;
    string[] _captions = Array.Empty<string>();

    string _sheet;
    readonly OptionsRowAnimation _optionsRow = new();
    int _timer2, _musicPoll;
    IOverlay? _overlay;
    bool _canvasCaptured;
    Point? _pendingMove;
    long _lastMoveDelivered;
    bool _cursorOnCanvas;
    int _exitCountdown = -1;
    IAnimation? _animation;
    int _animationElapsed;

    public StudioScreen(GameHost host, LevelDefinition level) : base(host)
    {
        _level = level;
        _art = new StudioArt(host.Images, level);
        _audio = new StudioAudio(host.Audio, host.Assets, _s);
        _input = new StudioInput(level, _s, this);
        _sheet = level.InitialSheet;
        var ctx = new ToolContext
        {
            State = _s,
            Random = _random,
            Canvas = new CanvasPainter(_canvas),
            PlaySound = _audio.Effect,
            Image = host.Images.TryFile,
            StartAnimation = StartAnimation,
            Backing = _undoA,
            AskText = OpenTextInput,
            AskRotation = OpenRotator,
            LoadPath = LoadBitmap,
        };
        _tools = new ToolDispatcher(ctx);
        _mediaRoot = StudioAudio.MediaRoot(host.Assets);
        try { Fonts.LoadPrivate(host.Assets.Root); }
        catch (IOException ex) { Debug.WriteLine($"Fonts: {ex.Message}"); }
    }

    /// <summary>True while a Mr. Brush / Train animation plays (input is ignored, spec §3.5).</summary>
    public bool AnimationRunning => _animation != null;

    void StartAnimation(IAnimation animation)
    {
        _animation = animation;
        _animationElapsed = 0;
    }

    public StudioState State => _s;

    // ---------------------------------------------------------------- lifecycle

    public override void Enter()
    {
        if (Host.Assets.Exists(_level.CaptionFile)) _captions = Host.Assets.ReadLines(_level.CaptionFile);
        // First paint: FormPaint, the colour-mode panel flag, option strip at slot 0, then PC1.
        _s.ShowPanel(PanelId.ColorMode);
        _art.FormPaint(_form, _s);
        _input.SetOptionStrip(0);
        _toolbar.Draw(_level, Host.Images, 0);
        _audio.PlayNextSong();
        Host.RequestRedraw();
    }

    public override void Leave()
    {
        _audio.StopMusic();
        _toolbar.Dispose();
        _tooltip.Dispose();
        _form.Dispose();
        _canvas.Dispose();
        _undoA.Dispose();
        _tooltipB.Dispose();
    }

    // ---------------------------------------------------------------- rendering

    public override void Render(DibSurface frame)
    {
        frame.CopyFrom(_form);
        frame.Blit(_toolbar.Surface, ToolbarLayout.X0, ToolbarLayout.Y0);
        frame.Blit(_canvas, Hotspots.CanvasX, Hotspots.CanvasY);
        Gdi.FrameRect(frame.Hdc, 82, 92, 82 + Hotspots.CanvasW, 92 + Hotspots.CanvasH, 0x000000);
        if (_s.TooltipVisible) _tooltip.Draw(frame);
        _overlay?.Draw(frame);
    }

    // ---------------------------------------------------------------- IStudioEffects

    public void PlaySound(int snd) => _audio.Effect(snd);
    public void Snapshot() => _undoA.CopyFrom(_canvas);
    public void RestoreUndo() => _canvas.CopyFrom(_undoA);
    public void DrawOptionStrip(int slot) => _art.DrawOptionStrip(_form, slot);
    public void RedrawShadeGrid() => _art.DrawShadeGrid(_form, _s);
    public void RedrawColumn() => _art.RedrawColumn(_form, _s);

    public void TabChanged(int tab, string sheet)
    {
        _sheet = sheet;
        _optionsRow.Reset();
        _toolbar.Draw(_level, Host.Images, tab);
    }

    public void RunAction(string name)
    {
        var ctx = _tools.Context;
        switch (name)
        {
            case "PicturesColor": OpenPictures(PictureKind.ClipArtColor); break;
            case "PicturesBlackWhite": OpenPictures(PictureKind.ClipArtBlackWhite); break;
            case "Backgrounds": OpenPictures(PictureKind.Background); break;
            case "ColoringBook": OpenPictures(PictureKind.ColoringBook); break;
            case "MagicColor": OpenMagicColor(); break;
            case "FillStylesDialog": OpenFillStyles(); break;
            case "FontDialog": OpenFontForm(); break;
            // Younger Wacky slots 8/9 and the same routines at Older Change It slots 3/4 (tools-text-edit.md §5).
            // They read buffer A, which the option click has just refreshed.
            case "BarnZoom" or "Reduce": StartAnimation(ChangeItTools.Tunnel(ctx)); break;
            case "Shear" or "MoveAround": StartAnimation(ChangeItTools.Squiggle(ctx)); break;
            // Eraser (tools-text-edit.md §4)
            case "NewSlate": StartAnimation(EraserTools.NewSlate(ctx)); break;
            case "ShrinkAway": StartAnimation(EraserTools.ShrinkAway(ctx)); break;
            case "Crumple": StartAnimation(EraserTools.Crumple(ctx)); break;
            case "WindowShade": StartAnimation(EraserTools.WindowShade(ctx)); break;
            // PC (content-files.md §7)
            case "Save": SavePicture(); break;
            case "Open": OpenPictures(PictureKind.Open); break;
            case "Print": AskPrint(); break;
            case "Trash": OpenPictures(PictureKind.Trash); break;
            case "About": OpenAbout(); break;
            case "Jukebox": OpenJukebox(); break;
            default:
                Debug.WriteLine($"Unknown option action '{name}'");
                break;
        }
    }

    /// <summary>
    /// The Paint Patterns panel (input.md §2.5): back up the canvas, show TFillStyles modally (SND90 on
    /// show), and after the pick repaint the column and restore the canvas from the backup.
    /// </summary>
    void OpenFillStyles()
    {
        Snapshot();
        DibSurface? picture = null;
        int left = 0, top = -1;
        try
        {
            if (Host.Images.Exe(_level.Exe).Form("TFILLSTYLES")?.Child("Image1") is { } image)
            {
                left = image.Int("Left");
                top = image.Int("Top");
                picture = Host.Images.FormPicture(_level.Exe, "TFILLSTYLES", "Image1");
            }
        }
        catch (InvalidDataException ex) { Debug.WriteLine($"TFillStyles picture: {ex.Message}"); }
        bool bucket = _s.Mode is 0x3A or 0x50;
        _audio.Effect(90);
        _overlay = new FillStylesOverlay(picture, left, top, bucket, style =>
        {
            _s.FillPattern = style;
            _overlay = null;
            _art.RedrawColumn(_form, _s);
            RestoreUndo();
            Host.RequestRedraw();
        });
    }

    public void OpenHelp()
    {
        var store = Host.Assets;
        try
        {
            if (store.Exists("CRAYONS.HLP") && FindWinHelp() is string viewer)
            {
                Process.Start(new ProcessStartInfo(viewer, $"\"{store.PathOf("CRAYONS.HLP")}\"") { UseShellExecute = false });
                return;
            }
            var pdf = new[]
            {
                store.PathOf("XCRAYONS.PDF"),
                @"E:\Programs\Reverse Engineering Lab\targets\XCRAYONS.PDF",
            }.FirstOrDefault(File.Exists);
            if (pdf != null)
            {
                Process.Start(new ProcessStartInfo(pdf) { UseShellExecute = true });
                return;
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Debug.WriteLine($"Help viewer failed: {ex.Message}");
        }
        ShowBox("Crayons Studio Remake", "Help is not available on this computer.", MessageBoxOverlay.Icon.Information, new[] { "OK" }, _ => { });
    }

    static string? FindWinHelp()
    {
        const string otvdm = @"E:\Programs\otvdm\winhlp32.exe";
        if (File.Exists(otvdm)) return otvdm;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            try
            {
                var p = Path.Combine(dir.Trim(), "winhlp32.exe");
                if (File.Exists(p)) return p;
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    public void AskQuit() =>
        ShowBox("Just Checking...", "Do you really want to quit?", MessageBoxOverlay.Icon.Question, new[] { "Yes", "No" }, button =>
        {
            if (button != 0) return;
            _audio.Effect(99);
            // Let SND99 finish before the window closes (the original closed right after starting it).
            int ms = _s.SoundOn && _audio.EffectBytes(99) is { } wav ? Riff.WaveDurationMs(wav) : 0;
            _exitCountdown = Math.Clamp(ms, 0, 3000);
        });

    void ShowBox(string caption, string text, MessageBoxOverlay.Icon icon, string[] buttons, Action<int> onClose)
    {
        _overlay = new MessageBoxOverlay(caption, text, icon, buttons, b =>
        {
            _overlay = null;
            onClose(b);
            Host.RequestRedraw();
        });
        Host.RequestRedraw();
    }

    // ---------------------------------------------------------------- timers

    public override void Tick(int elapsedMs)
    {
        if (_exitCountdown >= 0)
        {
            _exitCountdown -= elapsedMs;
            if (_exitCountdown < 0) Host.Close();
            return;
        }

        if (_optionsRow.Tick(elapsedMs) is int row)
        {
            _art.DrawOptionsFrame(_form, _sheet, row);
            Host.RequestRedraw();
        }

        if (_overlay != null)
        {
            _overlay.Tick(elapsedMs);
            Host.RequestRedraw(); // caret blink, dialog timers
        }

        if (_s.TooltipVisible && !_s.TooltipDismissable)
        {
            _timer2 += elapsedMs;
            if (_timer2 >= Timer2Ms) _s.TooltipDismissable = true;
        }

        if (_animation != null)
        {
            // Timed replacement for the original's GetTickCount busy-loop: a frame once more than
            // IntervalMs has passed. Input is ignored meanwhile; the screen keeps repainting.
            _animationElapsed += elapsedMs;
            if (_animationElapsed > _animation.IntervalMs)
            {
                _animationElapsed = 0;
                if (!_animation.Step()) _animation = null;
                Host.RequestRedraw();
            }
        }
        else if (_pendingMove is Point p && Host.Now - _lastMoveDelivered >= MoveCoalesceMs) DeliverMove(p);

        _musicPoll += elapsedMs;
        if (_musicPoll >= MusicPollMs)
        {
            _musicPoll = 0;
            _audio.PollMusic();
        }
    }

    // ---------------------------------------------------------------- input routing

    static bool IsRight(MouseButtons b) => b == MouseButtons.Right;
    static Point ToCanvas(Point p) => new(p.X - Hotspots.CanvasX, p.Y - Hotspots.CanvasY);

    public override void MouseDown(Point p, MouseButtons button)
    {
        if (_exitCountdown >= 0) return;
        if (_overlay != null) { _overlay.MouseDown(p, button); Host.RequestRedraw(); return; }
        if (_canvasCaptured)
        {
            // A right press during a clip-art drag cancels the drop (CancelPlace, content-files.md §4).
            if (IsRight(button) && _s.Mode == 0x33) _s.CancelPlace = true;
            return;
        }
        if (_animation != null) return;

        // Preamble (input.md §2.1): hide a visible caption, commit ABCD typing, drop a pending selection.
        if (_s.TooltipVisible) HideTooltip();
        bool onCanvas = Hotspots.CanvasOuter.Contains(p.X, p.Y);
        if (_s.TypingActive)
        {
            TextTools.Abcd.Commit(_tools.Context);
            if (onCanvas) { Host.RequestRedraw(); return; } // that click does not start a new line
        }
        if (!onCanvas && _s.FloatingSelection)
        {
            RestoreUndo();
            _s.FloatingSelection = false;
            _s.Mode = 0;
        }

        if (onCanvas)
        {
            // The border is not client area; the right button draws nothing.
            if (Hotspots.CanvasClient.Contains(p.X, p.Y) && !IsRight(button))
            {
                // Parallelogram step 2 tracks the mouse unheld: bring its outline up to date first.
                if (_pendingMove is Point tracked && _tools.TracksWithoutButton) DeliverMove(tracked);
                var c = ToCanvas(p);
                _canvasCaptured = true;
                _pendingMove = null;
                _lastMoveDelivered = Host.Now;
                _tools.Down(c.X, c.Y);
            }
        }
        else if (Hotspots.Toolbar.Contains(p.X, p.Y))
        {
            int tab = Hotspots.TabIndexAt(p.X);
            if (IsRight(button))
            {
                var (ax, ay) = ToolbarLayout.TabTooltipAnchor(p.X, p.Y);
                ShowTooltip(_level.TabTooltipBase + tab, ax, ay);
            }
            else if (button == MouseButtons.Left) _input.TabClick(tab);
        }
        else if (IsRight(button))
            ShowTooltip(_level.FrameTooltip(_s, p.X, p.Y), p.X, p.Y);
        else
            _input.FormClick(p.X, p.Y);
        Host.RequestRedraw();
    }

    public override void MouseMove(Point p, MouseButtons buttons)
    {
        if (_overlay != null) { _overlay.MouseMove(p); Host.RequestRedraw(); return; }
        if (_s.TooltipVisible && _s.TooltipDismissable) { HideTooltip(); Host.RequestRedraw(); }

        _cursorOnCanvas = _canvasCaptured || Hotspots.CanvasClient.Contains(p.X, p.Y);
        if (_animation != null) return; // input is ignored while an animation plays
        bool tracking = _tools.TracksWithoutButton && Hotspots.CanvasClient.Contains(p.X, p.Y);
        if (!_canvasCaptured && !tracking) return;
        if (Host.Now - _lastMoveDelivered >= MoveCoalesceMs) DeliverMove(p);
        else _pendingMove = p; // keep only the latest position (spec §3.4)
    }

    void DeliverMove(Point p)
    {
        _pendingMove = null;
        _lastMoveDelivered = Host.Now;
        var c = ToCanvas(p);
        _tools.Move(c.X, c.Y);
        Host.RequestRedraw();
    }

    public override void MouseUp(Point p, MouseButtons button)
    {
        if (_overlay != null) { _overlay.MouseUp(p); Host.RequestRedraw(); return; }
        if (!_canvasCaptured || IsRight(button)) return;
        if (_animation != null)
        {
            // The release ends the stroke; the animating tools have no mouse-up action.
            _canvasCaptured = false;
            _pendingMove = null;
            _s.StrokeActive = false;
            return;
        }
        if (_pendingMove is Point pending) DeliverMove(pending);
        _canvasCaptured = false;
        var c = ToCanvas(p);
        _tools.Up(c.X, c.Y);
        Host.RequestRedraw();
    }

    public override void KeyDown(KeyEventArgs e)
    {
        if (_overlay != null) { _overlay.KeyDown(e.KeyCode); Host.RequestRedraw(); }
        // The original had no keyboard shortcuts (input.md §6).
    }

    public override void KeyPress(KeyPressEventArgs e)
    {
        if (_overlay != null) { _overlay.KeyPress(e.KeyChar); Host.RequestRedraw(); return; }
        if (_s.TypingActive && _animation == null)
        {
            TextTools.Abcd.Key(_tools.Context, e.KeyChar); // FormKeyPress is active only while typing
            Host.RequestRedraw();
        }
    }

    /// <summary>WM_SYSCOMMAND: commit pending text, then snapshot (shell.md §3.7).</summary>
    public override void SysCommand()
    {
        if (_s.TypingActive) TextTools.Abcd.Commit(_tools.Context);
        Snapshot();
    }

    // ---------------------------------------------------------------- tooltip

    void ShowTooltip(int index, int x, int y)
    {
        string text = index >= 0 && index < 291 && index < _captions.Length ? _captions[index] : "";
        if (text.Length == 0 || text[0] == ' ') return;
        _tooltipB.CopyFrom(_canvas);
        _tooltip.Build(text, x, y);
        _s.TooltipVisible = true;
        _s.TooltipDismissable = false;
        _timer2 = 0;
        _audio.Voice(index);
    }

    void HideTooltip()
    {
        _s.TooltipVisible = false;
        _s.TooltipDismissable = false;
        _canvas.CopyFrom(_tooltipB);
        _input.SetOptionStrip(_s.LastOptionSlot);
    }

    // ---------------------------------------------------------------- cursors

    public override Cursor Cursor
    {
        get
        {
            if (_overlay != null || !_cursorOnCanvas) return Cursors.Default;
            int index = CursorRules.CursorIndex(_s);
            if (index == 0) return Cursors.Default;
            int scale = Host.DisplayScale;
            if (!_cursors.TryGetValue((index, scale), out var cur))
            {
                cur = null;
                // Spec §8.4: CRAYON2.EXE's cursors for both levels (the images are identical).
                if (CursorRules.ResourceName(index) is string name && Host.Images.Exe("CRAYON2.EXE").Cursor(name) is { } c)
                    cur = CursorFactory.Create(c.Image, c.HotX, c.HotY, scale);
                _cursors[(index, scale)] = cur;
            }
            return cur ?? Cursors.Default;
        }
    }
}

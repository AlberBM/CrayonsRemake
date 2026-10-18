using System.Diagnostics;
using Crayons.App.Audio;
using Crayons.App.Rendering;
using Crayons.App.Studio;
using Crayons.App.Tools;
using Crayons.App.Ui;
using Crayons.Core;
using Crayons.Core.Assets;
using Crayons.Core.Studio;
using Crayons.Core.Tools;

namespace Crayons.Tests;

/// <summary>A scripted MCI sequencer: "open" and "play" block like the Windows GS synth does.</summary>
sealed class FakeMci : IMciCommands
{
    readonly object _gate = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();
    long _playStarted = -1;

    public int OpenMs { get; init; } = 300;
    public int PlayMs { get; init; } = 100;
    public int SeekingMs { get; init; } = 100;
    public int SongMs { get; init; } = 600;
    public bool FailOpen { get; init; }

    public List<string> Opened { get; } = new();
    public int StatusCalls;
    public int UiThreadCalls;
    public int UiThreadId = -1;

    public int Send(string command, out string result)
    {
        result = "";
        if (Environment.CurrentManagedThreadId == UiThreadId) Interlocked.Increment(ref UiThreadCalls);
        if (command.StartsWith("open "))
        {
            Thread.Sleep(OpenMs);
            if (FailOpen) return 263; // MCIERR_INVALID_DEVICE_NAME
            lock (_gate) Opened.Add(command.Split('"')[1]);
            return 0;
        }
        if (command.StartsWith("play "))
        {
            Thread.Sleep(PlayMs);
            lock (_gate) _playStarted = _clock.ElapsedMilliseconds;
            return 0;
        }
        if (command.StartsWith("status "))
        {
            Interlocked.Increment(ref StatusCalls);
            long t;
            lock (_gate) t = _playStarted < 0 ? -1 : _clock.ElapsedMilliseconds - _playStarted;
            result = t < 0 ? "stopped" : t < SeekingMs ? "seeking" : t < SongMs ? "playing" : "stopped";
            return 0;
        }
        if (command.StartsWith("close ")) lock (_gate) _playStarted = -1;
        return 0;
    }
}

/// <summary>R1: music polling and track changes never block the UI thread.</summary>
public class MusicResponsivenessTests
{
    [Theory]
    [InlineData(0, "playing", false)]
    [InlineData(0, "seeking", false)] // right after "play" MCI reports seeking for ~100 ms: not the end
    [InlineData(0, "not ready", false)]
    [InlineData(0, "stopped", true)]
    [InlineData(0, "STOPPED", true)]
    [InlineData(263, "", true)]       // the device is gone
    public void OnlyStoppedOrAnErrorEndsASong(int rc, string mode, bool finished) =>
        Assert.Equal(finished, MciMusicPlayer.IsFinished(rc, mode));

    [Fact]
    public void Play_DoesNotWaitForMci_AndSeekingIsStillPlaying()
    {
        var mci = new FakeMci { OpenMs = 400, PlayMs = 200, SeekingMs = 300, SongMs = 5000, UiThreadId = Environment.CurrentManagedThreadId };
        using var player = new MciMusicPlayer(mci, pollMs: 20);
        var sw = Stopwatch.StartNew();
        player.Play(@"C:\x\PC1.MID");
        Assert.True(player.IsPlaying);
        Assert.True(sw.ElapsedMilliseconds < 50, $"Play took {sw.ElapsedMilliseconds} ms");
        Thread.Sleep(800); // open + play + well into "seeking"
        Assert.True(player.IsPlaying);
        Assert.True(mci.StatusCalls > 0);
        Assert.Equal(0, mci.UiThreadCalls);
        Assert.Single(mci.Opened);
    }

    [Fact]
    public void FailedOpen_EndsAndReportsFailure()
    {
        var mci = new FakeMci { OpenMs = 10, FailOpen = true };
        using var player = new MciMusicPlayer(mci, pollMs: 10);
        player.Play(@"C:\x\PC1.MID");
        var sw = Stopwatch.StartNew();
        while (player.IsPlaying && sw.ElapsedMilliseconds < 2000) Thread.Sleep(5);
        Assert.False(player.IsPlaying);
        Assert.True(player.LastFailed);
    }

    sealed class ThrowingMci : IMciCommands
    {
        public int Send(string command, out string result)
        {
            result = "";
            if (command.StartsWith("status")) throw new InvalidOperationException("driver failure");
            return 0;
        }
    }

    [Fact]
    public void WorkerException_EndsTheSong_WithoutKillingTheProcess()
    {
        using var player = new MciMusicPlayer(new ThrowingMci(), pollMs: 10);
        player.Play(@"C:\x\PC1.MID");
        var sw = Stopwatch.StartNew();
        while (player.IsPlaying && sw.ElapsedMilliseconds < 2000) Thread.Sleep(5);
        Assert.False(player.IsPlaying);
        Assert.True(player.LastFailed);
        player.Play(@"C:\x\PC2.MID"); // the worker is still alive and takes new requests
        Assert.True(player.IsPlaying);
    }

    /// <summary>
    /// Headless soak of the studio's tick + music poll loop with blocking MCI: every UI-side call returns
    /// at once, tracks chain PC1, PC2, PC3 in order (each opened exactly once), and none is skipped.
    /// </summary>
    [Fact]
    public void Soak_TrackChangesKeepTheTickRate()
    {
        string root = Path.Combine(Path.GetTempPath(), "CrayonsSoak_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "SND"));
        try
        {
            File.WriteAllBytes(Path.Combine(root, "A1.ASL"), new byte[1]);
            File.WriteAllBytes(Path.Combine(root, "A900.ASL"), new byte[1]);
            for (int i = 1; i <= 20; i++) File.WriteAllBytes(Path.Combine(root, "SND", $"PC{i}.MID"), new byte[1]);
            var mci = new FakeMci { OpenMs = 300, PlayMs = 150, SeekingMs = 120, SongMs = 700, UiThreadId = Environment.CurrentManagedThreadId };
            using var audio = new WinmmAudio(mci, musicPollMs: 20);
            var state = new StudioState();
            var music = new StudioAudio(audio, new AssetStore(root), state);

            var clock = Stopwatch.StartNew();
            long worst = 0;
            int ticks = 0, poll = 0;
            music.PlayNextSong();
            long last = clock.ElapsedMilliseconds;
            while (clock.ElapsedMilliseconds < 4000)
            {
                Thread.Sleep(15); // the host's 15-ms timer
                long now = clock.ElapsedMilliseconds;
                poll += (int)(now - last);
                last = now;
                ticks++;
                if (poll >= 250)
                {
                    poll = 0;
                    var t0 = clock.ElapsedTicks;
                    music.PollMusic();
                    worst = Math.Max(worst, (clock.ElapsedTicks - t0) * 1000 / Stopwatch.Frequency);
                }
            }
            Assert.True(worst < 20, $"a music poll blocked for {worst} ms");
            Assert.Equal(0, mci.UiThreadCalls);
            Assert.True(ticks > 150, $"only {ticks} ticks in 4 s");
            // 4 s with ~1.25 s per track (open + play + 0.7 s song + poll latency): at least PC1..PC3, in order, no skips.
            var names = mci.Opened.Select(Path.GetFileName).ToList();
            Assert.True(names.Count >= 3, string.Join(",", names));
            Assert.Equal(Enumerable.Range(1, names.Count).Select(i => $"PC{i}.MID"), names);
        }
        finally { Directory.Delete(root, true); }
    }
}

/// <summary>A temporary folder that passes <see cref="AssetStore.IsInstall"/> (marker files only, no original data).</summary>
sealed class FakeInstall : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "CrayonsFake_" + Guid.NewGuid().ToString("N"));

    public FakeInstall()
    {
        Directory.CreateDirectory(Path.Combine(Root, "SND"));
        foreach (var f in new[] { "A1.ASL", "A900.ASL", Path.Combine("SND", "PC1.MID") }) File.WriteAllBytes(Path.Combine(Root, f), new byte[1]);
    }

    public void Dispose() => Directory.Delete(Root, true);
}

public class RuntimeCorrectionsTests : IDisposable
{
    readonly StudioState _s = new();
    readonly DibSurface _canvas = new(556, 384, 0xFFFFFF);
    readonly DibSurface _backing = new(556, 384, 0xFFFFFF);
    readonly ToolContext _c;
    readonly ToolDispatcher _d;

    public RuntimeCorrectionsTests()
    {
        _c = new ToolContext { State = _s, Random = new DelphiRandom(7), Canvas = new CanvasPainter(_canvas), Backing = _backing };
        _d = new ToolDispatcher(_c);
    }

    public void Dispose()
    {
        _canvas.Dispose();
        _backing.Dispose();
    }

    void Drag(int x0, int y0, int x1, int y1)
    {
        _d.Down(x0, y0);
        _d.Move(x1, y1);
        _d.Up(x1, y1);
    }

    // ---- R2 paint-can preview ----------------------------------------------------------------

    static (StudioArt Art, FakeInstall Install, ImageCache Images) Art()
    {
        var install = new FakeInstall();
        var images = new ImageCache(new AssetStore(install.Root));
        return (new StudioArt(images, LevelDefinition.Older), install, images);
    }

    [Fact]
    public void PaintCanPreview_HatchIsTransparent_WithACurrentColourOutline()
    {
        var (art, install, images) = Art();
        using (install)
        using (images)
        using (var form = new DibSurface(640, 480, 0x123456))
        {
            var s = new StudioState { FillPattern = 2 }; // horizontal hatch
            art.DrawPaintCanPreview(form, s);
            int red = s.CurrentColor;
            Assert.Equal(red, form.GetRgb(19, 299));      // a polygon vertex: the 1-px outline
            Assert.Equal(red, form.GetRgb(30, 307));      // a hatch row (y = 3 mod 8 with the form's brush origin)
            Assert.Equal(0x123456, form.GetRgb(30, 309)); // between hatch rows the art shows through
            Assert.Equal(0x123456, form.GetRgb(5, 310));  // outside the splat
        }
    }

    [Fact]
    public void PaintCanPreview_Style0_IsOutlineOnly_AndPatternsHaveNoOutline()
    {
        var (art, install, images) = Art();
        using (install)
        using (images)
        using (var form = new DibSurface(640, 480, 0x123456))
        {
            var s = new StudioState { FillPattern = 0 };
            art.DrawPaintCanPreview(form, s);
            Assert.Equal(s.CurrentColor, form.GetRgb(19, 299));
            Assert.Equal(0x123456, form.GetRgb(40, 312)); // the inside keeps the art
            Assert.Equal((15, 299, 65, 325), PaintMath.PaintCanBounds); // the gradient box
        }
    }

    // ---- R4 mirror -----------------------------------------------------------------------------

    void Stripes()
    {
        for (int x = 0; x < 60; x++) Gdi.FillRect(_canvas.Hdc, x, 0, x + 1, 60, 0x010000 * x + 0x80);
        _backing.CopyFrom(_canvas);
    }

    [Fact]
    public void Reverse_MapsColumnXToLPlusRMinusX()
    {
        Stripes();
        _s.Mode = 0x42;
        _s.Flip = 4;
        Drag(10, 10, 20, 30);
        for (int x = 10; x < 20; x++) Assert.Equal(0x010000 * (10 + 20 - x) + 0x80, _canvas.GetRgb(x, 15));
        Assert.Equal(0x010000 * 20 + 0x80, _canvas.GetRgb(20, 15)); // clipped to the box: column r is unchanged
        Assert.Equal(0x010000 * 9 + 0x80, _canvas.GetRgb(9, 15));
        Assert.Equal(25, ChangeItMath.MirrorSource(10, 20, 5));
        Assert.Equal((11, 10), ChangeItMath.MirrorSourceOrigin(new Box(10, 10, 20, 30), horizontal: true));
        Assert.Equal((10, 11), ChangeItMath.MirrorSourceOrigin(new Box(10, 10, 20, 30), horizontal: false));
    }

    [Fact]
    public void Reverse_UpsideDown_MapsRowYToTPlusBMinusY()
    {
        for (int y = 0; y < 60; y++) Gdi.FillRect(_canvas.Hdc, 0, y, 60, y + 1, 0x000100 * y + 0x80);
        _s.Mode = 0x42;
        _s.Flip = 3;
        Drag(10, 10, 30, 20);
        for (int y = 10; y < 20; y++) Assert.Equal(0x000100 * (10 + 20 - y) + 0x80, _canvas.GetRgb(15, y));
    }

    [Fact]
    public void CopyFlipper_MirrorsWithTheSameRule()
    {
        Stripes();
        _s.Mode = 0x47;
        _s.Flip = 4;
        Drag(10, 10, 20, 30);  // select (10,10)-(20,30)
        Assert.Equal(0x4A, _s.Mode);
        _d.Down(12, 12);      // grab
        _d.Move(112, 12);
        _d.Up(112, 12);       // drop 100 px to the right
        for (int i = 0; i < 10; i++) Assert.Equal(0x010000 * (20 - i) + 0x80, _canvas.GetRgb(110 + i, 15));
    }

    // ---- R5 zoom smaller -----------------------------------------------------------------------

    [Fact]
    public void ZoomSmaller_Keeps1PxLinesSolid()
    {
        Gdi.Line(_canvas.Hdc, 200, 0, 200, 384, 0x000000);
        Gdi.Line(_canvas.Hdc, 0, 201, 556, 201, 0x000000);
        _s.Mode = 0x41;
        _s.Size = 1;
        Drag(50, 50, 250, 200);
        bool column = Enumerable.Range(50, 250 - 50).Any(x => Enumerable.Range(52, 140).All(y => _canvas.GetRgb(x, y) == 0));
        bool row = Enumerable.Range(50, 150).Any(y => Enumerable.Range(52, 190).All(x => _canvas.GetRgb(x, y) == 0));
        Assert.True(column, "the vertical line broke up");
        Assert.True(row, "the horizontal line broke up");
    }

    // ---- R6 marquee ------------------------------------------------------------------------------

    [Fact]
    public void SelectionMarquee_IsDiagonallyStriped_AsObserved()
    {
        // The observed box: inner rectangle (150,150)-(301,301), frame 146..304 (frame coordinates).
        var px = ChangeItMath.SelectionMarquee(new Box(150, 150, 301, 301)).ToDictionary(p => (p.X, p.Y), p => p.Blue);
        static int M(int v) => ((v % 6) + 6) % 6;
        foreach (var ((x, y), blue) in px)
        {
            bool inCorner = (x <= 150 || x >= 300) && (y <= 150 || y >= 300);
            if (inCorner) continue;
            bool expected = y <= 150 ? M(x - y) < 3          // top: "\" stripes
                : x <= 150 ? M(x - y) is 0 or 4 or 5           // left
                : M(x + y) < 3;                                // right and bottom: "/" stripes
            Assert.True(expected == blue, $"({x},{y})");
        }
        Assert.Equal(159 * 159 - 149 * 149, px.Count); // every pixel of the 5-px band once
    }

    // ---- R7 dialog close box -----------------------------------------------------------------------

    sealed class ClosableDialog : DialogOverlay
    {
        public int Closed, ClientClicks;
        public ClosableDialog() : base("About", 100, 100, 200, 100) { SystemMenu = true; CloseBox = true; }
        protected override void DrawClient(DibSurface frame, IntPtr hdc) { }
        protected override void ClientDown(Point p, MouseButtons button) => ClientClicks++;
        protected override void CloseBoxClicked() => Closed++;
    }

    [Fact]
    public void CaptionCloseBox_ClosesOnPressAndRelease()
    {
        var d = new ClosableDialog();
        var x = Win95.CloseButtonRect(d.Outer);
        Assert.Equal(new Rectangle(d.Outer.Right - 3 - 2 - 16, d.Outer.Top + 3 + 2, 16, 14), x);
        var mid = new Point(x.X + 8, x.Y + 7);
        d.MouseDown(mid, MouseButtons.Left);
        d.MouseMove(new Point(0, 0)); // off the button: released there, nothing happens
        d.MouseUp(new Point(0, 0));
        Assert.Equal(0, d.Closed);
        d.MouseDown(mid, MouseButtons.Left);
        d.MouseUp(mid);
        Assert.Equal(1, d.Closed);
        Assert.Equal(0, d.ClientClicks);
        using var frame = new DibSurface(640, 480, 0);
        d.Draw(frame); // icon, caption and button render without error
        Assert.Equal(0xC0C0C0, frame.GetRgb(x.X + 2, x.Y + 2));
    }

    // ---- R8, R9, R10, R14 layout math -------------------------------------------------------

    [Fact]
    public void ShadeMarker_DiagonalsRunCornerToCorner()
    {
        Assert.Equal(new[] { (16, 196, 32, 212), (32, 196, 16, 212) }, LinesMath.ShadeMarkerDiagonals(16, 196));
        using var form = new DibSurface(640, 480, 0);
        using var install = new FakeInstall();
        using var images = new ImageCache(new AssetStore(install.Root));
        new StudioArt(images, LevelDefinition.Older).DrawShadeGrid(form, new StudioState { ShadeIndex = 0 });
        int mark = ColorTable.Get(ColorTable.Yellow); // the marker is yellow on the red colour group
        Assert.Equal(mark, form.GetRgb(16, 180)); // the second diagonal starts at (r, t), outside the frame
        Assert.Equal(mark, form.GetRgb(15, 181));
        Assert.Equal(mark, form.GetRgb(2, 194));  // ... and stops before (l, b)
        Assert.Equal(mark, form.GetRgb(14, 194)); // the first stops before (r, b)
        Assert.NotEqual(mark, form.GetRgb(16, 196));
        Assert.NotEqual(mark, form.GetRgb(14, 180 + 1)); // the old (r-1, t) start is gone
    }

    [Fact]
    public void TabTooltip_AnchorsAtXPlus80()
    {
        Assert.Equal((198, 70), ToolbarLayout.TabTooltipAnchor(200, 70));
        Assert.Equal(90, ToolbarLayout.IconClipBottomFor(8, 8));  // R10: the selected tab's y = 90 row covers its icon
        Assert.Equal(91, ToolbarLayout.IconClipBottomFor(7, 8));
    }

    // ---- R12, R13 input ------------------------------------------------------------------------

    sealed class Fx : IStudioEffects
    {
        public readonly List<string> Log = new();
        public void PlaySound(int snd) => Log.Add($"snd{snd}");
        public void Snapshot() => Log.Add("snapshot");
        public void RestoreUndo() => Log.Add("undo");
        public void DrawOptionStrip(int slot) => Log.Add($"strip{slot}");
        public void RedrawShadeGrid() => Log.Add("grid");
        public void RedrawColumn() => Log.Add("tail");
        public void TabChanged(int tab, string sheet) => Log.Add($"tab{tab}");
        public void RunAction(string name) => Log.Add(name);
        public void OpenHelp() => Log.Add("help");
        public void AskQuit() => Log.Add("quit");
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(1, 9)]
    [InlineData(2, 8)]
    [InlineData(2, 9)]
    public void Younger_EmptySlots_RemoveTheMarker(int tool, int slot)
    {
        var s = new StudioState();
        var fx = new Fx();
        var input = new StudioInput(LevelDefinition.Younger, s, fx);
        if (tool != 1) input.TabClick(tool - 1);
        input.OptionClick(0);
        int mode = s.Mode;
        input.OptionClick(slot);
        Assert.Equal(12, s.LastOptionSlot);
        Assert.Equal("strip12", fx.Log[^1]);
        Assert.Equal(mode, s.Mode);
    }

    [Fact]
    public void ClickingTheSelectedTab_DoesNothing()
    {
        var s = new StudioState();
        var fx = new Fx();
        var input = new StudioInput(LevelDefinition.Older, s, fx);
        input.OptionClick(3);
        fx.Log.Clear();
        int mode = s.Mode;
        Assert.False(input.TabClick(0)); // tab 0 is selected at startup
        Assert.Empty(fx.Log);
        Assert.Equal(mode, s.Mode);
        Assert.Equal(3, s.LastOptionSlot); // the marker stays
        Assert.True(input.TabClick(4));
        fx.Log.Clear();
        Assert.False(input.TabClick(4));
        Assert.Empty(fx.Log);
    }

    // ---- R15 options row --------------------------------------------------------------------------

    [Fact]
    public void OptionsRow_BlitsThenAdvances_FromRow0()
    {
        var a = new OptionsRowAnimation();
        Assert.Null(a.Tick(124)); // nothing before the first tick
        var rows = new List<int>();
        for (int i = 0; i < 12; i++) rows.Add(a.Tick(i == 0 ? 1 : 125)!.Value);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 4, 3, 2, 1, 0, 1 }, rows);
        a.Reset(); // tab click
        Assert.Null(a.Tick(100));
        Assert.Equal(0, a.Tick(25));
    }

    // ---- R16, R17 pacing ----------------------------------------------------------------------------

    /// <summary>The host's scheduling rule: a step once more than IntervalMs has passed, 15.625-ms ticks. Returns the run time.</summary>
    static double RunTime(IAnimation a)
    {
        double t = 0, elapsed = 0;
        const double tick = 15.625;
        while (true)
        {
            t += tick;
            elapsed += tick;
            if (elapsed > a.IntervalMs)
            {
                elapsed = 0;
                if (!a.Step()) return t;
            }
            if (t > 60000) throw new InvalidOperationException("runaway");
        }
    }

    [Fact]
    public void MrBrush_WaitsAbout1400ms_ThenAbout110msPerFrame()
    {
        var a = new WackyTools.MrBrush.Animation(_c, 200, 50);
        Assert.Equal(1400, a.IntervalMs);
        double t = 0, elapsed = 0;
        while (!a.Started) { t += 15.625; elapsed += 15.625; if (elapsed > a.IntervalMs) { elapsed = 0; a.Step(); } }
        Assert.InRange(t, 1400, 1420);
        Assert.Equal(100, a.IntervalMs); // "more than 100 ms": 7 host ticks, ~109 ms
    }

    [Fact]
    public void EraserEffects_RunAtTheObservedSpeed()
    {
        Assert.InRange(RunTime(EraserTools.NewSlate(_c)), 0, 40);
        Assert.InRange(RunTime(EraserTools.WindowShade(_c)), 0, 40);
        Assert.InRange(RunTime(EraserTools.Crumple(_c)), 120, 160);
        Assert.Equal(0xFFFFFF, _canvas.GetRgb(300, 200));
    }
}

using System.Text;

namespace Crayons.App.Audio;

/// <summary>Sends one MCI command string (mciSendString). Abstracted so the player can be tested headless.</summary>
public interface IMciCommands
{
    /// <summary>Returns the MCI error code (0 = success); <paramref name="result"/> is the return string.</summary>
    int Send(string command, out string result);
}

/// <summary>The real winmm implementation.</summary>
public sealed class WinmmMci : IMciCommands
{
    [System.Runtime.InteropServices.DllImport("winmm.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "mciSendStringW")]
    static extern int mciSendString(string command, StringBuilder? ret, int retLen, IntPtr callback);

    public int Send(string command, out string result)
    {
        var sb = new StringBuilder(64);
        int rc = mciSendString(command, sb, sb.Capacity, IntPtr.Zero);
        result = sb.ToString();
        return rc;
    }
}

/// <summary>
/// Plays one MIDI song at a time through the MCI sequencer on a dedicated worker thread (spec §9.1 R1).
/// MCI "open" takes about 1 s and "play" about 0.35-0.65 s on Windows 10/11 (the GS synth starts up),
/// so no MCI command may run on the UI thread. The UI side only records the wanted song; the worker
/// opens, plays, polls "status mode" and closes. A song counts as finished only when MCI reports
/// "stopped" (or the device fails): right after "play" it reports "seeking" for about 100 ms, and
/// treating that as the end made the remake skip through every track with a 1.6-s UI stall each.
/// </summary>
public sealed class MciMusicPlayer : IDisposable
{
    public const string Alias = "crayonsmusic";

    readonly IMciCommands _mci;
    readonly int _pollMs;
    readonly Thread _worker;
    readonly AutoResetEvent _wake = new(false);
    readonly object _gate = new();

    // Written by the UI thread under _gate.
    string? _wanted;
    int _wantedGeneration;
    bool _disposed;

    // Written by the worker under _gate.
    int _finishedGeneration;   // the last generation that ended (played out, failed or was replaced)
    bool _lastFailed;

    public MciMusicPlayer(IMciCommands mci, int pollMs = 250)
    {
        _mci = mci;
        _pollMs = pollMs;
        _worker = new Thread(Run) { IsBackground = true, Name = "MCI music" };
        _worker.Start();
    }

    /// <summary>True when an MCI "status mode" answer means the song has ended.</summary>
    public static bool IsFinished(int errorCode, string mode) =>
        errorCode != 0 || mode.Equals("stopped", StringComparison.OrdinalIgnoreCase);

    /// <summary>Queues a song to start from the beginning (replacing the current one). Never blocks.</summary>
    public void Play(string midiPath)
    {
        lock (_gate)
        {
            _wanted = midiPath;
            _wantedGeneration++;
            _lastFailed = false;
        }
        _wake.Set();
    }

    /// <summary>Stops and closes the current song. Never blocks.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (_wanted == null) return;
            _wanted = null;
            _wantedGeneration++;
        }
        _wake.Set();
    }

    /// <summary>True from <see cref="Play"/> until the song has played out (or failed to open/play).</summary>
    public bool IsPlaying
    {
        get { lock (_gate) return _wanted != null && _finishedGeneration != _wantedGeneration; }
    }

    /// <summary>True when the last requested song could not be opened or played.</summary>
    public bool LastFailed
    {
        get { lock (_gate) return _lastFailed && _finishedGeneration == _wantedGeneration; }
    }

    void Run()
    {
        int handled = 0;   // the last request generation acted on
        bool open = false; // the alias is open on the device
        while (true)
        {
            _wake.WaitOne(_pollMs);
            string? wanted;
            int generation;
            lock (_gate)
            {
                if (_disposed) break;
                wanted = _wanted;
                generation = _wantedGeneration;
            }

            try { Service(wanted, generation, ref handled, ref open); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Music must never take the app down: drop the song and report it as failed.
                System.Diagnostics.Debug.WriteLine($"MCI music: {ex.Message}");
                if (open) { try { Close(); } catch (Exception) { /* the device is already unusable */ } }
                open = false;
                Finish(generation, failed: true);
            }
        }
        if (open) Close();
    }

    /// <summary>One worker pass: act on a new request, or poll the open song.</summary>
    void Service(string? wanted, int generation, ref int handled, ref bool open)
    {
        if (generation != handled)
        {
            // A new request (play or stop) replaces whatever is open.
            handled = generation;
            if (open) { Close(); open = false; }
            if (wanted == null) return;
            bool ok = _mci.Send($"open \"{wanted}\" type sequencer alias {Alias}", out _) == 0;
            if (ok)
            {
                open = true;
                ok = _mci.Send($"play {Alias} from 0", out _) == 0;
            }
            if (!ok)
            {
                if (open) { Close(); open = false; }
                Finish(generation, failed: true);
            }
            return;
        }

        if (!open) return;
        int rc = _mci.Send($"status {Alias} mode", out var mode);
        if (IsFinished(rc, mode))
        {
            Close();
            open = false;
            Finish(generation, failed: false);
        }
    }

    void Close()
    {
        _mci.Send($"stop {Alias}", out _);
        _mci.Send($"close {Alias}", out _);
    }

    void Finish(int generation, bool failed)
    {
        lock (_gate)
        {
            if (generation != _wantedGeneration) return; // superseded meanwhile
            _finishedGeneration = generation;
            _lastFailed = failed;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _wake.Set();
        _worker.Join(5000); // closing the sequencer takes < 0.2 s
        _wake.Dispose();
    }
}

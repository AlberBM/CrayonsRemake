using System.Runtime.InteropServices;

namespace Crayons.App.Audio;

/// <summary>
/// Plays the original WAV effects and MIDI songs through the Windows multimedia
/// API (the same path the 1997 program used via MCI), so MIDI goes through the
/// Windows GS synthesizer and sounds like the original did on Windows.
/// All MCI work runs on <see cref="MciMusicPlayer"/>'s worker thread; nothing here blocks the UI.
/// </summary>
public sealed class WinmmAudio : IDisposable
{
    const uint SND_ASYNC = 0x0001, SND_NODEFAULT = 0x0002, SND_MEMORY = 0x0004, SND_PURGE = 0x0040;

    [DllImport("winmm.dll", SetLastError = true)]
    static extern bool PlaySound(byte[]? sound, IntPtr hmod, uint flags);

    readonly MciMusicPlayer _music;
    byte[]? _currentSound; // must stay alive while PlaySound(SND_MEMORY|SND_ASYNC) plays

    public WinmmAudio() : this(new WinmmMci()) { }

    /// <summary>Uses the given MCI back end for music (tests pass a fake one).</summary>
    public WinmmAudio(IMciCommands mci, int musicPollMs = 250) => _music = new MciMusicPlayer(mci, musicPollMs);

    public bool SoundsEnabled { get; set; } = true;
    public bool MusicEnabled { get; set; } = true;
    public string? CurrentSong { get; private set; }

    /// <summary>Plays a WAV image asynchronously, replacing any effect already playing.</summary>
    public void PlayEffect(byte[] wav)
    {
        if (!SoundsEnabled) return;
        _currentSound = wav;
        PlaySound(wav, IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
    }

    public void StopEffect()
    {
        PlaySound(null, IntPtr.Zero, SND_PURGE);
        _currentSound = null;
    }

    /// <summary>
    /// Starts a MIDI file from the beginning on the music thread. Returns false when music is
    /// disabled or the file does not exist; an MCI failure shows up later as <see cref="SongFailed"/>.
    /// </summary>
    public bool PlaySong(string midiPath)
    {
        StopSong();
        if (!MusicEnabled || !File.Exists(midiPath)) return false;
        CurrentSong = midiPath;
        _music.Play(midiPath);
        return true;
    }

    /// <summary>True while the current song is starting or playing (poll to chain the next track). Never blocks.</summary>
    public bool IsSongPlaying => CurrentSong != null && _music.IsPlaying;

    /// <summary>True when MCI could not open or play the current song.</summary>
    public bool SongFailed => CurrentSong != null && _music.LastFailed;

    public void StopSong()
    {
        if (CurrentSong == null) return;
        _music.Stop();
        CurrentSong = null;
    }

    public void Dispose()
    {
        StopSong();
        _music.Dispose();
        StopEffect();
    }
}

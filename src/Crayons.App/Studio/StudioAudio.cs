using Crayons.App.Audio;
using Crayons.Core.Assets;
using Crayons.Core.Studio;

namespace Crayons.App.Studio;

/// <summary>
/// Studio sound effects, help voice clips and the PC1..PC20 music loop (spec §3.7).
/// Effects and voices share one channel: a new sound cuts off the previous one.
/// </summary>
public sealed class StudioAudio
{
    readonly WinmmAudio _audio;
    readonly AssetStore _store;
    readonly StudioState _state;
    readonly string _mediaRoot;
    readonly Dictionary<string, byte[]?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public StudioAudio(WinmmAudio audio, AssetStore store, StudioState state)
    {
        _audio = audio;
        _store = store;
        _state = state;
        _mediaRoot = MediaRoot(store);
    }

    /// <summary>The voice/art media root: first line of crayons.ini when present, otherwise the install.</summary>
    public static string MediaRoot(AssetStore store)
    {
        if (store.Exists("crayons.ini"))
        {
            var first = store.ReadLines("crayons.ini").FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(first) && Directory.Exists(first)) return first;
        }
        return store.Root;
    }

    byte[]? Load(string fullPath)
    {
        if (!_cache.TryGetValue(fullPath, out var data))
            _cache[fullPath] = data = File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
        return data;
    }

    /// <summary>The bytes of snd\snd&lt;n&gt;.asl, or null.</summary>
    public byte[]? EffectBytes(int n) => Load(_store.PathOf(Path.Combine("SND", $"SND{n}.ASL")));

    /// <summary>Plays snd\snd&lt;n&gt;.asl when SoundOn.</summary>
    public void Effect(int n)
    {
        if (!_state.SoundOn) return;
        if (EffectBytes(n) is { } wav) _audio.PlayEffect(wav);
    }

    /// <summary>Plays the help voice snd\hv&lt;index+1&gt;.asl (media root) when Audio Help is on.</summary>
    public void Voice(int captionIndex)
    {
        if (!_state.AudioHelpOn) return;
        if (Load(Path.Combine(_mediaRoot, "SND", $"HV{captionIndex + 1}.ASL")) is { } wav) _audio.PlayEffect(wav);
    }

    /// <summary>Starts the next track (PC1 after PC20).</summary>
    public void PlayNextSong()
    {
        _state.MusicTrack = _state.MusicTrack + 1 > 20 ? 1 : _state.MusicTrack + 1;
        _state.MusicPlaying = _audio.PlaySong(_store.PathOf(Path.Combine("SND", $"PC{_state.MusicTrack}.MID")));
    }

    /// <summary>
    /// Host-tick poll: when the current track ends, close it and start the next one if music is on.
    /// A track MCI could not play ends the chain, as a failed start always did. Never blocks (R1).
    /// </summary>
    public void PollMusic()
    {
        if (!_state.MusicPlaying || _audio.IsSongPlaying) return;
        bool failed = _audio.SongFailed;
        _audio.StopSong();
        _state.MusicPlaying = false;
        if (_state.MusicOn && !failed) PlayNextSong();
    }

    public void StopMusic()
    {
        _audio.StopSong();
        _state.MusicPlaying = false;
    }
}

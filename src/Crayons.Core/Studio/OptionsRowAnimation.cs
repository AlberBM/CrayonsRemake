namespace Crayons.Core.Studio;

/// <summary>
/// The options-row animation (Timer1, 125 ms, spec §4.3, §8.7): each tick blits the current sheet row
/// at (100,10) and then advances, ping-ponging 0..5..0 (spec §9.1 R15, Q8). The first row shown, after
/// startup and after every tab click, is row 0, and nothing is drawn before the first tick.
/// </summary>
public sealed class OptionsRowAnimation
{
    public const int IntervalMs = 125, LastFrame = 5;

    int _frame, _dir, _elapsed;

    /// <summary>Restarts at row 0 with a full interval before the first blit (startup, tab click).</summary>
    public void Reset()
    {
        _frame = 0;
        _dir = 0;
        _elapsed = 0;
    }

    /// <summary>Advances the timer; returns the row to blit when Timer1 fires, otherwise null.</summary>
    public int? Tick(int elapsedMs)
    {
        _elapsed += elapsedMs;
        if (_elapsed < IntervalMs) return null;
        _elapsed %= IntervalMs;
        int row = _frame; // blit, then advance
        _frame += _dir == 0 ? 1 : -1;
        if (_frame > LastFrame) { _frame = LastFrame - 1; _dir = 1; }
        if (_frame < 0) { _frame = 1; _dir = 0; }
        return row;
    }
}

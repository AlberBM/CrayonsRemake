namespace Crayons.Core;

/// <summary>
/// Delphi 1 compatible pseudo-random generator (System.Random / Randomize).
/// Tool effects depend on the exact sequence and on the order of calls, so every
/// random draw described in the spec must go through one shared instance.
/// </summary>
public sealed class DelphiRandom
{
    public uint Seed { get; set; }

    public DelphiRandom(uint seed) => Seed = seed;

    /// <summary>Seeds from the clock, like Randomize.</summary>
    public static DelphiRandom FromClock() => new((uint)Environment.TickCount64 ^ (uint)DateTime.Now.Ticks);

    /// <summary>Random(n): an integer in 0..n-1 (0 when n &lt;= 0).</summary>
    public int Next(int n)
    {
        Seed = unchecked(Seed * 0x08088405u + 1u);
        if (n <= 0) return 0;
        return (int)(((ulong)Seed * (uint)n) >> 32);
    }
}

/// <summary>Numeric helpers matching the original's x87 behaviour.</summary>
public static class DMath
{
    /// <summary>Delphi Round: half to even, on a float32 value.</summary>
    public static int Round(float v) => (int)Math.Round(v, MidpointRounding.ToEven);

    /// <summary>Round(Sqrt(dx²+dy²)) with float32 intermediates.</summary>
    public static int Dist(int dx, int dy)
    {
        float fx = Math.Abs(dx), fy = Math.Abs(dy);
        float s = (float)Math.Sqrt(fx * fx + fy * fy);
        return Round(s);
    }

    /// <summary>Pascal div: truncates toward zero.</summary>
    public static int Div(int a, int b) => a / b;
}

using Crayons.Core;

namespace Crayons.Tests;

public class CoreMathTests
{
    [Fact]
    public void DelphiRandom_FollowsLinearCongruence()
    {
        var r = new DelphiRandom(0);
        // seed1 = 1, seed2 = 0x08088406, seed3 = 0x08088406*0x08088405+1 mod 2^32
        Assert.Equal(0, r.Next(10));                       // (1*10)>>32
        Assert.Equal(1u, r.Seed);
        int second = r.Next(100);
        Assert.Equal(0x08088406u, r.Seed);
        Assert.Equal((int)((0x08088406ul * 100) >> 32), second);
        r.Next(1);
        Assert.Equal(unchecked(0x08088406u * 0x08088405u + 1u), r.Seed);
    }

    [Fact]
    public void DelphiRandom_StaysInRange()
    {
        var r = new DelphiRandom(12345);
        for (int i = 0; i < 10000; i++)
        {
            int v = r.Next(159);
            Assert.InRange(v, 0, 158);
        }
        Assert.Equal(0, r.Next(0));
    }

    [Theory]
    [InlineData(2.5f, 2)]
    [InlineData(3.5f, 4)]
    [InlineData(-2.5f, -2)]
    [InlineData(2.4f, 2)]
    public void Round_IsHalfToEven(float v, int expected) => Assert.Equal(expected, DMath.Round(v));

    [Theory]
    [InlineData(3, 4, 5)]
    [InlineData(-6, 8, 10)]
    [InlineData(1, 1, 1)]
    [InlineData(8, 0, 8)]
    public void Dist_RoundsEuclidean(int dx, int dy, int expected) => Assert.Equal(expected, DMath.Dist(dx, dy));

    [Fact]
    public void ColorTable_HasDocumentedEntries()
    {
        Assert.Equal(211, ColorTable.Count);
        Assert.Equal(0xff0000, ColorTable.Get(9, 5));
        Assert.Equal(0xffffff, ColorTable.Get(ColorTable.White));
        Assert.Equal(0x000000, ColorTable.Get(ColorTable.Black));
        Assert.Equal(0x0000ffu, ColorTable.ColorRef(ColorTable.Red));
    }
}

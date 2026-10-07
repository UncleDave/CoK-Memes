namespace ChampionsOfKhazad.Bot.Tests;

public class RandomUtilsTests
{
    [Fact]
    public void ZeroChanceNeverSucceeds()
    {
        for (var i = 0; i < 100; i++)
            Assert.False(RandomUtils.Roll(0).Success);
    }

    [Fact]
    public void FullChanceAlwaysSucceeds()
    {
        for (var i = 0; i < 100; i++)
            Assert.True(RandomUtils.Roll(100).Success);
    }

    [Fact]
    public void FullChanceWithCustomMaximumAlwaysSucceeds()
    {
        for (var i = 0; i < 100; i++)
            Assert.True(RandomUtils.Roll(1000, 1000).Success);
    }

    [Theory]
    [InlineData(0.1, 0.0005, true)]
    [InlineData(0.1, 0.001, false)]
    [InlineData(0.1, 0.005, false)]
    [InlineData(1, 0.005, true)]
    [InlineData(1, 0.01, false)]
    [InlineData(20, 0.199, true)]
    [InlineData(20, 0.2, false)]
    [InlineData(0, 0, false)]
    [InlineData(100, 0.999999, true)]
    public void ChanceIsAPercentage(double chance, double sample, bool expectedSuccess)
    {
        var result = RandomUtils.Roll(chance, random: new FixedRandom(sample));

        Assert.Equal(expectedSuccess, result.Success);
        Assert.Equal(sample * 100, result.Roll);
    }

    private sealed class FixedRandom(double sample) : Random
    {
        public override double NextDouble() => sample;
    }
}

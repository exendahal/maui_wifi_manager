using MauiWifiManager.Abstractions;

namespace MauiWifiManager.Tests;

public class WifiSignalTests
{
    [Theory]
    [InlineData(-120, 0)]
    [InlineData(-100, 0)]
    [InlineData(-89, 0)]
    [InlineData(-88, 1)]
    [InlineData(-78, 1)]
    [InlineData(-77, 2)]
    [InlineData(-67, 2)]
    [InlineData(-66, 3)]
    [InlineData(-56, 3)]
    [InlineData(-55, 4)]
    [InlineData(-30, 4)]
    public void GetSignalLevel_DefaultFiveLevels(int rssi, int expected)
    {
        Assert.Equal(expected, WifiSignal.GetSignalLevel(rssi));
    }

    [Fact]
    public void GetSignalLevel_CustomLevelCount_SpansFullRange()
    {
        Assert.Equal(0, WifiSignal.GetSignalLevel(-100, numLevels: 10));
        Assert.Equal(9, WifiSignal.GetSignalLevel(-55, numLevels: 10));
    }

    [Fact]
    public void GetSignalLevel_IsMonotonic()
    {
        int previous = 0;
        for (int rssi = -110; rssi <= -40; rssi++)
        {
            int level = WifiSignal.GetSignalLevel(rssi);
            Assert.True(level >= previous, $"Level dropped at {rssi} dBm");
            previous = level;
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-3)]
    public void GetSignalLevel_FewerThanTwoLevels_Throws(int numLevels)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WifiSignal.GetSignalLevel(-60, numLevels));
    }

    [Theory]
    [InlineData(-120, 0)]
    [InlineData(-100, 0)]
    [InlineData(-75, 50)]
    [InlineData(-60, 80)]
    [InlineData(-50, 100)]
    [InlineData(-20, 100)]
    public void GetSignalPercentage_ClampsToZeroToHundred(int rssi, int expected)
    {
        Assert.Equal(expected, WifiSignal.GetSignalPercentage(rssi));
    }
}

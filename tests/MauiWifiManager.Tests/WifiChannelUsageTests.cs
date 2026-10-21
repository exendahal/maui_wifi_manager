using MauiWifiManager.Abstractions;

namespace MauiWifiManager.Tests;

public class WifiChannelUsageTests
{
    private static NetworkData Network(WifiFrequencyBand band, int? channel, int? rssi = null) =>
        new() { FrequencyBand = band, ChannelNumber = channel, RssiDbm = rssi };

    [Fact]
    public void FromScanResults_Empty_ReturnsEmpty()
    {
        Assert.Empty(WifiChannelUsage.FromScanResults(Array.Empty<NetworkData>()));
    }

    [Fact]
    public void FromScanResults_GroupsByBandAndChannel()
    {
        var usage = WifiChannelUsage.FromScanResults(new[]
        {
            Network(WifiFrequencyBand.Band2_4GHz, 6, -70),
            Network(WifiFrequencyBand.Band2_4GHz, 6, -40),
            Network(WifiFrequencyBand.Band2_4GHz, 6, -55),
            Network(WifiFrequencyBand.Band5GHz, 36, -60),
        });

        Assert.Equal(2, usage.Count);
        var ch6 = usage.Single(u => u.ChannelNumber == 6);
        Assert.Equal(WifiFrequencyBand.Band2_4GHz, ch6.FrequencyBand);
        Assert.Equal(3, ch6.NetworkCount);
        Assert.Equal(-40, ch6.StrongestRssiDbm);
    }

    [Fact]
    public void FromScanResults_SameChannelNumberOnDifferentBands_StaysSeparate()
    {
        // Channel 1 exists on both 2.4 GHz and 6 GHz.
        var usage = WifiChannelUsage.FromScanResults(new[]
        {
            Network(WifiFrequencyBand.Band2_4GHz, 1),
            Network(WifiFrequencyBand.Band6GHz, 1),
        });

        Assert.Equal(2, usage.Count);
        Assert.All(usage, u => Assert.Equal(1, u.NetworkCount));
    }

    [Fact]
    public void FromScanResults_SkipsNetworksWithoutChannel()
    {
        var usage = WifiChannelUsage.FromScanResults(new[]
        {
            Network(WifiFrequencyBand.Unknown, null),
            Network(WifiFrequencyBand.Band2_4GHz, 11),
        });

        Assert.Single(usage);
        Assert.Equal(11, usage[0].ChannelNumber);
    }

    [Fact]
    public void FromScanResults_OrdersBusiestFirstThenBandThenChannel()
    {
        var usage = WifiChannelUsage.FromScanResults(new[]
        {
            Network(WifiFrequencyBand.Band5GHz, 149),
            Network(WifiFrequencyBand.Band5GHz, 36),
            Network(WifiFrequencyBand.Band2_4GHz, 11),
            Network(WifiFrequencyBand.Band2_4GHz, 1),
            Network(WifiFrequencyBand.Band2_4GHz, 1),
        });

        Assert.Equal(new[] { 1, 11, 36, 149 }, usage.Select(u => u.ChannelNumber));
    }

    [Fact]
    public void FromScanResults_NoRssiReported_StrongestIsNull()
    {
        var usage = WifiChannelUsage.FromScanResults(new[] { Network(WifiFrequencyBand.Band5GHz, 44) });

        Assert.Null(usage[0].StrongestRssiDbm);
    }

    [Fact]
    public void FromScanResults_MixedRssi_IgnoresMissingValues()
    {
        var usage = WifiChannelUsage.FromScanResults(new[]
        {
            Network(WifiFrequencyBand.Band5GHz, 44),
            Network(WifiFrequencyBand.Band5GHz, 44, -80),
        });

        Assert.Equal(-80, usage[0].StrongestRssiDbm);
    }
}

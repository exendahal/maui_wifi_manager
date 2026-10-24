using WifiManager.MacVendors;

namespace WifiManager.MacVendors.Tests;

public class MacVendorRegistryTests
{
    [Theory]
    [InlineData("00:1B:63:84:45:E6", "Apple")]
    [InlineData("3C:CD:57:73:6E:E2", "Xiaomi Mobile Software")]
    [InlineData("3c-cd-57-73-6e-e2", "Xiaomi Mobile Software")]
    [InlineData("3CCD57736EE2", "Xiaomi Mobile Software")]
    [InlineData("3ccd.5773.6ee2", "Xiaomi Mobile Software")]
    public void Lookup_KnownPrefix_ReturnsCleanedVendor(string mac, string vendor)
    {
        Assert.Equal(vendor, MacVendorRegistry.Lookup(mac));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a mac")]
    [InlineData("00:1B:63")]
    [InlineData("00:1B:63:84:45:E6:01")]
    [InlineData("GG:1B:63:84:45:E6")]
    public void Lookup_Malformed_ReturnsNull(string? mac)
    {
        Assert.Null(MacVendorRegistry.Lookup(mac));
    }

    [Fact]
    public void Lookup_PrivateAddress_ReturnsNull()
    {
        // 0x02 bit of the first byte set: a randomized address, which no manufacturer owns.
        Assert.Null(MacVendorRegistry.Lookup("02:1B:63:84:45:E6"));
    }

    [Fact]
    public void Count_CoversTheWholeRegistry()
    {
        Assert.InRange(MacVendorRegistry.Count, 30_000, 60_000);
    }

    [Fact]
    public void Read_SkipsMalformedLines()
    {
        using var stream = new MemoryStream("3CCD57\tXiaomi\nnot a line\nZZZZZZ\tBad\n001B63\tApple\n"u8.ToArray());

        var table = MacVendorRegistry.Read(stream);

        Assert.Equal(2, table.Count);
        Assert.Equal("Apple", table[0x001B63]);
    }

    [Fact]
    public void Lookup_IsSafeFromManyThreadsAtOnce()
    {
        var results = new string?[64];

        Parallel.For(0, results.Length, i => results[i] = MacVendorRegistry.Lookup("3C:CD:57:73:6E:E2"));

        Assert.All(results, vendor => Assert.Equal("Xiaomi Mobile Software", vendor));
    }
}

using MauiWifiManager.Abstractions;

namespace MauiWifiManager.Tests;

public class MacVendorTests
{
    [Theory]
    [InlineData("00:1B:63:84:45:E6", "Apple")]
    [InlineData("3c-cd-57-73-6e-e2", "Xiaomi Mobile Software")]
    [InlineData("3CCD57736EE2", "Xiaomi Mobile Software")]
    [InlineData("3ccd.5773.6ee2", "Xiaomi Mobile Software")]
    public void Lookup_KnownPrefix_ReturnsCleanedVendor(string mac, string vendor)
    {
        Assert.Equal(vendor, MacVendor.Lookup(mac));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a mac")]
    [InlineData("00:1B:63")]
    [InlineData("00:1B:63:84:45:E6:01")]
    public void Lookup_Malformed_ReturnsNull(string? mac)
    {
        Assert.Null(MacVendor.Lookup(mac));
    }

    [Fact]
    public void Lookup_PrivateAddress_ReturnsNull()
    {
        // 0x02 bit of the first byte set: a randomized address, which no manufacturer owns.
        Assert.Null(MacVendor.Lookup("02:1B:63:84:45:E6"));
    }

    [Fact]
    public void LanDeviceDetails_Vendor_FollowsMacAddress()
    {
        Assert.Equal("Apple", new LanDeviceDetails { MacAddress = "00:1B:63:84:45:E6" }.Vendor);
        Assert.Null(new LanDeviceDetails().Vendor);
    }
}

using MauiWifiManager.Abstractions;
using WifiManager.MacVendors;

namespace MauiWifiManager.Tests;

/// <summary>
/// MacVendor forwards to a pluggable lookup; the registry itself is tested with its own package in
/// tests/WifiManager.MacVendors.Tests. These tests change the global lookup, so they share one class:
/// xUnit runs a class's tests one at a time.
/// </summary>
public class MacVendorTests
{
    [Fact]
    public void WithTheRegistry_NamesTheMaker()
    {
        MacVendor.SetLookup(MacVendorRegistry.Lookup);

        Assert.True(MacVendor.IsAvailable);
        Assert.Equal("Xiaomi Mobile Software", MacVendor.Lookup("3C:CD:57:73:6E:E2"));
        Assert.Equal("Apple", new LanDeviceDetails { MacAddress = "00:1B:63:84:45:E6" }.Vendor);
        Assert.Null(new LanDeviceDetails().Vendor);
        Assert.Null(MacVendor.Lookup(null));
    }

    [Fact]
    public void WithoutALookup_ReturnsNull()
    {
        MacVendor.SetLookup(null);

        Assert.False(MacVendor.IsAvailable);
        Assert.Null(MacVendor.Lookup("00:1B:63:84:45:E6"));
        Assert.Null(new LanDeviceDetails { MacAddress = "00:1B:63:84:45:E6" }.Vendor);

        MacVendor.SetLookup(MacVendorRegistry.Lookup);
    }

    [Fact]
    public void AnyLookupCanBePluggedIn()
    {
        MacVendor.SetLookup(mac => mac.StartsWith("AA", StringComparison.OrdinalIgnoreCase) ? "Test Maker" : null);

        Assert.Equal("Test Maker", MacVendor.Lookup("aa:bb:cc:dd:ee:ff"));
        Assert.Null(MacVendor.Lookup("00:1B:63:84:45:E6"));

        MacVendor.SetLookup(MacVendorRegistry.Lookup);
    }
}

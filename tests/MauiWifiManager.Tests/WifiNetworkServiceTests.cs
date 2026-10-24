using MauiWifiManager.Abstractions;

namespace MauiWifiManager.Tests;

/// <summary>
/// Tests the non-platform <see cref="WifiNetworkService"/> (plain net10.0 build) and the default
/// implementations that keep pre-existing <see cref="IWifiNetworkService"/> implementations compiling.
/// </summary>
public class WifiNetworkServiceTests
{
    private readonly IWifiNetworkService _Service = new WifiNetworkService();

    [Fact]
    public async Task ExistingMethods_ReportPlatformUnavailable()
    {
        Assert.Equal(WifiErrorCodes.NetworkUnavailable, (await _Service.ConnectWifiAsync("ssid", "pw")).ErrorCode);
        Assert.Equal(WifiErrorCodes.NetworkUnavailable, (await _Service.GetNetworkInfoAsync()).ErrorCode);
        Assert.Equal(WifiErrorCodes.NetworkUnavailable, (await _Service.ScanWifiNetworksAsync()).ErrorCode);
        Assert.Equal(WifiErrorCodes.NetworkUnavailable, (await _Service.IsInternetAvailableAsync()).ErrorCode);
        Assert.False(await _Service.OpenWifiSetting());
        Assert.False(_Service.IsScanning);
    }

    [Fact]
    public async Task ExistingMethods_CanceledToken_ReturnCanceledTask()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _Service.ConnectWifiAsync("ssid", "pw", cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _Service.GetNetworkInfoAsync(cts.Token));
    }

    [Fact]
    public async Task StopScanning_WithoutSession_SucceedsWithFalse()
    {
        var response = await _Service.StopScanningAsync();

        Assert.Equal(WifiErrorCodes.Success, response.ErrorCode);
        Assert.False(response.Data);
    }

    public static TheoryData<string, Func<IWifiNetworkService, Task<WifiErrorCodes?>>> NewMethods => new()
    {
        { "IsWifiEnabledAsync", async s => (await s.IsWifiEnabledAsync()).ErrorCode },
        { "RequestPermissionsAsync", async s => (await s.RequestPermissionsAsync()).ErrorCode },
        { "IsMeteredConnectionAsync", async s => (await s.IsMeteredConnectionAsync()).ErrorCode },
        { "GetSavedNetworksAsync", async s => (await s.GetSavedNetworksAsync()).ErrorCode },
        { "ForgetNetworkAsync", async s => (await s.ForgetNetworkAsync("ssid")).ErrorCode },
        { "AddNetworkSuggestionAsync", async s => (await s.AddNetworkSuggestionAsync("ssid", "pw", new WifiConnectionOptions())).ErrorCode },
        { "ConnectEnterpriseWifiAsync", async s => (await s.ConnectEnterpriseWifiAsync("ssid", new WifiEnterpriseOptions())).ErrorCode },
        { "ConnectLocalOnlyAsync", async s => (await s.ConnectLocalOnlyAsync("ssid", "pw", new WifiConnectionOptions())).ErrorCode },
        { "ReleaseLocalOnlyAsync", async s => (await s.ReleaseLocalOnlyAsync()).ErrorCode },
        { "CanStartScanAsync", async s => (await s.CanStartScanAsync()).ErrorCode },
        { "SetWifiEnabledAsync", async s => (await s.SetWifiEnabledAsync(true)).ErrorCode },
        { "StartHotspotAsync", async s => (await s.StartHotspotAsync()).ErrorCode },
        { "StopHotspotAsync", async s => (await s.StopHotspotAsync()).ErrorCode },
        { "GetHotspotClientsAsync", async s => (await s.GetHotspotClientsAsync()).ErrorCode },
    };

    [Fact]
    public void NewOptions_DefaultToPreviousBehavior()
    {
        var options = new WifiConnectionOptions();

        Assert.False(options.IsSsidPrefix);
        Assert.Null(options.Timeout);
    }

    [Fact]
    public void ScanResultsEvent_DefaultIsNoOp()
    {
        IWifiNetworkService legacy = new LegacyImplementation();
        EventHandler<WifiScanResultsEventArgs> handler = (_, _) => { };

        legacy.ScanResultsAvailable += handler;
        legacy.ScanResultsAvailable -= handler;
    }

    [Fact]
    public async Task SharedTools_WorkThroughLegacyImplementation()
    {
        // Ping, speed test, discovery and mDNS have shared default implementations, so even
        // a custom IWifiNetworkService written for 10.0.x gets them.
        IWifiNetworkService legacy = new LegacyImplementation();

        var ping = await legacy.PingAsync("127.0.0.1", 1);

        Assert.Equal(WifiErrorCodes.Success, ping.ErrorCode);
        Assert.Equal(1, ping.Data!.Received);
    }

    [Theory]
    [MemberData(nameof(NewMethods))]
    public async Task NewMethods_DefaultToUnsupported(string name, Func<IWifiNetworkService, Task<WifiErrorCodes?>> call)
    {
        Assert.Equal(WifiErrorCodes.UnsupportedHardware, await call(_Service));
        Assert.Equal(WifiErrorCodes.UnsupportedHardware, await call(new LegacyImplementation()));
        _ = name; // shown in the test name only
    }

    [Fact]
    public async Task DefaultImplementation_MessageNamesTheOperation()
    {
        var response = await _Service.IsMeteredConnectionAsync();

        Assert.Contains(nameof(IWifiNetworkService.IsMeteredConnectionAsync), response.ErrorMessage);
    }

    [Fact]
    public void NewEvents_DefaultToNoOp()
    {
        IWifiNetworkService legacy = new LegacyImplementation();
        EventHandler<WifiStateChangedEventArgs> onState = (_, _) => { };
        EventHandler<WifiSignalChangedEventArgs> onSignal = (_, _) => { };

        legacy.WifiStateChanged += onState;
        legacy.WifiStateChanged -= onState;
        legacy.SignalStrengthChanged += onSignal;
        legacy.SignalStrengthChanged -= onSignal;
    }

    [Fact]
    public async Task CrossWifiManager_DelegatesToCurrent()
    {
        Assert.True(CrossWifiManager.IsSupported);
        Assert.IsType<WifiNetworkService>(CrossWifiManager.Current);

        Assert.Equal(WifiErrorCodes.UnsupportedHardware, (await CrossWifiManager.IsWifiEnabledAsync()).ErrorCode);
        Assert.Equal(WifiErrorCodes.UnsupportedHardware, (await CrossWifiManager.GetSavedNetworksAsync()).ErrorCode);
        Assert.Equal(WifiErrorCodes.NetworkUnavailable, (await CrossWifiManager.GetNetworkInfoAsync()).ErrorCode);
    }

    [Fact]
    public void CrossWifiManager_Dispose_CreatesFreshInstance()
    {
        var first = CrossWifiManager.Current;

        CrossWifiManager.Dispose();

        Assert.NotSame(first, CrossWifiManager.Current);
    }

    /// <summary>
    /// Implements only the members that existed in 10.0.2, the way a consumer's custom service or test fake would.
    /// If this stops compiling, a change to <see cref="IWifiNetworkService"/> broke existing implementations.
    /// </summary>
    private sealed class LegacyImplementation : IWifiNetworkService
    {
        public event EventHandler<WifiNetworkChangedEventArgs>? WifiNetworkChanged { add { } remove { } }
        public event EventHandler<NetworkData>? DeviceDiscovered { add { } remove { } }
        public event EventHandler<NetworkData>? DeviceLost { add { } remove { } }
        public bool IsScanning => false;
        public Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password, string? bssid = null) => throw new NotImplementedException();
        public Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, WifiConnectionOptions options, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WifiManagerResponse<NetworkData>> GetNetworkInfo() => throw new NotImplementedException();
        public Task<WifiManagerResponse<NetworkData>> GetNetworkInfoAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public void DisconnectWifi(string ssid) { }
        public Task<bool> OpenWifiSetting() => Task.FromResult(false);
        public Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworks() => throw new NotImplementedException();
        public Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworksAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WifiManagerResponse<bool>> StartScanningForDevicesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WifiManagerResponse<bool>> StopScanningAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> OpenWirelessSetting() => Task.FromResult(false);
        public Task<WifiManagerResponse<bool>> IsInternetAvailableAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WifiManagerResponse<bool>> IsCaptivePortalDetectedAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public void Dispose() { }
    }
}

using MauiWifiManager.Abstractions;

namespace MauiWifiManager.Tests;

public class ModelTests
{
    [Fact]
    public void SuccessResponse_SetsDataCodeAndMessage()
    {
        var response = WifiManagerResponse<int>.SuccessResponse(42, "done");

        Assert.Equal(42, response.Data);
        Assert.Equal(WifiErrorCodes.Success, response.ErrorCode);
        Assert.Equal("done", response.ErrorMessage);
    }

    [Fact]
    public void ErrorResponse_HasNoData()
    {
        var response = WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.PermissionDenied, "denied");

        Assert.Null(response.Data);
        Assert.Equal(WifiErrorCodes.PermissionDenied, response.ErrorCode);
        Assert.Equal("denied", response.ErrorMessage);
    }

    [Fact]
    public void WifiErrorCodes_ValuesAreStable()
    {
        // Callers persist and compare these numerically; changing them is a breaking change.
        Assert.Equal(0, (int)WifiErrorCodes.WifiNotEnabled);
        Assert.Equal(1, (int)WifiErrorCodes.Success);
        Assert.Equal(2, (int)WifiErrorCodes.PermissionDenied);
        Assert.Equal(3, (int)WifiErrorCodes.NoConnection);
        Assert.Equal(4, (int)WifiErrorCodes.UnsupportedHardware);
        Assert.Equal(5, (int)WifiErrorCodes.NetworkUnavailable);
        Assert.Equal(6, (int)WifiErrorCodes.OperationTimeout);
        Assert.Equal(7, (int)WifiErrorCodes.InvalidCredential);
        Assert.Equal(8, (int)WifiErrorCodes.UnknownError);
        Assert.Equal(9, (int)WifiErrorCodes.OperationCanceled);
    }

    [Fact]
    public void WifiStandard_MatchesAndroidConstants()
    {
        // Android's ScanResult.WIFI_STANDARD_* values are cast straight to this enum.
        Assert.Equal(0, (int)WifiStandard.Unknown);
        Assert.Equal(1, (int)WifiStandard.Legacy);
        Assert.Equal(4, (int)WifiStandard.Ieee80211n);
        Assert.Equal(5, (int)WifiStandard.Ieee80211ac);
        Assert.Equal(6, (int)WifiStandard.Ieee80211ax);
        Assert.Equal(7, (int)WifiStandard.Ieee80211ad);
        Assert.Equal(8, (int)WifiStandard.Ieee80211be);
    }

    [Fact]
    public void WifiConnectionOptions_Defaults()
    {
        var options = new WifiConnectionOptions();

        Assert.Null(options.Bssid);
        Assert.False(options.IsHidden);
        Assert.Equal(WifiSecurityType.Wpa2Psk, options.SecurityType);
    }

    [Fact]
    public void WifiEnterpriseOptions_DefaultsToPeapMschapv2()
    {
        var options = new WifiEnterpriseOptions();

        Assert.Equal(WifiEapMethod.Peap, options.EapMethod);
        Assert.Equal(WifiPhase2Method.Mschapv2, options.Phase2Method);
        Assert.Equal(string.Empty, options.Identity);
        Assert.Equal(string.Empty, options.Password);
        Assert.Null(options.AnonymousIdentity);
        Assert.Null(options.ServerDomain);
        Assert.Null(options.CaCertificate);
        Assert.False(options.IsHidden);
        Assert.False(options.UseWpa3);
    }

    [Fact]
    public void NetworkData_NewFieldsDefaultToUnknown()
    {
        var data = new NetworkData();

        Assert.Equal(WifiStandard.Unknown, data.WifiStandard);
        Assert.Null(data.ChannelWidthMhz);
        Assert.Equal(WifiFrequencyBand.Unknown, data.FrequencyBand);
    }

    [Fact]
    public void WifiStateChangedEventArgs_KeepsState()
    {
        Assert.True(new WifiStateChangedEventArgs(true).IsEnabled);
        Assert.False(new WifiStateChangedEventArgs(false).IsEnabled);
    }

    [Fact]
    public void WifiSignalChangedEventArgs_KeepsReadings()
    {
        var args = new WifiSignalChangedEventArgs(-60, 3, 80);

        Assert.Equal(-60, args.RssiDbm);
        Assert.Equal(3, args.SignalLevel);
        Assert.Equal(80, args.SignalPercentage);
    }

    [Fact]
    public void WifiNetworkChangedEventArgs_KeepsSnapshots()
    {
        var oldNetwork = new NetworkData { Ssid = "A" };
        var newNetwork = new NetworkData { Ssid = "B" };

        var args = new WifiNetworkChangedEventArgs(oldNetwork, newNetwork);

        Assert.Same(oldNetwork, args.OldNetwork);
        Assert.Same(newNetwork, args.NewNetwork);
    }
}

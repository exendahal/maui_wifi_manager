namespace DemoApp;
using MauiWifiManager;
using MauiWifiManager.Abstractions;
using System.Diagnostics;
using System.Net;
using System.Text;

public partial class NetworkInfo : ContentPage
{
    private bool _isSubscribedToWifiEvents;

    public NetworkInfo()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        PermissionStatus status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted || DeviceInfo.Current.Platform == DevicePlatform.WinUI)
        {
            SubscribeToWifiChanges();
            await LoadAndRenderCurrentNetworkInfo();
        }
        else
            await DisplayAlertAsync("No location permission", "Please provide location permission", "OK");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        UnsubscribeFromWifiChanges();
    }

    private async Task LoadAndRenderCurrentNetworkInfo()
    {
        var response = await CrossWifiManager.Current.GetNetworkInfoAsync();
        if (response?.ErrorCode == WifiErrorCodes.Success && response.Data != null)
            RenderNetworkData(response.Data);
    }

    private void RenderNetworkData(NetworkData data)
    {
        IPAddress ipAddress = new(BitConverter.GetBytes(data.IpAddress));
        IPAddress gateway = new(BitConverter.GetBytes(data.GatewayAddress));
        IPAddress serverAddress = new(BitConverter.GetBytes(data.DhcpServerAddress));

        wifiSsid.Text = data.Ssid;
        wifiBssid.Text = data.Bssid?.ToString();
        ipAddressTxt.Text = $"IP: {ipAddress}\nGateway: {gateway}\nDHCP: {serverAddress}";
        ipv6AddressTxt.Text = data.IPv6Address ?? "—";
        subnetMaskTxt.Text = data.SubnetMask ?? "—";
        dnsServersTxt.Text = data.DnsAddresses?.Count > 0
            ? string.Join(", ", data.DnsAddresses)
            : "—";
        securityTxt.Text = data.SecurityType?.ToString();
        rssiTxt.Text = data.RssiDbm.HasValue ? $"{data.RssiDbm} dBm" : "—";
        frequencyBandTxt.Text = data.FrequencyBand.ToString();
        channelTxt.Text = data.ChannelNumber.HasValue ? data.ChannelNumber.ToString() : "—";
        linkSpeedTxt.Text = data.LinkSpeedMbps.HasValue ? $"{data.LinkSpeedMbps} Mbps" : "—";

        if (data.NativeObject != null)
        {
            Debug.WriteLine(data.NativeObject);
            nativeObject.Text = FormatNativeObject(data.NativeObject);
        }
    }

    private async void CheckInternetBtnClicked(object sender, EventArgs e)
    {
        CheckInternetBtn.IsEnabled = false;
        internetStatusTxt.Text = "Checking...";
        var response = await CrossWifiManager.IsInternetAvailableAsync();
        internetStatusTxt.Text = response.ErrorCode == WifiErrorCodes.Success
            ? (response.Data ? "Internet available" : "No internet connection")
            : $"Error: {response.ErrorMessage}";
        CheckInternetBtn.IsEnabled = true;
    }

    private async void CheckCaptiveBtnClicked(object sender, EventArgs e)
    {
        CheckCaptiveBtn.IsEnabled = false;
        captivePortalTxt.Text = "Checking...";
        var response = await CrossWifiManager.IsCaptivePortalDetectedAsync();
        captivePortalTxt.Text = response.ErrorCode == WifiErrorCodes.Success
            ? (response.Data ? "Captive portal detected" : "No captive portal")
            : $"Error: {response.ErrorMessage}";
        CheckCaptiveBtn.IsEnabled = true;
    }

    private void SubscribeToWifiChanges()
    {
        if (_isSubscribedToWifiEvents)
            return;
        CrossWifiManager.WifiNetworkChanged += OnWifiNetworkChanged;
        _isSubscribedToWifiEvents = true;
        wifiChangeStatus.Text = "Subscribed: listening for Wi-Fi changes";
    }

    private void UnsubscribeFromWifiChanges()
    {
        if (!_isSubscribedToWifiEvents)
            return;
        CrossWifiManager.WifiNetworkChanged -= OnWifiNetworkChanged;
        _isSubscribedToWifiEvents = false;
        wifiChangeStatus.Text = "Not subscribed";
    }

    private void OnWifiNetworkChanged(object? sender, WifiNetworkChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var oldSnapshot = FormatNetworkSnapshot("Old", e.OldNetwork);
            var newSnapshot = FormatNetworkSnapshot("New", e.NewNetwork);
            wifiChangeLog.Text = $"{oldSnapshot}\n\n{newSnapshot}";

            if (e.NewNetwork != null)
            {
                RenderNetworkData(e.NewNetwork);
            }
            else
            {
                wifiSsid.Text = "";
                wifiBssid.Text = "";
                ipAddressTxt.Text = string.Empty;
                ipv6AddressTxt.Text = "—";
                subnetMaskTxt.Text = "—";
                dnsServersTxt.Text = "—";
                securityTxt.Text = string.Empty;
                rssiTxt.Text = "—";
                frequencyBandTxt.Text = "—";
                channelTxt.Text = "—";
                linkSpeedTxt.Text = "—";
                nativeObject.Text = string.Empty;
            }
        });
    }

    private static string FormatNetworkSnapshot(string title, MauiWifiManager.Abstractions.NetworkData? network)
    {
        if (network == null)
            return $"{title}: (none)";

        var ipAddress = new IPAddress(BitConverter.GetBytes(network.IpAddress));
        var gateway = new IPAddress(BitConverter.GetBytes(network.GatewayAddress));
        var serverAddress = new IPAddress(BitConverter.GetBytes(network.DhcpServerAddress));

        return $"{title}:\n"
            + $"StatusId: {network.StatusId}\n"
            + $"SSID: {network.Ssid}\n"
            + $"BSSID: {network.Bssid}\n"
            + $"SignalStrength: {network.SignalStrength}\n"
            + $"SecurityType: {network.SecurityType}\n"
            + $"IP: {ipAddress}\n"
            + $"Gateway: {gateway}\n"
            + $"DHCP: {serverAddress}\n"
            + $"IPv6: {network.IPv6Address ?? "—"}\n"
            + $"Subnet: {network.SubnetMask ?? "—"}\n"
            + $"DNS: {(network.DnsAddresses?.Count > 0 ? string.Join(", ", network.DnsAddresses) : "—")}\n"
            + $"RSSI: {(network.RssiDbm.HasValue ? $"{network.RssiDbm} dBm" : "—")}\n"
            + $"Band: {network.FrequencyBand}\n"
            + $"Channel: {(network.ChannelNumber.HasValue ? network.ChannelNumber.ToString() : "—")}\n"
            + $"LinkSpeed: {(network.LinkSpeedMbps.HasValue ? $"{network.LinkSpeedMbps} Mbps" : "—")}";
    }

    private string FormatNativeObject(object nativeObject)
    {
        if (nativeObject == null) return "No data available";

        var sb = new StringBuilder();
        Type type = nativeObject.GetType();

        sb.AppendLine($"Object Type: {type.Name}");

        foreach (var prop in type.GetProperties())
        {
            try
            {
                var value = prop.GetValue(nativeObject, null);
                sb.AppendLine($"{prop.Name}: {value}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{prop.Name}: Error retrieving value ({ex.Message})");
            }
        }

        return sb.ToString();
    }
}

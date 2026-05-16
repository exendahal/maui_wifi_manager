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
        if (response != null)
        {
            if (response.ErrorCode == WifiErrorCodes.Success)
            {
                if (response.Data != null)
                {
                    IPAddress ipAddress = new(BitConverter.GetBytes(response.Data.IpAddress));
                    IPAddress gateway = new(BitConverter.GetBytes(response.Data.GatewayAddress));
                    IPAddress serverAddress = new(BitConverter.GetBytes(response.Data.DhcpServerAddress));
                    wifiSsid.Text = response.Data.Ssid;
                    wifiBssid.Text = response.Data.Bssid?.ToString();
                    ipAddressTxt.Text = $"IP:{ipAddress.ToString()}\nGateway:{gateway.ToString()}\nDHCP Server:{serverAddress.ToString()}";
                    securityTxt.Text = response.Data.SecurityType?.ToString();
                    if (response.Data.NativeObject != null)
                    {
                        Debug.WriteLine(response.Data.NativeObject);
                        nativeObject.Text = FormatNativeObject(response.Data.NativeObject);
                    }
                }
            }
        }
    }

    private void SubscribeToWifiChanges()
    {
        if (_isSubscribedToWifiEvents)
        {
            return;
        }

        CrossWifiManager.WifiNetworkChanged += OnWifiNetworkChanged;
        _isSubscribedToWifiEvents = true;
        wifiChangeStatus.Text = "Subscribed: listening for Wi-Fi changes";
    }

    private void UnsubscribeFromWifiChanges()
    {
        if (!_isSubscribedToWifiEvents)
        {
            return;
        }

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
                wifiSsid.Text = e.NewNetwork.Ssid;
                wifiBssid.Text = e.NewNetwork.Bssid?.ToString();
                IPAddress ipAddress = new(BitConverter.GetBytes(e.NewNetwork.IpAddress));
                IPAddress gateway = new(BitConverter.GetBytes(e.NewNetwork.GatewayAddress));
                IPAddress serverAddress = new(BitConverter.GetBytes(e.NewNetwork.DhcpServerAddress));
                ipAddressTxt.Text = $"IP:{ipAddress}\nGateway:{gateway}\nDHCP Server:{serverAddress}";
                securityTxt.Text = e.NewNetwork.SecurityType?.ToString();
                nativeObject.Text = e.NewNetwork.NativeObject != null ? FormatNativeObject(e.NewNetwork.NativeObject) : string.Empty;
            }
            else
            {
                wifiSsid.Text = "";
                wifiBssid.Text = "";
                ipAddressTxt.Text = string.Empty;
                securityTxt.Text = string.Empty;
                nativeObject.Text = string.Empty;
            }
        });
    }

    private static string FormatNetworkSnapshot(string title, MauiWifiManager.Abstractions.NetworkData? network)
    {
        if (network == null)
        {
            return $"{title}: (none)";
        }

        var ipAddress = new IPAddress(BitConverter.GetBytes(network.IpAddress));
        var gateway = new IPAddress(BitConverter.GetBytes(network.GatewayAddress));
        var serverAddress = new IPAddress(BitConverter.GetBytes(network.DhcpServerAddress));

        return $"{title}:\n"
            + $"StatusId: {network.StatusId}\n"
            + $"SSID: {network.Ssid}\n"
            + $"BSSID: {network.Bssid}\n"
            + $"SignalStrength: {network.SignalStrength}\n"
            + $"SecurityType: {network.SecurityType}\n"
            + $"IpAddress: {ipAddress}\n"
            + $"GatewayAddress: {gateway}\n"
            + $"DhcpServerAddress: {serverAddress}\n"
            + $"NativeObjectType: {network.NativeObject?.GetType().Name}";
    }

    private string FormatNativeObject(object nativeObject)
    {
        if (nativeObject == null) return "No data available";

        var sb = new StringBuilder();
        Type type = nativeObject.GetType();

        sb.AppendLine($"Object Type: {type.Name}");

        // Get all properties dynamically
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
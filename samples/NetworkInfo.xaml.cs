namespace DemoApp;
using MauiWifiManager;
using MauiWifiManager.Abstractions;
using System.Text;

public partial class NetworkInfo : ContentPage
{
    private const int MaxActivityEntries = 20;

    private NetworkData? _Current;
    private bool _IsSubscribedToWifiEvents;

    public NetworkInfo()
    {
        InitializeComponent();
        versionTxt.Text = $"MauiWifiManager {typeof(CrossWifiManager).Assembly.GetName().Version}";
        disconnectHintTxt.Text = DeviceInfo.Current.Platform == DevicePlatform.Android
            ? "On Android 10 and later this opens the system Wi-Fi panel."
            : DeviceInfo.Current.Platform == DevicePlatform.iOS || DeviceInfo.Current.Platform == DevicePlatform.MacCatalyst
                ? "Removes the configuration this app added for the network."
                : string.Empty;
        disconnectHintTxt.IsVisible = disconnectHintTxt.Text.Length > 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        SubscribeToWifiChanges();
        await RefreshAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        UnsubscribeFromWifiChanges();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await RefreshAsync();
        refreshView.IsRefreshing = false;
    }

    private async Task RefreshAsync()
    {
        var locationError = await LocationAccess.CheckAsync();
        if (locationError != null)
        {
            Render(null);
            ssidTxt.Text = "Location needed";
            summaryTxt.Text = locationError;
        }
        else
        {
            var response = await CrossWifiManager.GetNetworkInfoAsync();
            Render(response.ErrorCode == WifiErrorCodes.Success ? response.Data : null);
        }
        await RunConnectivityChecksAsync();
    }

    private void Render(NetworkData? data)
    {
        var isConnected = !string.IsNullOrWhiteSpace(data?.Ssid);
        _Current = isConnected ? data : null;

        ssidTxt.Text = isConnected ? data!.Ssid : "Not connected";
        summaryTxt.Text = isConnected
            ? string.Join("  ·  ", new[]
              {
                  NetworkFormat.Band(data!.FrequencyBand),
                  data.ChannelNumber is int ch ? $"Ch {ch}" : null,
                  NetworkFormat.Security(data.SecurityType),
                  data.LinkSpeedMbps is int mbps ? $"{mbps} Mbps" : null
              }.Where(s => !string.IsNullOrEmpty(s)))
            : "Join a network from the Networks or Connect tab.";

        statusPillTxt.Text = isConnected ? "Connected" : "Offline";
        statusPillTxt.TextColor = ThemeColor.Get(isConnected ? "Success" : "TextMuted");
        statusPill.BackgroundColor = ThemeColor.Get("SurfaceMuted");

        disconnectSection.IsVisible = isConnected;
        disconnectBtn.Text = isConnected ? $"Disconnect from {data!.Ssid}" : "Disconnect";

        RenderDetails(_Current);
        nativeTxt.Text = _Current?.NativeObject != null ? FormatNativeObject(_Current.NativeObject) : "No platform object.";
    }

    private void RenderDetails(NetworkData? data)
    {
        string Or(string? value) => string.IsNullOrWhiteSpace(value) ? NetworkFormat.None : value;

        var rows = new (string Label, string Value, bool Mono)[]
        {
            ("BSSID", Or(data?.Bssid?.ToString()), true),
            ("Signal", data != null ? NetworkFormat.Signal(data) : NetworkFormat.None, false),
            ("Link speed", data?.LinkSpeedMbps is int mbps ? $"{mbps} Mbps" : NetworkFormat.None, false),
            ("Band", data != null ? Or(NetworkFormat.Band(data.FrequencyBand)) : NetworkFormat.None, false),
            ("Wi-Fi standard", data != null ? Or(NetworkFormat.Standard(data.WifiStandard)) : NetworkFormat.None, false),
            ("Channel", Or(data?.ChannelNumber?.ToString()), false),
            ("Security", Or(NetworkFormat.Security(data?.SecurityType) ?? data?.SecurityType?.ToString()), false),
            ("IPv4 address", data != null ? NetworkFormat.Ip(data.IpAddress) : NetworkFormat.None, true),
            ("Subnet mask", Or(data?.SubnetMask), true),
            ("Gateway", data != null ? NetworkFormat.Ip(data.GatewayAddress) : NetworkFormat.None, true),
            ("DHCP server", data != null ? NetworkFormat.Ip(data.DhcpServerAddress) : NetworkFormat.None, true),
            ("IPv6 address", Or(data?.IPv6Address), true),
            ("DNS servers", data?.DnsAddresses is { Count: > 0 } dns ? string.Join("\n", dns) : NetworkFormat.None, true)
        };

        DetailList.Fill(detailsList, rows);
    }

    private async void CheckClicked(object sender, EventArgs e) => await RunConnectivityChecksAsync();

    private async Task RunConnectivityChecksAsync()
    {
        checkBtn.IsEnabled = false;
        SetStatus(internetTxt, "Checking…", "TextMuted");
        SetStatus(captiveTxt, "Checking…", "TextMuted");

        var internet = await CrossWifiManager.IsInternetAvailableAsync();
        if (internet.ErrorCode == WifiErrorCodes.Success)
            SetStatus(internetTxt, internet.Data ? "Available" : "Not available", internet.Data ? "Success" : "Danger");
        else
            SetStatus(internetTxt, internet.ErrorMessage ?? "Unknown", "Warning");

        var captive = await CrossWifiManager.IsCaptivePortalDetectedAsync();
        if (captive.ErrorCode == WifiErrorCodes.Success)
            SetStatus(captiveTxt, captive.Data ? "Sign-in required" : "None detected", captive.Data ? "Warning" : "Text");
        else
            SetStatus(captiveTxt, captive.ErrorMessage ?? "Unknown", "Warning");

        checkBtn.IsEnabled = true;
    }

    private static void SetStatus(Label label, string text, string color)
    {
        label.Text = text;
        label.TextColor = ThemeColor.Get(color);
    }

    private void OnWifiNetworkChanged(object? sender, WifiNetworkChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            AddActivity(DescribeChange(e.OldNetwork, e.NewNetwork));
            Render(e.NewNetwork);
            await RunConnectivityChecksAsync();
        });
    }

    private static string DescribeChange(NetworkData? oldNetwork, NetworkData? newNetwork)
    {
        var oldSsid = oldNetwork?.Ssid;
        var newSsid = newNetwork?.Ssid;
        if (string.IsNullOrEmpty(oldSsid) && !string.IsNullOrEmpty(newSsid))
            return $"Connected to {newSsid}";
        if (!string.IsNullOrEmpty(oldSsid) && string.IsNullOrEmpty(newSsid))
            return $"Disconnected from {oldSsid}";
        if (oldSsid != newSsid)
            return $"Switched from {oldSsid} to {newSsid}";
        if (oldNetwork?.Bssid?.ToString() != newNetwork?.Bssid?.ToString())
            return $"Roamed to another access point on {newSsid}";
        return $"Connection details updated on {newSsid}";
    }

    private void AddActivity(string text)
    {
        activityEmptyTxt.IsVisible = false;

        var row = new Grid { ColumnDefinitions = LabelValueColumns(), Style = DetailList.AppStyle("FieldRow") };
        row.Add(new Label
        {
            Text = DateTime.Now.ToString("HH:mm:ss"),
            FontFamily = (string)Application.Current!.Resources["MonoFont"],
            Style = DetailList.AppStyle("Muted"),
            VerticalOptions = LayoutOptions.Center
        });
        row.Add(new Label { Text = text, VerticalOptions = LayoutOptions.Center }, 1);

        if (activityList.Count > 0)
            activityList.Insert(0, new BoxView { Style = DetailList.AppStyle("Divider") });
        activityList.Insert(0, row);

        // Each entry is a row plus a divider.
        while (activityList.Count > MaxActivityEntries * 2 - 1)
            activityList.RemoveAt(activityList.Count - 1);
    }

    private async void DisconnectClicked(object sender, EventArgs e)
    {
        if (_Current?.Ssid is not string ssid)
            return;

        if (await DisplayAlertAsync($"Disconnect from {ssid}?", "Your device may reconnect to another saved network.", "Disconnect", "Cancel"))
            CrossWifiManager.DisconnectWifi(ssid);
    }

    private async void WifiSettingsTapped(object? sender, TappedEventArgs e) => await CrossWifiManager.OpenWifiSetting();

    private async void NetworkSettingsTapped(object? sender, TappedEventArgs e) => await CrossWifiManager.OpenWirelessSetting();

    private void NativeToggleClicked(object sender, EventArgs e)
    {
        nativeCard.IsVisible = !nativeCard.IsVisible;
        nativeToggleBtn.Text = nativeCard.IsVisible ? "Hide platform object" : "Show platform object";
    }

    private void SubscribeToWifiChanges()
    {
        if (_IsSubscribedToWifiEvents)
            return;
        CrossWifiManager.WifiNetworkChanged += OnWifiNetworkChanged;
        _IsSubscribedToWifiEvents = true;
    }

    private void UnsubscribeFromWifiChanges()
    {
        if (!_IsSubscribedToWifiEvents)
            return;
        CrossWifiManager.WifiNetworkChanged -= OnWifiNetworkChanged;
        _IsSubscribedToWifiEvents = false;
    }

    private static ColumnDefinitionCollection LabelValueColumns() =>
        new(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star));

    private static string FormatNativeObject(object nativeObject)
    {
        var sb = new StringBuilder();
        var type = nativeObject.GetType();
        sb.AppendLine(type.Name);

        foreach (var prop in type.GetProperties())
        {
            try
            {
                sb.AppendLine($"{prop.Name}: {prop.GetValue(nativeObject, null)}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{prop.Name}: <{ex.GetType().Name}>");
            }
        }

        return sb.ToString().TrimEnd();
    }
}

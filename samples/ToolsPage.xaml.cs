namespace DemoApp;
using MauiWifiManager;
using MauiWifiManager.Abstractions;

/// <summary>
/// Demonstrates the Wi-Fi state, signal and connection-type events, permissions, metered check, saved networks,
/// QR generation, hotspot, network diagnostics, the self-test and the advanced connection types.
/// </summary>
public partial class ToolsPage : ContentPage
{
    private const int LocalOnlyMode = 0;
    private const int EnterpriseMode = 1;
    private const int SuggestionMode = 2;

    private bool _IsSubscribed;
    private bool _HotspotRunning;

    public ToolsPage()
    {
        InitializeComponent();
        modePicker.SelectedIndex = LocalOnlyMode;
        eapPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_IsSubscribed)
        {
            CrossWifiManager.WifiStateChanged += OnWifiStateChanged;
            CrossWifiManager.SignalStrengthChanged += OnSignalChanged;
            CrossWifiManager.ConnectionTypeChanged += OnConnectionTypeChanged;
            _IsSubscribed = true;
        }
        await RefreshStatusAsync();
        await RefreshSavedAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_IsSubscribed)
        {
            CrossWifiManager.WifiStateChanged -= OnWifiStateChanged;
            CrossWifiManager.SignalStrengthChanged -= OnSignalChanged;
            CrossWifiManager.ConnectionTypeChanged -= OnConnectionTypeChanged;
            _IsSubscribed = false;
        }
    }

    // ---- Status ----

    private async Task RefreshStatusAsync()
    {
        var radio = await CrossWifiManager.IsWifiEnabledAsync();
        if (radio.ErrorCode == WifiErrorCodes.Success)
            ShowRadio(radio.Data);
        else
        {
            SetStatus(radioTxt, "Not available on this platform", "TextMuted");
            radioToggleBtn.IsVisible = false;
        }

        var types = await CrossWifiManager.GetConnectionTypesAsync();
        ShowConnectionTypes(types.Data ?? new List<NetworkConnectionType>(), hasInternet: null);

        var scanReady = await CrossWifiManager.CanStartScanAsync();
        if (scanReady.ErrorCode == WifiErrorCodes.Success)
            SetStatus(scanReadyTxt, scanReady.ErrorMessage ?? scanReady.Data.ToString(), scanReady.Data == WifiScanAvailability.Ready ? "Success" : "Warning");
        else
            SetStatus(scanReadyTxt, "Not available on this platform", "TextMuted");

        var metered = await CrossWifiManager.IsMeteredConnectionAsync();
        if (metered.ErrorCode == WifiErrorCodes.Success)
            SetStatus(meteredTxt, metered.Data ? "Yes — avoid large downloads" : "No", metered.Data ? "Warning" : "Text");
        else
            SetStatus(meteredTxt, metered.ErrorMessage ?? "Unknown", "Warning");

        // Seed the signal row from the current connection until the first change event arrives.
        var info = await CrossWifiManager.GetNetworkInfoAsync();
        if (info.Data?.RssiDbm is int rssi)
            ShowSignal(rssi, WifiSignal.GetSignalPercentage(rssi));
    }

    private async void RefreshStatusClicked(object sender, EventArgs e) => await RefreshStatusAsync();

    private async void RequestPermissionsClicked(object sender, EventArgs e)
    {
        var response = await CrossWifiManager.RequestPermissionsAsync();
        if (response.ErrorCode == WifiErrorCodes.Success)
            SetStatus(permissionTxt, "Granted", "Success");
        else
            SetStatus(permissionTxt, response.ErrorMessage ?? "Denied", "Danger");
    }

    private void OnWifiStateChanged(object? sender, WifiStateChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => ShowRadio(e.IsEnabled));

    private void ShowRadio(bool enabled)
    {
        SetStatus(radioTxt, enabled ? "On" : "Off", enabled ? "Success" : "Danger");
        radioToggleBtn.Text = enabled ? "Turn off" : "Turn on";
        radioToggleBtn.IsVisible = true;
    }

    private async void ToggleWifiClicked(object sender, EventArgs e)
    {
        bool turnOn = radioToggleBtn.Text == "Turn on";
        var response = await CrossWifiManager.SetWifiEnabledAsync(turnOn);
        if (response.ErrorCode != WifiErrorCodes.Success)
            await DisplayAlertAsync("Could not change Wi-Fi", response.ErrorMessage, "OK");
        else if (!response.Data)
            SetStatus(radioTxt, "Use the Wi-Fi panel", "Warning"); // Android 10+ opened the system panel
        else
            ShowRadio(turnOn);
    }

    private void OnConnectionTypeChanged(object? sender, ConnectionTypeChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => ShowConnectionTypes(e.ConnectionTypes, e.HasInternet));

    private void ShowConnectionTypes(IReadOnlyList<NetworkConnectionType> types, bool? hasInternet)
    {
        var text = types.Count == 0 ? "Offline" : string.Join(" + ", types);
        if (hasInternet == false && types.Count > 0)
            text += " (no internet)";
        SetStatus(connectionTypeTxt, text, types.Count == 0 ? "Danger" : "Text");
    }

    private void OnSignalChanged(object? sender, WifiSignalChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => ShowSignal(e.RssiDbm, e.SignalPercentage));

    private void ShowSignal(int? rssiDbm, int percentage)
    {
        var color = percentage >= 60 ? "Success" : percentage >= 30 ? "Warning" : "Danger";
        SetStatus(signalTxt, rssiDbm is int dbm ? $"{percentage}%  ({dbm} dBm)" : $"{percentage}%", color);
    }

    // ---- Saved networks ----

    private async Task RefreshSavedAsync()
    {
        savedList.Clear();
        var response = await CrossWifiManager.GetSavedNetworksAsync();
        if (response.ErrorCode != WifiErrorCodes.Success)
        {
            savedEmptyTxt.Text = response.ErrorMessage;
            savedEmptyTxt.IsVisible = true;
            return;
        }

        var ssids = response.Data ?? new List<string>();
        savedEmptyTxt.Text = DeviceInfo.Current.Platform == DevicePlatform.WinUI
            ? "No saved networks."
            : "No networks saved by this app yet. Connect from the Connect tab or add a suggestion below.";
        savedEmptyTxt.IsVisible = ssids.Count == 0;

        foreach (var ssid in ssids)
        {
            if (savedList.Count > 0)
                savedList.Add(new BoxView { Style = DetailList.AppStyle("Divider") });

            var row = new Grid
            {
                ColumnDefinitions = new(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
                Style = DetailList.AppStyle("FieldRow")
            };
            row.Add(new Label { Text = ssid, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation });
            var forget = new Button { Text = "Forget", Style = DetailList.AppStyle("TextButton"), Padding = 0 };
            forget.Clicked += async (_, _) => await ForgetAsync(ssid);
            row.Add(forget, 1);
            savedList.Add(row);
        }
    }

    private async Task ForgetAsync(string ssid)
    {
        if (!await DisplayAlertAsync($"Forget {ssid}?", "The device will no longer join this network automatically.", "Forget", "Cancel"))
            return;

        var response = await CrossWifiManager.ForgetNetworkAsync(ssid);
        if (response.ErrorCode != WifiErrorCodes.Success)
            await DisplayAlertAsync("Could not forget network", response.ErrorMessage, "OK");
        await RefreshSavedAsync();
    }

    // ---- QR code ----

    private void GenerateQrClicked(object sender, EventArgs e)
    {
        var ssid = qrSsidEntry.Text?.Trim();
        if (string.IsNullOrEmpty(ssid))
            return;

        var password = qrPasswordEntry.Text ?? string.Empty;
        var code = new WifiQrCode(ssid, password, password.Length == 0 ? WifiSecurityType.Open : WifiSecurityType.Wpa2Psk);
        qrView.Value = code.ToString();
        qrPayloadTxt.Text = code.ToString();
        qrResult.IsVisible = true;
    }


    // ---- Hotspot ----

    private async void HotspotClicked(object sender, EventArgs e)
    {
        hotspotBtn.IsEnabled = false;
        if (_HotspotRunning)
        {
            var stopped = await CrossWifiManager.StopHotspotAsync();
            _HotspotRunning = false;
            hotspotTxt.Text = stopped.ErrorMessage;
            hotspotDetails.Clear();
        }
        else
        {
            hotspotTxt.Text = "Starting…";
            var started = await CrossWifiManager.StartHotspotAsync();
            _HotspotRunning = started.ErrorCode == WifiErrorCodes.Success;
            hotspotTxt.Text = started.ErrorMessage;
            if (_HotspotRunning)
                DetailList.Fill(hotspotDetails, new (string, string, bool)[]
                {
                    ("Name", started.Data!.Ssid ?? NetworkFormat.None, false),
                    ("Password", started.Data.Password ?? NetworkFormat.None, true),
                    ("Internet sharing", started.Data.IsLocalOnly ? "No (local-only)" : "Yes", false)
                });
        }
        hotspotBtn.Text = _HotspotRunning ? "Stop hotspot" : "Start hotspot";
        hotspotClientsBtn.IsVisible = _HotspotRunning && DeviceInfo.Current.Platform == DevicePlatform.WinUI;
        hotspotBtn.IsEnabled = true;
    }

    private async void HotspotClientsClicked(object sender, EventArgs e)
    {
        var clients = await CrossWifiManager.GetHotspotClientsAsync();
        var list = clients.Data is { Count: > 0 } c
            ? string.Join("\n", c.Select(x => $"{x.HostName ?? "Unknown"}  {x.MacAddress}"))
            : clients.ErrorMessage;
        await DisplayAlertAsync("Connected devices", list, "OK");
    }

    // ---- Diagnostics ----

    private async void PingClicked(object sender, EventArgs e)
    {
        var host = pingHostEntry.Text?.Trim();
        if (string.IsNullOrEmpty(host)) return;
        pingBtn.IsEnabled = false;
        pingTxt.IsVisible = true;
        SetStatus(pingTxt, "Pinging…", "TextMuted");
        var response = await CrossWifiManager.PingAsync(host);
        if (response.Data is { } stats && stats.Received > 0)
            SetStatus(pingTxt, $"{stats.Received}/{stats.Sent} replies · min {stats.MinMs} / avg {stats.AverageMs:0} / max {stats.MaxMs} ms · {stats.PacketLossPercent:0}% loss", "Text");
        else
            SetStatus(pingTxt, response.ErrorMessage ?? "No reply", "Danger");
        pingBtn.IsEnabled = true;
    }

    private async void SpeedTestClicked(object sender, EventArgs e)
    {
        speedBtn.IsEnabled = false;
        SetStatus(speedTxt, "Testing… (up to 20 s)", "TextMuted");
        var response = await CrossWifiManager.MeasureSpeedAsync();
        if (response.Data is { } r)
            SetStatus(speedTxt, $"↓ {r.DownloadMbps:0.0} Mbps · ↑ {r.UploadMbps:0.0} Mbps · {r.LatencyMs:0} ms", "Text");
        else
            SetStatus(speedTxt, response.ErrorMessage ?? "Failed", "Danger");
        speedBtn.IsEnabled = true;
    }

    private async void BrowseServicesClicked(object sender, EventArgs e)
    {
        browseBtn.IsEnabled = false;
        servicesList.Clear();
        servicesTxt.IsVisible = true;
        SetStatus(servicesTxt, "Listening for 3 s…", "TextMuted");
        var type = serviceTypeEntry.Text?.Trim();
        var response = string.IsNullOrEmpty(type)
            ? await CrossWifiManager.BrowseServicesAsync()
            : await CrossWifiManager.BrowseServicesAsync(type);
        SetStatus(servicesTxt, response.ErrorMessage ?? string.Empty, response.ErrorCode == WifiErrorCodes.Success ? "TextMuted" : "Danger");
        DetailList.Fill(servicesList, (response.Data ?? new List<NetworkServiceInfo>()).Select(s =>
            string.IsNullOrEmpty(type)
                ? (s.Name, "Enter this type and tap Browse", false)
                : (s.Name, $"{s.IpAddress ?? s.HostName}:{s.Port}", true)));
        browseBtn.IsEnabled = true;
    }

    // ---- Self-test ----

    private async void SelfTestClicked(object sender, EventArgs e)
    {
        selfTestBtn.IsEnabled = false;
        selfTestTxt.IsVisible = true;
        selfTestTxt.Text = string.Empty;
        await SelfTest.RunAsync("basic", line => MainThread.BeginInvokeOnMainThread(() =>
            selfTestTxt.Text += line.Replace(SelfTest.LogPrefix + " ", string.Empty) + "\n"));
        selfTestBtn.IsEnabled = true;
    }

    // ---- Advanced connection ----

    private void ModeChanged(object? sender, EventArgs e)
    {
        var mode = modePicker.SelectedIndex;
        identityRow.IsVisible = enterpriseRows.IsVisible = mode == EnterpriseMode;
        modeHintTxt.Text = mode switch
        {
            EnterpriseMode => "Joins a WPA2/WPA3-Enterprise network such as eduroam with a username and password. On Windows only the username and password are used.",
            SuggestionMode => "Saves the network without UI. Android joins it automatically when in range, even after the app closes. Android 10+ only.",
            _ => "Joins a device's setup access point that has no internet and routes this app's traffic through it. Tap Release when finished to return to your previous network."
        };
        advConnectBtn.Text = mode == SuggestionMode ? "Save suggestion" : "Connect";
    }

    private async void AdvancedConnectClicked(object sender, EventArgs e)
    {
        var ssid = advSsidEntry.Text?.Trim();
        if (string.IsNullOrEmpty(ssid))
        {
            ShowResult("Enter a network name.", "Warning");
            return;
        }

        var password = advPasswordEntry.Text ?? string.Empty;
        var options = new WifiConnectionOptions
        {
            SecurityType = password.Length == 0 ? WifiSecurityType.Open : WifiSecurityType.Wpa2Psk,
            IsSsidPrefix = advPrefixSwitch.IsToggled,
            Timeout = double.TryParse(advTimeoutEntry.Text, out var seconds) && seconds > 0 ? TimeSpan.FromSeconds(seconds) : null
        };

        advConnectBtn.IsEnabled = false;
        ShowResult("Working…", "TextMuted");

        string message;
        bool success;
        switch (modePicker.SelectedIndex)
        {
            case EnterpriseMode:
                var ttls = eapPicker.SelectedIndex == 1;
                var enterprise = await CrossWifiManager.ConnectEnterpriseWifiAsync(ssid, new WifiEnterpriseOptions
                {
                    EapMethod = ttls ? WifiEapMethod.Ttls : WifiEapMethod.Peap,
                    Phase2Method = ttls ? WifiPhase2Method.Pap : WifiPhase2Method.Mschapv2,
                    Identity = identityEntry.Text?.Trim() ?? string.Empty,
                    Password = password,
                    ServerDomain = string.IsNullOrWhiteSpace(domainEntry.Text) ? null : domainEntry.Text.Trim()
                });
                success = enterprise.ErrorCode == WifiErrorCodes.Success;
                message = success ? $"Connected to {enterprise.Data?.Ssid ?? ssid}." : enterprise.ErrorMessage ?? "Connection failed.";
                break;

            case SuggestionMode:
                var suggestion = await CrossWifiManager.AddNetworkSuggestionAsync(ssid, password, options);
                success = suggestion.ErrorCode == WifiErrorCodes.Success;
                message = suggestion.ErrorMessage ?? (success ? "Suggestion saved." : "Failed.");
                if (success)
                    await RefreshSavedAsync();
                break;

            default:
                var local = await CrossWifiManager.ConnectLocalOnlyAsync(ssid, password, options);
                success = local.ErrorCode == WifiErrorCodes.Success;
                message = success
                    ? $"Connected to {ssid}. App traffic now goes to the device (gateway {NetworkFormat.Ip(local.Data?.GatewayAddress ?? 0)})."
                    : local.ErrorMessage ?? "Connection failed.";
                releaseBtn.IsVisible = success;
                break;
        }

        advConnectBtn.IsEnabled = true;
        ShowResult(message, success ? "Success" : "Danger");
    }

    private async void ReleaseClicked(object sender, EventArgs e)
    {
        var response = await CrossWifiManager.ReleaseLocalOnlyAsync();
        releaseBtn.IsVisible = false;
        ShowResult(response.ErrorMessage ?? "Released.", "Text");
    }

    private void ShowResult(string text, string color)
    {
        advResultTxt.Text = text;
        advResultTxt.TextColor = ThemeColor.Get(color);
        advResultNotice.IsVisible = true;
    }

    private static void SetStatus(Label label, string text, string color)
    {
        label.Text = text;
        label.TextColor = ThemeColor.Get(color);
    }
}

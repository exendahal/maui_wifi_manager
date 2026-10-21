namespace DemoApp;
using MauiWifiManager;
using MauiWifiManager.Abstractions;

/// <summary>
/// Demonstrates the Wi-Fi state and signal events, permissions, metered check, saved networks,
/// QR generation, channel congestion and the advanced connection types.
/// </summary>
public partial class ToolsPage : ContentPage
{
    private const int LocalOnlyMode = 0;
    private const int EnterpriseMode = 1;
    private const int SuggestionMode = 2;

    private bool _IsSubscribed;

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
            _IsSubscribed = false;
        }
    }

    // ---- Status ----

    private async Task RefreshStatusAsync()
    {
        var radio = await CrossWifiManager.IsWifiEnabledAsync();
        if (radio.ErrorCode == WifiErrorCodes.Success)
            SetStatus(radioTxt, radio.Data ? "On" : "Off", radio.Data ? "Success" : "Danger");
        else
            SetStatus(radioTxt, "Not available on this platform", "TextMuted");

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
        MainThread.BeginInvokeOnMainThread(() => SetStatus(radioTxt, e.IsEnabled ? "On" : "Off", e.IsEnabled ? "Success" : "Danger"));

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

    // ---- Channel congestion ----

    private async void AnalyzeChannelsClicked(object sender, EventArgs e)
    {
        var locationError = await LocationAccess.CheckAsync();
        if (locationError != null)
        {
            channelEmptyTxt.Text = locationError;
            return;
        }

        analyzeBtn.IsEnabled = false;
        channelEmptyTxt.Text = "Scanning…";
        channelEmptyTxt.IsVisible = true;
        channelList.Clear();
        channelCharts.Clear();
        channelSelectionTxt.IsVisible = false;

        var scan = await CrossWifiManager.ScanWifiNetworksAsync();
        analyzeBtn.IsEnabled = true;
        if (scan.ErrorCode != WifiErrorCodes.Success || scan.Data == null)
        {
            channelEmptyTxt.Text = scan.ErrorMessage;
            return;
        }

        var usage = WifiChannelUsage.FromScanResults(scan.Data);
        channelEmptyTxt.IsVisible = usage.Count == 0;
        channelEmptyTxt.Text = "No channel information in the scan results.";
        DetailList.Fill(channelList, usage.Select(u => (
            $"{NetworkFormat.Band(u.FrequencyBand) ?? "?"} · ch {u.ChannelNumber}",
            $"{u.NetworkCount} network{(u.NetworkCount == 1 ? "" : "s")}" + (u.StrongestRssiDbm is int dbm ? $" · strongest {dbm} dBm" : ""),
            false)));

        channelTableBtn.IsVisible = usage.Count > 0;
        foreach (var band in usage.Select(u => u.FrequencyBand).Distinct().OrderBy(b => b))
            channelCharts.Add(CreateChart(band, usage.Where(u => u.FrequencyBand == band)));
        channelSelectionTxt.Text = "Tap a bar for details.";
        channelSelectionTxt.TextColor = ThemeColor.Get("TextMuted");
        channelSelectionTxt.IsVisible = usage.Count > 0;
    }

    private View CreateChart(WifiFrequencyBand band, IEnumerable<WifiChannelUsage> usage)
    {
        var chart = new ChannelChart(band, usage);
        var view = new GraphicsView { Drawable = chart, HeightRequest = 150 };
        SemanticProperties.SetDescription(view, $"{NetworkFormat.Band(band)} channel usage: " +
            string.Join(", ", chart.Bars.Where(b => b.NetworkCount > 0).Select(b => $"channel {b.ChannelNumber}, {b.NetworkCount}")));

        void Select(PointF? point)
        {
            var index = point is PointF p ? chart.HitTest(p) : null;
            if (index == chart.SelectedIndex) return;
            // Only one bar is selected across all band charts.
            foreach (var other in channelCharts.OfType<VerticalStackLayout>().SelectMany(s => s.OfType<GraphicsView>()))
                if (other != view && other.Drawable is ChannelChart c && c.SelectedIndex != null)
                {
                    c.SelectedIndex = null;
                    other.Invalidate();
                }
            chart.SelectedIndex = index;
            view.Invalidate();

            if (index is int i)
            {
                var bar = chart.Bars[i];
                channelSelectionTxt.Text = $"{NetworkFormat.Band(band)} · channel {bar.ChannelNumber}: "
                    + (bar.NetworkCount == 0 ? "free"
                        : $"{bar.NetworkCount} network{(bar.NetworkCount == 1 ? "" : "s")}" + (bar.StrongestRssiDbm is int dbm ? $", strongest {dbm} dBm" : ""));
                channelSelectionTxt.TextColor = ThemeColor.Get("Text");
            }
        }

        view.StartInteraction += (_, e) => Select(e.Touches.FirstOrDefault());
        view.StartHoverInteraction += (_, e) => Select(e.Touches.FirstOrDefault());
        view.MoveHoverInteraction += (_, e) => Select(e.Touches.FirstOrDefault());

        return new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = NetworkFormat.Band(band) ?? "Unknown band", Style = DetailList.AppStyle("FieldLabel") },
                view
            }
        };
    }

    private void ChannelTableClicked(object sender, EventArgs e)
    {
        channelList.IsVisible = !channelList.IsVisible;
        channelTableBtn.Text = channelList.IsVisible ? "Hide table" : "Show as table";
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
            SecurityType = password.Length == 0 ? WifiSecurityType.Open : WifiSecurityType.Wpa2Psk
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

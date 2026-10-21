namespace DemoApp;
using MauiWifiManager;
using MauiWifiManager.Abstractions;
using System.Diagnostics;

/// <summary>
/// Devices on the local network (ping sweep + mDNS names) and Wi-Fi channel congestion with a channel recommendation.
/// </summary>
public partial class AnalyzerPage : ContentPage
{
    // Non-overlapping 2.4 GHz channels, and the 5 GHz channels every router supports (no DFS).
    private static readonly int[] Recommended24GHz = { 1, 6, 11 };
    private static readonly int[] Recommended5GHz = { 36, 40, 44, 48, 149, 153, 157, 161 };

    // Friendly names for common mDNS service types.
    private static readonly Dictionary<string, string> ServiceKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["_googlecast._tcp"] = "Chromecast",
        ["_airplay._tcp"] = "AirPlay",
        ["_raop._tcp"] = "AirPlay speaker",
        ["_ipp._tcp"] = "Printer",
        ["_ipps._tcp"] = "Printer",
        ["_printer._tcp"] = "Printer",
        ["_pdl-datastream._tcp"] = "Printer",
        ["_scanner._tcp"] = "Scanner",
        ["_hap._tcp"] = "HomeKit",
        ["_spotify-connect._tcp"] = "Spotify Connect",
        ["_smb._tcp"] = "File sharing",
        ["_afpovertcp._tcp"] = "File sharing",
        ["_companion-link._tcp"] = "Apple device",
        ["_nearbypresence._tcp"] = "Android device",
        ["_sonos._tcp"] = "Sonos",
        ["_http._tcp"] = "Web interface",
    };

    public AnalyzerPage()
    {
        InitializeComponent();
    }

    // ---- Devices ----

    private sealed record DeviceRow(string Ip, string? HostName, long? RoundTripMs, string? Role, List<string> Services);

    private async void ScanDevicesClicked(object sender, EventArgs e)
    {
        devicesBtn.IsEnabled = false;
        devicesList.Clear();
        SetText(devicesSummaryTxt, "Scanning… this takes about 10–15 seconds.", "TextMuted");
        var sw = Stopwatch.StartNew();

        // Ping sweep and mDNS run side by side; mDNS also finds devices that ignore ping.
        var sweepTask = CrossWifiManager.DiscoverDevicesAsync();
        var servicesTask = BrowseAllServicesAsync();
        var info = await CrossWifiManager.GetNetworkInfoAsync();
        var sweep = await sweepTask;
        var services = await servicesTask;

        if (sweep.ErrorCode != WifiErrorCodes.Success)
        {
            SetText(devicesSummaryTxt, sweep.ErrorMessage ?? "Could not scan the network.", "Danger");
            devicesBtn.IsEnabled = true;
            return;
        }

        string? gateway = Ip(info.Data?.GatewayAddress ?? 0);
        string? self = Ip(info.Data?.IpAddress ?? 0);
        var rows = new Dictionary<string, DeviceRow>();
        foreach (var d in sweep.Data!)
            rows[d.IpAddress] = new DeviceRow(d.IpAddress, d.HostName, d.RoundTripMs, null, new());
        if (self != null && !rows.ContainsKey(self))
            rows[self] = new DeviceRow(self, DeviceInfo.Current.Name, null, null, new());
        foreach (var s in services.Where(s => s.IpAddress != null))
        {
            if (!rows.TryGetValue(s.IpAddress!, out var row))
                rows[s.IpAddress!] = row = new DeviceRow(s.IpAddress!, s.HostName, null, null, new());
            var kind = ServiceKinds.GetValueOrDefault(s.ServiceType);
            var label = kind != null && !s.Name.StartsWith("nearby-", StringComparison.Ordinal) ? $"{kind}: {s.Name}" : kind ?? s.Name;
            if (!row.Services.Contains(label))
                row.Services.Add(label);
        }

        var ordered = rows.Values
            .Select(r => r with { Role = r.Ip == gateway ? "Router" : r.Ip == self ? "This device" : null })
            .OrderBy(r => r.Role == "Router" ? 0 : r.Role == "This device" ? 1 : 2)
            .ThenBy(r => System.Net.IPAddress.Parse(r.Ip).GetAddressBytes()[3])
            .ToList();
        foreach (var row in ordered)
            AddDeviceRow(row);

        int named = ordered.Count(r => r.Services.Count > 0 || r.HostName != null);
        SetText(devicesSummaryTxt,
            $"{ordered.Count} device{(ordered.Count == 1 ? "" : "s")} found in {sw.Elapsed.TotalSeconds:0} s, {named} with a name. Devices that ignore ping and don't advertise over mDNS won't appear.",
            "TextMuted");
        devicesBtn.Text = "Scan again";
        devicesBtn.IsEnabled = true;
    }

    /// <summary>Lists the service types on the network, then every instance of each type.</summary>
    private static async Task<List<NetworkServiceInfo>> BrowseAllServicesAsync()
    {
        var types = await CrossWifiManager.BrowseServicesAsync(duration: TimeSpan.FromSeconds(3));
        if (types.Data is not { Count: > 0 })
            return new();
        var perType = await Task.WhenAll(types.Data.Take(10).Select(t => CrossWifiManager.BrowseServicesAsync(t.Name, TimeSpan.FromSeconds(3))));
        return perType.SelectMany(r => r.Data ?? new()).ToList();
    }

    private void AddDeviceRow(DeviceRow device)
    {
        if (devicesList.Count > 0)
            devicesList.Add(new BoxView { Style = DetailList.AppStyle("Divider") });

        string title = device.Role == "This device" ? DeviceInfo.Current.Name
            : device.Services.FirstOrDefault() ?? device.HostName ?? "Unknown device";
        var details = new List<string> { device.Ip };
        if (device.HostName != null && device.HostName != title) details.Add(device.HostName);
        details.AddRange(device.Services.Where(s => s != title));

        var row = new Grid
        {
            ColumnDefinitions = new(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 12,
            Style = DetailList.AppStyle("FieldRow")
        };
        row.Add(new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = title, LineBreakMode = LineBreakMode.TailTruncation },
                new Label { Text = string.Join(" · ", details), Style = DetailList.AppStyle("Muted"), LineBreakMode = LineBreakMode.TailTruncation }
            }
        });
        var right = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End };
        if (device.Role != null)
            right.Add(new Label { Text = device.Role, FontAttributes = FontAttributes.Bold, FontSize = 12, HorizontalTextAlignment = TextAlignment.End });
        if (device.RoundTripMs is long ms)
            right.Add(new Label { Text = $"{ms} ms", Style = DetailList.AppStyle("Muted"), HorizontalTextAlignment = TextAlignment.End });
        row.Add(right, 1);

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await PingDeviceAsync(device.Ip, title);
        row.GestureRecognizers.Add(tap);
        SemanticProperties.SetDescription(row, $"{title}, {string.Join(", ", details)}. Double tap to ping.");
        devicesList.Add(row);
    }

    private async Task PingDeviceAsync(string ip, string title)
    {
        var response = await CrossWifiManager.PingAsync(ip, 4);
        var message = response.Data is { Received: > 0 } stats
            ? $"{stats.Received} of {stats.Sent} replies\nmin {stats.MinMs} ms · avg {stats.AverageMs:0} ms · max {stats.MaxMs} ms\n{stats.PacketLossPercent:0}% packet loss"
            : "No reply. The device may be asleep or block ping.";
        await DisplayAlertAsync(title, message, "OK");
    }

    // ---- Channels ----

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
        recommendationList.Clear();
        channelSelectionTxt.IsVisible = false;

        var infoTask = CrossWifiManager.GetNetworkInfoAsync();
        var scan = await CrossWifiManager.ScanWifiNetworksAsync();
        var info = await infoTask;
        analyzeBtn.IsEnabled = true;
        if (scan.ErrorCode != WifiErrorCodes.Success || scan.Data == null)
        {
            channelEmptyTxt.Text = scan.ErrorMessage;
            return;
        }

        var usage = WifiChannelUsage.FromScanResults(scan.Data);
        channelEmptyTxt.IsVisible = usage.Count == 0;
        channelEmptyTxt.Text = "No channel information in the scan results.";
        if (usage.Count == 0)
            return;

        // Your network: Android reports the channel directly; otherwise use the strongest scan entry with the same SSID.
        var mine = info.Data?.ChannelNumber is int ch
            ? (Band: info.Data.FrequencyBand, Channel: ch)
            : scan.Data.Where(n => n.Ssid != null && n.Ssid == info.Data?.Ssid && n.ChannelNumber != null)
                .OrderByDescending(n => n.RssiDbm ?? int.MinValue)
                .Select(n => ((WifiFrequencyBand Band, int Channel)?)(n.FrequencyBand, n.ChannelNumber!.Value))
                .FirstOrDefault() ?? default;

        ShowRecommendations(scan.Data, usage, info.Data?.Ssid, mine.Channel == 0 ? null : mine);

        DetailList.Fill(channelList, usage.Select(u => (
            $"{NetworkFormat.Band(u.FrequencyBand) ?? "?"} · ch {u.ChannelNumber}",
            $"{u.NetworkCount} network{(u.NetworkCount == 1 ? "" : "s")}" + (u.StrongestRssiDbm is int dbm ? $" · strongest {dbm} dBm" : ""),
            false)));
        channelTableBtn.IsVisible = true;

        foreach (var band in usage.Select(u => u.FrequencyBand).Distinct().OrderBy(b => b))
            channelCharts.Add(CreateChart(band, usage.Where(u => u.FrequencyBand == band), mine.Band == band ? mine.Channel : null));
        channelSelectionTxt.IsVisible = true;
        if (mine.Channel == 0)
            SetText(channelSelectionTxt, "Tap a bar for details.", "TextMuted");
    }

    private void ShowRecommendations(List<NetworkData> networks, List<WifiChannelUsage> usage, string? ssid, (WifiFrequencyBand Band, int Channel)? mine)
    {
        var rows = new List<(string, string, bool)>();
        if (mine is { } m)
        {
            // Your own access point is in the scan too, so it is not counted against you.
            int others = usage.FirstOrDefault(u => u.FrequencyBand == m.Band && u.ChannelNumber == m.Channel)?.NetworkCount - 1 ?? 0;
            rows.Add(("Your network", $"{ssid} · {NetworkFormat.Band(m.Band)} ch {m.Channel} · {(others <= 0 ? "no other networks" : $"{others} other network{(others == 1 ? "" : "s")}")}", false));
        }

        var on24 = networks.Where(n => n.FrequencyBand == WifiFrequencyBand.Band2_4GHz && n.ChannelNumber != null).ToList();
        if (on24.Count > 0)
        {
            // 2.4 GHz channels overlap with neighbours up to 4 channels away, so count those too.
            var best = Recommended24GHz
                .Select(c => (Channel: c, Load: on24.Count(n => Math.Abs(n.ChannelNumber!.Value - c) <= 4)))
                .OrderBy(x => x.Load).ThenBy(x => x.Channel).First();
            rows.Add(("Best 2.4 GHz", $"Channel {best.Channel} · {best.Load} overlapping network{(best.Load == 1 ? "" : "s")}", false));
        }

        var on5 = networks.Where(n => n.FrequencyBand == WifiFrequencyBand.Band5GHz && n.ChannelNumber != null).ToList();
        if (on5.Count > 0)
        {
            var best = Recommended5GHz
                .Select(c => (Channel: c, Load: on5.Count(n => n.ChannelNumber == c)))
                .OrderBy(x => x.Load).ThenBy(x => x.Channel).First();
            rows.Add(("Best 5 GHz", $"Channel {best.Channel} · {(best.Load == 0 ? "unused nearby" : $"{best.Load} network{(best.Load == 1 ? "" : "s")}")}", false));
        }

        DetailList.Fill(recommendationList, rows);
        if (rows.Count > 0)
            recommendationList.Add(new BoxView { Style = DetailList.AppStyle("Divider") });
    }

    private View CreateChart(WifiFrequencyBand band, IEnumerable<WifiChannelUsage> usage, int? myChannel)
    {
        var chart = new ChannelChart(band, usage);
        var view = new GraphicsView { Drawable = chart, HeightRequest = 150 };
        SemanticProperties.SetDescription(view, $"{NetworkFormat.Band(band)} channel usage: " +
            string.Join(", ", chart.Bars.Where(b => b.NetworkCount > 0).Select(b => $"channel {b.ChannelNumber}, {b.NetworkCount}")));

        void ShowSelection(int? index)
        {
            // Only one bar is selected across all band charts.
            foreach (var other in channelCharts.OfType<VerticalStackLayout>().SelectMany(s => s.OfType<GraphicsView>()))
                if (other != view && other.Drawable is ChannelChart c && c.SelectedIndex != null)
                {
                    c.SelectedIndex = null;
                    other.Invalidate();
                }
            chart.SelectedIndex = index;
            view.Invalidate();
            if (index is not int i)
                return;
            var bar = chart.Bars[i];
            var mineNote = bar.ChannelNumber == myChannel ? " (your network)" : string.Empty;
            SetText(channelSelectionTxt, $"{NetworkFormat.Band(band)} · channel {bar.ChannelNumber}{mineNote}: "
                + (bar.NetworkCount == 0 ? "free"
                    : $"{bar.NetworkCount} network{(bar.NetworkCount == 1 ? "" : "s")}" + (bar.StrongestRssiDbm is int dbm ? $", strongest {dbm} dBm" : "")), "Text");
        }

        void Select(PointF? point)
        {
            var index = point is PointF p ? chart.HitTest(p) : null;
            if (index != chart.SelectedIndex)
                ShowSelection(index);
        }

        view.StartInteraction += (_, e) => Select(e.Touches.FirstOrDefault());
        view.StartHoverInteraction += (_, e) => Select(e.Touches.FirstOrDefault());
        view.MoveHoverInteraction += (_, e) => Select(e.Touches.FirstOrDefault());

        // Start with your own channel selected.
        if (myChannel is int mine && chart.Bars.FindIndex(b => b.ChannelNumber == mine) is int start and >= 0)
            ShowSelection(start);

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

    private static void SetText(Label label, string text, string color)
    {
        label.Text = text;
        label.TextColor = ThemeColor.Get(color);
    }

    private static string? Ip(int address) =>
        address == 0 ? null : new System.Net.IPAddress(BitConverter.GetBytes(address)).ToString();
}

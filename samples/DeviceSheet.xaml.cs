using CommunityToolkit.Maui.Views;
using MauiWifiManager;
using MauiWifiManager.Abstractions;

namespace DemoApp;

/// <summary>
/// Details for a device on the local network: what the scan already knows is shown at once,
/// then <see cref="CrossWifiManager.GetDeviceDetailsAsync"/> fills in names, MAC, model, ports and ping.
/// </summary>
public partial class DeviceSheet : Popup
{
    // mDNS TXT keys that commonly carry a model or friendly name.
    private static readonly string[] ModelKeys = { "md", "model", "ty", "usb_MDL", "product", "fn", "am" };

    private readonly string _Ip;
    private readonly string? _Role;
    private readonly IReadOnlyList<NetworkServiceInfo> _Services;
    private Uri? _WebUrl;

    public DeviceSheet(string title, string ip, string? role, IReadOnlyList<NetworkServiceInfo> services)
    {
        InitializeComponent();
        _Ip = ip;
        _Role = role;
        _Services = services;
        titleTxt.Text = title;
        summaryTxt.Text = role ?? ip;
        Fill(null);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var response = await CrossWifiManager.GetDeviceDetailsAsync(_Ip);
        loadingRow.IsVisible = false;
        // Replace a placeholder title with the best name the device gave us.
        if (titleTxt.Text == "Unknown device" && response.Data is { } d
            && (d.FriendlyName ?? d.NetBiosName ?? d.HttpTitle ?? d.HostName) is { } name)
            titleTxt.Text = name;
        Fill(response.Data);
        if (response.ErrorCode != WifiErrorCodes.Success)
            summaryTxt.Text = response.ErrorMessage;
    }

    private void Fill(LanDeviceDetails? details)
    {
        var rows = new List<(string Label, string Value, bool Mono)> { ("IP address", _Ip, true) };
        if (details != null)
        {
            if (details.MacAddress != null)
                rows.Add(("MAC address", details.MacAddress + (details.IsRandomizedMac == true ? " (private)" : string.Empty), true));
            if (details.HostName != null)
                rows.Add(("Host name", details.HostName, false));
            if (details.NetBiosName != null)
                rows.Add(("Computer name", details.NetBiosName, false));
            if (details.FriendlyName != null)
                rows.Add(("Name", details.FriendlyName, false));
            if (details.Manufacturer != null)
                rows.Add(("Manufacturer", details.Manufacturer, false));
            var model = string.Join(" ", new[] { details.ModelName, details.ModelNumber }.Where(s => !string.IsNullOrEmpty(s)));
            if (model.Length > 0)
                rows.Add(("Model", model, false));
        }

        // mDNS: the services the scan found, plus model hints from their TXT records.
        foreach (var service in _Services)
        {
            rows.Add(("Service", $"{service.Name} ({service.ServiceType}){(service.Port > 0 ? $" · port {service.Port}" : "")}", false));
            foreach (var key in ModelKeys)
                if (service.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    rows.Add(($"  {key}", value, false));
        }

        if (details != null)
        {
            if (details.HttpTitle != null || details.HttpServer != null)
                rows.Add(("Web interface", string.Join(" · ", new[] { details.HttpTitle, details.HttpServer }.Where(s => s != null)), false));
            rows.Add(("Open ports", details.OpenPorts.Count == 0
                ? "None of the common ports"
                : string.Join("\n", details.OpenPorts.Select(p => $"{p} {LanDeviceDetails.KnownPorts.GetValueOrDefault(p, "")}".Trim())), false));
            rows.Add(("Ping", details.Ping is { Received: > 0 } ping
                ? $"{ping.AverageMs:0} ms average · {ping.PacketLossPercent:0}% loss"
                : "No reply (asleep or blocks ping)", false));

            var kind = GuessKind(details);
            if (kind != null)
                summaryTxt.Text = _Role != null ? $"{_Role} · looks like {kind}" : $"Looks like {kind}";
            privateMacTxt.IsVisible = details.IsRandomizedMac == true;

            _WebUrl = details.OpenPorts.Contains(80) ? new Uri($"http://{_Ip}/")
                : details.OpenPorts.Contains(8080) ? new Uri($"http://{_Ip}:8080/")
                : details.OpenPorts.Contains(443) ? new Uri($"https://{_Ip}/")
                : null;
            openWebBtn.IsVisible = _WebUrl != null;
        }

        DetailList.Fill(detailsList, rows);
    }

    /// <summary>Best guess at the device type from its services, ports and names.</summary>
    private string? GuessKind(LanDeviceDetails details)
    {
        var ports = details.OpenPorts;
        var types = _Services.Select(s => s.ServiceType).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var text = string.Join(" ", new[] { details.HttpTitle, details.FriendlyName, details.ModelName, details.Manufacturer }).ToLowerInvariant();

        if (_Role == "Router" || text.Contains("router") || text.Contains("gateway"))
            return "a router";
        if (types.Contains("_googlecast._tcp") || ports.Contains(8008) || ports.Contains(8009))
            return "a Chromecast or Google TV";
        if (types.Contains("_ipp._tcp") || types.Contains("_printer._tcp") || ports.Contains(9100) || ports.Contains(631))
            return "a printer";
        if (types.Contains("_airplay._tcp") || ports.Contains(7000))
            return "an Apple TV or AirPlay speaker";
        if (ports.Contains(62078))
            return "an iPhone or iPad";
        if (types.Contains("_nearbypresence._tcp"))
            return "an Android phone or tablet";
        if (ports.Contains(554))
            return "a camera";
        if (ports.Contains(3389))
            return "a Windows PC";
        if (ports.Contains(445) || ports.Contains(548) || types.Contains("_smb._tcp"))
            return "a computer or NAS";
        if (ports.Contains(1883) || types.Contains("_hap._tcp"))
            return "a smart home device";
        if (ports.Contains(80) || ports.Contains(443))
            return "a device with a web interface";
        return null;
    }

    private async void OpenWebClicked(object sender, EventArgs e)
    {
        if (_WebUrl != null)
            await Launcher.OpenAsync(_WebUrl);
    }

    private async void CloseClicked(object sender, EventArgs e) => await CloseAsync();
}

namespace DemoApp;
using CommunityToolkit.Maui.Extensions;
using MauiWifiManager;
using MauiWifiManager.Abstractions;

public partial class ConnectWifi : ContentPage, IQueryAttributable
{
    public const string NetworkQueryKey = "network";

    // Same order as the picker items.
    private static readonly WifiSecurityType[] SecurityOptions =
    [
        WifiSecurityType.Wpa2Psk,
        WifiSecurityType.Wpa3Sae,
        WifiSecurityType.WpaPsk,
        WifiSecurityType.Wep,
        WifiSecurityType.Open
    ];

    public ConnectWifi()
    {
        InitializeComponent();
        securityPicker.SelectedIndex = 0;
    }

    private WifiSecurityType SelectedSecurity =>
        securityPicker.SelectedIndex >= 0 ? SecurityOptions[securityPicker.SelectedIndex] : WifiSecurityType.Wpa2Psk;

    /// <summary>Receives a network picked on the Networks tab.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(NetworkQueryKey, out var value) && value is NetworkDataModel network)
        {
            Fill(network.Ssid, password: null, ToSecurityType(network.Security), network.IsHidden, network.Bssid);
            HideResult();
            (network.IsHidden ? ssidEntry : passwordEntry).Focus();
        }
    }

    private async void ConnectClicked(object sender, EventArgs e)
    {
        var ssid = ssidEntry.Text?.Trim();
        var security = SelectedSecurity;
        var password = security == WifiSecurityType.Open ? string.Empty : passwordEntry.Text ?? string.Empty;

        if (string.IsNullOrEmpty(ssid))
        {
            ShowResult("Enter the network name.", isError: true);
            return;
        }
        if (security != WifiSecurityType.Open && password.Length == 0)
        {
            ShowResult("Enter the password, or set security to None.", isError: true);
            return;
        }

        var locationError = await LocationAccess.CheckAsync();
        if (locationError != null)
        {
            ShowResult(locationError, isError: true);
            return;
        }

        var options = new WifiConnectionOptions
        {
            SecurityType = security,
            IsHidden = hiddenSwitch.IsToggled,
            Bssid = string.IsNullOrWhiteSpace(bssidEntry.Text) ? null : bssidEntry.Text.Trim()
        };

        connectBtn.IsEnabled = false;
        connectBtn.Text = "Connecting…";
        HideResult();
        try
        {
            var response = await CrossWifiManager.ConnectWifiAsync(ssid, password, options);
            if (response.ErrorCode == WifiErrorCodes.Success)
                ShowResult($"Connected to {response.Data?.Ssid ?? ssid}.", isError: false);
            else
                ShowResult(response.ErrorMessage ?? "Could not connect.", isError: true);
        }
        finally
        {
            connectBtn.IsEnabled = true;
            connectBtn.Text = "Connect";
        }
    }

    private async void ScanQrClicked(object sender, EventArgs e)
    {
        if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
        {
            ShowResult("Camera permission is needed to scan a QR code.", isError: true);
            return;
        }

        var popup = new ScanQr();
        await this.ShowPopupAsync(popup, AppPopup.Options);
        if (popup.Result is not WifiQrCode code)
            return;

        Fill(code.Ssid, code.Password, code.Security, code.IsHidden, bssid: null);
        ShowResult($"Scanned \"{code.Ssid}\". Check the details, then tap Connect.", isError: false);
    }

    private void SecurityChanged(object? sender, EventArgs e) =>
        passwordRow.IsVisible = SelectedSecurity != WifiSecurityType.Open;

    private void TogglePasswordClicked(object sender, EventArgs e)
    {
        passwordEntry.IsPassword = !passwordEntry.IsPassword;
        showPasswordBtn.Text = passwordEntry.IsPassword ? "Show" : "Hide";
    }

    private void Fill(string ssid, string? password, WifiSecurityType security, bool isHidden, string? bssid)
    {
        ssidEntry.Text = ssid;
        passwordEntry.Text = password ?? string.Empty;
        securityPicker.SelectedIndex = Array.IndexOf(SecurityOptions, security);
        hiddenSwitch.IsToggled = isHidden;
        bssidEntry.Text = bssid ?? string.Empty;
    }

    private static WifiSecurityType ToSecurityType(string? label) => label switch
    {
        "WPA3" => WifiSecurityType.Wpa3Sae,
        "WPA" => WifiSecurityType.WpaPsk,
        "WEP" => WifiSecurityType.Wep,
        "Open" => WifiSecurityType.Open,
        _ => WifiSecurityType.Wpa2Psk
    };

    private void ShowResult(string message, bool isError)
    {
        resultTxt.Text = message;
        resultTxt.TextColor = ThemeColor.Get(isError ? "Danger" : "Text");
        resultNotice.IsVisible = true;
    }

    private void HideResult() => resultNotice.IsVisible = false;
}

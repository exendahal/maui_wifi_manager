namespace DemoApp;
using MauiWifiManager;
using MauiWifiManager.Abstractions;

public partial class ConnectWifi : ContentPage
{
    public ConnectWifi()
    {
        InitializeComponent();
        SecurityTypePicker.SelectedIndex = 0;
    }

    private async void ConnectBtnClicked(object sender, EventArgs e)
    {
        var securityType = SecurityTypePicker.SelectedIndex switch
        {
            1 => WifiSecurityType.Wpa3Sae,
            2 => WifiSecurityType.WpaPsk,
            3 => WifiSecurityType.Open,
            4 => WifiSecurityType.Wep,
            _ => WifiSecurityType.Wpa2Psk
        };

        bool isOpen = securityType == WifiSecurityType.Open;

        if (string.IsNullOrWhiteSpace(WifiSsid.Text))
        {
            await DisplayAlertAsync("Empty SSID", "SSID cannot be empty", "OK");
            return;
        }

        if (!isOpen && string.IsNullOrWhiteSpace(WifiPassword.Text))
        {
            await DisplayAlertAsync("Empty Password", "Password cannot be empty for this security type", "OK");
            return;
        }

        var options = new WifiConnectionOptions
        {
            IsHidden = IsHiddenSwitch.IsToggled,
            SecurityType = securityType
        };

        var response = await CrossWifiManager.Current.ConnectWifiAsync(
            WifiSsid.Text,
            isOpen ? string.Empty : WifiPassword.Text,
            options);

        if (response.ErrorCode == WifiErrorCodes.Success)
            await DisplayAlertAsync("Wi-Fi Info", response?.Data?.Ssid?.ToString(), "OK");
        else
            await DisplayAlertAsync("Wi-Fi Info", response.ErrorMessage, "OK");
    }
}

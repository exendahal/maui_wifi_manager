namespace DemoApp;
using MauiWifiManager;
using MauiWifiManager.Abstractions;

public partial class ConnectWifi : ContentPage
{
	public ConnectWifi()
	{
		InitializeComponent();
	}

    private async void ConnectBtnClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(WifiSsid.Text) || string.IsNullOrWhiteSpace(WifiPassword.Text))
        {
            await DisplayAlert("Empty SSID or Password", "SSID and Password cannot be empty", "OK");
            return;
        }

        var response = await CrossWifiManager.Current.ConnectWifi(WifiSsid.Text, WifiPassword.Text);
        if (response.ErrorCode == WifiErrorCodes.Success)
            await DisplayAlert("Wi-Fi Info", response?.Data?.Ssid?.ToString(),"OK");
    }
}
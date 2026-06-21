using CommunityToolkit.Maui.Extensions;
using MauiWifiManager;
using MauiWifiManager.Abstractions;

namespace DemoApp;

public partial class ScanAndConnect : ContentPage
{
	public ScanAndConnect()
    {
		InitializeComponent();       
    }
    private async void ConnectBtnClicked(object sender, EventArgs e)
    {

        var popup = new ScanQr();
        await this.ShowPopupAsync(popup);
        var responseString = popup.ScanResult;
        if (!string.IsNullOrWhiteSpace(responseString))
        {
            var wifiParts = responseString.Split(':', 2);
            string ssid = wifiParts[0];
            string password = wifiParts.Length > 1 ? wifiParts[1] : string.Empty;
            var response = await CrossWifiManager.Current.ConnectWifiAsync(ssid, password);
            if (response.ErrorCode == WifiErrorCodes.Success)
            {
                await DisplayAlertAsync("Wi-Fi Info", response?.Data?.NativeObject?.ToString(), "OK");
            }
        }       
    }
}
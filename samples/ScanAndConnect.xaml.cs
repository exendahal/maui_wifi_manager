using CommunityToolkit.Maui.Views;
using Plugin.MauiWifiManager;

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
        var result = await this.ShowPopupAsync(popup);
        var responseString = result?.ToString();
        if (!string.IsNullOrWhiteSpace(responseString))
        {
            string ssid = responseString.Split(':')[0];
            string password = responseString.Split(':')[1];
            var response = await CrossWifiManager.Current.ConnectWifi(ssid, password);
            if (response?.NativeObject != null)
            {
                await DisplayAlert("Wi-Fi Info", response?.NativeObject?.ToString(), "OK");
            }
        }       
    }
}
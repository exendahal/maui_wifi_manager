using Plugin.MauiWifiManager;

namespace DemoApp;

public partial class HotspotPage : ContentPage
{
	public HotspotPage()
	{
		InitializeComponent();
    }

    private async void StartBtnClicked(object sender, EventArgs e)
    {
        var btn = (Button)sender;
        if (btn.Text == "Start Hotspot")
        {
            var info = await CrossWifiManager.Current.StartLocalHotspot();
            if (info != null && !string.IsNullOrWhiteSpace(info.Ssid))
            {
                string ssid = info.Ssid;
                string password = info.Password;
                string encryptionType = "WPA2-PSK";
                string qrCodeData = $"WIFI:T:{encryptionType};S:{ssid};P:{password};;";
                WiFiQR.Value = qrCodeData;
                HotSpotSsid.Text = $"SSID: {ssid}";
                HotSpotPassword.Text = $"Password: {password}";
                HotSpotStack.IsVisible = true;
                StartBtn.Text = "Stop Hotspot";
            }
        }
        else
        {
            await CrossWifiManager.Current.StopLocalHotspot();
            HotSpotStack.IsVisible = false;
            btn.Text = "Start Hotspot";
        }
        
       
    }
}
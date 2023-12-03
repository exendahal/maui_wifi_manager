namespace DemoApp;
using Plugin.MauiWifiManager;

public partial class DisconnectWifi : ContentPage
{
    private string ssid {  get; set; }
	public DisconnectWifi()
	{
		InitializeComponent();
	}

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        PermissionStatus status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted || DeviceInfo.Current.Platform == DevicePlatform.WinUI)
        {
            var response = await CrossWifiManager.Current.GetNetworkInfo();
            ssid = response.Ssid;
            ssidTxt.Text = ssid;           
        }
        else
            await DisplayAlert("No location permisson", "Please provide location permission", "OK");
    }
    private async void DisConnectBtnClicked(object sender, EventArgs e)
    {
		var result = await DisplayAlert("Disconnect " + ssid,"Do you want to disconnect current connected " + ssid + "?","YES","NO");
		if (result)
		{
            CrossWifiManager.Current.DisconnectWifi(ssid);
        }
    }
}
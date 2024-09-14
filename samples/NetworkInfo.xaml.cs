namespace DemoApp;
using Plugin.MauiWifiManager;
using System.Net;

public partial class NetworkInfo : ContentPage
{
	public NetworkInfo()
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
            Console.WriteLine(response.NativeObject);
            IPAddress ipAddress = new IPAddress(BitConverter.GetBytes(response.IpAddress));
            wifiSsid.Text = response.Ssid;
            wifiBssid.Text = response?.Bssid?.ToString();
            ipAddressTxt.Text = ipAddress.ToString();
            gatewayAddress.Text = response?.GatewayAddress;
            nativeObject.Text = response?.NativeObject?.ToString();
        }
        else
            await DisplayAlert("No location permisson", "Please provide location permission", "OK");
    }
}
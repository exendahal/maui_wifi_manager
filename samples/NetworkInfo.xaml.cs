namespace DemoApp;
using MauiWifiManager;
using MauiWifiManager.Abstractions;
using System.Diagnostics;
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
            if (response.ErrorCode == WifiErrorCodes.Success)
            {
                Debug.WriteLine(response?.Data?.NativeObject);
                IPAddress ipAddress = new IPAddress(BitConverter.GetBytes(response.Data.IpAddress));
                wifiSsid.Text = response.Data.Ssid;
                wifiBssid.Text = response?.Data?.Bssid?.ToString();
                ipAddressTxt.Text = ipAddress.ToString();
                nativeObject.Text = response?.Data?.NativeObject?.ToString();

            }
            
        }
        else
            await DisplayAlert("No location permisson", "Please provide location permission", "OK");
    }
}
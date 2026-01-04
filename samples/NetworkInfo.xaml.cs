namespace DemoApp;
using MauiWifiManager;
using MauiWifiManager.Abstractions;
using System.Diagnostics;
using System.Net;
using System.Text;

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
            if (response != null)
            {
                if (response.ErrorCode == WifiErrorCodes.Success)
                {
                    if (response.Data != null)
                    {
                        IPAddress ipAddress = new(BitConverter.GetBytes(response.Data.IpAddress));
                        IPAddress gateway = new(BitConverter.GetBytes(response.Data.GatewayAddress));
                        IPAddress serverAddress = new(BitConverter.GetBytes(response.Data.DhcpServerAddress));
                        wifiSsid.Text = response.Data.Ssid;
                        wifiBssid.Text = response.Data.Bssid?.ToString();
                        ipAddressTxt.Text = $"IP:{ipAddress.ToString()}\nGateway:{gateway.ToString()}\nDHCP Server:{serverAddress.ToString()}";
                        securityTxt.Text = response.Data.SecurityType?.ToString();
                        if (response.Data.NativeObject != null)
                        {
                            Debug.WriteLine(response.Data.NativeObject);
                            nativeObject.Text = FormatNativeObject(response.Data.NativeObject);
                        }                       
                    }
                }
            }
           
            
        }
        else
            await DisplayAlertAsync("No location permission", "Please provide location permission", "OK");
    }
    private string FormatNativeObject(object nativeObject)
    {
        if (nativeObject == null) return "No data available";

        var sb = new StringBuilder();
        Type type = nativeObject.GetType();

        sb.AppendLine($"Object Type: {type.Name}");

        // Get all properties dynamically
        foreach (var prop in type.GetProperties())
        {
            try
            {
                var value = prop.GetValue(nativeObject, null);
                sb.AppendLine($"{prop.Name}: {value}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{prop.Name}: Error retrieving value ({ex.Message})");
            }
        }

        return sb.ToString();
    }

}
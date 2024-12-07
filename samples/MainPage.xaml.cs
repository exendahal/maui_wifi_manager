using DemoApp.Services.Interfaces;
using Plugin.MauiWifiManager;
namespace DemoApp
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }        

        private async void ScanTapped(object sender, TappedEventArgs e)
        {
            var gpsStatus = await IPlatformApplication.Current.Services.GetService<IGpsService>().GpsStatus();
            if (gpsStatus)
            {
                PermissionStatus status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status == PermissionStatus.Granted || DeviceInfo.Current.Platform == DevicePlatform.WinUI)
                    await Navigation.PushAsync(new ScanListPage());
                else
                    await DisplayAlert("No location permission", "Please provide location permission", "OK");
            }
            else
            {
                await DisplayAlert("No location", "Please turn on location service", "OK");
            }
            
           
        }

        private async void ConnectWiFiTapped(object sender, TappedEventArgs e)
        {
            var gpsStatus = await IPlatformApplication.Current.Services.GetService<IGpsService>().GpsStatus();
            if (gpsStatus)
            {
                PermissionStatus status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status == PermissionStatus.Granted || DeviceInfo.Current.Platform == DevicePlatform.WinUI)
                    await Navigation.PushAsync(new ConnectWifi());
                else
                    await DisplayAlert("No location permission", "Please provide location permission", "OK");
            }
            else
            {
                await DisplayAlert("No location", "Please turn on location service", "OK");
            }

        }

        private async void NetworkInfoTapped(object sender, TappedEventArgs e)
        {
            var gpsStatus = await IPlatformApplication.Current.Services.GetService<IGpsService>().GpsStatus();
            if (gpsStatus)
            {
                PermissionStatus status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status == PermissionStatus.Granted || DeviceInfo.Current.Platform == DevicePlatform.WinUI)
                    await Navigation.PushAsync(new NetworkInfo());
                else
                    await DisplayAlert("No location permission", "Please provide location permission", "OK");
            }
            else
            {
                await DisplayAlert("No location", "Please turn on location service", "OK");
            }
                        
        }

        private async void DisconnectSettingTapped(object sender, TappedEventArgs e)
        {
            var gpsStatus = await IPlatformApplication.Current.Services.GetService<IGpsService>().GpsStatus();
            if (gpsStatus)
            {
                PermissionStatus status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status == PermissionStatus.Granted || DeviceInfo.Current.Platform == DevicePlatform.WinUI)
                    await Navigation.PushAsync(new DisconnectWifi());
                else
                    await DisplayAlert("No location permission", "Please provide location permission", "OK");
            } else
            {
                await DisplayAlert("No location", "Please turn on location service", "OK");
            }

        }

        private async void OpenSettingTapped(object sender, TappedEventArgs e)
        {
            var result = await DisplayAlert("Open setting", "Do you want to open Wi-Fi setting?", "YES", "NO");
            if (result)
            {
                await CrossWifiManager.Current.OpenWifiSetting();
            }
        }

        private async void NetworkSettingOpen(object sender, TappedEventArgs e)
        {
            await CrossWifiManager.Current.OpenWirelessSetting();
        }
        private async void InfoTapped(object sender, TappedEventArgs e)
        {
             await DisplayAlert("MAUI Wi-Fi Manager", "Target Framework: .NET 8\nDeveloped by: Santosh Dahal", "OK");
        }
        
    }

}

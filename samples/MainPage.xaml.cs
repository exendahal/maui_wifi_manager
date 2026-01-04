using DemoApp.Services.Interfaces;
using MauiWifiManager;

namespace DemoApp
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            var assemblyName = "MauiWifiManager";
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (assembly != null)
            {
                var version = assembly.GetName().Version;
                VersionNo.Text = $"Version No: {version}";
            }                
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
                    await DisplayAlertAsync("No location permission", "Please provide location permission", "OK");
            }
            else
                await DisplayAlertAsync("No location", "Please turn on location service", "OK");
        }

        private async void ConnectWiFiTapped(object sender, TappedEventArgs e)
        {
            var gpsStatus = await IPlatformApplication.Current.Services.GetService<IGpsService>().GpsStatus();
            if (gpsStatus)
            {
                PermissionStatus status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status == PermissionStatus.Granted || DeviceInfo.Current.Platform == DevicePlatform.WinUI)
                    await Navigation.PushModalAsync(new ConnectWifiContainer());
                else
                    await DisplayAlertAsync("No location permission", "Please provide location permission", "OK");
            }
            else
                await DisplayAlertAsync("No location", "Please turn on location service", "OK");

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
                    await DisplayAlertAsync("No location permission", "Please provide location permission", "OK");
            }
            else
                await DisplayAlertAsync("No location", "Please turn on location service", "OK");

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
                    await DisplayAlertAsync("No location permission", "Please provide location permission", "OK");
            } 
            else
                await DisplayAlertAsync("No location", "Please turn on location service", "OK");

        }

        private async void OpenSettingTapped(object sender, TappedEventArgs e)
        {
            var result = await DisplayAlertAsync("Open setting", "Do you want to open Wi-Fi setting?", "YES", "NO");
            if (result)
            {
                await CrossWifiManager.Current.OpenWifiSetting();
            }
        }

        private async void NetworkSettingOpen(object sender, TappedEventArgs e)
        {
            var result = await DisplayAlertAsync("Open setting", "Do you want to open Wi-Fi setting?", "YES", "NO");
            if (result)
            {
                await CrossWifiManager.Current.OpenWirelessSetting();
            }           
        }
        private async void InfoTapped(object sender, TappedEventArgs e)
        {
             await DisplayAlertAsync("MAUI Wi-Fi Manager", "Target Framework: .NET 10\nDeveloped by: Santosh Dahal", "OK");
        }        
    }

}

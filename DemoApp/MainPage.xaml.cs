using Plugin.MauiWifiManager;
using System.Net;
namespace DemoApp
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
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
        }

        private async void InfoBtnClicked(object sender, EventArgs e)
        {
            PermissionStatus status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status == PermissionStatus.Granted)
            {
                var response = await CrossWifiManager.Current.GetNetworkInfo();
                Console.WriteLine(response.NativeObject);
                IPAddress ipAddress = new IPAddress(BitConverter.GetBytes(response.IpAddress));
                await DisplayAlert(response.Ssid,"IP Address: " + ipAddress, "OK");
            }                
            else
                await DisplayAlert("No location permisson","Please provide location permission","OK");
        }

        private void DisconnectBtnClicked(object sender, EventArgs e)
        {
            CrossWifiManager.Current.DisconnectWifi(WifiSsid.Text);
        }

        private async void OpenWifiSettingClicked(object sender, EventArgs e)
        {
            var response = await CrossWifiManager.Current.OpenWifiSetting();
        }

        private async void StartScanClicked(object sender, EventArgs e)
        {
            var response = await CrossWifiManager.Current.ScanWifiNetworks();
        }

        private async void ScanListClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new ScanListPage());
        }
    }

}

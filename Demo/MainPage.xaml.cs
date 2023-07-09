using Plugin.MauiWifiManager;

namespace Demo
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
                await DisplayAlert("Empty SSID or Password","SSID and Password cannot be empty","OK");
                return;
            }

            var response = await CrossWifiManager.Current.ConnectWifi(WifiSsid.Text, WifiPassword.Text);
        }

        private async void InfoBtnClicked(object sender, EventArgs e)
        {
            var response = await CrossWifiManager.Current.GetNetworkInfo();
        }

        private void DisconnectBtnClicked(object sender, EventArgs e)
        {
            CrossWifiManager.Current.DisconnectWifi(WifiSsid.Text);
        }

        private void OpenWifiSettingClicked(object sender, EventArgs e)
        {
            CrossWifiManager.Current.OpenWifiSetting();
        }
    }
}
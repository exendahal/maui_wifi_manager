using Plugin.MauiWifiManager;
using Plugin.MauiWifiManager.Abstractions;

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

            var response = CrossWifiManager.Current.ConnectWifi(WifiSsid.Text, WifiPassword.Text);
        }

        private void InfoBtnClicked(object sender, EventArgs e)
        {

        }

        private void DisconnectBtnClicked(object sender, EventArgs e)
        {

        }
    }
}
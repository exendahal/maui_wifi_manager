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
            await Navigation.PushAsync(new ScanListPage());
        }

        private async void ConnectWiFiTapped(object sender, TappedEventArgs e)
        {
            await Navigation.PushAsync(new ConnectWifi());
        }

        private async void NetworkInfoTapped(object sender, TappedEventArgs e)
        {
            await Navigation.PushAsync(new NetworkInfo());
        }

        private async void DisconnectSettingTapped(object sender, TappedEventArgs e)
        {
            await Navigation.PushAsync(new DisconnectWifi());
        }

        private async void OpenSettingTapped(object sender, TappedEventArgs e)
        {
            var result = await DisplayAlert("Open setting", "Do you want to open Wi-Fi setting?", "YES", "NO");
            if (result)
            {
                await CrossWifiManager.Current.OpenWifiSetting();
            }
        }

        private async void InfoTapped(object sender, TappedEventArgs e)
        {
             await DisplayAlert("MAUI Wi-Fi Manager", "Target Framework: .NET 8\nDeveloped by: Santosh Dahal", "OK");
        }
    }

}

namespace DemoApp;
using Plugin.MauiWifiManager;
using System.ComponentModel.DataAnnotations;

public partial class ScanListPage : ContentPage
{
    List<NetworkDataModel> networkDataModel = new List<NetworkDataModel>();
    public ScanListPage()
	{
		InitializeComponent();
	}

    protected override void OnAppearing()
    {
        base.OnAppearing();        
    }
    private async void ScanClicked(object sender, EventArgs e)
    {
        PermissionStatus status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted || DeviceInfo.Current.Platform == DevicePlatform.WinUI)
        {
            var response = await CrossWifiManager.Current.ScanWifiNetworks();
            foreach (var item in response)
            {
                networkDataModel.Add(new NetworkDataModel() { Bssid = item.Bssid,Ssid=item.Ssid});
            }
            scanCollectionView.ItemsSource = networkDataModel;
        }
        else
            await DisplayAlert("No location permisson", "Please provide location permission", "OK");
    }

    private async void ScanCollectionViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        NetworkDataModel previous = (e.PreviousSelection.FirstOrDefault() as NetworkDataModel);
        NetworkDataModel current = (e.CurrentSelection.FirstOrDefault() as NetworkDataModel);
        if (!string.IsNullOrWhiteSpace(current.SsidName))
        {
            var response = await DisplayPromptAsync("Connect "+ current.SsidName, "Enter password to connect");
            if (!string.IsNullOrWhiteSpace(response) && response.Length >= 8)
            {
                var status = await CrossWifiManager.Current.ConnectWifi(current.SsidName, response);
            }
        }
    }
}
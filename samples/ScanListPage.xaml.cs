namespace DemoApp;
using Plugin.MauiWifiManager;

public partial class ScanListPage : ContentPage
{
    List<NetworkDataModel> networkDataModel = [];
    public Command InfoCommand { get; }
    public Command ConnectCommand { get; }
    public ScanListPage()
	{
		InitializeComponent();
        BindingContext = this;
        InfoCommand = new Command<NetworkDataModel>(ExecuteInfoCommand);
        ConnectCommand = new Command<NetworkDataModel>(ExecuteConnectCommand);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await GetWifiList();
    }
    private async void ExecuteConnectCommand(NetworkDataModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.SsidName))
        {
            var response = await DisplayPromptAsync("Connect " + model.SsidName, "Enter password to connect");
            if (!string.IsNullOrWhiteSpace(response) && response.Length >= 8)
            {
                var status = await CrossWifiManager.Current.ConnectWifi(model.SsidName, response);
            }
        }
    }

    private async void ExecuteInfoCommand(NetworkDataModel model)
    {
        var info = $"StatusId: {model.StatusId}, " +
               $"Ssid: {model.SsidName}, " +
               $"IpAddress: {model.IpAddress}, " +
               $"GatewayAddress: {model.GatewayAddress ?? "N/A"}, " +
               $"NativeObject: {model.NativeObject}, " +
               $"Bssid: {model.Bssid}";
        await DisplayAlert("Network info", info, "OK");
    }   
    private async void ScanClicked(object sender, EventArgs e)
    {
        await GetWifiList();
    }

    private async Task GetWifiList()
    {
        PermissionStatus status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted || DeviceInfo.Current.Platform == DevicePlatform.WinUI)
        {
            await Task.Delay(1000);
            loading.IsRunning = true;
            scanCollectionView.IsVisible = false;
            var response = await CrossWifiManager.Current.ScanWifiNetworks();
            foreach (var item in response)
            {
                networkDataModel.Add(new NetworkDataModel() 
                {
                    StatusId = item.StatusId,
                    IpAddress = (int)item.IpAddress, 
                    Bssid = item.Bssid, 
                    Ssid = item.Ssid, 
                    GatewayAddress = item.GatewayAddress, 
                    NativeObject = item.NativeObject 
                });
            }
            scanCollectionView.ItemsSource = networkDataModel;
            loading.IsRunning = false;
            scanCollectionView.IsVisible = true;
        }
        else
            await DisplayAlert("No location permisson", "Please provide location permission", "OK");
    }
}
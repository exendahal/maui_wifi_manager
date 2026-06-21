namespace DemoApp;
using MauiWifiManager;
using MauiWifiManager.Abstractions;
using System.Collections.ObjectModel;
using System.Diagnostics;

public partial class ScanListPage : ContentPage
{
    private readonly ObservableCollection<NetworkDataModel> _NetworkDataModel = [];
    private readonly HashSet<string> _DiscoveredNetworkKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<NetworkData> _PendingNetworks = new();
    private readonly object _QueueLock = new();
    private bool _IsDeviceDiscoveredSubscribed;
    private bool _IsProcessingQueue ;

    public Command InfoCommand { get; }
    public Command ConnectCommand { get; }

    public ScanListPage()
	{
		InitializeComponent();
        BindingContext = this;
        InfoCommand = new Command<NetworkDataModel>(ExecuteInfoCommand);
        ConnectCommand = new Command<NetworkDataModel>(ExecuteConnectCommand);
        scanCollectionView.ItemsSource = _NetworkDataModel;
        UpdateScanButtonState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await StartScanSessionAsync();
    }

    protected override async void OnDisappearing()
    {
        await StopScanSessionAsync();
        UnsubscribeDeviceDiscovered();
        base.OnDisappearing();
    }

    private async void ExecuteConnectCommand(NetworkDataModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.SsidName))
        {
            var response = await DisplayPromptAsync("Connect " + model.SsidName, "Enter password to connect");
            if (!string.IsNullOrWhiteSpace(response) && response.Length >= 8)
            {
                var status = await CrossWifiManager.Current.ConnectWifiAsync(model.SsidName, response);
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
        await DisplayAlertAsync("Network info", info, "OK");
    }   

    private async void ScanClicked(object sender, EventArgs e)
    {
        if (CrossWifiManager.IsScanning)
        {
            await StopScanSessionAsync();
        }
        else
        {
            await StartScanSessionAsync();
        }
    }

    private async Task StartScanSessionAsync()
    {
        if (!await EnsureLocationPermissionAsync())
        {
            return;
        }

        loading.IsRunning = true;
        scanCollectionView.IsVisible = true;
        _NetworkDataModel.Clear();
        _DiscoveredNetworkKeys.Clear();
        lock (_QueueLock)
        {
            _PendingNetworks.Clear();
            _IsProcessingQueue = false;
        }

        SubscribeDeviceDiscovered();

        var response = await CrossWifiManager.StartScanningForDevicesAsync();
        if (response.ErrorCode == WifiErrorCodes.Success)
        {
            UpdateScanButtonState();
        }
        else
        {
            loading.IsRunning = false;
            scanCollectionView.IsVisible = true;
            await DisplayAlertAsync("Scan failed", response.ErrorMessage ?? "Unable to start scanning.", "OK");
            UpdateScanButtonState();
        }
    }

    private async Task StopScanSessionAsync()
    {
        var response = await CrossWifiManager.StopScanningAsync();
        if (response.ErrorCode != WifiErrorCodes.Success)
        {
            Debug.WriteLine("Stop scanning failed: " + response.ErrorMessage);
        }

        lock (_QueueLock)
        {
            _PendingNetworks.Clear();
            _IsProcessingQueue = false;
        }

        UpdateScanButtonState();
        loading.IsRunning = false;
        scanCollectionView.IsVisible = true;
    }

    private void OnDeviceDiscovered(object? sender, NetworkData network)
    {
        lock (_QueueLock)
        {
            _PendingNetworks.Enqueue(network);
            if (_IsProcessingQueue)
            {
                return;
            }

            _IsProcessingQueue = true;
        }

        _ = ProcessDiscoveredQueueAsync();
    }

    private async Task ProcessDiscoveredQueueAsync()
    {
        while (true)
        {
            NetworkData? nextNetwork;

            lock (_QueueLock)
            {
                if (_PendingNetworks.Count == 0)
                {
                    _IsProcessingQueue = false;
                    return;
                }

                nextNetwork = _PendingNetworks.Dequeue();
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AddDiscoveredNetwork(nextNetwork);
                loading.IsRunning = false;
                scanCollectionView.IsVisible = true;
            });

            await Task.Delay(75);
        }
    }

    private void AddDiscoveredNetwork(NetworkData item)
    {
        var key = BuildNetworkKey(item.Ssid, item.Bssid);
        if (!_DiscoveredNetworkKeys.Add(key))
        {
            return;
        }

        _NetworkDataModel.Add(new NetworkDataModel()
        {
            StatusId = item.StatusId,
            IpAddress = item.IpAddress,
            Bssid = item.Bssid,
            Ssid = item.Ssid,
            GatewayAddress = item.GatewayAddress.ToString(),
            NativeObject = item.NativeObject
        });

        loading.IsRunning = false;
        Debug.WriteLine("Networks found: " + _NetworkDataModel.Count);
    }

    private static string BuildNetworkKey(string? ssid, object? bssid)
    {
        return string.Concat(ssid?.Trim() ?? string.Empty, "|", bssid?.ToString()?.Trim() ?? string.Empty);
    }

    private async Task<bool> EnsureLocationPermissionAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted || DeviceInfo.Current.Platform == DevicePlatform.WinUI)
        {
            return true;
        }

        await DisplayAlertAsync("No location permisson", "Please provide location permission", "OK");
        return false;
    }

    private void SubscribeDeviceDiscovered()
    {
        if (_IsDeviceDiscoveredSubscribed)
        {
            return;
        }

        CrossWifiManager.DeviceDiscovered += OnDeviceDiscovered;
        _IsDeviceDiscoveredSubscribed = true;
    }

    private void UnsubscribeDeviceDiscovered()
    {
        if (!_IsDeviceDiscoveredSubscribed)
        {
            return;
        }

        CrossWifiManager.DeviceDiscovered -= OnDeviceDiscovered;
        _IsDeviceDiscoveredSubscribed = false;
    }

    private void UpdateScanButtonState()
    {
        scan.Text = CrossWifiManager.IsScanning ? "Stop" : "Scan";
    }
}
namespace DemoApp;
using CommunityToolkit.Maui.Extensions;
using MauiWifiManager;
using MauiWifiManager.Abstractions;
using System.Collections.ObjectModel;
using System.Diagnostics;

public partial class ScanListPage : ContentPage
{
    // Android refreshes scan results about every 30 seconds, so a session needs a few minutes to show changes.
    private static readonly TimeSpan ScanSessionDuration = TimeSpan.FromMinutes(2);

    private readonly ObservableCollection<NetworkDataModel> _Networks = [];
    private readonly Dictionary<string, NetworkDataModel> _NetworksByKey = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _ScanCts;
    private bool _IsScanEventsSubscribed;
    // The toolkit shows popups as modal pages, which raises OnDisappearing/OnAppearing on this page.
    private bool _IsShowingPopup;

    public ScanListPage()
    {
        InitializeComponent();
        networkList.ItemsSource = _Networks;
        throttleNotice.IsVisible = DeviceInfo.Current.Platform == DevicePlatform.Android;
        UpdateStatus();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_IsShowingPopup)
            await StartScanSessionAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (_IsShowingPopup)
            return;
        await StopScanSessionAsync();
        UnsubscribeScanEvents();
    }

    private async void ScanClicked(object sender, EventArgs e)
    {
        if (CrossWifiManager.IsScanning)
            await StopScanSessionAsync();
        else
            await StartScanSessionAsync();
    }

    private async Task StartScanSessionAsync()
    {
        if (CrossWifiManager.IsScanning)
            return;

        var locationError = await LocationAccess.CheckAsync();
        if (locationError != null)
        {
            ShowEmptyState("Location needed", locationError);
            return;
        }

        ClearNetworks();
        ShowEmptyState("Looking for networks", "Networks in range appear here as they are found.");
        SubscribeScanEvents();

        // The plugin stops the session when this token is canceled.
        var cts = new CancellationTokenSource(ScanSessionDuration);
        _ScanCts = cts;
        cts.Token.Register(() => MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_ScanCts == cts)
                await StopScanSessionAsync();
        }));

        var response = await CrossWifiManager.StartScanningForDevicesAsync(cts.Token);
        if (response.ErrorCode != WifiErrorCodes.Success)
        {
            _ScanCts = null;
            cts.Dispose();
            ShowEmptyState("Scanning unavailable", response.ErrorMessage ?? "This device could not start a Wi-Fi scan.");
        }
        UpdateStatus();
    }

    private async Task StopScanSessionAsync()
    {
        var cts = _ScanCts;
        _ScanCts = null;

        var response = await CrossWifiManager.StopScanningAsync();
        if (response.ErrorCode != WifiErrorCodes.Success)
            Debug.WriteLine("Stop scanning failed: " + response.ErrorMessage);

        cts?.Dispose();
        if (_Networks.Count == 0)
            ShowEmptyState("Not scanning", "Tap Scan or pull down to look for networks.");
        UpdateStatus();
    }

    /// <summary>Pull to refresh runs a single ScanWifiNetworksAsync call and replaces the list with its result.</summary>
    private async void OnRefreshing(object? sender, EventArgs e)
    {
        var locationError = await LocationAccess.CheckAsync();
        if (locationError != null)
        {
            ShowEmptyState("Location needed", locationError);
            refreshView.IsRefreshing = false;
            return;
        }

        var response = await CrossWifiManager.ScanWifiNetworksAsync();
        if (response.ErrorCode == WifiErrorCodes.Success && response.Data != null)
        {
            ClearNetworks();
            foreach (var network in response.Data)
                AddNetwork(network);
            ShowEmptyState("No networks found", "Move closer to an access point and pull down to scan again.");
        }
        else
            ShowEmptyState("Scan failed", response.ErrorMessage ?? "This device could not scan for Wi-Fi networks.");

        refreshView.IsRefreshing = false;
        UpdateStatus();
    }

    private void OnDeviceDiscovered(object? sender, NetworkData network) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            AddNetwork(network);
            UpdateStatus();
        });

    private void OnDeviceLost(object? sender, NetworkData network) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_NetworksByKey.Remove(BuildNetworkKey(network), out var model))
                _Networks.Remove(model);
            UpdateStatus();
        });

    private void AddNetwork(NetworkData network)
    {
        var key = BuildNetworkKey(network);
        if (_NetworksByKey.ContainsKey(key))
            return;

        var model = new NetworkDataModel(network);
        _NetworksByKey[key] = model;

        // Keep the strongest networks on top.
        var index = 0;
        while (index < _Networks.Count && (_Networks[index].Data.RssiDbm ?? int.MinValue) >= (network.RssiDbm ?? int.MinValue))
            index++;
        _Networks.Insert(index, model);
    }

    private void ClearNetworks()
    {
        _Networks.Clear();
        _NetworksByKey.Clear();
    }

    private async void NetworkTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not NetworkDataModel model)
            return;

        _IsShowingPopup = true;
        var result = await this.ShowPopupAsync<bool>(new NetworkSheet(model), AppPopup.Options);
        _IsShowingPopup = false;
        if (result.Result)
        {
            await Shell.Current.GoToAsync("//connect", new ShellNavigationQueryParameters
            {
                { ConnectWifi.NetworkQueryKey, model }
            });
        }
    }

    private void ShowEmptyState(string title, string message)
    {
        emptyTitle.Text = title;
        emptyMessage.Text = message;
    }

    private void UpdateStatus()
    {
        var count = _Networks.Count == 1 ? "1 network" : $"{_Networks.Count} networks";
        statusTxt.Text = CrossWifiManager.IsScanning ? $"Scanning  ·  {count}" : $"Not scanning  ·  {count}";
        scanBtn.Text = CrossWifiManager.IsScanning ? "Stop" : "Scan";
        scanningIndicator.IsRunning = scanningIndicator.IsVisible = CrossWifiManager.IsScanning;
    }

    private static string BuildNetworkKey(NetworkData network) =>
        string.Concat(network.Ssid?.Trim() ?? string.Empty, "|", network.Bssid?.ToString()?.Trim() ?? string.Empty);

    private void SubscribeScanEvents()
    {
        if (_IsScanEventsSubscribed)
            return;

        CrossWifiManager.DeviceDiscovered += OnDeviceDiscovered;
        CrossWifiManager.DeviceLost += OnDeviceLost;
        _IsScanEventsSubscribed = true;
    }

    private void UnsubscribeScanEvents()
    {
        if (!_IsScanEventsSubscribed)
            return;

        CrossWifiManager.DeviceDiscovered -= OnDeviceDiscovered;
        CrossWifiManager.DeviceLost -= OnDeviceLost;
        _IsScanEventsSubscribed = false;
    }
}

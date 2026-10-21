using MauiWifiManager.Abstractions;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Windows.Devices.Radios;
using Windows.Devices.WiFi;
using Windows.Networking.NetworkOperators;
using Windows.Networking.Connectivity;
using Windows.Security.Credentials;

namespace MauiWifiManager
{
    public class WifiNetworkService : IWifiNetworkService
    {
        private EventHandler<WifiNetworkChangedEventArgs>? _WifiNetworkChanged;
        private EventHandler<NetworkData>? _DeviceDiscovered;
        private EventHandler<NetworkData>? _DeviceLost;
        private readonly object _MonitorLock = new();
        private readonly object _ScanLock = new();
        private NetworkData? _LastKnownNetwork;
        private CancellationTokenSource? _ScanSessionCts;
        private Task? _ScanSessionTask;
        private bool _IsMonitoring;
        private EventHandler<WifiStateChangedEventArgs>? _WifiStateChanged;
        private EventHandler<WifiSignalChangedEventArgs>? _SignalStrengthChanged;
        private Radio? _WifiRadio;
        private RadioState? _LastRadioState;
        private CancellationTokenSource? _SignalPollCts;
        private bool _LocalOnlyActive;
        private EventHandler<WifiScanResultsEventArgs>? _ScanResultsAvailable;
        private WiFiAdapter? _ScanEventAdapter;
        // AvailableNetworksChanged only fires on the WiFiAdapter object that ran the scan, so scans and the
        // ScanResultsAvailable event share one instance. ponytail: not refreshed if the adapter is replaced.
        private static WiFiAdapter? _SharedAdapter;

        private static readonly TimeSpan DefaultScanSessionDuration = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan SignalPollInterval = TimeSpan.FromSeconds(5);

        public bool IsScanning { get; private set; }

        public event EventHandler<WifiNetworkChangedEventArgs>? WifiNetworkChanged
        {
            add
            {
                _WifiNetworkChanged += value;
                EnsureMonitoringStarted();
            }
            remove => _WifiNetworkChanged -= value;
        }

        public event EventHandler<NetworkData>? DeviceDiscovered
        {
            add => _DeviceDiscovered += value;
            remove => _DeviceDiscovered -= value;
        }

        public event EventHandler<NetworkData>? DeviceLost
        {
            add => _DeviceLost += value;
            remove => _DeviceLost -= value;
        }

        public event EventHandler<WifiStateChangedEventArgs>? WifiStateChanged
        {
            add
            {
                bool attach;
                lock (_MonitorLock)
                {
                    attach = _WifiStateChanged == null;
                    _WifiStateChanged += value;
                }
                if (attach)
                    _ = AttachRadioAsync();
            }
            remove
            {
                lock (_MonitorLock)
                {
                    _WifiStateChanged -= value;
                    if (_WifiStateChanged == null)
                        DetachRadio();
                }
            }
        }

        public event EventHandler<WifiSignalChangedEventArgs>? SignalStrengthChanged
        {
            add
            {
                lock (_MonitorLock)
                {
                    _SignalStrengthChanged += value;
                    if (_SignalPollCts == null)
                    {
                        _SignalPollCts = new CancellationTokenSource();
                        _ = PollSignalAsync(_SignalPollCts.Token);
                    }
                }
            }
            remove
            {
                lock (_MonitorLock)
                {
                    _SignalStrengthChanged -= value;
                    if (_SignalStrengthChanged == null)
                        StopSignalPolling();
                }
            }
        }

        public event EventHandler<WifiScanResultsEventArgs>? ScanResultsAvailable
        {
            add
            {
                bool attach;
                lock (_MonitorLock)
                {
                    attach = _ScanResultsAvailable == null;
                    _ScanResultsAvailable += value;
                }
                if (attach)
                    _ = AttachScanAdapterAsync();
            }
            remove
            {
                lock (_MonitorLock)
                {
                    _ScanResultsAvailable -= value;
                    if (_ScanResultsAvailable == null)
                        DetachScanAdapter();
                }
            }
        }

        public WifiNetworkService() { }

        [Obsolete("Use ConnectWifiAsync(string ssid, string password, CancellationToken cancellationToken = default) or ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default) instead.")]
        public Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password, string? bssid = null)
        {
            return ConnectWifiAsync(ssid, password, bssid, CancellationToken.None);
        }

        public Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, CancellationToken cancellationToken = default)
        {
            return ConnectWifiAsync(ssid, password, new WifiConnectionOptions(), cancellationToken);
        }

        public Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default)
        {
            return ConnectWifiAsync(ssid, password, new WifiConnectionOptions { Bssid = bssid }, cancellationToken);
        }

        public Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, WifiConnectionOptions options, CancellationToken cancellationToken = default)
        {
            return ConnectCoreAsync(ssid, new PasswordCredential { Password = password }, options, WiFiReconnectionKind.Automatic, cancellationToken);
        }

        private async Task<WifiManagerResponse<NetworkData>> ConnectCoreAsync(string ssid, PasswordCredential credential, WifiConnectionOptions options, WiFiReconnectionKind reconnectionKind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = new WifiManagerResponse<NetworkData>();

            var access = await WiFiAdapter.RequestAccessAsync();
            if (access != WiFiAccessStatus.Allowed)
            {
                Debug.WriteLine("No Wi-Fi Access Status");
                response.ErrorCode = WifiErrorCodes.PermissionDenied;
                response.ErrorMessage = "No Wi-Fi Access Status.";
                return response;
            }

            var result = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(WiFiAdapter.GetDeviceSelector());
            if (result.Count < 1)
            {
                response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                response.ErrorMessage = "No Wi-Fi adapters found.";
                return response;
            }

            var adapter = await WiFiAdapter.FromIdAsync(result[0].Id);
            if (adapter == null)
            {
                response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                response.ErrorMessage = "Failed to get Wi-Fi adapter.";
                return response;
            }

            // The timeout covers the whole attempt (scan + connect), matching Android and iOS.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (options.Timeout is TimeSpan timeout)
                timeoutCts.CancelAfter(timeout);
            try
            {
                await adapter.ScanAsync().AsTask(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.OperationTimeout, "Timed out connecting to the network.");
            }

            // Find network by SSID or SSID prefix (and optionally BSSID); with a prefix, take the strongest match.
            WiFiAvailableNetwork? target = adapter.NetworkReport.AvailableNetworks
                .Where(network => (options.IsSsidPrefix ? network.Ssid.StartsWith(ssid, StringComparison.Ordinal) : network.Ssid == ssid)
                    && (string.IsNullOrWhiteSpace(options.Bssid) || string.Equals(network.Bssid, options.Bssid, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(network => network.NetworkRssiInDecibelMilliwatts)
                .FirstOrDefault();
            if (target != null && options.IsSsidPrefix)
                ssid = target.Ssid;

            // For hidden networks, fall back to empty-SSID entries matched by BSSID
            if (target == null && options.IsHidden)
            {
                foreach (var network in adapter.NetworkReport.AvailableNetworks)
                {
                    if (string.IsNullOrEmpty(network.Ssid) &&
                        (!string.IsNullOrWhiteSpace(options.Bssid)
                            ? string.Equals(network.Bssid, options.Bssid, StringComparison.OrdinalIgnoreCase)
                            : true))
                    {
                        target = network;
                        break;
                    }
                }
            }

            if (target == null)
            {
                Debug.WriteLine(options.IsHidden
                    ? "Hidden network not found in scan results. Connect via Wi-Fi settings."
                    : "The specified network was not found.");
                response.ErrorCode = WifiErrorCodes.NoConnection;
                response.ErrorMessage = options.IsHidden
                    ? "Hidden network not found in scan results."
                    : options.IsSsidPrefix ? $"No network starting with \"{ssid}\" was found."
                    : (string.IsNullOrWhiteSpace(options.Bssid)
                        ? "The specified network was not found."
                        : "The specified SSID/BSSID network was not found.");
                return response;
            }

            // Use 4-param overload when connecting to a hidden network so Windows passes SSID explicitly
            WiFiConnectionResult status;
            try
            {
                if (options.IsHidden || string.IsNullOrEmpty(target.Ssid))
                    status = await adapter.ConnectAsync(target, reconnectionKind, credential, ssid).AsTask(timeoutCts.Token);
                else
                    status = await adapter.ConnectAsync(target, reconnectionKind, credential).AsTask(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.OperationTimeout, "Timed out connecting to the network.");
            }

            if (status.ConnectionStatus == WiFiConnectionStatus.Success)
            {
                Debug.WriteLine("Connected successfully to the network.");
                var networkData = await GetNetworkInfoAsync(cancellationToken);
                if (networkData.ErrorCode == WifiErrorCodes.Success)
                {
                    response.ErrorCode = WifiErrorCodes.Success;
                    response.Data = networkData.Data;
                }
                else
                {
                    response.ErrorCode = WifiErrorCodes.UnknownError;
                    response.ErrorMessage = "Connected but failed to retrieve network info.";
                }
            }
            else
            {
                response.ErrorCode = status.ConnectionStatus switch
                {
                    WiFiConnectionStatus.InvalidCredential => WifiErrorCodes.InvalidCredential,
                    WiFiConnectionStatus.Timeout => WifiErrorCodes.OperationTimeout,
                    _ => WifiErrorCodes.UnknownError
                };
                Debug.WriteLine($"Connection failed: {status.ConnectionStatus}");
                response.ErrorMessage = $"Connection failed: {status.ConnectionStatus}";
            }

            return response;
        }

        public async void DisconnectWifi(string ssid)
        {
            var result = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(WiFiAdapter.GetDeviceSelector());
            if (result.Count >= 1)
            {
                var adapter = await WiFiAdapter.FromIdAsync(result[0].Id);
                adapter.Disconnect();
            }
        }

        [Obsolete("Use GetNetworkInfoAsync(CancellationToken cancellationToken = default) instead.")]
        public Task<WifiManagerResponse<NetworkData>> GetNetworkInfo()
        {
            return GetNetworkInfoAsync(CancellationToken.None);
        }

        public Task<WifiManagerResponse<NetworkData>> GetNetworkInfoAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<WifiManagerResponse<NetworkData>>(cancellationToken);

            var response = new WifiManagerResponse<NetworkData>();
            var networkData = new NetworkData();

            try
            {
                Windows.Networking.Connectivity.ConnectionProfile? profile = NetworkInformation
                    .GetConnectionProfiles()
                    .FirstOrDefault(x => x.IsWlanConnectionProfile && x.GetNetworkConnectivityLevel() > NetworkConnectivityLevel.None);

                if (profile == null)
                {
                    response.ErrorCode = WifiErrorCodes.NoConnection;
                    response.ErrorMessage = "No active Wi-Fi network connection found.";
                    return Task.FromResult(response);
                }

                networkData.StatusId = (int)profile.GetNetworkConnectivityLevel();
                networkData.Ssid = profile.WlanConnectionProfileDetails.GetConnectedSsid();
                networkData.Bssid = profile.NetworkAdapter.NetworkAdapterId;
                networkData.NativeObject = profile;
                networkData.SignalStrength = profile.GetSignalBars();

                if (profile.NetworkSecuritySettings != null)
                    networkData.SecurityType = GetSecurityType(profile.NetworkSecuritySettings.NetworkAuthenticationType);

                var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                                      && n.OperationalStatus == OperationalStatus.Up);

                if (networkInterface != null)
                {
                    var ipProps = networkInterface.GetIPProperties();

                    var ipv4 = ipProps.UnicastAddresses
                        .FirstOrDefault(n => n.Address.AddressFamily == AddressFamily.InterNetwork);
                    if (ipv4 != null)
                    {
                        networkData.IpAddress = BitConverter.ToInt32(ipv4.Address.GetAddressBytes(), 0);
                        if (ipv4.IPv4Mask != null)
                            networkData.SubnetMask = ipv4.IPv4Mask.ToString();
                    }

                    var gateway = ipProps.GatewayAddresses
                        .FirstOrDefault(n => n.Address.AddressFamily == AddressFamily.InterNetwork);
                    if (gateway != null)
                        networkData.GatewayAddress = IpAddressToInt(gateway.Address);

                    var dhcp = ipProps.DhcpServerAddresses
                        .FirstOrDefault(n => n.AddressFamily == AddressFamily.InterNetwork);
                    if (dhcp != null)
                        networkData.DhcpServerAddress = IpAddressToInt(dhcp);

                    // IPv6
                    var ipv6 = ipProps.UnicastAddresses
                        .Where(u => u.Address.AddressFamily == AddressFamily.InterNetworkV6
                                 && !u.Address.IsIPv6LinkLocal
                                 && !IPAddress.IsLoopback(u.Address))
                        .Select(u => u.Address.ToString())
                        .FirstOrDefault();
                    networkData.IPv6Address = ipv6;

                    // DNS
                    var dns = ipProps.DnsAddresses
                        .Where(a => !IPAddress.IsLoopback(a))
                        .Select(a => a.ToString())
                        .ToList();
                    if (dns.Count > 0) networkData.DnsAddresses = dns;
                }

                response.ErrorCode = WifiErrorCodes.Success;
                response.Data = networkData;
                response.ErrorMessage = "Fetched Wi-Fi connection info successfully.";
            }
            catch (Exception ex)
            {
                response.ErrorCode = WifiErrorCodes.UnknownError;
                response.ErrorMessage = $"An error occurred while retrieving network info: {ex.Message}";
            }
            return Task.FromResult(response);
        }

        public async Task<bool> OpenWifiSetting()
        {
            return await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:network-wifi"));
        }

        public void Dispose()
        {
            _ = StopScanningAsync();
            StopMonitoring();
            lock (_MonitorLock)
            {
                DetachRadio();
                StopSignalPolling();
                DetachScanAdapter();
            }
        }

        [Obsolete("Use ScanWifiNetworksAsync(CancellationToken cancellationToken = default) instead.")]
        public Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworks()
        {
            return ScanWifiNetworksAsync(CancellationToken.None);
        }

        public async Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworksAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = new WifiManagerResponse<List<NetworkData>>();
            try
            {
                List<NetworkData> wifiNetworks = new();
                var accessStatus = await WiFiAdapter.RequestAccessAsync();
                if (accessStatus == WiFiAccessStatus.Allowed)
                {
                    var wifiAdapter = await GetSharedAdapterAsync();
                    if (wifiAdapter != null)
                    {
                        Debug.WriteLine("Wi-Fi Scan started.");
                        Exception? scanError = null;
                        try
                        {
                            await wifiAdapter.ScanAsync().AsTask(cancellationToken);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            // Some drivers intermittently abort or time out a scan; the adapter still has its last report.
                            scanError = ex;
                        }

                        wifiNetworks.AddRange(wifiAdapter.NetworkReport.AvailableNetworks.Select(ToNetworkData));
                        if (scanError != null)
                        {
                            response.ErrorCode = wifiNetworks.Count > 0 ? WifiErrorCodes.Success : WifiErrorCodes.UnknownError;
                            response.ErrorMessage = wifiNetworks.Count > 0
                                ? $"Windows did not complete a new scan (0x{scanError.HResult:X8}), so these are the previous results."
                                : $"Error while scanning Wi-Fi: {scanError.Message} (0x{scanError.HResult:X8})";
                            response.Data = wifiNetworks.Count > 0 ? wifiNetworks : null;
                            return response;
                        }
                    }
                    Debug.WriteLine("Wi-Fi Scan complete.");
                    response.ErrorCode = WifiErrorCodes.Success;
                    response.ErrorMessage = "Wi-Fi Scan complete.";
                    response.Data = wifiNetworks;
                }
                else
                {
                    response.ErrorCode = WifiErrorCodes.UnknownError;
                    response.ErrorMessage = "Request Access Async failed.";
                }
            }
            catch (Exception ex)
            {
                response.ErrorCode = WifiErrorCodes.UnknownError;
                response.ErrorMessage = $"Error while scanning Wi-Fi: {ex.Message}";
            }
            return response;
        }

        public Task<WifiManagerResponse<bool>> StartScanningForDevicesAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.OperationCanceled, "StartScanningForDevicesAsync operation was canceled."));

            lock (_ScanLock)
            {
                if (IsScanning)
                    return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(true, "Scan session is already running."));

                _ScanSessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (!cancellationToken.CanBeCanceled)
                    _ScanSessionCts.CancelAfter(DefaultScanSessionDuration);
                IsScanning = true;
                _ScanSessionTask = RunScanSessionAsync(_ScanSessionCts.Token);
            }

            return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(true, "Wi-Fi scan session started."));
        }

        public async Task<WifiManagerResponse<bool>> StopScanningAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CancellationTokenSource? cts;
            Task? scanTask;
            lock (_ScanLock)
            {
                if (!IsScanning)
                    return WifiManagerResponse<bool>.SuccessResponse(false, "No active scan session.");

                cts = _ScanSessionCts;
                scanTask = _ScanSessionTask;
                IsScanning = false;
                _ScanSessionCts = null;
                _ScanSessionTask = null;
            }

            try
            {
                cts?.Cancel();
                if (scanTask != null) await scanTask;
            }
            catch (OperationCanceledException) { }
            finally { cts?.Dispose(); }

            return WifiManagerResponse<bool>.SuccessResponse(true, "Wi-Fi scan session stopped.");
        }

        public async Task<bool> OpenWirelessSetting()
        {
            return await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:network"));
        }

        public Task<WifiManagerResponse<bool>> IsInternetAvailableAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled."));

            try
            {
                var profile = NetworkInformation.GetInternetConnectionProfile();
                var level = profile?.GetNetworkConnectivityLevel();
                bool hasInternet = level == NetworkConnectivityLevel.InternetAccess;
                return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(
                    hasInternet,
                    hasInternet ? "Internet is available." : "No internet access."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, ex.Message));
            }
        }

        public Task<WifiManagerResponse<bool>> IsCaptivePortalDetectedAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled."));

            try
            {
                var profile = NetworkInformation.GetInternetConnectionProfile();
                var level = profile?.GetNetworkConnectivityLevel();
                // ConstrainedInternetAccess typically means captive portal
                bool isCaptive = level == NetworkConnectivityLevel.ConstrainedInternetAccess
                              || level == NetworkConnectivityLevel.LocalAccess;
                return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(
                    isCaptive,
                    isCaptive ? "Captive portal detected." : "No captive portal."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, ex.Message));
            }
        }

        public async Task<WifiManagerResponse<bool>> IsWifiEnabledAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");

            try
            {
                var radio = await GetWifiRadioAsync();
                if (radio == null)
                    return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "No Wi-Fi radio found.");

                bool enabled = radio.State == RadioState.On;
                return WifiManagerResponse<bool>.SuccessResponse(enabled, enabled ? "Wi-Fi is enabled." : "Wi-Fi is disabled.");
            }
            catch (Exception ex)
            {
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, ex.Message);
            }
        }

        public async Task<WifiManagerResponse<bool>> RequestPermissionsAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");

            var access = await WiFiAdapter.RequestAccessAsync();
            return access == WiFiAccessStatus.Allowed
                ? WifiManagerResponse<bool>.SuccessResponse(true, "Wi-Fi access is allowed.")
                : WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.PermissionDenied, $"Wi-Fi access was not allowed: {access}.");
        }

        public Task<WifiManagerResponse<bool>> IsMeteredConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled."));

            try
            {
                var cost = NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost();
                if (cost == null)
                    return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(false, "No active network."));

                bool metered = cost.NetworkCostType is NetworkCostType.Fixed or NetworkCostType.Variable
                    || cost.ApproachingDataLimit || cost.OverDataLimit || cost.Roaming;
                return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(metered, metered ? "Connection is metered." : "Connection is not metered."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, ex.Message));
            }
        }

        public Task<WifiManagerResponse<List<string>>> GetSavedNetworksAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(WifiManagerResponse<List<string>>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled."));

            try
            {
                // Includes saved profiles that are not currently connected. Profile names match the SSID unless renamed.
                var ssids = NetworkInformation.GetConnectionProfiles()
                    .Where(p => p.IsWlanConnectionProfile && !string.IsNullOrEmpty(p.ProfileName))
                    .Select(p => p.ProfileName)
                    .Distinct()
                    .ToList();
                return Task.FromResult(WifiManagerResponse<List<string>>.SuccessResponse(ssids, $"Found {ssids.Count} saved network(s)."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(WifiManagerResponse<List<string>>.ErrorResponse(WifiErrorCodes.UnknownError, ex.Message));
            }
        }

        public Task<WifiManagerResponse<bool>> ForgetNetworkAsync(string ssid, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled."));

            // WinRT has no API to delete a Wi-Fi profile, so this goes through the native WLAN API.
            uint error = WlanOpenHandle(2, IntPtr.Zero, out _, out var handle);
            if (error != 0)
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, $"WLAN service is unavailable (error {error})."));

            try
            {
                error = WlanEnumInterfaces(handle, IntPtr.Zero, out var interfaceList);
                if (error != 0)
                    return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, $"Failed to list Wi-Fi adapters (error {error})."));

                bool removed = false;
                try
                {
                    // WLAN_INTERFACE_INFO_LIST: two DWORDs, then WLAN_INTERFACE_INFO entries of GUID + WCHAR[256] + state.
                    const int entrySize = 16 + 512 + 4;
                    int count = Marshal.ReadInt32(interfaceList);
                    for (int i = 0; i < count; i++)
                    {
                        var interfaceGuid = Marshal.PtrToStructure<Guid>(interfaceList + 8 + i * entrySize);
                        if (WlanDeleteProfile(handle, ref interfaceGuid, ssid, IntPtr.Zero) == 0)
                            removed = true;
                    }
                }
                finally
                {
                    WlanFreeMemory(interfaceList);
                }

                return Task.FromResult(removed
                    ? WifiManagerResponse<bool>.SuccessResponse(true, "Network removed.")
                    : WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, "No saved network with this name was found."));
            }
            finally
            {
                WlanCloseHandle(handle, IntPtr.Zero);
            }
        }

        public Task<WifiManagerResponse<NetworkData>> ConnectEnterpriseWifiAsync(string ssid, WifiEnterpriseOptions options, CancellationToken cancellationToken = default)
        {
            // Windows negotiates the EAP method and validates the server itself; only the user credentials are passed.
            var credential = new PasswordCredential { UserName = options.Identity, Password = options.Password };
            return ConnectCoreAsync(ssid, credential, new WifiConnectionOptions { IsHidden = options.IsHidden }, WiFiReconnectionKind.Automatic, cancellationToken);
        }

        public async Task<WifiManagerResponse<NetworkData>> ConnectLocalOnlyAsync(string ssid, string password, WifiConnectionOptions options, CancellationToken cancellationToken = default)
        {
            // Manual reconnection keeps Windows from auto-joining this network later. Local subnet traffic
            // always goes over the interface that owns the subnet, so no extra routing is needed.
            var response = await ConnectCoreAsync(ssid, new PasswordCredential { Password = password }, options, WiFiReconnectionKind.Manual, cancellationToken);
            _LocalOnlyActive = response.ErrorCode == WifiErrorCodes.Success;
            return response;
        }

        public async Task<WifiManagerResponse<bool>> ReleaseLocalOnlyAsync(CancellationToken cancellationToken = default)
        {
            if (!_LocalOnlyActive)
                return WifiManagerResponse<bool>.SuccessResponse(false, "No local-only network to release.");

            _LocalOnlyActive = false;
            var adapters = await WiFiAdapter.FindAllAdaptersAsync();
            if (adapters.Count > 0)
                adapters[0].Disconnect(); // Windows then reconnects to its preferred network.
            return WifiManagerResponse<bool>.SuccessResponse(true, "Local-only network released.");
        }

        private static NetworkData ToNetworkData(WiFiAvailableNetwork network)
        {
            int freqMHz = (int)(network.ChannelCenterFrequencyInKilohertz / 1000);
            return new NetworkData
            {
                Ssid = network.Ssid,
                Bssid = network.Bssid,
                SignalStrength = network.SignalBars,
                SecurityType = network.PhyKind,
                FrequencyBand = freqMHz > 0 ? GetBandFromFrequencyMHz(freqMHz) : WifiFrequencyBand.Unknown,
                ChannelNumber = freqMHz > 0 ? GetChannelFromFrequencyMHz(freqMHz) : null,
                RssiDbm = (int)network.NetworkRssiInDecibelMilliwatts,
                WifiStandard = GetWifiStandard(network.PhyKind),
                NativeObject = network
            };
        }

        public async Task<WifiManagerResponse<WifiScanAvailability>> CanStartScanAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return WifiManagerResponse<WifiScanAvailability>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");

            try
            {
                if (await WiFiAdapter.RequestAccessAsync() != WiFiAccessStatus.Allowed)
                    return WifiManagerResponse<WifiScanAvailability>.SuccessResponse(WifiScanAvailability.PermissionDenied, "Wi-Fi access is not allowed for this app.");

                var radio = await GetWifiRadioAsync();
                if (radio == null)
                    return WifiManagerResponse<WifiScanAvailability>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "No Wi-Fi radio found.");
                if (radio.State != RadioState.On)
                    return WifiManagerResponse<WifiScanAvailability>.SuccessResponse(WifiScanAvailability.WifiDisabled, "Wi-Fi is turned off.");

                // Windows does not throttle app-initiated scans.
                return WifiManagerResponse<WifiScanAvailability>.SuccessResponse(WifiScanAvailability.Ready, "A scan can start now.");
            }
            catch (Exception ex)
            {
                return WifiManagerResponse<WifiScanAvailability>.ErrorResponse(WifiErrorCodes.UnknownError, ex.Message);
            }
        }

        private async Task AttachScanAdapterAsync()
        {
            WiFiAdapter? adapter;
            try
            {
                if (await WiFiAdapter.RequestAccessAsync() != WiFiAccessStatus.Allowed) return;
                adapter = await GetSharedAdapterAsync();
            }
            catch { return; }
            if (adapter == null) return;

            lock (_MonitorLock)
            {
                if (_ScanResultsAvailable == null || _ScanEventAdapter != null) return;
                _ScanEventAdapter = adapter;
                adapter.AvailableNetworksChanged += OnAvailableNetworksChanged;
            }
        }

        private static async Task<WiFiAdapter?> GetSharedAdapterAsync()
        {
            if (_SharedAdapter != null)
                return _SharedAdapter;
            var adapters = await WiFiAdapter.FindAllAdaptersAsync();
            return _SharedAdapter ??= adapters.FirstOrDefault();
        }

        private void DetachScanAdapter()
        {
            if (_ScanEventAdapter == null) return;
            _ScanEventAdapter.AvailableNetworksChanged -= OnAvailableNetworksChanged;
            _ScanEventAdapter = null;
        }

        private void OnAvailableNetworksChanged(WiFiAdapter sender, object args)
        {
            var networks = sender.NetworkReport.AvailableNetworks.Select(ToNetworkData).ToList();
            _ScanResultsAvailable?.Invoke(this, new WifiScanResultsEventArgs(networks, isFresh: true));
        }

        public async Task<WifiManagerResponse<bool>> SetWifiEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");

            try
            {
                if (await Radio.RequestAccessAsync() != RadioAccessStatus.Allowed)
                    return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.PermissionDenied, "Radio access is not allowed for this app.");

                var radio = await GetWifiRadioAsync();
                if (radio == null)
                    return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "No Wi-Fi radio found.");

                var wanted = enabled ? RadioState.On : RadioState.Off;
                if (radio.State == wanted)
                    return WifiManagerResponse<bool>.SuccessResponse(true, $"Wi-Fi is already {(enabled ? "on" : "off")}.");

                var result = await radio.SetStateAsync(wanted);
                return result == RadioAccessStatus.Allowed
                    ? WifiManagerResponse<bool>.SuccessResponse(true, $"Wi-Fi turned {(enabled ? "on" : "off")}.")
                    : WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.PermissionDenied, $"Windows refused to change the Wi-Fi state: {result}.");
            }
            catch (Exception ex)
            {
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, ex.Message);
            }
        }

        public async Task<WifiManagerResponse<HotspotInfo>> StartHotspotAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return WifiManagerResponse<HotspotInfo>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");

            try
            {
                var profile = NetworkInformation.GetInternetConnectionProfile();
                if (profile == null)
                    return WifiManagerResponse<HotspotInfo>.ErrorResponse(WifiErrorCodes.NoConnection, "Mobile Hotspot shares an internet connection, and none is active.");

                var capability = NetworkOperatorTetheringManager.GetTetheringCapabilityFromConnectionProfile(profile);
                if (capability != TetheringCapability.Enabled)
                    return WifiManagerResponse<HotspotInfo>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, $"Mobile Hotspot is not available: {capability}.");

                var manager = NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
                if (manager.TetheringOperationalState != TetheringOperationalState.On)
                {
                    var result = await manager.StartTetheringAsync().AsTask(cancellationToken);
                    if (result.Status != TetheringOperationStatus.Success)
                        return WifiManagerResponse<HotspotInfo>.ErrorResponse(WifiErrorCodes.UnknownError,
                            $"Mobile Hotspot did not start: {result.Status}. {result.AdditionalErrorMessage}".Trim());
                }

                var config = manager.GetCurrentAccessPointConfiguration();
                var info = new HotspotInfo { Ssid = config.Ssid, Password = config.Passphrase, IsLocalOnly = false };
                return WifiManagerResponse<HotspotInfo>.SuccessResponse(info, "Mobile Hotspot is on.");
            }
            catch (OperationCanceledException)
            {
                return WifiManagerResponse<HotspotInfo>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");
            }
            catch (Exception ex)
            {
                return WifiManagerResponse<HotspotInfo>.ErrorResponse(WifiErrorCodes.UnknownError, ex.Message);
            }
        }

        public async Task<WifiManagerResponse<bool>> StopHotspotAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var manager = GetTetheringManager();
                if (manager == null || manager.TetheringOperationalState != TetheringOperationalState.On)
                    return WifiManagerResponse<bool>.SuccessResponse(false, "Mobile Hotspot is not running.");

                var result = await manager.StopTetheringAsync().AsTask(cancellationToken);
                return result.Status == TetheringOperationStatus.Success
                    ? WifiManagerResponse<bool>.SuccessResponse(true, "Mobile Hotspot stopped.")
                    : WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, $"Mobile Hotspot did not stop: {result.Status}.");
            }
            catch (Exception ex)
            {
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, ex.Message);
            }
        }

        public Task<WifiManagerResponse<List<HotspotClient>>> GetHotspotClientsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var manager = GetTetheringManager();
                if (manager == null || manager.TetheringOperationalState != TetheringOperationalState.On)
                    return Task.FromResult(WifiManagerResponse<List<HotspotClient>>.ErrorResponse(WifiErrorCodes.NoConnection, "Mobile Hotspot is not running."));

                var clients = manager.GetTetheringClients()
                    .Select(c => new HotspotClient { MacAddress = c.MacAddress, HostName = c.HostNames.FirstOrDefault()?.DisplayName })
                    .ToList();
                return Task.FromResult(WifiManagerResponse<List<HotspotClient>>.SuccessResponse(clients, $"{clients.Count} device(s) connected."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(WifiManagerResponse<List<HotspotClient>>.ErrorResponse(WifiErrorCodes.UnknownError, ex.Message));
            }
        }

        private static NetworkOperatorTetheringManager? GetTetheringManager()
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            return profile == null ? null : NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
        }

        private static async Task<Radio?> GetWifiRadioAsync()
        {
            var radios = await Radio.GetRadiosAsync();
            return radios.FirstOrDefault(r => r.Kind == RadioKind.WiFi);
        }

        private async Task AttachRadioAsync()
        {
            Radio? radio;
            try { radio = await GetWifiRadioAsync(); }
            catch { return; }
            if (radio == null) return;

            lock (_MonitorLock)
            {
                if (_WifiStateChanged == null || _WifiRadio != null) return;
                _WifiRadio = radio;
                _LastRadioState = radio.State;
                radio.StateChanged += OnRadioStateChanged;
            }
        }

        private void DetachRadio()
        {
            if (_WifiRadio == null) return;
            _WifiRadio.StateChanged -= OnRadioStateChanged;
            _WifiRadio = null;
            _LastRadioState = null;
        }

        private void OnRadioStateChanged(Radio sender, object args)
        {
            lock (_MonitorLock)
            {
                // StateChanged can fire several times for one switch; report only real on/off changes.
                if (sender.State == _LastRadioState) return;
                _LastRadioState = sender.State;
            }
            _WifiStateChanged?.Invoke(this, new WifiStateChangedEventArgs(sender.State == RadioState.On));
        }

        private async Task PollSignalAsync(CancellationToken cancellationToken)
        {
            // ponytail: Windows has no signal-change event, so poll signal bars; RSSI is not exposed for the connected network.
            byte? lastBars = null;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var bars = NetworkInformation.GetConnectionProfiles()
                        .FirstOrDefault(p => p.IsWlanConnectionProfile && p.GetNetworkConnectivityLevel() > NetworkConnectivityLevel.None)
                        ?.GetSignalBars();
                    if (bars != null && bars != lastBars)
                    {
                        lastBars = bars;
                        int clamped = Math.Min((int)bars.Value, 5);
                        _SignalStrengthChanged?.Invoke(this, new WifiSignalChangedEventArgs(null, clamped * 4 / 5, clamped * 20));
                    }
                    await Task.Delay(SignalPollInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void StopSignalPolling()
        {
            _SignalPollCts?.Cancel();
            _SignalPollCts?.Dispose();
            _SignalPollCts = null;
        }

        private static WifiStandard GetWifiStandard(WiFiPhyKind phyKind) => (int)phyKind switch
        {
            2 or 4 or 5 or 6 => WifiStandard.Legacy, // Dsss, Ofdm (a), Hrdsss (b), Erp (g)
            7 => WifiStandard.Ieee80211n,            // HT
            8 => WifiStandard.Ieee80211ac,           // Vht
            9 => WifiStandard.Ieee80211ad,           // Dmg
            10 => WifiStandard.Ieee80211ax,          // HE
            11 => WifiStandard.Ieee80211be,          // Eht (newer SDKs only, hence the int match)
            _ => WifiStandard.Unknown
        };

        [DllImport("wlanapi.dll")]
        private static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

        [DllImport("wlanapi.dll")]
        private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        private static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
        private static extern uint WlanDeleteProfile(IntPtr hClientHandle, ref Guid pInterfaceGuid, string strProfileName, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        private static extern void WlanFreeMemory(IntPtr pMemory);

        private void EnsureMonitoringStarted()
        {
            lock (_MonitorLock)
            {
                if (_IsMonitoring) return;
                NetworkChange.NetworkAddressChanged += OnNetworkChanged;
                NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
                _IsMonitoring = true;
            }
            _ = RefreshSnapshotAsync(raiseEvent: false);
        }

        private void StopMonitoring()
        {
            lock (_MonitorLock)
            {
                if (!_IsMonitoring) return;
                NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
                NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
                _IsMonitoring = false;
                _LastKnownNetwork = null;
            }
        }

        private void OnNetworkChanged(object? sender, EventArgs e) => _ = RefreshSnapshotAsync(raiseEvent: true);
        private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) => _ = RefreshSnapshotAsync(raiseEvent: true);

        private async Task RefreshSnapshotAsync(bool raiseEvent)
        {
            NetworkData? currentNetwork = null;
            try
            {
                var info = await GetNetworkInfoAsync();
                if (info.ErrorCode == WifiErrorCodes.Success && info.Data != null)
                    currentNetwork = CloneNetworkData(info.Data);
            }
            catch { currentNetwork = null; }

            NetworkData? oldNetwork;
            bool changed;
            lock (_MonitorLock)
            {
                oldNetwork = CloneNetworkData(_LastKnownNetwork);
                changed = !AreSameNetwork(_LastKnownNetwork, currentNetwork);
                _LastKnownNetwork = CloneNetworkData(currentNetwork);
            }

            if (raiseEvent && changed)
                _WifiNetworkChanged?.Invoke(this, new WifiNetworkChangedEventArgs(oldNetwork, CloneNetworkData(currentNetwork)));
        }

        private static bool AreSameNetwork(NetworkData? first, NetworkData? second)
        {
            if (first == null && second == null) return true;
            if (first == null || second == null) return false;
            return string.Equals(first.Ssid, second.Ssid, StringComparison.Ordinal)
                && string.Equals(first.Bssid?.ToString(), second.Bssid?.ToString(), StringComparison.Ordinal)
                && first.IpAddress == second.IpAddress;
        }

        private static NetworkData? CloneNetworkData(NetworkData? source)
        {
            if (source == null) return null;
            return new NetworkData
            {
                StatusId = source.StatusId,
                Ssid = source.Ssid,
                IpAddress = source.IpAddress,
                GatewayAddress = source.GatewayAddress,
                DhcpServerAddress = source.DhcpServerAddress,
                NativeObject = source.NativeObject,
                Bssid = source.Bssid,
                SignalStrength = source.SignalStrength,
                SecurityType = source.SecurityType,
                IPv6Address = source.IPv6Address,
                DnsAddresses = source.DnsAddresses != null ? new List<string>(source.DnsAddresses) : null,
                SubnetMask = source.SubnetMask,
                RssiDbm = source.RssiDbm,
                FrequencyBand = source.FrequencyBand,
                ChannelNumber = source.ChannelNumber,
                LinkSpeedMbps = source.LinkSpeedMbps,
                WifiStandard = source.WifiStandard,
                ChannelWidthMhz = source.ChannelWidthMhz,
            };
        }

        private async Task RunScanSessionAsync(CancellationToken cancellationToken)
        {
            var known = new Dictionary<string, NetworkData>(StringComparer.OrdinalIgnoreCase);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var scanResponse = await ScanWifiNetworksAsync(cancellationToken);
                    // Wi-Fi turned off means every network is gone; other errors keep the current list.
                    var networks = scanResponse.ErrorCode == WifiErrorCodes.Success ? scanResponse.Data
                        : scanResponse.ErrorCode == WifiErrorCodes.WifiNotEnabled ? new List<NetworkData>()
                        : null;
                    if (networks != null)
                    {
                        var current = new Dictionary<string, NetworkData>(StringComparer.OrdinalIgnoreCase);
                        foreach (var network in networks)
                        {
                            var key = BuildNetworkKey(network);
                            if (!string.IsNullOrWhiteSpace(key))
                                current[key] = network;
                        }

                        foreach (var (key, network) in known)
                            if (!current.ContainsKey(key))
                                _DeviceLost?.Invoke(this, network);

                        foreach (var (key, network) in current)
                            if (!known.ContainsKey(key))
                                _DeviceDiscovered?.Invoke(this, CloneNetworkData(network)!);

                        known = current;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                lock (_ScanLock)
                {
                    // StopScanningAsync may already have cleared this session and a new one may be running.
                    if (_ScanSessionCts != null && _ScanSessionCts.Token == cancellationToken)
                    {
                        IsScanning = false;
                        _ScanSessionCts.Dispose();
                        _ScanSessionCts = null;
                        _ScanSessionTask = null;
                    }
                }
            }
        }

        private static string BuildNetworkKey(NetworkData network)
        {
            var ssid = network.Ssid?.Trim() ?? string.Empty;
            var bssid = network.Bssid?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ssid) && string.IsNullOrWhiteSpace(bssid)) return string.Empty;
            return string.Concat(ssid, "|", bssid);
        }

        private static WifiFrequencyBand GetBandFromFrequencyMHz(int frequencyMhz)
        {
            if (frequencyMhz >= 2400 && frequencyMhz < 2500) return WifiFrequencyBand.Band2_4GHz;
            if (frequencyMhz >= 4900 && frequencyMhz < 5925) return WifiFrequencyBand.Band5GHz;
            if (frequencyMhz >= 5925 && frequencyMhz < 7125) return WifiFrequencyBand.Band6GHz;
            return WifiFrequencyBand.Unknown;
        }

        private static int? GetChannelFromFrequencyMHz(int frequencyMhz)
        {
            if (frequencyMhz == 2484) return 14;
            if (frequencyMhz >= 2412 && frequencyMhz <= 2484) return (frequencyMhz - 2412) / 5 + 1;
            if (frequencyMhz >= 5180 && frequencyMhz <= 5885) return (frequencyMhz - 5000) / 5;
            if (frequencyMhz >= 5955 && frequencyMhz <= 7115) return (frequencyMhz - 5955) / 5 + 1;
            return null;
        }

        private string GetSecurityType(NetworkAuthenticationType authType)
        {
            return authType switch
            {
                NetworkAuthenticationType.RsnaPsk => "WPA2-PSK",
                NetworkAuthenticationType.Rsna => "WPA2-Enterprise",
                NetworkAuthenticationType.WpaPsk => "WPA-PSK",
                NetworkAuthenticationType.Wpa => "WPA-Enterprise",
                NetworkAuthenticationType.Open80211 => "Open (No Security)",
                _ => "Unknown or WPA3"
            };
        }

        private static int IpAddressToInt(IPAddress? ip)
        {
            if (ip == null) return 0;
            var bytes = ip.GetAddressBytes();
            if (bytes.Length != 4) return 0;
            return (bytes[0] & 0xFF) |
                   ((bytes[1] & 0xFF) << 8) |
                   ((bytes[2] & 0xFF) << 16) |
                   ((bytes[3] & 0xFF) << 24);
        }
    }
}

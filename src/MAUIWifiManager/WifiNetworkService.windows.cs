using MauiWifiManager.Abstractions;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;
using Windows.Security.Credentials;

namespace MauiWifiManager
{
    public class WifiNetworkService : IWifiNetworkService
    {
        private EventHandler<WifiNetworkChangedEventArgs>? _WifiNetworkChanged;
        private EventHandler<NetworkData>? _DeviceDiscovered;
        private readonly object _MonitorLock = new();
        private readonly object _ScanLock = new();
        private NetworkData? _LastKnownNetwork;
        private CancellationTokenSource? _ScanSessionCts;
        private Task? _ScanSessionTask;
        private readonly HashSet<string> _DiscoveredNetworkKeys = new(StringComparer.OrdinalIgnoreCase);
        private bool _IsMonitoring;

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

        public async Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, WifiConnectionOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = new WifiManagerResponse<NetworkData>();

            var credential = new PasswordCredential { Password = password };
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

            await adapter.ScanAsync().AsTask(cancellationToken);

            // Find network by SSID (and optionally BSSID)
            WiFiAvailableNetwork? target = null;
            foreach (var network in adapter.NetworkReport.AvailableNetworks)
            {
                if (network.Ssid == ssid &&
                    (string.IsNullOrWhiteSpace(options.Bssid) || string.Equals(network.Bssid, options.Bssid, StringComparison.OrdinalIgnoreCase)))
                {
                    target = network;
                    break;
                }
            }

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
                    : (string.IsNullOrWhiteSpace(options.Bssid)
                        ? "The specified network was not found."
                        : "The specified SSID/BSSID network was not found.");
                return response;
            }

            // Use 4-param overload when connecting to a hidden network so Windows passes SSID explicitly
            WiFiConnectionResult status;
            if (options.IsHidden || string.IsNullOrEmpty(target.Ssid))
                status = await adapter.ConnectAsync(target, WiFiReconnectionKind.Automatic, credential, ssid).AsTask(cancellationToken);
            else
                status = await adapter.ConnectAsync(target, WiFiReconnectionKind.Automatic, credential).AsTask(cancellationToken);

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
                    var result = await WiFiAdapter.FindAllAdaptersAsync();
                    if (result.Count > 0)
                    {
                        var wifiAdapter = result[0];
                        Debug.WriteLine("Wi-Fi Scan started.");
                        await wifiAdapter.ScanAsync().AsTask(cancellationToken);

                        foreach (var network in wifiAdapter.NetworkReport.AvailableNetworks)
                        {
                            int freqMHz = (int)(network.ChannelCenterFrequencyInKilohertz / 1000);
                            wifiNetworks.Add(new NetworkData
                            {
                                Ssid = network.Ssid,
                                Bssid = network.Bssid,
                                SignalStrength = network.SignalBars,
                                SecurityType = network.PhyKind,
                                FrequencyBand = freqMHz > 0 ? GetBandFromFrequencyMHz(freqMHz) : WifiFrequencyBand.Unknown,
                                ChannelNumber = freqMHz > 0 ? GetChannelFromFrequencyMHz(freqMHz) : null,
                                NativeObject = network
                            });
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

                _DiscoveredNetworkKeys.Clear();
                _ScanSessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
            };
        }

        private async Task RunScanSessionAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var scanResponse = await ScanWifiNetworksAsync(cancellationToken);
                    if (scanResponse.ErrorCode == WifiErrorCodes.Success && scanResponse.Data != null)
                    {
                        foreach (var network in scanResponse.Data)
                        {
                            var key = BuildNetworkKey(network);
                            if (string.IsNullOrWhiteSpace(key)) continue;

                            if (_DiscoveredNetworkKeys.Add(key))
                                _DeviceDiscovered?.Invoke(this, CloneNetworkData(network)!);
                        }
                    }
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                lock (_ScanLock)
                {
                    IsScanning = false;
                    _ScanSessionCts?.Dispose();
                    _ScanSessionCts = null;
                    _ScanSessionTask = null;
                    _DiscoveredNetworkKeys.Clear();
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

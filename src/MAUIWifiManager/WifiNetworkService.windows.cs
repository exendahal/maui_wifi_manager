using MauiWifiManager.Abstractions;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;
using Windows.Security.Credentials;

namespace MauiWifiManager
{
    /// <summary>
    /// Interface for Wi-FiNetworkService
    /// </summary>
    public class WifiNetworkService : IWifiNetworkService
    {
        private EventHandler<WifiNetworkChangedEventArgs>? _wifiNetworkChanged;
        private readonly object _monitorLock = new();
        private NetworkData? _lastKnownNetwork;
        private bool _isMonitoring;

        public event EventHandler<WifiNetworkChangedEventArgs>? WifiNetworkChanged
        {
            add
            {
                _wifiNetworkChanged += value;
                EnsureMonitoringStarted();
            }
            remove => _wifiNetworkChanged -= value;
        }

        public WifiNetworkService()
        {
        }

        /// <summary>
        /// Connect Wi-Fi
        /// </summary>
        [Obsolete("Use ConnectWifiAsync(string ssid, string password, CancellationToken cancellationToken = default) or ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default) instead.")]
        public Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password, string? bssid = null)
        {
            return ConnectWifiAsync(ssid, password, bssid, CancellationToken.None);
        }

        /// <summary>
        /// Connect Wi-Fi
        /// </summary>
        public Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, CancellationToken cancellationToken = default)
        {
            return ConnectWifiAsync(ssid, password, null, cancellationToken);
        }

        /// <summary>
        /// Connect Wi-Fi
        /// </summary>
        public async Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = new WifiManagerResponse<NetworkData>();
            var credential = new PasswordCredential
            {
                Password = password
            };
            WiFiAdapter adapter;
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

            adapter = await WiFiAdapter.FromIdAsync(result[0].Id);
            if (adapter != null)
            {
                await adapter.ScanAsync().AsTask(cancellationToken);
                WiFiAvailableNetwork? wiFiAvailableNetwork = null;
                foreach (var network in adapter.NetworkReport.AvailableNetworks)
                {
                    if (network.Ssid == ssid && (string.IsNullOrWhiteSpace(bssid) || string.Equals(network.Bssid, bssid, StringComparison.OrdinalIgnoreCase)))
                    {
                        wiFiAvailableNetwork = network;
                        break;
                    }
                }
                if (wiFiAvailableNetwork != null)
                {
                    var status = await adapter.ConnectAsync(wiFiAvailableNetwork, WiFiReconnectionKind.Automatic, credential).AsTask(cancellationToken);
                    if (status.ConnectionStatus == WiFiConnectionStatus.Success)
                    {
                        Debug.WriteLine("Connected successfully to the network.");
                        Windows.Networking.Connectivity.ConnectionProfile InternetConnectionProfile = NetworkInformation.GetInternetConnectionProfile();
                        var hostname = NetworkInformation.GetHostNames().FirstOrDefault(hn => hn.IPInformation?.NetworkAdapter != null && hn.IPInformation.NetworkAdapter.NetworkAdapterId == InternetConnectionProfile?.NetworkAdapter.NetworkAdapterId);
                        var networkData = await GetNetworkInfoAsync(cancellationToken);
                        if (networkData.ErrorCode == WifiErrorCodes.Success)
                        {
                            response.ErrorCode = WifiErrorCodes.Success;
                            response.Data = networkData.Data;
                        }
                        else
                        {
                            Debug.WriteLine("Failed to get network info.");
                            response.ErrorCode = WifiErrorCodes.UnknownError;
                            response.ErrorMessage = "Failed to get network info.";
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
                }
                else
                {
                    Debug.WriteLine(string.IsNullOrWhiteSpace(bssid)
                        ? "The specified network was not found."
                        : "The specified SSID/BSSID network was not found.");
                    response.ErrorCode = WifiErrorCodes.NoConnection;
                    response.ErrorMessage = string.IsNullOrWhiteSpace(bssid)
                        ? "The specified network was not found."
                        : "The specified SSID/BSSID network was not found.";
                }
            }
            else
            {
                Debug.WriteLine("Failed to get Wi-Fi adapter.");
                response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                response.ErrorMessage = "Failed to get Wi-Fi adapter.";
            }
            return response;
        }

        /// <summary>
        /// Disconnect Wi-Fi
        /// </summary>
        public async void DisconnectWifi(string ssid)
        {
            WiFiAdapter adapter;
            var result = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(WiFiAdapter.GetDeviceSelector());
            if (result.Count >= 1)
            {
                adapter = await WiFiAdapter.FromIdAsync(result[0].Id);
                adapter.Disconnect();
            }
        }

        /// <summary>
        /// Get Network Info
        /// </summary>
        [Obsolete("Use GetNetworkInfoAsync(CancellationToken cancellationToken = default) instead.")]
        public Task<WifiManagerResponse<NetworkData>> GetNetworkInfo()
        {
            return GetNetworkInfoAsync(CancellationToken.None);
        }

        /// <summary>
        /// Get Network Info
        /// </summary>
        public Task<WifiManagerResponse<NetworkData>> GetNetworkInfoAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<WifiManagerResponse<NetworkData>>(cancellationToken);
            }

            var response = new WifiManagerResponse<NetworkData>();
            var networkData = new NetworkData();

            try
            {
                Windows.Networking.Connectivity.ConnectionProfile? profile = NetworkInformation.GetConnectionProfiles().FirstOrDefault(x => x.IsWlanConnectionProfile && x.GetNetworkConnectivityLevel() > NetworkConnectivityLevel.None);
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
                {
                    networkData.SecurityType = GetSecurityType(profile.NetworkSecuritySettings.NetworkAuthenticationType);
                }

                var networkInterface = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && n.OperationalStatus == OperationalStatus.Up);
                if (networkInterface != null)
                {
                    var ipaddress = networkInterface.GetIPProperties();
                    var ip = ipaddress.UnicastAddresses.FirstOrDefault(n => n.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (ip != null)
                    {
                        networkData.IpAddress = BitConverter.ToInt32(ip.Address.GetAddressBytes(), 0);
                    }
                    else
                    {
                        networkData.IpAddress = 0;
                    }

                    var gatewayInfo = ipaddress.GatewayAddresses.FirstOrDefault(n => n.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (gatewayInfo != null)
                    {
                        networkData.GatewayAddress = IpAddressToInt(gatewayInfo.Address);
                    }

                    var internetworkAddress = ipaddress.DhcpServerAddresses.FirstOrDefault(n => n.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (internetworkAddress != null)
                    {
                        networkData.DhcpServerAddress = IpAddressToInt(internetworkAddress);
                    }
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

        /// <summary>
        /// Open Wi-Fi Setting
        /// </summary>
        public async Task<bool> OpenWifiSetting()
        {
            return await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:network-wifi"));
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            StopMonitoring();
        }

        /// <summary>
        /// Scan Wi-Fi Networks
        /// </summary>
        [Obsolete("Use ScanWifiNetworksAsync(CancellationToken cancellationToken = default) instead.")]
        public Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworks()
        {
            return ScanWifiNetworksAsync(CancellationToken.None);
        }

        /// <summary>
        /// Scan Wi-Fi Networks
        /// </summary>
        public async Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworksAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = new WifiManagerResponse<List<NetworkData>>();
            try
            {
                List<NetworkData> wifiNetworks = new List<NetworkData>();

                var accessStatus = await WiFiAdapter.RequestAccessAsync();
                if (accessStatus == WiFiAccessStatus.Allowed)
                {
                    var result = await WiFiAdapter.FindAllAdaptersAsync();
                    if (result.Count > 0)
                    {
                        var wifiAdapter = result[0];
                        Debug.WriteLine($"Wi-Fi Scan started.");
                        await wifiAdapter.ScanAsync().AsTask(cancellationToken);
                        var availableNetworks = wifiAdapter.NetworkReport.AvailableNetworks;
                        foreach (var network in availableNetworks)
                        {
                            wifiNetworks.Add(new NetworkData
                            {
                                Ssid = network.Ssid,
                                Bssid = network.Bssid,
                                SignalStrength = network.SignalBars,
                                SecurityType = network.PhyKind
                            });
                        }
                    }
                    Debug.WriteLine($"Wi-Fi Scan complete.");
                    response.ErrorCode = WifiErrorCodes.Success;
                    response.ErrorMessage = $"Wi-Fi Scan complete.";
                    response.Data = wifiNetworks;
                }
                else
                {
                    Debug.WriteLine($"Request Access Async failed.");
                    response.ErrorCode = WifiErrorCodes.UnknownError;
                    response.ErrorMessage = $"Request Access Async failed.";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while scanning Wi-Fi: {ex.Message}");
                response.ErrorCode = WifiErrorCodes.UnknownError;
                response.ErrorMessage = $"Error while scanning Wi-Fi: {ex.Message}";
            }
            return response;
        }


        /// <summary>
        /// Open Network and Internet Setting
        /// </summary>
        public async Task<bool> OpenWirelessSetting()
        {
            return await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:network"));
        }

        private void EnsureMonitoringStarted()
        {
            lock (_monitorLock)
            {
                if (_isMonitoring)
                {
                    return;
                }

                NetworkChange.NetworkAddressChanged += OnNetworkChanged;
                NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
                _isMonitoring = true;
            }

            _ = RefreshSnapshotAsync(raiseEvent: false);
        }

        private void StopMonitoring()
        {
            lock (_monitorLock)
            {
                if (!_isMonitoring)
                {
                    return;
                }

                NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
                NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
                _isMonitoring = false;
                _lastKnownNetwork = null;
            }
        }

        private void OnNetworkChanged(object? sender, EventArgs e)
        {
            _ = RefreshSnapshotAsync(raiseEvent: true);
        }

        private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            _ = RefreshSnapshotAsync(raiseEvent: true);
        }

        private async Task RefreshSnapshotAsync(bool raiseEvent)
        {
            NetworkData? currentNetwork = null;

            try
            {
                var info = await GetNetworkInfoAsync();
                if (info.ErrorCode == WifiErrorCodes.Success && info.Data != null)
                {
                    currentNetwork = CloneNetworkData(info.Data);
                }
            }
            catch
            {
                currentNetwork = null;
            }

            NetworkData? oldNetwork;
            bool changed;

            lock (_monitorLock)
            {
                oldNetwork = CloneNetworkData(_lastKnownNetwork);
                changed = !AreSameNetwork(_lastKnownNetwork, currentNetwork);
                _lastKnownNetwork = CloneNetworkData(currentNetwork);
            }

            if (raiseEvent && changed)
            {
                _wifiNetworkChanged?.Invoke(this, new WifiNetworkChangedEventArgs(oldNetwork, CloneNetworkData(currentNetwork)));
            }
        }

        private static bool AreSameNetwork(NetworkData? first, NetworkData? second)
        {
            if (first == null && second == null)
            {
                return true;
            }

            if (first == null || second == null)
            {
                return false;
            }

            return string.Equals(first.Ssid, second.Ssid, StringComparison.Ordinal)
                && string.Equals(first.Bssid?.ToString(), second.Bssid?.ToString(), StringComparison.Ordinal)
                && first.IpAddress == second.IpAddress;
        }

        private static NetworkData? CloneNetworkData(NetworkData? source)
        {
            if (source == null)
            {
                return null;
            }

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
                SecurityType = source.SecurityType
            };
        }

        private string GetSecurityType(NetworkAuthenticationType authType)
        {
            switch (authType)
            {
                case NetworkAuthenticationType.RsnaPsk:
                    return "WPA2-PSK";  // WPA2 Personal (Pre-Shared Key)
                case NetworkAuthenticationType.Rsna:
                    return "WPA2-Enterprise";  // WPA2 Enterprise
                case NetworkAuthenticationType.WpaPsk:
                    return "WPA-PSK";  // WPA Personal (Pre-Shared Key)
                case NetworkAuthenticationType.Wpa:
                    return "WPA-Enterprise";  // WPA Enterprise
                case NetworkAuthenticationType.Open80211:
                    return "Open (No Security)";  // Open Network
                case NetworkAuthenticationType.None:
                default:
                    return "Unknown or WPA3 (Possibly)"; // Handling missing WPA3 types
            }
        }

        private int IpAddressToInt(IPAddress? ip)
        {
            if (ip == null) return 0;

            var bytes = ip.GetAddressBytes();
            if (bytes.Length != 4) return 0;

            // Convert big-endian network order → little-endian int
            return (bytes[0] & 0xFF) |
                   ((bytes[1] & 0xFF) << 8) |
                   ((bytes[2] & 0xFF) << 16) |
                   ((bytes[3] & 0xFF) << 24);
        }
    }
}

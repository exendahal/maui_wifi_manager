using CoreLocation;
using Foundation;
using MauiWifiManager.Abstractions;
using NetworkExtension;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SystemConfiguration;
using UIKit;

namespace MauiWifiManager
{
    public class WifiNetworkService : IWifiNetworkService
    {
        public NEHotspotHelper _HotspotHelper;
        private EventHandler<WifiNetworkChangedEventArgs>? _WifiNetworkChanged;
        public event EventHandler<NetworkData>? DeviceDiscovered;
        private readonly object _MonitorLock = new();
        private NetworkData? _LastKnownNetwork;
        private bool _IsMonitoring;

        public bool IsScanning => false;

        public event EventHandler<WifiNetworkChangedEventArgs>? WifiNetworkChanged
        {
            add
            {
                _WifiNetworkChanged += value;
                EnsureMonitoringStarted();
            }
            remove => _WifiNetworkChanged -= value;
        }

        public WifiNetworkService()
        {
            _HotspotHelper = new NEHotspotHelper();
        }

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

            try
            {
                NEHotspotConfigurationManager.SharedManager.RemoveConfiguration(ssid);

                NEHotspotConfiguration config;
                if (options.SecurityType == WifiSecurityType.Open || string.IsNullOrEmpty(password))
                {
                    config = new NEHotspotConfiguration(ssid);
                }
                else
                {
                    bool isWep = options.SecurityType == WifiSecurityType.Wep;
                    config = new NEHotspotConfiguration(ssid, password, isWep: isWep);
                }

                config.JoinOnce = false;

                // Hidden network support (iOS 13+)
                if (options.IsHidden && IsAppleVersionAtLeast(13))
                    config.Hidden = true;

                var tcs = new TaskCompletionSource<NSError>();
                NEHotspotConfigurationManager.SharedManager.ApplyConfiguration(config, err =>
                {
                    tcs.TrySetResult(err);
                });

                var error = await tcs.Task.WaitAsync(cancellationToken);

                if (error == null)
                {
                    Debug.WriteLine("Successfully connected to the network.");
                    var networkData = await GetNetworkInfoAsync(cancellationToken);
                    return WifiManagerResponse<NetworkData>.SuccessResponse(networkData.Data, "Successfully connected to the network.");
                }
                else if (error.LocalizedDescription == "already associated.")
                {
                    var networkData = await GetNetworkInfoAsync(cancellationToken);
                    return WifiManagerResponse<NetworkData>.SuccessResponse(networkData.Data, "Already associated with the network.");
                }
                else
                {
                    Debug.WriteLine($"Connection failed: {error.LocalizedDescription}");
                    return WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, $"Failed to connect: {error.LocalizedDescription}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error connecting to WiFi: {ex.Message}");
                return WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.UnknownError, ex.Message);
            }
        }

        public void DisconnectWifi(string ssid)
        {
            NEHotspotConfigurationManager.SharedManager.RemoveConfiguration(ssid);
        }

        [Obsolete("Use GetNetworkInfoAsync(CancellationToken cancellationToken = default) instead.")]
        public Task<WifiManagerResponse<NetworkData>> GetNetworkInfo()
        {
            return GetNetworkInfoAsync(CancellationToken.None);
        }

        public async Task<WifiManagerResponse<NetworkData>> GetNetworkInfoAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = new WifiManagerResponse<NetworkData>();
            var locationManager = new CLLocationManager();

            if (OperatingSystem.IsIOSVersionAtLeast(8))
                locationManager.RequestWhenInUseAuthorization();

            if (IsAppleVersionAtLeast(14))
            {
                var tcs = new TaskCompletionSource<WifiManagerResponse<NetworkData>>();
                if (locationManager.AuthorizationStatus == CLAuthorizationStatus.Authorized ||
                    locationManager.AuthorizationStatus == CLAuthorizationStatus.AuthorizedAlways ||
                    locationManager.AuthorizationStatus == CLAuthorizationStatus.AuthorizedWhenInUse)
                {
                    NEHotspotNetwork.FetchCurrent(hotspotNetwork =>
                    {
                        if (hotspotNetwork != null)
                        {
                            var data = new NetworkData
                            {
                                StatusId = (int)WifiErrorCodes.Success,
                                Ssid = hotspotNetwork.Ssid,
                                Bssid = hotspotNetwork.Bssid,
                                SignalStrength = hotspotNetwork.SignalStrength,
                                SecurityType = IsAppleVersionAtLeast(15) ? hotspotNetwork.SecurityType : null,
                                NativeObject = hotspotNetwork
                            };
                            PopulateNetworkInterfaceExtendedData(data);
                            response.ErrorCode = WifiErrorCodes.Success;
                            response.ErrorMessage = "Fetched Wi-Fi connection info successfully.";
                            response.Data = data;
                        }
                        else
                        {
                            response.ErrorCode = WifiErrorCodes.NetworkUnavailable;
                            response.ErrorMessage = "No network is currently connected.";
                        }
                        tcs.SetResult(response);
                    });
                }
                else
                {
                    response.ErrorCode = WifiErrorCodes.PermissionDenied;
                    response.ErrorMessage = "Location permissions are not granted.";
                    tcs.SetResult(response);
                }
                return await tcs.Task.WaitAsync(cancellationToken);
            }
            else
            {
                if (CaptiveNetwork.TryGetSupportedInterfaces(out string[] supportedInterfaces) == StatusCode.OK)
                {
                    if (supportedInterfaces != null)
                    {
                        foreach (var interfaceName in supportedInterfaces)
                        {
                            if (CaptiveNetwork.TryCopyCurrentNetworkInfo(interfaceName, out NSDictionary? info) == StatusCode.OK)
                            {
                                var data = new NetworkData
                                {
                                    StatusId = 1,
                                    Ssid = info?[CaptiveNetwork.NetworkInfoKeySSID]?.ToString(),
                                    Bssid = info?[CaptiveNetwork.NetworkInfoKeyBSSID]?.ToString(),
                                    NativeObject = info
                                };
                                PopulateNetworkInterfaceExtendedData(data);
                                response.ErrorCode = WifiErrorCodes.Success;
                                response.Data = data;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    response.ErrorCode = WifiErrorCodes.NetworkUnavailable;
                    response.ErrorMessage = "No network is currently connected.";
                }
            }
            return response;
        }

        public async Task<bool> OpenWifiSetting()
        {
            return await OpenSettings();
        }

        public void Dispose()
        {
            StopMonitoring();
        }

        [Obsolete("Use ScanWifiNetworksAsync(CancellationToken cancellationToken = default) instead.")]
        public Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworks()
        {
            return ScanWifiNetworksAsync(CancellationToken.None);
        }

        public Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworksAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<WifiManagerResponse<List<NetworkData>>>(cancellationToken);

            var response = new WifiManagerResponse<List<NetworkData>>();
            Debug.WriteLine("ScanWifiNetworks is not supported on iOS/Mac Catalyst.");
            response.ErrorCode = WifiErrorCodes.WifiNotEnabled;
            response.ErrorMessage = "ScanWifiNetworks is not supported on iOS/Mac Catalyst.";
            return Task.FromResult(response);
        }

        public Task<WifiManagerResponse<bool>> StartScanningForDevicesAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<WifiManagerResponse<bool>>(cancellationToken);

            return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.WifiNotEnabled, "Continuous scanning is not supported on iOS/Mac Catalyst."));
        }

        public Task<WifiManagerResponse<bool>> StopScanningAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<WifiManagerResponse<bool>>(cancellationToken);

            return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(false, "No active scan session."));
        }

        public async Task<bool> OpenWirelessSetting()
        {
            return await OpenSettings();
        }

        public async Task<WifiManagerResponse<bool>> IsInternetAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool wifiUp = NetworkInterface.GetAllNetworkInterfaces()
                    .Any(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                            && (ni.OperationalStatus == OperationalStatus.Up || ni.OperationalStatus == OperationalStatus.Unknown));

                if (!wifiUp)
                    return WifiManagerResponse<bool>.SuccessResponse(false, "Wi-Fi interface is not active.");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var entry = await System.Net.Dns.GetHostEntryAsync("www.apple.com", cts.Token);
                return WifiManagerResponse<bool>.SuccessResponse(true, "Internet is available.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return WifiManagerResponse<bool>.SuccessResponse(false, "Internet check timed out.");
            }
            catch
            {
                return WifiManagerResponse<bool>.SuccessResponse(false, "Internet is not available.");
            }
        }

        public async Task<WifiManagerResponse<bool>> IsCaptivePortalDetectedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var networkInfo = await GetNetworkInfoAsync(cancellationToken);
                if (networkInfo.ErrorCode != WifiErrorCodes.Success || networkInfo.Data?.Ssid == null)
                    return WifiManagerResponse<bool>.SuccessResponse(false, "Not connected to Wi-Fi.");

                using var handler = new HttpClientHandler { AllowAutoRedirect = false };
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };

                var httpResponse = await client.GetAsync("http://captive.apple.com/hotspot-detect.html", cancellationToken);

                if ((int)httpResponse.StatusCode is >= 300 and < 400)
                    return WifiManagerResponse<bool>.SuccessResponse(true, "Captive portal detected (redirect).");

                if (httpResponse.IsSuccessStatusCode)
                {
                    var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                    bool isCaptive = !content.Contains("<BODY>Success</BODY>", StringComparison.OrdinalIgnoreCase);
                    return WifiManagerResponse<bool>.SuccessResponse(isCaptive,
                        isCaptive ? "Captive portal detected." : "No captive portal.");
                }

                return WifiManagerResponse<bool>.SuccessResponse(false, "No captive portal detected.");
            }
            catch (OperationCanceledException)
            {
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");
            }
            catch (Exception ex)
            {
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, ex.Message);
            }
        }

        private static async Task<bool> OpenSettings()
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion(8, 0))
            {
                try
                {
                    var url = new NSUrl(UIApplication.OpenSettingsUrlString);
                    if (UIApplication.SharedApplication.CanOpenUrl(url))
                    {
                        var success = await UIApplication.SharedApplication.OpenUrlAsync(url, new UIApplicationOpenUrlOptions());
                        if (!success) Debug.WriteLine("Failed to open app settings.");
                        return success;
                    }
                    Debug.WriteLine("Cannot open app settings URL.");
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error: {ex.Message}");
                    return false;
                }
            }
            Debug.WriteLine("OpenWirelessSetting is not supported on this Apple platform version.");
            return false;
        }

        private static bool IsAppleVersionAtLeast(int majorVersion)
        {
            return OperatingSystem.IsIOSVersionAtLeast(majorVersion)
                || OperatingSystem.IsMacCatalystVersionAtLeast(majorVersion);
        }

        private static void PopulateNetworkInterfaceExtendedData(NetworkData networkData)
        {
            try
            {
                var wifiInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(iface =>
                        iface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                        (iface.OperationalStatus == OperationalStatus.Up || iface.OperationalStatus == OperationalStatus.Unknown));

                if (wifiInterface == null) return;

                var ipProps = wifiInterface.GetIPProperties();

                var unicastIpv4 = ipProps.UnicastAddresses
                    .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);
                if (unicastIpv4 != null)
                {
                    networkData.IpAddress = IpAddressToInt(unicastIpv4.Address);
                    if (unicastIpv4.IPv4Mask != null)
                        networkData.SubnetMask = unicastIpv4.IPv4Mask.ToString();
                }

                var gateway = ipProps.GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
                if (gateway != null)
                    networkData.GatewayAddress = IpAddressToInt(gateway.Address);

                var ipv6 = ipProps.UnicastAddresses
                    .Where(u => u.Address.AddressFamily == AddressFamily.InterNetworkV6
                             && !u.Address.IsIPv6LinkLocal
                             && !IPAddress.IsLoopback(u.Address))
                    .Select(u => u.Address.ToString())
                    .FirstOrDefault();
                networkData.IPv6Address = ipv6;

                var dns = ipProps.DnsAddresses
                    .Where(a => !IPAddress.IsLoopback(a))
                    .Select(a => a.ToString())
                    .ToList();
                if (dns.Count > 0) networkData.DnsAddresses = dns;
            }
            catch { }
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
    }
}

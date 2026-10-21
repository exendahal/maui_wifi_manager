#if ANDROID
using Microsoft.Maui.LifecycleEvents;
#endif
namespace MauiWifiManager
{
    /// <summary>
    /// Static façade for Wi-Fi network management. Delegates all calls to the platform-specific <see cref="IWifiNetworkService"/> implementation.
    /// </summary>
    public static class CrossWifiManager
    {
        private static Lazy<IWifiNetworkService> _Implementation = new(() => CreateWifiManager(), System.Threading.LazyThreadSafetyMode.PublicationOnly);

        private static IWifiNetworkService CreateWifiManager()
        {
            return new WifiNetworkService();
        }

        /// <summary>
        /// Gets if the package is supported on the current platform.
        /// </summary>
        public static bool IsSupported => _Implementation.Value == null ? false : true;

        /// <summary>
        /// Current package implementation to use
        /// </summary>
        public static IWifiNetworkService Current
        {
            get
            {
                IWifiNetworkService ret = _Implementation.Value;
                if (ret == null)
                {
                    throw NotImplementedInReferenceAssembly();
                }
                return ret;
            }
        }

        /// <summary>
        /// Raised when the currently connected Wi-Fi network changes.
        /// </summary>
        public static event EventHandler<Abstractions.WifiNetworkChangedEventArgs>? WifiNetworkChanged
        {
            add => Current.WifiNetworkChanged += value;
            remove
            {
                if (_Implementation != null && _Implementation.IsValueCreated)
                {
                    _Implementation.Value.WifiNetworkChanged -= value;
                }
            }
        }

        /// <summary>
        /// Raised when a network is discovered during an active scan session.
        /// </summary>
        public static event EventHandler<Abstractions.NetworkData>? DeviceDiscovered
        {
            add => Current.DeviceDiscovered += value;
            remove
            {
                if (_Implementation != null && _Implementation.IsValueCreated)
                {
                    _Implementation.Value.DeviceDiscovered -= value;
                }
            }
        }

        /// <summary>
        /// Raised when a previously discovered network is no longer visible during an active scan session.
        /// </summary>
        public static event EventHandler<Abstractions.NetworkData>? DeviceLost
        {
            add => Current.DeviceLost += value;
            remove
            {
                if (_Implementation != null && _Implementation.IsValueCreated)
                {
                    _Implementation.Value.DeviceLost -= value;
                }
            }
        }

        /// <summary>
        /// Gets whether the current implementation is scanning for devices.
        /// </summary>
        public static bool IsScanning => Current.IsScanning;

        internal static Exception NotImplementedInReferenceAssembly()
        {
            return new NotImplementedException("This functionality is not implemented in the portable version of this assembly.  You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");
        }

        /// <summary>
        /// Connects to a Wi-Fi network. Deprecated — use the <see cref="ConnectWifiAsync(string, string, CancellationToken)"/> overload instead.
        /// </summary>
        [Obsolete("Use ConnectWifiAsync(string ssid, string password) or ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default) instead.")]
        public static Task<Abstractions.WifiManagerResponse<Abstractions.NetworkData>> ConnectWifi(string ssid, string password, string? bssid = null)
        {
            return Current.ConnectWifi(ssid, password, bssid);
        }

        /// <summary>
        /// Connects to a Wi-Fi network with the specified SSID and password.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<Abstractions.NetworkData>> ConnectWifiAsync(string ssid, string password, CancellationToken cancellationToken = default)
        {
            return Current.ConnectWifiAsync(ssid, password, cancellationToken);
        }

        /// <summary>
        /// Connects to a Wi-Fi network targeting a specific access point by BSSID.
        /// BSSID targeting is supported on Android and Windows; ignored on iOS/Mac Catalyst.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<Abstractions.NetworkData>> ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default)
        {
            return Current.ConnectWifiAsync(ssid, password, bssid, cancellationToken);
        }

        /// <summary>
        /// Connects to a Wi-Fi network using the provided connection options.
        /// Supports hidden networks (all platforms) and WPA3-SAE (Android API 29+, iOS 15+, Windows).
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<Abstractions.NetworkData>> ConnectWifiAsync(string ssid, string password, Abstractions.WifiConnectionOptions options, CancellationToken cancellationToken = default)
        {
            return Current.ConnectWifiAsync(ssid, password, options, cancellationToken);
        }

        /// <summary>
        /// Retrieves details of the currently connected Wi-Fi network. Deprecated — use <see cref="GetNetworkInfoAsync(CancellationToken)"/> instead.
        /// </summary>
        [Obsolete("Use GetNetworkInfoAsync(CancellationToken cancellationToken = default) instead.")]
        public static Task<Abstractions.WifiManagerResponse<Abstractions.NetworkData>> GetNetworkInfo()
        {
            return Current.GetNetworkInfo();
        }

        /// <summary>
        /// Retrieves details of the currently connected Wi-Fi network.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<Abstractions.NetworkData>> GetNetworkInfoAsync(CancellationToken cancellationToken = default)
        {
            return Current.GetNetworkInfoAsync(cancellationToken);
        }

        /// <summary>
        /// Disconnects from the specified Wi-Fi network.
        /// </summary>
        public static void DisconnectWifi(string ssid)
        {
            Current.DisconnectWifi(ssid);
        }

        /// <summary>
        /// Opens the device's Wi-Fi settings for quick access.
        /// On iOS, this opens the app's settings instead of the Wi-Fi settings.
        /// </summary>
        public static Task<bool> OpenWifiSetting()
        {
            return Current.OpenWifiSetting();
        }

        /// <summary>
        /// Scans for available Wi-Fi networks (Android and Windows only). Deprecated — use <see cref="ScanWifiNetworksAsync(CancellationToken)"/> instead.
        /// </summary>
        [Obsolete("Use ScanWifiNetworksAsync(CancellationToken cancellationToken = default) instead.")]
        public static Task<Abstractions.WifiManagerResponse<List<Abstractions.NetworkData>>> ScanWifiNetworks()
        {
            return Current.ScanWifiNetworks();
        }

        /// <summary>
        /// Scans for available Wi-Fi networks (Android and Windows only).
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<List<Abstractions.NetworkData>>> ScanWifiNetworksAsync(CancellationToken cancellationToken = default)
        {
            return Current.ScanWifiNetworksAsync(cancellationToken);
        }

        /// <summary>
        /// Starts a continuous scan session and emits discovered networks through <see cref="DeviceDiscovered"/>.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<bool>> StartScanningForDevicesAsync(CancellationToken cancellationToken = default)
        {
            return Current.StartScanningForDevicesAsync(cancellationToken);
        }

        /// <summary>
        /// Stops the currently running scan session.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<bool>> StopScanningAsync(CancellationToken cancellationToken = default)
        {
            return Current.StopScanningAsync(cancellationToken);
        }

        /// <summary>
        /// Opens the device's wireless settings.
        /// On iOS, this opens the app's settings instead of wireless settings.
        /// </summary>
        public static Task<bool> OpenWirelessSetting()
        {
            return Current.OpenWirelessSetting();
        }

        /// <summary>
        /// Returns true when the device has a validated internet connection over Wi-Fi.
        /// On Android uses NetworkCapabilities; on Windows uses NetworkConnectivityLevel;
        /// on iOS/Mac Catalyst performs a DNS reachability check.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<bool>> IsInternetAvailableAsync(CancellationToken cancellationToken = default)
        {
            return Current.IsInternetAvailableAsync(cancellationToken);
        }

        /// <summary>
        /// Returns true when a captive portal is detected on the current Wi-Fi network.
        /// On Android uses NET_CAPABILITY_CAPTIVE_PORTAL; on Windows uses ConstrainedInternetAccess;
        /// on iOS performs an HTTP probe to Apple's captive portal detection endpoint.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<bool>> IsCaptivePortalDetectedAsync(CancellationToken cancellationToken = default)
        {
            return Current.IsCaptivePortalDetectedAsync(cancellationToken);
        }

        /// <summary>
        /// Raised when Wi-Fi is turned on or off. Supported on Android and Windows.
        /// </summary>
        public static event EventHandler<Abstractions.WifiStateChangedEventArgs>? WifiStateChanged
        {
            add => Current.WifiStateChanged += value;
            remove
            {
                if (_Implementation != null && _Implementation.IsValueCreated)
                {
                    _Implementation.Value.WifiStateChanged -= value;
                }
            }
        }

        /// <summary>
        /// Raised when the signal strength of the connected network changes. Supported on Android and Windows.
        /// </summary>
        public static event EventHandler<Abstractions.WifiSignalChangedEventArgs>? SignalStrengthChanged
        {
            add => Current.SignalStrengthChanged += value;
            remove
            {
                if (_Implementation != null && _Implementation.IsValueCreated)
                {
                    _Implementation.Value.SignalStrengthChanged -= value;
                }
            }
        }

        /// <summary>
        /// Returns true when the Wi-Fi radio is on. Supported on Android and Windows.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<bool>> IsWifiEnabledAsync(CancellationToken cancellationToken = default)
        {
            return Current.IsWifiEnabledAsync(cancellationToken);
        }

        /// <summary>
        /// Checks and requests every permission needed to scan and read Wi-Fi details.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<bool>> RequestPermissionsAsync(CancellationToken cancellationToken = default)
        {
            return Current.RequestPermissionsAsync(cancellationToken);
        }

        /// <summary>
        /// Returns true when the active connection is metered or data-limited.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<bool>> IsMeteredConnectionAsync(CancellationToken cancellationToken = default)
        {
            return Current.IsMeteredConnectionAsync(cancellationToken);
        }

        /// <summary>
        /// Lists saved Wi-Fi networks by SSID.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<List<string>>> GetSavedNetworksAsync(CancellationToken cancellationToken = default)
        {
            return Current.GetSavedNetworksAsync(cancellationToken);
        }

        /// <summary>
        /// Removes a saved Wi-Fi network.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<bool>> ForgetNetworkAsync(string ssid, CancellationToken cancellationToken = default)
        {
            return Current.ForgetNetworkAsync(ssid, cancellationToken);
        }

        /// <summary>
        /// Silently saves a network suggestion that persists after the app closes (Android 10+ only).
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<bool>> AddNetworkSuggestionAsync(string ssid, string password, Abstractions.WifiConnectionOptions options, CancellationToken cancellationToken = default)
        {
            return Current.AddNetworkSuggestionAsync(ssid, password, options, cancellationToken);
        }

        /// <summary>
        /// Connects to an 802.1X (WPA2/WPA3-Enterprise) network.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<Abstractions.NetworkData>> ConnectEnterpriseWifiAsync(string ssid, Abstractions.WifiEnterpriseOptions options, CancellationToken cancellationToken = default)
        {
            return Current.ConnectEnterpriseWifiAsync(ssid, options, cancellationToken);
        }

        /// <summary>
        /// Connects to a network without internet access (e.g. an IoT device's access point) and routes app traffic through it.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<Abstractions.NetworkData>> ConnectLocalOnlyAsync(string ssid, string password, Abstractions.WifiConnectionOptions options, CancellationToken cancellationToken = default)
        {
            return Current.ConnectLocalOnlyAsync(ssid, password, options, cancellationToken);
        }

        /// <summary>
        /// Releases the network acquired by <see cref="ConnectLocalOnlyAsync"/>.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<bool>> ReleaseLocalOnlyAsync(CancellationToken cancellationToken = default)
        {
            return Current.ReleaseLocalOnlyAsync(cancellationToken);
        }

        /// <summary>
        /// Raised when the device switches between Wi-Fi, mobile data, Ethernet and offline.
        /// </summary>
        public static event EventHandler<Abstractions.ConnectionTypeChangedEventArgs>? ConnectionTypeChanged
        {
            add => Current.ConnectionTypeChanged += value;
            remove
            {
                if (_Implementation != null && _Implementation.IsValueCreated)
                {
                    _Implementation.Value.ConnectionTypeChanged -= value;
                }
            }
        }

        /// <summary>
        /// Raised whenever the OS finishes a Wi-Fi scan. Supported on Android and Windows.
        /// </summary>
        public static event EventHandler<Abstractions.WifiScanResultsEventArgs>? ScanResultsAvailable
        {
            add => Current.ScanResultsAvailable += value;
            remove
            {
                if (_Implementation != null && _Implementation.IsValueCreated)
                {
                    _Implementation.Value.ScanResultsAvailable -= value;
                }
            }
        }

        /// <summary>
        /// Lists the kinds of network the device is connected to.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<List<Abstractions.NetworkConnectionType>>> GetConnectionTypesAsync(CancellationToken cancellationToken = default)
        {
            return Current.GetConnectionTypesAsync(cancellationToken);
        }

        /// <summary>
        /// Reports whether a Wi-Fi scan can start now, and if not, why. Supported on Android and Windows.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<Abstractions.WifiScanAvailability>> CanStartScanAsync(CancellationToken cancellationToken = default)
        {
            return Current.CanStartScanAsync(cancellationToken);
        }

        /// <summary>
        /// Checks internet access by requesting <paramref name="probeUrl"/>.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<bool>> IsInternetAvailableAsync(Uri probeUrl, CancellationToken cancellationToken = default)
        {
            return Current.IsInternetAvailableAsync(probeUrl, cancellationToken);
        }

        /// <summary>
        /// Turns Wi-Fi on or off (Android 9 and lower, Windows); opens the Wi-Fi panel on Android 10+.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<bool>> SetWifiEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            return Current.SetWifiEnabledAsync(enabled, cancellationToken);
        }

        /// <summary>
        /// Starts a hotspot and returns its credentials. Supported on Android and Windows.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<Abstractions.HotspotInfo>> StartHotspotAsync(CancellationToken cancellationToken = default)
        {
            return Current.StartHotspotAsync(cancellationToken);
        }

        /// <summary>
        /// Stops the hotspot started by <see cref="StartHotspotAsync"/>.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<bool>> StopHotspotAsync(CancellationToken cancellationToken = default)
        {
            return Current.StopHotspotAsync(cancellationToken);
        }

        /// <summary>
        /// Lists devices connected to the hotspot. Supported on Windows only.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<List<Abstractions.HotspotClient>>> GetHotspotClientsAsync(CancellationToken cancellationToken = default)
        {
            return Current.GetHotspotClientsAsync(cancellationToken);
        }

        /// <summary>
        /// Finds devices on the local network by pinging the subnet.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<List<Abstractions.LanDevice>>> DiscoverDevicesAsync(CancellationToken cancellationToken = default)
        {
            return Current.DiscoverDevicesAsync(cancellationToken);
        }

        /// <summary>
        /// Finds services advertised over mDNS/Bonjour.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<List<Abstractions.NetworkServiceInfo>>> BrowseServicesAsync(string serviceType = "_services._dns-sd._udp", TimeSpan? duration = null, CancellationToken cancellationToken = default)
        {
            return Current.BrowseServicesAsync(serviceType, duration, cancellationToken);
        }

        /// <summary>
        /// Pings a host and returns round-trip statistics.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<Abstractions.PingStatistics>> PingAsync(string host, int count = 4, CancellationToken cancellationToken = default)
        {
            return Current.PingAsync(host, count, cancellationToken);
        }

        /// <summary>
        /// Measures download and upload speed and latency.
        /// </summary>
        public static Task<Abstractions.WifiManagerResponse<Abstractions.SpeedTestResult>> MeasureSpeedAsync(Abstractions.SpeedTestOptions? options = null, CancellationToken cancellationToken = default)
        {
            return Current.MeasureSpeedAsync(options, cancellationToken);
        }

        /// <summary>
        /// Dispose of everything
        /// </summary>
        public static void Dispose()
        {
            if (_Implementation != null && _Implementation.IsValueCreated)
            {
                _Implementation.Value.Dispose();
                _Implementation = new Lazy<IWifiNetworkService>(() => CreateWifiManager(), System.Threading.LazyThreadSafetyMode.PublicationOnly);
            }
        }
    }

    /// <summary>
    /// Initialize WifiManager
    /// </summary>
    public static class Initialize
    {
        /// <summary>
        /// Initialize WifiManager on Android
        /// </summary>
        public static MauiAppBuilder UseMauiWifiManager(this MauiAppBuilder builder)
        {
        #if ANDROID
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddAndroid(android => android.OnCreate((activity, bundle) =>
                {                    
                    WifiNetworkService.Init(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity);
                }));
            });
          
        #endif
            return builder;
        }
    }
}


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


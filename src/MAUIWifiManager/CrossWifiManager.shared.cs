#if ANDROID
using Microsoft.Maui.LifecycleEvents;
#endif
namespace MauiWifiManager
{
    /// <summary>
    /// Interface for WiFiNetworkService
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

        internal static Exception NotImplementedInReferenceAssembly()
        {
            return new NotImplementedException("This functionality is not implemented in the portable version of this assembly.  You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");
        }

        [Obsolete("Use ConnectWifiAsync(string ssid, string password) or ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default) instead.")]
        public static Task<Abstractions.WifiManagerResponse<Abstractions.NetworkData>> ConnectWifi(string ssid, string password, string? bssid = null)
        {
            return Current.ConnectWifi(ssid, password, bssid);
        }

        public static Task<Abstractions.WifiManagerResponse<Abstractions.NetworkData>> ConnectWifiAsync(string ssid, string password, CancellationToken cancellationToken = default)
        {
            return Current.ConnectWifiAsync(ssid, password, cancellationToken);
        }

        public static Task<Abstractions.WifiManagerResponse<Abstractions.NetworkData>> ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default)
        {
            return Current.ConnectWifiAsync(ssid, password, bssid, cancellationToken);
        }

        [Obsolete("Use GetNetworkInfoAsync(CancellationToken cancellationToken = default) instead.")]
        public static Task<Abstractions.WifiManagerResponse<Abstractions.NetworkData>> GetNetworkInfo()
        {
            return Current.GetNetworkInfo();
        }

        public static Task<Abstractions.WifiManagerResponse<Abstractions.NetworkData>> GetNetworkInfoAsync(CancellationToken cancellationToken = default)
        {
            return Current.GetNetworkInfoAsync(cancellationToken);
        }

        public static void DisconnectWifi(string ssid)
        {
            Current.DisconnectWifi(ssid);
        }

        public static Task<bool> OpenWifiSetting()
        {
            return Current.OpenWifiSetting();
        }

        [Obsolete("Use ScanWifiNetworksAsync(CancellationToken cancellationToken = default) instead.")]
        public static Task<Abstractions.WifiManagerResponse<List<Abstractions.NetworkData>>> ScanWifiNetworks()
        {
            return Current.ScanWifiNetworks();
        }

        public static Task<Abstractions.WifiManagerResponse<List<Abstractions.NetworkData>>> ScanWifiNetworksAsync(CancellationToken cancellationToken = default)
        {
            return Current.ScanWifiNetworksAsync(cancellationToken);
        }

        public static Task<bool> OpenWirelessSetting()
        {
            return Current.OpenWirelessSetting();
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


using MauiWifiManager.Abstractions;

namespace MauiWifiManager
{
    /// <summary>
    /// Provides methods for managing Wi-Fi on the device.
    /// </summary>
    public interface IWifiNetworkService : IDisposable
    {
        /// <summary>
        /// Raised when the connected Wi-Fi network changes (including OS-driven changes).
        /// </summary>
        event EventHandler<WifiNetworkChangedEventArgs>? WifiNetworkChanged;

        /// <summary>
        /// Raised when a Wi-Fi network is discovered while an active scan session is running.
        /// </summary>
        event EventHandler<NetworkData>? DeviceDiscovered;

        /// <summary>
        /// Raised when a previously discovered Wi-Fi network is no longer visible (or Wi-Fi was turned off) during an active scan session.
        /// </summary>
        event EventHandler<NetworkData>? DeviceLost;

        /// <summary>
        /// Gets a value indicating whether a scan session is currently running.
        /// </summary>
        bool IsScanning { get; }

        /// <summary>
        /// Connects to a Wi-Fi network with the specified SSID and password.
        /// When provided, bssid targets a specific access point for that SSID.
        /// BSSID-targeted connection is supported on Android and Windows.
        /// On Apple platforms, the OS API does not support selecting a BSSID for connect.
        /// </summary>
        [Obsolete("Use ConnectWifiAsync(string ssid, string password, CancellationToken cancellationToken = default) or ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default) instead.")]
        Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password, string? bssid = null);

        /// <summary>
        /// Connects to a Wi-Fi network with the specified SSID and password.
        /// When provided, bssid targets a specific access point for that SSID.
        /// BSSID-targeted connection is supported on Android and Windows.
        /// On Apple platforms, the OS API does not support selecting a BSSID for connect.
        /// </summary>
        Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, CancellationToken cancellationToken = default);

        /// <summary>
        /// Connects to a Wi-Fi network with the specified SSID and password.
        /// When provided, bssid targets a specific access point for that SSID.
        /// BSSID-targeted connection is supported on Android and Windows.
        /// On Apple platforms, the OS API does not support selecting a BSSID for connect.
        /// </summary>
        Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Connects to a Wi-Fi network using the provided connection options.
        /// Supports hidden networks (all platforms) and WPA3-SAE (Android API 29+, iOS 15+, Windows).
        /// </summary>
        Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, WifiConnectionOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves details of the currently connected Wi-Fi network.
        /// </summary>
        [Obsolete("Use GetNetworkInfoAsync(CancellationToken cancellationToken = default) instead.")]
        Task<WifiManagerResponse<NetworkData>> GetNetworkInfo();

        /// <summary>
        /// Retrieves details of the currently connected Wi-Fi network.
        /// </summary>
        Task<WifiManagerResponse<NetworkData>> GetNetworkInfoAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from the specified Wi-Fi network.
        /// </summary>
        void DisconnectWifi(string ssid);

        /// <summary>
        /// Opens the device's Wi-Fi settings for quick access.
        /// On iOS, this opens the app's settings instead of the Wi-Fi settings.
        /// </summary>
        Task<bool> OpenWifiSetting();

        /// <summary>
        /// Scans for available Wi-Fi networks (Android and Windows only).
        /// </summary>
        [Obsolete("Use ScanWifiNetworksAsync(CancellationToken cancellationToken = default) instead.")]
        Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworks();

        /// <summary>
        /// Scans for available Wi-Fi networks (Android and Windows only).
        /// </summary>
        Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworksAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a continuous scan session and emits discovered networks through DeviceDiscovered
        /// and networks that went out of range through DeviceLost.
        /// The session stops when cancellationToken is canceled; when no cancelable token is passed it stops after 30 seconds.
        /// </summary>
        Task<WifiManagerResponse<bool>> StartScanningForDevicesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the currently running scan session.
        /// </summary>
        Task<WifiManagerResponse<bool>> StopScanningAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens the device's wireless settings.
        /// On iOS, this opens the app's settings instead of wireless settings.
        /// </summary>
        Task<bool> OpenWirelessSetting();

        /// <summary>
        /// Returns true when the device has a validated internet connection over Wi-Fi.
        /// On Android uses NetworkCapabilities; on Windows uses NetworkConnectivityLevel;
        /// on iOS/Mac Catalyst performs a DNS reachability check.
        /// </summary>
        Task<WifiManagerResponse<bool>> IsInternetAvailableAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns true when a captive portal is detected on the current Wi-Fi network.
        /// On Android uses NET_CAPABILITY_CAPTIVE_PORTAL; on Windows uses ConstrainedInternetAccess;
        /// on iOS performs an HTTP probe to Apple's captive portal detection endpoint.
        /// </summary>
        Task<WifiManagerResponse<bool>> IsCaptivePortalDetectedAsync(CancellationToken cancellationToken = default);

        // Members below were added after 10.0.2. They have default implementations so existing
        // implementations of this interface (custom services, test fakes) keep compiling.

        /// <summary>
        /// Raised when Wi-Fi is turned on or off. Supported on Android and Windows.
        /// </summary>
        event EventHandler<WifiStateChangedEventArgs>? WifiStateChanged { add { } remove { } }

        /// <summary>
        /// Raised when the signal strength of the connected network changes.
        /// Android uses the system RSSI broadcast; Windows polls signal bars every 5 seconds.
        /// </summary>
        event EventHandler<WifiSignalChangedEventArgs>? SignalStrengthChanged { add { } remove { } }

        /// <summary>
        /// Returns true when the Wi-Fi radio is on. Supported on Android and Windows.
        /// iOS/Mac Catalyst have no public API for this and return <see cref="WifiErrorCodes.UnsupportedHardware"/>.
        /// </summary>
        Task<WifiManagerResponse<bool>> IsWifiEnabledAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(NotSupported<bool>(nameof(IsWifiEnabledAsync)));

        /// <summary>
        /// Checks and requests every permission needed to scan and read Wi-Fi details
        /// (location on Android/iOS, nearby Wi-Fi devices on Android 13+, Wi-Fi access on Windows).
        /// Returns true when everything is granted; on Android also fails when location services are turned off.
        /// </summary>
        Task<WifiManagerResponse<bool>> RequestPermissionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(NotSupported<bool>(nameof(RequestPermissionsAsync)));

        /// <summary>
        /// Returns true when the active connection is metered or data-limited
        /// (Android: NET_CAPABILITY_NOT_METERED; Windows: ConnectionCost; iOS: NWPath.IsExpensive/IsConstrained).
        /// </summary>
        Task<WifiManagerResponse<bool>> IsMeteredConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(NotSupported<bool>(nameof(IsMeteredConnectionAsync)));

        /// <summary>
        /// Lists saved Wi-Fi networks by SSID.
        /// Windows: all saved profiles. iOS: networks configured by this app.
        /// Android 10+: network suggestions added by this app; Android 9 and lower: configured networks.
        /// </summary>
        Task<WifiManagerResponse<List<string>>> GetSavedNetworksAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(NotSupported<List<string>>(nameof(GetSavedNetworksAsync)));

        /// <summary>
        /// Removes a saved Wi-Fi network. Same scope as <see cref="GetSavedNetworksAsync"/>:
        /// on Android 11+ networks the user saved through the system dialog can only be removed by the user.
        /// </summary>
        Task<WifiManagerResponse<bool>> ForgetNetworkAsync(string ssid, CancellationToken cancellationToken = default)
            => Task.FromResult(NotSupported<bool>(nameof(ForgetNetworkAsync)));

        /// <summary>
        /// Silently saves a network suggestion (Android 10+). The OS connects to it automatically when in range,
        /// and keeps it after the app closes. The user is asked once to allow suggestions from the app.
        /// Supported on Android only.
        /// </summary>
        Task<WifiManagerResponse<bool>> AddNetworkSuggestionAsync(string ssid, string password, WifiConnectionOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(NotSupported<bool>(nameof(AddNetworkSuggestionAsync)));

        /// <summary>
        /// Connects to an 802.1X (WPA2/WPA3-Enterprise) network using PEAP, TTLS or PWD.
        /// Android 10+ saves it as a network suggestion; Windows negotiates the EAP method itself and
        /// uses only <see cref="WifiEnterpriseOptions.Identity"/> and <see cref="WifiEnterpriseOptions.Password"/>.
        /// </summary>
        Task<WifiManagerResponse<NetworkData>> ConnectEnterpriseWifiAsync(string ssid, WifiEnterpriseOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(NotSupported<NetworkData>(nameof(ConnectEnterpriseWifiAsync)));

        /// <summary>
        /// Connects to a network without internet access, such as an IoT device's setup access point,
        /// and routes the app's traffic through it so HTTP calls reach the device instead of falling back to cellular.
        /// Call <see cref="ReleaseLocalOnlyAsync"/> when done so the device returns to its previous network.
        /// </summary>
        Task<WifiManagerResponse<NetworkData>> ConnectLocalOnlyAsync(string ssid, string password, WifiConnectionOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(NotSupported<NetworkData>(nameof(ConnectLocalOnlyAsync)));

        /// <summary>
        /// Releases the network acquired by <see cref="ConnectLocalOnlyAsync"/>.
        /// </summary>
        Task<WifiManagerResponse<bool>> ReleaseLocalOnlyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(NotSupported<bool>(nameof(ReleaseLocalOnlyAsync)));

        /// <summary>
        /// Raised when the device switches between Wi-Fi, mobile data, Ethernet and offline, or gains or loses internet access.
        /// </summary>
        event EventHandler<ConnectionTypeChangedEventArgs>? ConnectionTypeChanged
        {
            add => ConnectionMonitor.Add(this, value);
            remove => ConnectionMonitor.Remove(value);
        }

        /// <summary>
        /// Lists the kinds of network the device is connected to (Wi-Fi, mobile data, Ethernet, Bluetooth); empty when offline.
        /// </summary>
        Task<WifiManagerResponse<List<NetworkConnectionType>>> GetConnectionTypesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ConnectionMonitor.GetConnectionTypes());

        /// <summary>
        /// Reports whether a Wi-Fi scan can start now, and if not, why (Wi-Fi off, permission, location off, throttled).
        /// Supported on Android and Windows.
        /// </summary>
        Task<WifiManagerResponse<WifiScanAvailability>> CanStartScanAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(NotSupported<WifiScanAvailability>(nameof(CanStartScanAsync)));

        /// <summary>
        /// Raised whenever the OS finishes a Wi-Fi scan, including scans the system or other apps started.
        /// Listening costs no scan quota. Supported on Android and Windows.
        /// </summary>
        event EventHandler<WifiScanResultsEventArgs>? ScanResultsAvailable { add { } remove { } }

        /// <summary>
        /// Checks internet access by requesting <paramref name="probeUrl"/>; any 2xx response counts as available.
        /// Use this where the OS check is unreliable, e.g. networks that block the default probe servers.
        /// </summary>
        Task<WifiManagerResponse<bool>> IsInternetAvailableAsync(Uri probeUrl, CancellationToken cancellationToken = default)
            => NetworkTools.ProbeInternetAsync(probeUrl, cancellationToken);

        /// <summary>
        /// Turns Wi-Fi on or off. Android 10+ does not allow apps to do this, so it opens the system Wi-Fi panel instead and
        /// returns <see langword="false"/> in <see cref="WifiManagerResponse{T}.Data"/>. Supported on Android and Windows.
        /// </summary>
        Task<WifiManagerResponse<bool>> SetWifiEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
            => Task.FromResult(NotSupported<bool>(nameof(SetWifiEnabledAsync)));

        /// <summary>
        /// Starts a hotspot and returns its credentials. Android starts a local-only hotspot (devices connect to this phone,
        /// no internet sharing); Windows turns on Mobile Hotspot and shares the current connection.
        /// </summary>
        Task<WifiManagerResponse<HotspotInfo>> StartHotspotAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(NotSupported<HotspotInfo>(nameof(StartHotspotAsync)));

        /// <summary>
        /// Stops the hotspot started by <see cref="StartHotspotAsync"/>.
        /// </summary>
        Task<WifiManagerResponse<bool>> StopHotspotAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(NotSupported<bool>(nameof(StopHotspotAsync)));

        /// <summary>
        /// Lists devices connected to the hotspot. Supported on Windows only.
        /// </summary>
        Task<WifiManagerResponse<List<HotspotClient>>> GetHotspotClientsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(NotSupported<List<HotspotClient>>(nameof(GetHotspotClientsAsync)));

        /// <summary>
        /// Finds devices on the local network by pinging every address in the subnet (at most the /24 around this device),
        /// then looking up their host names. Devices that ignore ping are not found.
        /// </summary>
        Task<WifiManagerResponse<List<LanDevice>>> DiscoverDevicesAsync(CancellationToken cancellationToken = default)
            => NetworkTools.DiscoverDevicesAsync(cancellationToken);

        /// <summary>
        /// Finds services advertised over mDNS/Bonjour, e.g. <c>_http._tcp</c>, <c>_ipp._tcp</c> or <c>_googlecast._tcp</c>.
        /// The default <c>_services._dns-sd._udp</c> lists the service types present on the network.
        /// Android needs the CHANGE_WIFI_MULTICAST_STATE permission; iOS requires the multicast networking
        /// entitlement and NSLocalNetworkUsageDescription.
        /// </summary>
        Task<WifiManagerResponse<List<NetworkServiceInfo>>> BrowseServicesAsync(string serviceType = "_services._dns-sd._udp", TimeSpan? duration = null, CancellationToken cancellationToken = default)
            => NetworkTools.BrowseServicesAsync(serviceType, duration ?? NetworkTools.DefaultBrowseDuration, cancellationToken);

        /// <summary>
        /// Pings a host and returns min/average/max round-trip times and packet loss.
        /// </summary>
        Task<WifiManagerResponse<PingStatistics>> PingAsync(string host, int count = 4, CancellationToken cancellationToken = default)
            => NetworkTools.PingAsync(host, count, cancellationToken);

        /// <summary>
        /// Measures download and upload speed and latency. Uses Cloudflare's public speed test endpoints unless
        /// <paramref name="options"/> points elsewhere. Transfers up to about 30 MB.
        /// </summary>
        Task<WifiManagerResponse<SpeedTestResult>> MeasureSpeedAsync(SpeedTestOptions? options = null, CancellationToken cancellationToken = default)
            => NetworkTools.MeasureSpeedAsync(options ?? new SpeedTestOptions(), cancellationToken);

        private static WifiManagerResponse<T> NotSupported<T>(string operation)
            => WifiManagerResponse<T>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, $"{operation} is not supported on this platform.");
    }
}

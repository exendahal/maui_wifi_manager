namespace MauiWifiManager.Abstractions
{
    /// <summary>
    /// Provides data for the <see cref="IWifiNetworkService.WifiNetworkChanged"/> event.
    /// </summary>
    public sealed class WifiNetworkChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance with the previous and current network snapshots.
        /// </summary>
        public WifiNetworkChangedEventArgs(NetworkData? oldNetwork, NetworkData? newNetwork)
        {
            OldNetwork = oldNetwork;
            NewNetwork = newNetwork;
        }

        /// <summary>
        /// Network snapshot before the change, or <see langword="null"/> if there was no previous connection.
        /// </summary>
        public NetworkData? OldNetwork { get; }

        /// <summary>
        /// Network snapshot after the change, or <see langword="null"/> if the device disconnected.
        /// </summary>
        public NetworkData? NewNetwork { get; }
    }

    /// <summary>
    /// Provides data for the <see cref="IWifiNetworkService.WifiStateChanged"/> event.
    /// </summary>
    public sealed class WifiStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance with the new Wi-Fi radio state.
        /// </summary>
        public WifiStateChangedEventArgs(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }

        /// <summary>
        /// <see langword="true"/> when Wi-Fi is turned on.
        /// </summary>
        public bool IsEnabled { get; }
    }

    /// <summary>
    /// Provides data for the <see cref="IWifiNetworkService.SignalStrengthChanged"/> event.
    /// </summary>
    public sealed class WifiSignalChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance with the new signal readings.
        /// </summary>
        public WifiSignalChangedEventArgs(int? rssiDbm, int signalLevel, int signalPercentage)
        {
            RssiDbm = rssiDbm;
            SignalLevel = signalLevel;
            SignalPercentage = signalPercentage;
        }

        /// <summary>
        /// Raw RSSI in dBm, or <see langword="null"/> when the platform only reports signal bars (Windows).
        /// </summary>
        public int? RssiDbm { get; }

        /// <summary>
        /// Signal level from 0 (weakest) to 4 (strongest).
        /// </summary>
        public int SignalLevel { get; }

        /// <summary>
        /// Signal quality from 0 to 100.
        /// </summary>
        public int SignalPercentage { get; }
    }

    /// <summary>
    /// IEEE 802.11 standard used by an access point. Values match Android's <c>ScanResult.WIFI_STANDARD_*</c> constants.
    /// </summary>
    public enum WifiStandard
    {
        /// <summary>Standard is unknown or not reported by the platform.</summary>
        Unknown = 0,
        /// <summary>802.11a/b/g.</summary>
        Legacy = 1,
        /// <summary>802.11n (Wi-Fi 4).</summary>
        Ieee80211n = 4,
        /// <summary>802.11ac (Wi-Fi 5).</summary>
        Ieee80211ac = 5,
        /// <summary>802.11ax (Wi-Fi 6/6E).</summary>
        Ieee80211ax = 6,
        /// <summary>802.11ad (WiGig).</summary>
        Ieee80211ad = 7,
        /// <summary>802.11be (Wi-Fi 7).</summary>
        Ieee80211be = 8,
    }

    /// <summary>
    /// Outer EAP method used by an 802.1X (enterprise) network.
    /// </summary>
    public enum WifiEapMethod
    {
        /// <summary>Protected EAP (most common, e.g. eduroam and corporate networks).</summary>
        Peap = 0,
        /// <summary>EAP-TTLS.</summary>
        Ttls = 1,
        /// <summary>EAP-PWD. Supported on Android only.</summary>
        Pwd = 2,
    }

    /// <summary>
    /// Inner (phase 2) authentication used inside a PEAP or TTLS tunnel.
    /// </summary>
    public enum WifiPhase2Method
    {
        /// <summary>No inner authentication.</summary>
        None = 0,
        /// <summary>MSCHAPv2 (default for PEAP).</summary>
        Mschapv2 = 1,
        /// <summary>PAP (TTLS only).</summary>
        Pap = 2,
        /// <summary>GTC. Supported on Android only.</summary>
        Gtc = 3,
    }

    /// <summary>
    /// Credentials and server validation settings for an 802.1X (WPA2/WPA3-Enterprise) network.
    /// </summary>
    public class WifiEnterpriseOptions
    {
        /// <summary>
        /// Outer EAP method. Defaults to <see cref="WifiEapMethod.Peap"/>.
        /// </summary>
        public WifiEapMethod EapMethod { get; set; } = WifiEapMethod.Peap;

        /// <summary>
        /// Inner authentication. Defaults to <see cref="WifiPhase2Method.Mschapv2"/>.
        /// </summary>
        public WifiPhase2Method Phase2Method { get; set; } = WifiPhase2Method.Mschapv2;

        /// <summary>
        /// User name (identity).
        /// </summary>
        public string Identity { get; set; } = string.Empty;

        /// <summary>
        /// User password.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Outer (anonymous) identity sent before the tunnel is established, e.g. <c>anonymous@example.com</c>.
        /// </summary>
        public string? AnonymousIdentity { get; set; }

        /// <summary>
        /// Domain the RADIUS server certificate must match, e.g. <c>radius.example.com</c>.
        /// Required on Android 11+ unless <see cref="CaCertificate"/> is set. Used as the trusted server name on iOS.
        /// </summary>
        public string? ServerDomain { get; set; }

        /// <summary>
        /// CA certificate used to validate the RADIUS server. Supported on Android only.
        /// </summary>
        public System.Security.Cryptography.X509Certificates.X509Certificate2? CaCertificate { get; set; }

        /// <summary>
        /// Whether the network's SSID is hidden (not broadcast).
        /// </summary>
        public bool IsHidden { get; set; }

        /// <summary>
        /// Use WPA3-Enterprise instead of WPA2-Enterprise. Supported on Android only.
        /// </summary>
        public bool UseWpa3 { get; set; }
    }

    /// <summary>
    /// Identifies the radio frequency band of a Wi-Fi connection.
    /// </summary>
    public enum WifiFrequencyBand
    {
        /// <summary>Frequency band is unknown or not reported by the platform.</summary>
        Unknown = 0,
        /// <summary>2.4 GHz band.</summary>
        Band2_4GHz = 1,
        /// <summary>5 GHz band.</summary>
        Band5GHz = 2,
        /// <summary>6 GHz band (Wi-Fi 6E).</summary>
        Band6GHz = 3,
    }

    /// <summary>
    /// Security protocol used by a Wi-Fi network.
    /// </summary>
    public enum WifiSecurityType
    {
        /// <summary>Security type is unknown or not reported by the platform.</summary>
        Unknown = 0,
        /// <summary>Open network — no authentication required.</summary>
        Open = 1,
        /// <summary>WEP authentication (legacy, insecure).</summary>
        Wep = 2,
        /// <summary>WPA-PSK (TKIP).</summary>
        WpaPsk = 3,
        /// <summary>WPA2-PSK (CCMP/AES).</summary>
        Wpa2Psk = 4,
        /// <summary>WPA3-SAE (Simultaneous Authentication of Equals).</summary>
        Wpa3Sae = 5,
    }

    /// <summary>
    /// Options that control how a Wi-Fi connection attempt is made.
    /// </summary>
    public class WifiConnectionOptions
    {
        /// <summary>
        /// Target BSSID (MAC address) of the access point to connect to.
        /// Supported on Android and Windows; ignored on iOS/Mac Catalyst.
        /// </summary>
        public string? Bssid { get; set; }

        /// <summary>
        /// Whether the network's SSID is hidden (not broadcast).
        /// Supported on Android (API 29+), iOS 13+, and Windows.
        /// </summary>
        public bool IsHidden { get; set; } = false;

        /// <summary>
        /// Security protocol to use when connecting.
        /// Defaults to <see cref="WifiSecurityType.Wpa2Psk"/>.
        /// </summary>
        public WifiSecurityType SecurityType { get; set; } = WifiSecurityType.Wpa2Psk;
    }

    /// <summary>
    /// Represents information about a Wi-Fi network or the currently connected network.
    /// </summary>
    public class NetworkData
    {
        /// <summary>
        /// Platform-specific status code for the current connection state.
        /// </summary>
        public int StatusId { get; set; }

        /// <summary>
        /// Network SSID (Service Set Identifier).
        /// </summary>
        public string? Ssid { get; set; }

        /// <summary>
        /// IPv4 address as a 32-bit integer in host byte order.
        /// </summary>
        public int IpAddress { get; set; }

        /// <summary>
        /// Default gateway address as a 32-bit integer in host byte order.
        /// </summary>
        public int GatewayAddress { get; set; }

        /// <summary>
        /// DHCP server address as a 32-bit integer in host byte order.
        /// Supported on Android and Windows only.
        /// </summary>
        public int DhcpServerAddress { get; set; }

        /// <summary>
        /// Platform-specific native network object (e.g. <c>WifiInfo</c> on Android).
        /// </summary>
        public object? NativeObject { get; set; }

        /// <summary>
        /// BSSID (MAC address) of the access point.
        /// </summary>
        public object? Bssid { get; set; }

        /// <summary>
        /// Platform-specific signal strength indicator (e.g. RSSI level on Android).
        /// </summary>
        public object? SignalStrength { get; set; }

        /// <summary>
        /// Security type string as reported by the platform.
        /// </summary>
        public object? SecurityType { get; set; }

        /// <summary>
        /// IPv6 address of the connected interface. Supported on all platforms.
        /// </summary>
        public string? IPv6Address { get; set; }

        /// <summary>
        /// DNS server addresses. Supported on all platforms.
        /// </summary>
        public List<string>? DnsAddresses { get; set; }

        /// <summary>
        /// IPv4 subnet mask (e.g. "255.255.255.0"). Supported on all platforms.
        /// </summary>
        public string? SubnetMask { get; set; }

        /// <summary>
        /// Raw RSSI in dBm. Supported on Android only.
        /// </summary>
        public int? RssiDbm { get; set; }

        /// <summary>
        /// Wi-Fi frequency band. Supported on Android and in Windows scan results.
        /// </summary>
        public WifiFrequencyBand FrequencyBand { get; set; } = WifiFrequencyBand.Unknown;

        /// <summary>
        /// Wi-Fi channel number. Supported on Android and in Windows scan results.
        /// </summary>
        public int? ChannelNumber { get; set; }

        /// <summary>
        /// TX link speed in Mbps. Supported on Android only.
        /// </summary>
        public int? LinkSpeedMbps { get; set; }

        /// <summary>
        /// IEEE 802.11 standard (Wi-Fi 4/5/6/7). Supported on Android (API 30+) and in Windows scan results.
        /// </summary>
        public WifiStandard WifiStandard { get; set; } = WifiStandard.Unknown;

        /// <summary>
        /// Channel width in MHz (20, 40, 80, 160 or 320). Supported in Android scan results only.
        /// </summary>
        public int? ChannelWidthMhz { get; set; }
    }

    /// <summary>
    /// Wraps a Wi-Fi operation result with an error code and optional data payload.
    /// </summary>
    public class WifiManagerResponse<T>
    {
        /// <summary>
        /// Result payload, populated on success.
        /// </summary>
        public T? Data { get; set; }

        /// <summary>
        /// Error code indicating the outcome of the operation.
        /// </summary>
        public WifiErrorCodes? ErrorCode { get; set; }

        /// <summary>
        /// Human-readable description of the outcome or error.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Creates a success response with the given data payload.
        /// </summary>
        public static WifiManagerResponse<T> SuccessResponse(T? data, string errorMessage)
        {
            return new WifiManagerResponse<T>
            {
                Data = data,
                ErrorCode = WifiErrorCodes.Success,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Creates an error response with no data payload.
        /// </summary>
        public static WifiManagerResponse<T> ErrorResponse(WifiErrorCodes errorCode, string errorMessage)
        {
            return new WifiManagerResponse<T>
            {
                Data = default,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// Outcome codes returned by Wi-Fi operations.
    /// </summary>
    public enum WifiErrorCodes
    {
        /// <summary>Wi-Fi is disabled on the device.</summary>
        WifiNotEnabled = 0,
        /// <summary>Operation completed successfully.</summary>
        Success = 1,
        /// <summary>Location or network access permission was denied.</summary>
        PermissionDenied = 2,
        /// <summary>No Wi-Fi network is currently connected.</summary>
        NoConnection = 3,
        /// <summary>The device does not support the requested operation.</summary>
        UnsupportedHardware = 4,
        /// <summary>The target network was not found or is unavailable.</summary>
        NetworkUnavailable = 5,
        /// <summary>The operation timed out before completing.</summary>
        OperationTimeout = 6,
        /// <summary>Authentication failed due to an incorrect password or credential.</summary>
        InvalidCredential = 7,
        /// <summary>An unclassified error occurred.</summary>
        UnknownError = 8,
        /// <summary>The operation was canceled by the caller.</summary>
        OperationCanceled = 9
    }
}

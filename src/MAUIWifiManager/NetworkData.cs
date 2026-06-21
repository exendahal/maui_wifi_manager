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

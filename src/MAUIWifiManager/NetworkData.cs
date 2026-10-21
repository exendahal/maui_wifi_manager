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

        /// <summary>
        /// Treat the SSID as a prefix and join the first network whose name starts with it, e.g. <c>SmartPlug-</c>.
        /// iOS 13+ matches natively; Android and Windows scan and pick the strongest matching network.
        /// Not supported for hidden networks.
        /// </summary>
        public bool IsSsidPrefix { get; set; }

        /// <summary>
        /// How long to wait for the connection before giving up with <see cref="WifiErrorCodes.OperationTimeout"/>.
        /// <see langword="null"/> uses the platform default (30 seconds on Android, the OS limit elsewhere).
        /// </summary>
        public TimeSpan? Timeout { get; set; }
    }

    /// <summary>
    /// Kind of network the device is currently using.
    /// </summary>
    public enum NetworkConnectionType
    {
        /// <summary>Connection type could not be determined.</summary>
        Unknown = 0,
        /// <summary>Wi-Fi.</summary>
        WiFi = 1,
        /// <summary>Mobile data.</summary>
        Cellular = 2,
        /// <summary>Wired Ethernet.</summary>
        Ethernet = 3,
        /// <summary>Bluetooth tethering.</summary>
        Bluetooth = 4,
    }

    /// <summary>
    /// Provides data for the <see cref="IWifiNetworkService.ConnectionTypeChanged"/> event.
    /// </summary>
    public sealed class ConnectionTypeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ConnectionTypeChangedEventArgs(IReadOnlyList<NetworkConnectionType> connectionTypes, bool hasInternet)
        {
            ConnectionTypes = connectionTypes;
            HasInternet = hasInternet;
        }

        /// <summary>
        /// Networks the device is connected to; empty when offline.
        /// </summary>
        public IReadOnlyList<NetworkConnectionType> ConnectionTypes { get; }

        /// <summary>
        /// <see langword="true"/> when the OS reports internet access.
        /// </summary>
        public bool HasInternet { get; }
    }

    /// <summary>
    /// Whether a Wi-Fi scan can run right now, and if not, why.
    /// </summary>
    public enum WifiScanAvailability
    {
        /// <summary>A scan can be started.</summary>
        Ready = 0,
        /// <summary>Wi-Fi is turned off.</summary>
        WifiDisabled = 1,
        /// <summary>Location or nearby Wi-Fi devices permission is missing.</summary>
        PermissionDenied = 2,
        /// <summary>Location services are turned off (Android).</summary>
        LocationServicesDisabled = 3,
        /// <summary>The app used up the OS scan quota (Android: 4 scans per 2 minutes); results will be cached.</summary>
        Throttled = 4,
    }

    /// <summary>
    /// Provides data for the <see cref="IWifiNetworkService.ScanResultsAvailable"/> event.
    /// </summary>
    public sealed class WifiScanResultsEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public WifiScanResultsEventArgs(IReadOnlyList<NetworkData> networks, bool isFresh)
        {
            Networks = networks;
            IsFresh = isFresh;
        }

        /// <summary>
        /// Networks found by the scan.
        /// </summary>
        public IReadOnlyList<NetworkData> Networks { get; }

        /// <summary>
        /// <see langword="false"/> when the scan failed and the OS returned its previous results.
        /// </summary>
        public bool IsFresh { get; }
    }

    /// <summary>
    /// Credentials of a hotspot started by <see cref="IWifiNetworkService.StartHotspotAsync"/>.
    /// </summary>
    public sealed class HotspotInfo
    {
        /// <summary>Hotspot SSID.</summary>
        public string? Ssid { get; init; }

        /// <summary>Hotspot password.</summary>
        public string? Password { get; init; }

        /// <summary>
        /// <see langword="true"/> on Android, where the hotspot only connects devices to this phone and does not share its internet.
        /// </summary>
        public bool IsLocalOnly { get; init; }
    }

    /// <summary>
    /// A device connected to the hotspot.
    /// </summary>
    public sealed class HotspotClient
    {
        /// <summary>MAC address.</summary>
        public string? MacAddress { get; init; }

        /// <summary>Host name, when the device reports one.</summary>
        public string? HostName { get; init; }
    }

    /// <summary>
    /// A device that answered on the local network.
    /// </summary>
    public sealed class LanDevice
    {
        /// <summary>IPv4 address.</summary>
        public string IpAddress { get; init; } = string.Empty;

        /// <summary>Host name from reverse DNS, when available.</summary>
        public string? HostName { get; init; }

        /// <summary>Ping round-trip time in milliseconds.</summary>
        public long RoundTripMs { get; init; }
    }

    /// <summary>
    /// Everything that can be learned about a device on the local network from the outside.
    /// Returned by <see cref="IWifiNetworkService.GetDeviceDetailsAsync"/>; any field may be missing.
    /// </summary>
    public sealed record LanDeviceDetails
    {
        /// <summary>IPv4 address.</summary>
        public string IpAddress { get; init; } = string.Empty;

        /// <summary>Host name from reverse DNS.</summary>
        public string? HostName { get; init; }

        /// <summary>NetBIOS computer name (Windows PCs, Samba servers and many NAS devices).</summary>
        public string? NetBiosName { get; init; }

        /// <summary>
        /// MAC address, e.g. <c>A4:5E:60:12:34:56</c>. From the ARP table on Windows, otherwise from NetBIOS when the device answers it.
        /// Android 10+ and iOS do not expose the ARP table.
        /// </summary>
        public string? MacAddress { get; init; }

        /// <summary>
        /// <see langword="true"/> when the MAC address is locally administered, which usually means the device uses a
        /// private (randomized) address, as phones do by default.
        /// </summary>
        public bool? IsRandomizedMac => MacAddress is { Length: >= 2 } mac && int.TryParse(mac[..2], System.Globalization.NumberStyles.HexNumber, null, out var first)
            ? (first & 0x02) != 0
            : null;

        /// <summary>Friendly name from the device's UPnP description, e.g. <c>Living Room TV</c>.</summary>
        public string? FriendlyName { get; init; }

        /// <summary>Manufacturer from UPnP, e.g. <c>Samsung Electronics</c>.</summary>
        public string? Manufacturer { get; init; }

        /// <summary>Model name from UPnP, e.g. <c>Archer C6</c>.</summary>
        public string? ModelName { get; init; }

        /// <summary>Model number from UPnP.</summary>
        public string? ModelNumber { get; init; }

        /// <summary>Server header of the device's web interface, e.g. <c>lighttpd/1.4</c>.</summary>
        public string? HttpServer { get; init; }

        /// <summary>Page title of the device's web interface, e.g. a router's login page.</summary>
        public string? HttpTitle { get; init; }

        /// <summary>Well-known TCP ports that accepted a connection; see <see cref="KnownPorts"/>.</summary>
        public IReadOnlyList<int> OpenPorts { get; init; } = Array.Empty<int>();

        /// <summary>Ping round-trip statistics.</summary>
        public PingStatistics? Ping { get; init; }

        /// <summary>
        /// TCP ports checked by <see cref="IWifiNetworkService.GetDeviceDetailsAsync"/> and what they usually mean.
        /// </summary>
        public static IReadOnlyDictionary<int, string> KnownPorts { get; } = new Dictionary<int, string>
        {
            [21] = "FTP",
            [22] = "SSH",
            [23] = "Telnet",
            [53] = "DNS",
            [80] = "Web (HTTP)",
            [139] = "NetBIOS",
            [443] = "Web (HTTPS)",
            [445] = "File sharing (SMB)",
            [548] = "File sharing (AFP)",
            [554] = "Video stream (RTSP)",
            [631] = "Printing (IPP)",
            [1883] = "MQTT (smart home)",
            [3389] = "Remote Desktop",
            [5000] = "UPnP / NAS admin",
            [7000] = "AirPlay",
            [8008] = "Chromecast",
            [8009] = "Chromecast",
            [8080] = "Web (alternate)",
            [8443] = "Web (alternate HTTPS)",
            [9100] = "Printing (raw)",
            [32400] = "Plex",
            [62078] = "iPhone / iPad sync",
        };
    }

    /// <summary>
    /// A service advertised over mDNS/Bonjour, such as a printer or Chromecast.
    /// </summary>
    public sealed class NetworkServiceInfo
    {
        /// <summary>Instance name, e.g. <c>Living Room TV</c>.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Service type, e.g. <c>_googlecast._tcp</c>.</summary>
        public string ServiceType { get; init; } = string.Empty;

        /// <summary>Host name, e.g. <c>tv.local</c>.</summary>
        public string? HostName { get; init; }

        /// <summary>IPv4 address, when the responder included it.</summary>
        public string? IpAddress { get; init; }

        /// <summary>Port.</summary>
        public int Port { get; init; }

        /// <summary>TXT record key/value pairs.</summary>
        public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Result of <see cref="IWifiNetworkService.PingAsync"/>.
    /// </summary>
    public sealed class PingStatistics
    {
        /// <summary>Host that was pinged.</summary>
        public string Host { get; init; } = string.Empty;

        /// <summary>Echo requests sent.</summary>
        public int Sent { get; init; }

        /// <summary>Replies received.</summary>
        public int Received { get; init; }

        /// <summary>Fastest reply in milliseconds, or <see langword="null"/> when nothing replied.</summary>
        public long? MinMs { get; init; }

        /// <summary>Average reply time in milliseconds, or <see langword="null"/> when nothing replied.</summary>
        public double? AverageMs { get; init; }

        /// <summary>Slowest reply in milliseconds, or <see langword="null"/> when nothing replied.</summary>
        public long? MaxMs { get; init; }

        /// <summary>Percentage of requests without a reply.</summary>
        public double PacketLossPercent => Sent == 0 ? 0 : 100.0 * (Sent - Received) / Sent;
    }

    /// <summary>
    /// Endpoints and sizes used by <see cref="IWifiNetworkService.MeasureSpeedAsync"/>.
    /// Defaults to Cloudflare's public speed test endpoints.
    /// </summary>
    public sealed class SpeedTestOptions
    {
        /// <summary>URL that returns a large response to download.</summary>
        public Uri DownloadUrl { get; set; } = new("https://speed.cloudflare.com/__down?bytes=25000000");

        /// <summary>URL that accepts a POST body, or <see langword="null"/> to skip the upload test.</summary>
        public Uri? UploadUrl { get; set; } = new("https://speed.cloudflare.com/__up");

        /// <summary>Upload size in bytes.</summary>
        public int UploadBytes { get; set; } = 5_000_000;

        /// <summary>Stops each direction early after this long and measures what was transferred.</summary>
        public TimeSpan MaxDurationPerDirection { get; set; } = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Result of <see cref="IWifiNetworkService.MeasureSpeedAsync"/>.
    /// </summary>
    public sealed class SpeedTestResult
    {
        /// <summary>Download speed in megabits per second.</summary>
        public double DownloadMbps { get; init; }

        /// <summary>Upload speed in megabits per second, or <see langword="null"/> when skipped.</summary>
        public double? UploadMbps { get; init; }

        /// <summary>Time to first byte of a tiny request in milliseconds.</summary>
        public double LatencyMs { get; init; }
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

        /// <summary>
        /// IPv4 broadcast address (e.g. "192.168.1.255"), calculated from <see cref="IpAddress"/> and <see cref="SubnetMask"/>.
        /// </summary>
        public string? BroadcastAddress =>
            TryGetIpv4AndMask(out var ip, out var mask) ? ToDottedString(ip | ~mask) : null;

        /// <summary>
        /// Network prefix length (e.g. 24 for 255.255.255.0), calculated from <see cref="SubnetMask"/>.
        /// </summary>
        public int? NetworkPrefixLength =>
            TryGetIpv4AndMask(out _, out var mask) ? System.Numerics.BitOperations.PopCount(mask) : null;

        // IpAddress stores the first octet in the lowest byte, so BitConverter on little-endian yields octets in order.
        private bool TryGetIpv4AndMask(out uint ip, out uint mask)
        {
            ip = mask = 0;
            if (IpAddress == 0 || !System.Net.IPAddress.TryParse(SubnetMask, out var maskAddress)
                || maskAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                return false;
            ip = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(BitConverter.GetBytes(IpAddress));
            mask = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(maskAddress.GetAddressBytes());
            return true;
        }

        private static string ToDottedString(uint address) =>
            $"{address >> 24}.{(address >> 16) & 0xFF}.{(address >> 8) & 0xFF}.{address & 0xFF}";
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

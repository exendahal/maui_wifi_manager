using System.Text;

namespace MauiWifiManager.Abstractions
{
    /// <summary>
    /// Wi-Fi network shared as a QR code: <c>WIFI:T:WPA;S:name;P:password;H:true;;</c>.
    /// Special characters (\ ; , : ") are escaped with a backslash.
    /// </summary>
    public sealed class WifiQrCode
    {
        private const string Prefix = "WIFI:";

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public WifiQrCode(string ssid, string password, WifiSecurityType securityType = WifiSecurityType.Wpa2Psk, bool isHidden = false)
        {
            Ssid = ssid;
            Password = password;
            SecurityType = securityType;
            IsHidden = isHidden;
        }

        /// <summary>Network SSID.</summary>
        public string Ssid { get; }

        /// <summary>Network password; empty for open networks.</summary>
        public string Password { get; }

        /// <summary>Security protocol.</summary>
        public WifiSecurityType SecurityType { get; }

        /// <summary>Whether the SSID is hidden (not broadcast).</summary>
        public bool IsHidden { get; }

        /// <summary>
        /// Parses a <c>WIFI:</c> QR code payload. Returns <see langword="null"/> when the text is not a valid Wi-Fi QR code.
        /// </summary>
        public static WifiQrCode? Parse(string? text)
        {
            if (text == null || !text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                return null;

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var token = new StringBuilder();
            string? key = null;
            for (var i = Prefix.Length; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\\' && i + 1 < text.Length)
                    token.Append(text[++i]);
                else if (c == ':' && key == null)
                {
                    key = token.ToString();
                    token.Clear();
                }
                else if (c == ';')
                {
                    if (key != null)
                        fields[key] = token.ToString();
                    key = null;
                    token.Clear();
                }
                else
                    token.Append(c);
            }

            if (!fields.TryGetValue("S", out var ssid) || string.IsNullOrEmpty(ssid))
                return null;

            var password = fields.GetValueOrDefault("P") ?? string.Empty;
            var security = fields.GetValueOrDefault("T")?.ToUpperInvariant() switch
            {
                "SAE" or "WPA3" => WifiSecurityType.Wpa3Sae,
                "WEP" => WifiSecurityType.Wep,
                "NOPASS" => WifiSecurityType.Open,
                null or "" when password.Length == 0 => WifiSecurityType.Open,
                _ => WifiSecurityType.Wpa2Psk // "WPA" covers WPA/WPA2 Personal
            };
            var isHidden = string.Equals(fields.GetValueOrDefault("H"), "true", StringComparison.OrdinalIgnoreCase);

            return new WifiQrCode(ssid, password, security, isHidden);
        }

        /// <summary>
        /// Builds options for <see cref="IWifiNetworkService.ConnectWifiAsync(string, string, WifiConnectionOptions, CancellationToken)"/>.
        /// </summary>
        public WifiConnectionOptions ToConnectionOptions() => new() { IsHidden = IsHidden, SecurityType = SecurityType };

        /// <summary>
        /// Returns the <c>WIFI:</c> payload to encode into a QR code.
        /// </summary>
        public override string ToString()
        {
            var type = SecurityType switch
            {
                WifiSecurityType.Open => "nopass",
                WifiSecurityType.Wep => "WEP",
                WifiSecurityType.Wpa3Sae => "SAE",
                _ => "WPA"
            };
            var sb = new StringBuilder(Prefix).Append("T:").Append(type).Append(";S:").Append(Escape(Ssid)).Append(';');
            if (SecurityType != WifiSecurityType.Open)
                sb.Append("P:").Append(Escape(Password)).Append(';');
            if (IsHidden)
                sb.Append("H:true;");
            return sb.Append(';').ToString();
        }

        private static string Escape(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (c is '\\' or ';' or ',' or ':' or '"')
                    sb.Append('\\');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Converts raw RSSI readings into user-friendly signal values.
    /// </summary>
    public static class WifiSignal
    {
        // Same range Android uses for WifiManager.calculateSignalLevel.
        private const int MinRssi = -100;
        private const int MaxRssi = -55;

        /// <summary>
        /// Maps an RSSI in dBm to a level from 0 to <paramref name="numLevels"/> - 1 (default 0–4, like signal bars).
        /// </summary>
        public static int GetSignalLevel(int rssiDbm, int numLevels = 5)
        {
            if (numLevels < 2) throw new ArgumentOutOfRangeException(nameof(numLevels));
            if (rssiDbm <= MinRssi) return 0;
            if (rssiDbm >= MaxRssi) return numLevels - 1;
            return (rssiDbm - MinRssi) * (numLevels - 1) / (MaxRssi - MinRssi);
        }

        /// <summary>
        /// Maps an RSSI in dBm to a quality percentage from 0 (-100 dBm or lower) to 100 (-50 dBm or higher).
        /// </summary>
        public static int GetSignalPercentage(int rssiDbm) => Math.Clamp(2 * (rssiDbm + 100), 0, 100);
    }

    /// <summary>
    /// How busy a single Wi-Fi channel is, built from scan results.
    /// </summary>
    public sealed class WifiChannelUsage
    {
        /// <summary>Frequency band of the channel.</summary>
        public WifiFrequencyBand FrequencyBand { get; init; }

        /// <summary>Channel number.</summary>
        public int ChannelNumber { get; init; }

        /// <summary>Number of access points seen on this channel.</summary>
        public int NetworkCount { get; init; }

        /// <summary>Strongest RSSI seen on this channel in dBm, or <see langword="null"/> when not reported.</summary>
        public int? StrongestRssiDbm { get; init; }

        /// <summary>
        /// Groups scan results by band and channel, busiest channel first. Networks without a channel number are skipped.
        /// </summary>
        public static List<WifiChannelUsage> FromScanResults(IEnumerable<NetworkData> networks)
        {
            return networks
                .Where(n => n.ChannelNumber.HasValue)
                .GroupBy(n => (n.FrequencyBand, Channel: n.ChannelNumber!.Value))
                .Select(g => new WifiChannelUsage
                {
                    FrequencyBand = g.Key.FrequencyBand,
                    ChannelNumber = g.Key.Channel,
                    NetworkCount = g.Count(),
                    StrongestRssiDbm = g.Max(n => n.RssiDbm)
                })
                .OrderByDescending(u => u.NetworkCount)
                .ThenBy(u => u.FrequencyBand)
                .ThenBy(u => u.ChannelNumber)
                .ToList();
        }
    }
}

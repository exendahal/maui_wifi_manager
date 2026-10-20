using MauiWifiManager.Abstractions;
using System.Net;

namespace DemoApp
{
    /// <summary>
    /// Display wrapper around <see cref="NetworkData"/> for list rows and detail views.
    /// </summary>
    public class NetworkDataModel
    {
        public NetworkDataModel(NetworkData data)
        {
            Data = data;
        }

        public NetworkData Data { get; }

        public string Ssid => Data.Ssid?.Trim() ?? string.Empty;
        public bool IsHidden => string.IsNullOrWhiteSpace(Data.Ssid);
        public string DisplayName => IsHidden ? "Hidden network" : Ssid;
        public string Bssid => Data.Bssid?.ToString() ?? string.Empty;
        public string? Security => NetworkFormat.Security(Data.SecurityType);

        /// <summary>Band · channel · security, skipping whatever the platform did not report.</summary>
        public string Summary => string.Join("  ·  ", new[]
        {
            NetworkFormat.Band(Data.FrequencyBand),
            Data.ChannelNumber is int ch ? $"Ch {ch}" : null,
            Security
        }.Where(s => !string.IsNullOrEmpty(s)));

        public string SignalText => Data.RssiDbm is int dbm ? $"{dbm} dBm" : string.Empty;

        public int SignalBars => NetworkFormat.SignalBars(Data);
        public double Bar1 => SignalBars >= 1 ? 1 : 0.2;
        public double Bar2 => SignalBars >= 2 ? 1 : 0.2;
        public double Bar3 => SignalBars >= 3 ? 1 : 0.2;
        public double Bar4 => SignalBars >= 4 ? 1 : 0.2;
    }

    public static class NetworkFormat
    {
        public const string None = "—";

        public static string Ip(int address) =>
            address == 0 ? None : new IPAddress(BitConverter.GetBytes(address)).ToString();

        public static string? Band(WifiFrequencyBand band) => band switch
        {
            WifiFrequencyBand.Band2_4GHz => "2.4 GHz",
            WifiFrequencyBand.Band5GHz => "5 GHz",
            WifiFrequencyBand.Band6GHz => "6 GHz",
            _ => null
        };

        /// <summary>
        /// 0–4 bars. Android reports RSSI in dBm; Windows reports signal bars (0–5) in SignalStrength.
        /// </summary>
        public static int SignalBars(NetworkData data)
        {
            if (data.RssiDbm is int dbm)
                return dbm >= -55 ? 4 : dbm >= -67 ? 3 : dbm >= -78 ? 2 : 1;
            if (data.SignalStrength is byte bars)
                return Math.Clamp((int)Math.Ceiling(bars * 4 / 5.0), 0, 4);
            return 0;
        }

        /// <summary>
        /// Maps the platform security value to a short label. Android passes the scan capabilities string
        /// (e.g. "[WPA2-PSK-CCMP][ESS]"); values that are not recognised return null rather than a guess.
        /// </summary>
        public static string? Security(object? securityType)
        {
            var raw = securityType?.ToString();
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var value = raw.ToUpperInvariant();
            if (value.Contains("ENTERPRISE") || value.Contains("EAP")) return "Enterprise";
            if (value.Contains("SAE")) return "WPA3";
            if (value.Contains("WPA2") || value.Contains("RSN")) return "WPA2";
            if (value.Contains("WPA")) return "WPA";
            if (value.Contains("WEP")) return "WEP";
            if (value.Contains("OWE")) return "Enhanced Open";
            if (value.Contains("PERSONAL")) return "Personal";
            if (value.Contains("OPEN") || value == "NONE") return "Open";
            return value.StartsWith('[') ? "Open" : null;
        }
    }

    public static class ThemeColor
    {
        /// <summary>Resolves a palette entry from Colors.xaml for the current theme, e.g. "Danger" → DangerLight/DangerDark.</summary>
        public static Color Get(string name)
        {
            var app = Application.Current!;
            var key = name + (app.RequestedTheme == AppTheme.Dark ? "Dark" : "Light");
            return app.Resources.TryGetValue(key, out var value) && value is Color color ? color : Colors.Gray;
        }
    }

    /// <summary>Label/value rows separated by dividers, used on the Connection tab and in the network sheet.</summary>
    public static class DetailList
    {
        public static void Fill(Layout list, IEnumerable<(string Label, string Value, bool Mono)> rows)
        {
            list.Clear();
            foreach (var (label, value, mono) in rows)
            {
                if (list.Count > 0)
                    list.Add(new BoxView { Style = AppStyle("Divider") });

                var row = new Grid
                {
                    ColumnDefinitions = new(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)),
                    Style = AppStyle("FieldRow")
                };
                row.Add(new Label { Text = label, Style = AppStyle("FieldLabel") });
                row.Add(new Label { Text = value, Style = AppStyle(mono ? "MonoValue" : "FieldValue") }, 1);
                list.Add(row);
            }
        }

        public static Style AppStyle(string key) => (Style)Application.Current!.Resources[key];
    }

    public static class AppPopup
    {
        /// <summary>Rounded, borderless popup over a dimmed page, matching the card style.</summary>
        public static CommunityToolkit.Maui.PopupOptions Options => new()
        {
            Shape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20, StrokeThickness = 0 },
            Shadow = null,
            PageOverlayColor = Color.FromRgba(0, 0, 0, 0.55)
        };
    }
}

using CommunityToolkit.Maui.Views;
using MauiWifiManager.Abstractions;
using System.Text;
using ZXing.Net.Maui;

namespace DemoApp;

public partial class ScanQr : Popup
{
    public WifiQrCode? Result { get; private set; }

    public ScanQr()
    {
        InitializeComponent();
        barcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            Multiple = false
        };
    }

    private void BarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var code = e.Results.Select(r => WifiQrCode.Parse(r.Value)).FirstOrDefault(c => c != null);
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (code == null)
            {
                hintTxt.Text = "That QR code is not a Wi-Fi network.";
                return;
            }
            if (Result != null)
                return;

            Result = code;
            barcodeReader.IsDetecting = false;
            await CloseAsync();
        });
    }

    private async void CancelClicked(object sender, EventArgs e)
    {
        barcodeReader.IsDetecting = false;
        await CloseAsync();
    }
}

/// <summary>
/// Wi-Fi network shared as a QR code: <c>WIFI:T:WPA;S:name;P:password;H:true;;</c>.
/// Special characters (\ ; , : ") are escaped with a backslash.
/// </summary>
public sealed record WifiQrCode(string Ssid, string Password, WifiSecurityType Security, bool IsHidden)
{
    public static WifiQrCode? Parse(string? text)
    {
        const string prefix = "WIFI:";
        if (text == null || !text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var token = new StringBuilder();
        string? key = null;
        for (var i = prefix.Length; i < text.Length; i++)
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
}

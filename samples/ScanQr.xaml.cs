using CommunityToolkit.Maui.Views;
using MauiWifiManager.Abstractions;
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

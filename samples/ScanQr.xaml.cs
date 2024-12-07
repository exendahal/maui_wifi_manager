using CommunityToolkit.Maui.Views;
using ZXing.Net.Maui;

namespace DemoApp;

public partial class ScanQr : Popup
{
	public ScanQr()
	{
		InitializeComponent();
        barcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            Multiple = true
        };
    }

    private void BarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        var first = e.Results.FirstOrDefault();
        if (first != null)
        {
            if (first.Value.StartsWith("WIFI:"))
            {
                var ssid = first.Value.Split(new[] { "S:" }, StringSplitOptions.None)[1].Split(';')[0];
                var password = first.Value.Split(new[] { "P:" }, StringSplitOptions.None)[1].Split(';')[0];
                string result = ssid + ":" +password;
                Close(result);
            }
        }
    }
}
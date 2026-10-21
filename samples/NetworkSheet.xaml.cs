using CommunityToolkit.Maui.Views;

namespace DemoApp;

/// <summary>Details for a scanned network. Closes with true when the user picks Connect.</summary>
public partial class NetworkSheet : Popup<bool>
{
    public NetworkSheet(NetworkDataModel network)
    {
        InitializeComponent();

        var data = network.Data;
        titleTxt.Text = network.DisplayName;
        summaryTxt.Text = network.Summary;
        hiddenHintTxt.IsVisible = network.IsHidden;

        string Or(string? value) => string.IsNullOrWhiteSpace(value) ? NetworkFormat.None : value;
        DetailList.Fill(detailsList, new (string, string, bool)[]
        {
            ("BSSID", Or(network.Bssid), true),
            ("Security", Or(network.Security), false),
            ("Signal", NetworkFormat.Signal(data), false),
            ("Band", Or(NetworkFormat.Band(data.FrequencyBand)), false),
            ("Channel", Or(data.ChannelNumber?.ToString()), false),
            ("Channel width", data.ChannelWidthMhz is int width ? $"{width} MHz" : NetworkFormat.None, false),
            ("Wi-Fi standard", Or(NetworkFormat.Standard(data.WifiStandard)), false),
            // Android reports the capabilities string here; Windows scan results carry the PHY kind (e.g. Vht, HE).
            (DeviceInfo.Current.Platform == DevicePlatform.WinUI ? "PHY type" : "Capabilities", Or(data.SecurityType?.ToString()), true)
        });
    }

    private async void ConnectClicked(object sender, EventArgs e) => await CloseAsync(true);

    private async void CloseClicked(object sender, EventArgs e) => await CloseAsync(false);
}

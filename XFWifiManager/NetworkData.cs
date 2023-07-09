
namespace Plugin.MauiWifiManager.Abstractions
{
    public class NetworkDataModel
    {
        public int StausId { get; set; }
        public string? Ssid { get; set; }
        public string? IpAddress { get; set; }
        public string? GatewayAddress { get; set; }
    }
}

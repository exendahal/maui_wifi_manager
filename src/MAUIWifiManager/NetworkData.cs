
namespace Plugin.MauiWifiManager.Abstractions
{
    public class NetworkData
    {
        public int StatusId { get; set; }
        public string? Ssid { get; set; }
        public int IpAddress { get; set; }
        public string? GatewayAddress { get; set; }
        public object? NativeObject { get; set; }
        public object? Bssid { get; set; }
        public object? SignalStrength { get; set; }
        public object? SecurityType { get; set; }
    }
}

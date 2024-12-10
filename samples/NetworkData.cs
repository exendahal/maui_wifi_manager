namespace DemoApp
{
    public class NetworkDataModel
    {
        public int StatusId { get; set; }
        public string? Ssid { get; set; }
        public string? SsidName { get { return string.IsNullOrWhiteSpace(Ssid) ? "Unknown" : Ssid; } }
        public int IpAddress { get; set; }
        public string? GatewayAddress { get; set; }
        public object? NativeObject { get; set; }
        public object? Bssid { get; set; }
    }
}

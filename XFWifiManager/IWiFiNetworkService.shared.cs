using System.Threading.Tasks;

namespace Plugin.XFWifiManager.Abstractions
{
    public interface IWiFiNetworkService
    {
        Task<NetworkDataModel> ConnectWifi(string ssid, string password);
        Task<NetworkDataModel> GetNetworkInfo();
        void DisconnectWifi(string ssid);
        Task<bool> OpenWifiSetting();
    }

    public class NetworkDataModel
    {
        public int StausId { get; set; }
        public string? Ssid { get; set; }
        public string? IpAddress { get; set; }
        public string? GatewayAddress { get; set; }
    }
}

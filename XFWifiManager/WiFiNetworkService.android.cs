using Plugin.XFWifiManager.Abstractions;
using System;
using System.Threading.Tasks;

namespace Plugin.XFWifiManager
{
    /// <summary>
    /// Interface for WiFiNetworkService
    /// </summary>
    public class WiFiNetworkService : IWiFiNetworkService
    {
        public Task<NetworkDataModel> ConnectWifi(string ssid, string password)
        {
            throw new NotImplementedException();
        }

        public void DisconnectWifi(string ssid)
        {
            throw new NotImplementedException();
        }

        public Task<NetworkDataModel> GetNetworkInfo()
        {
            throw new NotImplementedException();
        }

        public Task<bool> OpenWifiSetting()
        {
            throw new NotImplementedException();
        }
    }
}

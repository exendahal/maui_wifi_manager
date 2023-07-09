using Plugin.MauiWifiManager.Abstractions;
using System;
using System.Threading.Tasks;

namespace Plugin.MauiWifiManager
{
    /// <summary>
    /// Interface for WiFiNetworkService
    /// </summary>
    public class WifiNetworkService : IWifiNetworkService
    {
        public WifiNetworkService() 
        { 
        }
            
        public Task<NetworkData> ConnectWifi(string ssid, string password)
        {
            throw new NotImplementedException();
        }

        public void DisconnectWifi(string ssid)
        {
            throw new NotImplementedException();
        }       

        public Task<NetworkData> GetNetworkInfo()
        {
            throw new NotImplementedException();
        }

        public Task<bool> OpenWifiSetting()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {

        }
    }
}

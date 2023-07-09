using Plugin.MauiWifiManager.Abstractions;
using System;
using System.Threading.Tasks;

namespace Plugin.MauiWifiManager
{
    public interface IWifiNetworkService : IDisposable
    {
        Task<NetworkData> ConnectWifi(string ssid, string password);
        Task<NetworkData> GetNetworkInfo();
        void DisconnectWifi(string ssid);
        Task<bool> OpenWifiSetting();
    }    
}

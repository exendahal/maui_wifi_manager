using Plugin.MauiWifiManager.Abstractions;
using System;
using System.Threading.Tasks;

namespace Plugin.MauiWifiManager
{
    public interface IWifiNetworkService : IDisposable
    {
        Task<NetworkDataModel> ConnectWifi(string ssid, string password);
        Task<NetworkDataModel> GetNetworkInfo();
        void DisconnectWifi(string ssid);
        Task<bool> OpenWifiSetting();
    }    
}

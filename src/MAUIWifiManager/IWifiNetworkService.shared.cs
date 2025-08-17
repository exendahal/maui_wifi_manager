using MauiWifiManager.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MauiWifiManager
{
    /// <summary>
    /// Provides methods for managing Wi-Fi on the device.
    /// </summary>
    public interface IWifiNetworkService : IDisposable
    {
        /// <summary>
        /// Connects to a Wi-Fi network with the specified SSID and password.
        /// </summary>
        Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password);

        /// <summary>
        /// Retrieves details of the currently connected Wi-Fi network.
        /// </summary>
        Task<WifiManagerResponse<NetworkData>> GetNetworkInfo();

        /// <summary>
        /// Disconnects from the specified Wi-Fi network.
        /// </summary>
        void DisconnectWifi(string ssid);

        /// <summary>
        /// Opens the device's Wi-Fi settings for quick access.
        /// On iOS, this opens the app's settings instead of the Wi-Fi settings.
        /// </summary>
        Task<bool> OpenWifiSetting();

        /// <summary>
        /// Scans for available Wi-Fi networks (Android and Windows only).
        /// </summary>
        Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworks();

        /// <summary>
        /// Opens the device's wireless settings.
        /// On iOS, this opens the app's settings instead of wireless settings.
        /// </summary>
        Task<bool> OpenWirelessSetting();
    }    
}

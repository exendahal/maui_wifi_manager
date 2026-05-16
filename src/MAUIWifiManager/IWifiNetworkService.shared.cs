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
        /// Raised when the connected Wi-Fi network changes (including OS-driven changes).
        /// </summary>
        event EventHandler<WifiNetworkChangedEventArgs>? WifiNetworkChanged;

        /// <summary>
        /// Connects to a Wi-Fi network with the specified SSID and password.
        /// When provided, bssid targets a specific access point for that SSID.
        /// BSSID-targeted connection is supported on Android and Windows.
        /// On Apple platforms, the OS API does not support selecting a BSSID for connect.
        /// </summary>
        Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password, string? bssid = null);

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

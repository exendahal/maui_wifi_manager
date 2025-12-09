using MauiWifiManager.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
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
        /// <param name="ssid">The Service Set Identifier (SSID) of the Wi-Fi network.</param>
        /// <param name="password">The password for the Wi-Fi network.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the connection response with network data.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="ssid"/> is null or empty.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the <paramref name="cancellationToken"/>.</exception>
        Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves details of the currently connected Wi-Fi network.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains network information.</returns>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the <paramref name="cancellationToken"/>.</exception>
        Task<WifiManagerResponse<NetworkData>> GetNetworkInfo(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from the specified Wi-Fi network.
        /// </summary>
        /// <param name="ssid">The Service Set Identifier (SSID) of the Wi-Fi network to disconnect from.</param>
        /// <returns>A task that represents the asynchronous disconnect operation.</returns>
        Task DisconnectWifi(string ssid);

        /// <summary>
        /// Opens the device's Wi-Fi settings for quick access.
        /// On iOS, this opens the app's settings instead of the Wi-Fi settings.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if settings were opened successfully; otherwise, <c>false</c>.</returns>
        Task<bool> OpenWifiSetting();

        /// <summary>
        /// Scans for available Wi-Fi networks (Android and Windows only).
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of available networks.</returns>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the <paramref name="cancellationToken"/>.</exception>
        /// <remarks>This feature is not supported on iOS and will return an error response.</remarks>
        Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworks(CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens the device's wireless settings.
        /// On iOS, this opens the app's settings instead of wireless settings.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if settings were opened successfully; otherwise, <c>false</c>.</returns>
        Task<bool> OpenWirelessSetting();
    }    
}

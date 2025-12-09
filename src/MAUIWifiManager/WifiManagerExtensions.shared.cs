using MauiWifiManager.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MauiWifiManager
{
    /// <summary>
    /// Extension methods for Wi-Fi Manager operations.
    /// </summary>
    public static class WifiManagerExtensions
    {
        /// <summary>
        /// Connects to a Wi-Fi network with retry logic.
        /// </summary>
        /// <param name="service">The Wi-Fi network service.</param>
        /// <param name="ssid">The SSID of the network.</param>
        /// <param name="password">The password for the network.</param>
        /// <param name="retryCount">Number of retry attempts on failure.</param>
        /// <param name="retryDelayMilliseconds">Delay between retries in milliseconds.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation with the connection result.</returns>
        public static async Task<WifiManagerResponse<NetworkData>> ConnectWifiWithRetry(
            this IWifiNetworkService service,
            string ssid,
            string password,
            int retryCount = 3,
            int retryDelayMilliseconds = 1000,
            CancellationToken cancellationToken = default)
        {
            WifiManagerResponse<NetworkData>? lastResponse = null;
            
            for (int attempt = 0; attempt <= retryCount; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return WifiManagerResponse<NetworkData>.ErrorResponse(
                        WifiErrorCodes.UnknownError,
                        "Operation was cancelled.");
                }

                lastResponse = await service.ConnectWifi(ssid, password, cancellationToken);
                
                if (lastResponse.IsSuccess)
                {
                    return lastResponse;
                }

                // Don't retry for certain error codes
                if (lastResponse.ErrorCode == WifiErrorCodes.InvalidCredential ||
                    lastResponse.ErrorCode == WifiErrorCodes.PermissionDenied ||
                    lastResponse.ErrorCode == WifiErrorCodes.UnsupportedHardware)
                {
                    return lastResponse;
                }

                // Wait before retry (except on last attempt)
                if (attempt < retryCount)
                {
                    await Task.Delay(retryDelayMilliseconds, cancellationToken);
                }
            }

            return lastResponse ?? WifiManagerResponse<NetworkData>.ErrorResponse(
                WifiErrorCodes.UnknownError,
                "Connection failed after multiple attempts.");
        }

        /// <summary>
        /// Checks if currently connected to a Wi-Fi network.
        /// </summary>
        /// <param name="service">The Wi-Fi network service.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if connected to Wi-Fi, false otherwise.</returns>
        public static async Task<bool> IsConnectedToWifi(
            this IWifiNetworkService service,
            CancellationToken cancellationToken = default)
        {
            var response = await service.GetNetworkInfo(cancellationToken);
            return response.IsSuccess && !string.IsNullOrEmpty(response.Data?.Ssid);
        }

        /// <summary>
        /// Gets the SSID of the currently connected network.
        /// </summary>
        /// <param name="service">The Wi-Fi network service.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The SSID if connected, null otherwise.</returns>
        public static async Task<string?> GetCurrentSsid(
            this IWifiNetworkService service,
            CancellationToken cancellationToken = default)
        {
            var response = await service.GetNetworkInfo(cancellationToken);
            return response.IsSuccess ? response.Data?.Ssid : null;
        }

        /// <summary>
        /// Filters scanned networks by minimum signal strength.
        /// </summary>
        /// <param name="networks">List of networks to filter.</param>
        /// <param name="minimumSignalStrength">Minimum signal strength threshold (RSSI value, typically -100 to 0).</param>
        /// <returns>Filtered list of networks.</returns>
        public static List<NetworkData> FilterBySignalStrength(
            this List<NetworkData> networks,
            int minimumSignalStrength = -70)
        {
            return networks.Where(n =>
            {
                if (n.SignalStrength is int rssi)
                {
                    return rssi >= minimumSignalStrength;
                }
                return true; // Include networks where signal strength is unknown or in different format
            }).ToList();
        }

        /// <summary>
        /// Filters scanned networks by SSID pattern (case-insensitive).
        /// </summary>
        /// <param name="networks">List of networks to filter.</param>
        /// <param name="pattern">Pattern to match against SSID.</param>
        /// <returns>Filtered list of networks.</returns>
        public static List<NetworkData> FilterBySsidPattern(
            this List<NetworkData> networks,
            string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return networks;

            return networks.Where(n =>
                !string.IsNullOrEmpty(n.Ssid) &&
                n.Ssid.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Sorts networks by signal strength (strongest first).
        /// </summary>
        /// <param name="networks">List of networks to sort.</param>
        /// <returns>Sorted list of networks.</returns>
        public static List<NetworkData> SortBySignalStrength(this List<NetworkData> networks)
        {
            return networks.OrderByDescending(n =>
            {
                if (n.SignalStrength is int rssi)
                {
                    return rssi;
                }
                return int.MinValue; // Put unknown signal strength at the end
            }).ToList();
        }

        /// <summary>
        /// Removes duplicate networks with the same SSID (keeps the one with strongest signal).
        /// </summary>
        /// <param name="networks">List of networks to deduplicate.</param>
        /// <returns>Deduplicated list of networks.</returns>
        public static List<NetworkData> RemoveDuplicateSsids(this List<NetworkData> networks)
        {
            return networks
                .GroupBy(n => n.Ssid)
                .Select(g => g.OrderByDescending(n =>
                {
                    if (n.SignalStrength is int rssi)
                    {
                        return rssi;
                    }
                    return int.MinValue;
                }).First())
                .ToList();
        }
    }
}

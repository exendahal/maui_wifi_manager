using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MauiWifiManager.Abstractions;

namespace MauiWifiManager
{
#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS
    /// <summary>
    /// Reference implementation for non-platform targets (e.g. plain net9.0).
    /// Methods return an error response indicating the platform implementation is unavailable.
    /// </summary>
    public class WifiNetworkService : IWifiNetworkService
    {
        public WifiNetworkService() { }

        public Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password)
        {
            return Task.FromResult(WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, "Platform Wi-Fi implementation is not available in this build."));
        }

        public Task<WifiManagerResponse<NetworkData>> GetNetworkInfo()
        {
            return Task.FromResult(WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, "Platform Wi-Fi implementation is not available in this build."));
        }

        public void DisconnectWifi(string ssid)
        {
            // No-op on unsupported targets
        }

        public Task<bool> OpenWifiSetting()
        {
            return Task.FromResult(false);
        }

        public Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworks()
        {
            return Task.FromResult(WifiManagerResponse<List<NetworkData>>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, "Platform Wi-Fi implementation is not available in this build."));
        }

        public Task<bool> OpenWirelessSetting()
        {
            return Task.FromResult(false);
        }

        public void Dispose()
        {
            // No resources to dispose in reference implementation
        }
    }
#endif
}

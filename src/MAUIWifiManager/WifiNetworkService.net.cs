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
        public event EventHandler<WifiNetworkChangedEventArgs>? WifiNetworkChanged;

        public WifiNetworkService() { }

        [Obsolete("Use ConnectWifiAsync(string ssid, string password, CancellationToken cancellationToken = default) or ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default) instead.")]
        public Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password, string? bssid = null)
        {
            return ConnectWifiAsync(ssid, password, bssid, CancellationToken.None);
        }

        public Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, CancellationToken cancellationToken = default)
        {
            return ConnectWifiAsync(ssid, password, null, cancellationToken);
        }

        public Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<WifiManagerResponse<NetworkData>>(cancellationToken);
            }
            return Task.FromResult(WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, "Platform Wi-Fi implementation is not available in this build."));
        }

        [Obsolete("Use GetNetworkInfoAsync(CancellationToken cancellationToken = default) instead.")]
        public Task<WifiManagerResponse<NetworkData>> GetNetworkInfo()
        {
            return GetNetworkInfoAsync(CancellationToken.None);
        }

        public Task<WifiManagerResponse<NetworkData>> GetNetworkInfoAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<WifiManagerResponse<NetworkData>>(cancellationToken);
            }
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

        [Obsolete("Use ScanWifiNetworksAsync(CancellationToken cancellationToken = default) instead.")]
        public Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworks()
        {
            return ScanWifiNetworksAsync(CancellationToken.None);
        }

        public Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworksAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<WifiManagerResponse<List<NetworkData>>>(cancellationToken);
            }
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

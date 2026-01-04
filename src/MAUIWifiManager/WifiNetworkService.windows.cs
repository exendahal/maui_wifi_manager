using MauiWifiManager.Abstractions;
using MauiWifiManager.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Windows.Devices.WiFi;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Security.Credentials;
using Windows.System;

namespace MauiWifiManager
{
    /// <summary>
    /// Interface for Wi-FiNetworkService
    /// </summary>
    public class WifiNetworkService : IWifiNetworkService
    {
        public WifiNetworkService()
        {
        }

        /// <summary>
        /// Connect Wi-Fi
        /// </summary>
        public async Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password, CancellationToken cancellationToken = default)
        {
            // Validate inputs if enabled in options
            if (CrossWifiManager.Options.ValidateInputs)
            {
                try
                {
                    WifiValidationHelper.ValidateCredentials(ssid, password, CrossWifiManager.Options.AllowEmptyPassword);
                }
                catch (ArgumentException ex)
                {
                    WifiLogger.LogError($"Validation failed: {ex.Message}");
                    return WifiManagerResponse<NetworkData>.ErrorResponse(
                        WifiErrorCodes.InvalidCredential,
                        ex.Message);
                }
            }

            var response = new WifiManagerResponse<NetworkData>();
            var credential = new PasswordCredential
            {
                Password = password
            };
            WiFiAdapter adapter;
            var access = await WiFiAdapter.RequestAccessAsync();
            if (access != WiFiAccessStatus.Allowed)
            {
                WifiLogger.LogWarning("No Wi-Fi Access Status");
                response.ErrorCode = WifiErrorCodes.PermissionDenied;
                response.ErrorMessage = "No Wi-Fi Access Status.";
                return response;
            }
            
            var result = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(WiFiAdapter.GetDeviceSelector());

            if (result.Count < 1)
            {
                response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                response.ErrorMessage = "No Wi-Fi adapters found.";
                return response;
            }

            adapter = await WiFiAdapter.FromIdAsync(result[0].Id);
            if (adapter != null)
            {
                await adapter.ScanAsync();
                WiFiAvailableNetwork? wiFiAvailableNetwork = null;
                foreach (var network in adapter.NetworkReport.AvailableNetworks)
                {
                    if (network.Ssid == ssid)
                    {
                        wiFiAvailableNetwork = network;
                        break;
                    }
                }
                if (wiFiAvailableNetwork != null)
                {
                    var status = await adapter.ConnectAsync(wiFiAvailableNetwork, WiFiReconnectionKind.Automatic, credential);
                    if (status.ConnectionStatus == WiFiConnectionStatus.Success)
                    {
                        WifiLogger.LogInfo("Connected successfully to the network.");
                        Windows.Networking.Connectivity.ConnectionProfile InternetConnectionProfile = NetworkInformation.GetInternetConnectionProfile();
                        var hostname = NetworkInformation.GetHostNames().FirstOrDefault(hn => hn.IPInformation?.NetworkAdapter != null && hn.IPInformation.NetworkAdapter.NetworkAdapterId == InternetConnectionProfile?.NetworkAdapter.NetworkAdapterId);
                        var networkData = await GetNetworkInfo();
                        if (networkData.ErrorCode == WifiErrorCodes.Success)
                        {
                            response.ErrorCode = WifiErrorCodes.Success;
                            response.Data = networkData.Data;
                        }
                        else
                        {
                            WifiLogger.LogError("Failed to get network info.");
                            response.ErrorCode = WifiErrorCodes.UnknownError;
                            response.ErrorMessage = "Failed to get network info.";
                        }
                        
                    }
                    else
                    {
                        response.ErrorCode = status.ConnectionStatus switch
                        {
                            WiFiConnectionStatus.InvalidCredential => WifiErrorCodes.InvalidCredential,
                            WiFiConnectionStatus.Timeout => WifiErrorCodes.OperationTimeout,
                            _ => WifiErrorCodes.UnknownError
                        };
                        WifiLogger.LogWarning($"Connection failed: {status.ConnectionStatus}");
                        response.ErrorMessage = $"Connection failed: {status.ConnectionStatus}";
                    }
                }
                else
                {
                    WifiLogger.LogWarning("The specified network was not found.");
                    response.ErrorCode = WifiErrorCodes.NoConnection;
                    response.ErrorMessage = "The specified network was not found.";
                }
            }
            else
            {
                WifiLogger.LogError("Failed to get Wi-Fi adapter.");
                response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                response.ErrorMessage = "Failed to get Wi-Fi adapter.";
            }
            return response;
        }

        /// <summary>
        /// Disconnect Wi-Fi
        /// </summary>
        public async Task DisconnectWifi(string ssid)
        {
            WiFiAdapter adapter;
            var result = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(WiFiAdapter.GetDeviceSelector());
            if (result.Count >= 1)
            {
                adapter = await WiFiAdapter.FromIdAsync(result[0].Id);
                adapter.Disconnect();
            }
        }

        /// <summary>
        /// Get Network Info
        /// </summary>
        public Task<WifiManagerResponse<NetworkData>> GetNetworkInfo(CancellationToken cancellationToken = default)
        {
            var response = new WifiManagerResponse<NetworkData>();
            var networkData = new NetworkData();

            try
            {
                Windows.Networking.Connectivity.ConnectionProfile? profile = NetworkInformation.GetConnectionProfiles().FirstOrDefault(x => x.IsWlanConnectionProfile && x.GetNetworkConnectivityLevel() > NetworkConnectivityLevel.None);
                if (profile == null)
                {
                    response.ErrorCode = WifiErrorCodes.NoConnection;
                    response.ErrorMessage = "No active Wi-Fi network connection found.";
                    return Task.FromResult(response);
                }
                networkData.StatusId = (int)profile.GetNetworkConnectivityLevel();
                networkData.Ssid = profile.WlanConnectionProfileDetails.GetConnectedSsid();
                
                networkData.Bssid = profile.NetworkAdapter.NetworkAdapterId;
                networkData.NativeObject = profile;
                networkData.SignalStrength = profile.GetSignalBars();
                if (profile.NetworkSecuritySettings != null)
                {
                    networkData.SecurityType = GetSecurityType(profile.NetworkSecuritySettings.NetworkAuthenticationType);
                }

                var networkInterface = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && n.OperationalStatus == OperationalStatus.Up);
                if (networkInterface != null)
                {
                    var ipaddress = networkInterface.GetIPProperties();
                    var ip = ipaddress.UnicastAddresses.FirstOrDefault(n => n.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (ip != null)
                    {
                        networkData.IpAddress = BitConverter.ToInt32(ip.Address.GetAddressBytes(), 0);
                    }
                    else
                    {
                        networkData.IpAddress = 0;
                    }

                    var gatewayInfo = ipaddress.GatewayAddresses.FirstOrDefault(n => n.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (gatewayInfo != null)
                    {
                        networkData.GatewayAddress = IpAddressToInt(gatewayInfo.Address);
                    }

                    var internetworkAddress = ipaddress.DhcpServerAddresses.FirstOrDefault(n => n.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (internetworkAddress != null)
                    {
                        networkData.DhcpServerAddress = IpAddressToInt(internetworkAddress);
                    }
                }

                response.ErrorCode = WifiErrorCodes.Success;
                response.Data = networkData;
                response.ErrorMessage = "Fetched Wi-Fi connection info successfully.";
            }
            catch (Exception ex)
            {
                response.ErrorCode = WifiErrorCodes.UnknownError;
                response.ErrorMessage = $"An error occurred while retrieving network info: {ex.Message}";
            }
            return Task.FromResult(response);
        }

        /// <summary>
        /// Open Wi-Fi Setting
        /// </summary>
        public async Task<bool> OpenWifiSetting()
        {
            return await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:network-wifi"));
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {

        }

        /// <summary>
        /// Scan Wi-Fi Networks
        /// </summary>
        public async Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworks(CancellationToken cancellationToken = default)
        {
            var response = new WifiManagerResponse<List<NetworkData>>();
            try
            {
                List<NetworkData> wifiNetworks = new List<NetworkData>();

                var accessStatus = await WiFiAdapter.RequestAccessAsync();
                if (accessStatus == WiFiAccessStatus.Allowed)
                {
                    var result = await WiFiAdapter.FindAllAdaptersAsync();
                    if (result.Count > 0)
                    {
                        var wifiAdapter = result[0];
                        WifiLogger.LogInfo("Wi-Fi Scan started.");
                        await wifiAdapter.ScanAsync();
                        var availableNetworks = wifiAdapter.NetworkReport.AvailableNetworks;
                        foreach (var network in availableNetworks)
                        {
                            wifiNetworks.Add(new NetworkData
                            {
                                Ssid = network.Ssid,
                                Bssid = network.Bssid,
                                SignalStrength = network.SignalBars,
                                SecurityType = network.PhyKind
                            });
                        }
                    }
                    WifiLogger.LogInfo("Wi-Fi Scan complete.");
                    response.ErrorCode = WifiErrorCodes.Success;
                    response.ErrorMessage = $"Wi-Fi Scan complete.";
                    response.Data = wifiNetworks;
                }
                else
                {
                    WifiLogger.LogError("Request Access Async failed.");
                    response.ErrorCode = WifiErrorCodes.UnknownError;
                    response.ErrorMessage = "Request Access Async failed.";
                }
            }
            catch (Exception ex)
            {
                WifiLogger.LogError("Error while scanning Wi-Fi", ex);
                response.ErrorCode = WifiErrorCodes.UnknownError;
                response.ErrorMessage = $"Error while scanning Wi-Fi: {ex.Message}";
            }
            return response;
        }


        /// <summary>
        /// Open Network and Internet Setting
        /// </summary>
        public async Task<bool> OpenWirelessSetting()
        {
            return await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:network"));
        }

        private string GetSecurityType(NetworkAuthenticationType authType)
        {
            switch (authType)
            {
                case NetworkAuthenticationType.RsnaPsk:
                    return "WPA2-PSK";  // WPA2 Personal (Pre-Shared Key)
                case NetworkAuthenticationType.Rsna:
                    return "WPA2-Enterprise";  // WPA2 Enterprise
                case NetworkAuthenticationType.WpaPsk:
                    return "WPA-PSK";  // WPA Personal (Pre-Shared Key)
                case NetworkAuthenticationType.Wpa:
                    return "WPA-Enterprise";  // WPA Enterprise
                case NetworkAuthenticationType.Open80211:
                    return "Open (No Security)";  // Open Network
                case NetworkAuthenticationType.None:
                default:
                    return "Unknown or WPA3 (Possibly)"; // Handling missing WPA3 types
            }
        }

        private int IpAddressToInt(IPAddress? ip)
        {
            if (ip == null) return 0;

            var bytes = ip.GetAddressBytes();
            if (bytes.Length != 4) return 0;

            // Convert big-endian network order → little-endian int
            return (bytes[0] & 0xFF) |
                   ((bytes[1] & 0xFF) << 8) |
                   ((bytes[2] & 0xFF) << 16) |
                   ((bytes[3] & 0xFF) << 24);
        }
    }
}

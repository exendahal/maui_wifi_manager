using MauiWifiManager.Abstractions;
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
        public async Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password)
        {
            var response = new WifiManagerResponse<NetworkData>();
            var credential = new PasswordCredential
            {
                Password = password
            };
            WiFiAdapter adapter;
            var access = await WiFiAdapter.RequestAccessAsync();
            if (access != WiFiAccessStatus.Allowed)
            {
                Debug.WriteLine("No Wi-Fi Access Status");
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
                        Debug.WriteLine("Connected successfully to the network.");
                        ConnectionProfile InternetConnectionProfile = NetworkInformation.GetInternetConnectionProfile();
                        var hostname = NetworkInformation.GetHostNames().FirstOrDefault(hn => hn.IPInformation?.NetworkAdapter != null && hn.IPInformation.NetworkAdapter.NetworkAdapterId == InternetConnectionProfile?.NetworkAdapter.NetworkAdapterId);
                        var networkData = await GetNetworkInfo();
                        if (networkData.ErrorCode == WifiErrorCodes.Success)
                        {
                            response.ErrorCode = WifiErrorCodes.Success;
                            response.Data = networkData.Data;
                        }
                        else
                        {
                            Debug.WriteLine("Failed to get network info.");
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
                        Debug.WriteLine($"Connection failed: {status.ConnectionStatus}");
                        response.ErrorMessage = $"Connection failed: {status.ConnectionStatus}";
                    }
                }
                else
                {
                    Debug.WriteLine("The specified network was not found.");
                    response.ErrorCode = WifiErrorCodes.NoConnection;
                    response.ErrorMessage = "The specified network was not found.";
                }
            }
            else
            {
                Debug.WriteLine("Failed to get Wi-Fi adapter.");
                response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                response.ErrorMessage = "Failed to get Wi-Fi adapter.";
            }
            return response;
        }

        /// <summary>
        /// Disconnect Wi-Fi
        /// </summary>
        public async void DisconnectWifi(string ssid)
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
        public Task<WifiManagerResponse<NetworkData>> GetNetworkInfo()
        {
            var response = new WifiManagerResponse<NetworkData>();
            var networkData = new NetworkData();

            try
            {
                ConnectionProfile profile = NetworkInformation.GetInternetConnectionProfile();
                if (profile == null)
                {
                    response.ErrorCode = WifiErrorCodes.NoConnection;
                    response.ErrorMessage = "No active network connection found.";
                    return Task.FromResult(response);
                }
                networkData.StatusId = (int)profile.GetNetworkConnectivityLevel();

                if (profile.IsWlanConnectionProfile)
                {
                    networkData.Ssid = profile.WlanConnectionProfileDetails.GetConnectedSsid();
                }
                
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
                        networkData.GatewayAddress = gatewayInfo.Address.ToString();
                    }

                    var internetworkAddress = ipaddress.DhcpServerAddresses.FirstOrDefault(n => n.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (internetworkAddress != null)
                    {
                        networkData.DhcpServerAddress = internetworkAddress.ToString();
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
            return await Launcher.LaunchUriAsync(new Uri("ms-settings:network-wifi"));
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
        public async Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworks()
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
                        Debug.WriteLine($"Wi-Fi Scan started.");
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
                    Debug.WriteLine($"Wi-Fi Scan complete.");
                    response.ErrorCode = WifiErrorCodes.Success;
                    response.ErrorMessage = $"Wi-Fi Scan complete.";
                    response.Data = wifiNetworks;
                }
                else
                {
                    Debug.WriteLine($"Request Access Async failed.");
                    response.ErrorCode = WifiErrorCodes.UnknownError;
                    response.ErrorMessage = $"Request Access Async failed.";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while scanning Wi-Fi: {ex.Message}");
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
            return await Launcher.LaunchUriAsync(new Uri("ms-settings:network"));
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
    }
}

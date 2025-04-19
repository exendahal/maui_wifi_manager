using CoreLocation;
using Foundation;
using NetworkExtension;
using MauiWifiManager.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SystemConfiguration;
using UIKit;
using System.Net.NetworkInformation;
using System.Linq;

namespace MauiWifiManager
{
    /// <summary>
    /// Interface for WiFiNetworkService
    /// </summary>
    public class WifiNetworkService : IWifiNetworkService
    {
        public NEHotspotHelper hotspotHelper;

        public WifiNetworkService()
        {
            hotspotHelper = new NEHotspotHelper();
        }

        /// <summary>
        /// Connect to Wifi
        /// </summary>
        /// <param name="ssid"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public async Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password)
        {
           
            try
            {
                // Remove any existing configuration for the SSID
                NEHotspotConfigurationManager.SharedManager.RemoveConfiguration(ssid);

                // Create a new configuration for the SSID and password
                var config = new NEHotspotConfiguration(ssid, password, isWep: false)
                {
                    JoinOnce = false
                };

                var tcs = new TaskCompletionSource<NSError>();

                // Apply the configuration
                NEHotspotConfigurationManager.SharedManager.ApplyConfiguration(config, err =>
                {
                    tcs.TrySetResult(err);
                });

                // Await the result of the configuration task
                var error = await tcs.Task;

                // Handle connection status
                if (error == null)
                {
                    // Successfully connected
                    Debug.WriteLine("Successfully connected to the network.");
                    var networkData = await GetNetworkInfo();
                    return WifiManagerResponse<NetworkData>.SuccessResponse(networkData.Data, $"Successfully connected to the network.");
                }
                else if (error.LocalizedDescription == "already associated.")
                {
                    // Already connected
                    var networkData = await GetNetworkInfo();
                    return WifiManagerResponse<NetworkData>.SuccessResponse(networkData.Data, $"Already associated with the network.");
                }
                else
                {
                    // Connection failed
                    Debug.WriteLine($"Connection failed: {error.LocalizedDescription}");
                    return WifiManagerResponse<NetworkData>.ErrorResponse(
                    WifiErrorCodes.NetworkUnavailable,
                    $"Failed to connect: {error.LocalizedDescription}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error connecting to WiFi: {ex.Message}");
                return WifiManagerResponse<NetworkData>.ErrorResponse(
                WifiErrorCodes.UnknownError,
                ex.Message
             );
            }
        }

        /// <summary>
        /// Disconnect Wi-Fi
        /// </summary>
        public void DisconnectWifi(string ssid)
        {
            NEHotspotConfigurationManager.SharedManager.RemoveConfiguration(ssid);
        }

        private static void UpdateNetworkData(NetworkData networkData)
        {
            var ni = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && 
            (n.OperationalStatus == OperationalStatus.Up || n.OperationalStatus == OperationalStatus.Unknown));
            if (ni != null)
            {
                var ipp = ni.GetIPProperties();
                var ip = ipp.UnicastAddresses.FirstOrDefault(n => n.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (ip != null)
                {
                    networkData.IpAddress = BitConverter.ToInt32(ip.Address.GetAddressBytes(), 0);
                }
                else
                {
                    networkData.IpAddress = 0;
                }

                var ipg = ipp.GatewayAddresses.FirstOrDefault(n => n.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (ipg != null)
                {
                    networkData.GatewayAddress = ipg.Address.ToString();
                }

                /* Not supported on iOS
                var ipd = ipp.DhcpServerAddresses.FirstOrDefault(n => n.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (ipd != null)
                {
                    networkData.DhcpServerAddress = ipd.ToString();
                }
                */
            }
        }

        /// <summary>
        /// Get Wi-Fi Network Info
        /// </summary>
        public async Task<WifiManagerResponse<NetworkData>> GetNetworkInfo()
        {
            var response = new WifiManagerResponse<NetworkData>();
            var locationManager = new CLLocationManager();

            // Request location permissions for iOS 8+
            if (OperatingSystem.IsIOSVersionAtLeast(8))
                locationManager.RequestWhenInUseAuthorization();

            // Handle iOS 14+ using NEHotspotNetwork
            if (OperatingSystem.IsIOSVersionAtLeast(14))
            {
                var tcs = new TaskCompletionSource<WifiManagerResponse<NetworkData>>();
                if (locationManager.AuthorizationStatus == CLAuthorizationStatus.Authorized ||
                    locationManager.AuthorizationStatus == CLAuthorizationStatus.AuthorizedAlways ||
                    locationManager.AuthorizationStatus == CLAuthorizationStatus.AuthorizedWhenInUse)
                {
                    NEHotspotNetwork.FetchCurrent(hotspotNetwork =>
                    {
                        if (hotspotNetwork != null)
                        {
                            response.ErrorCode = WifiErrorCodes.Success;
                            response.ErrorMessage = "Fetched Wi-Fi connection info successfully.";
                            response.Data = new NetworkData
                            {
                                StatusId = (int)WifiErrorCodes.Success,
                                Ssid = hotspotNetwork.Ssid,
                                Bssid = hotspotNetwork.Bssid,
                                SignalStrength = hotspotNetwork.SignalStrength,
                                SecurityType = UIDevice.CurrentDevice.CheckSystemVersion(15, 0)? hotspotNetwork.SecurityType: null,
                                NativeObject = hotspotNetwork
                            };

                            UpdateNetworkData(response.Data);
                        }
                        else
                        {
                            response.ErrorCode = WifiErrorCodes.NetworkUnavailable;
                            response.ErrorMessage = "No network is currently connected.";
                        }
                        tcs.SetResult(response);
                    });

                }
                else
                {

                    response.ErrorCode = WifiErrorCodes.PermissionDenied;
                    response.ErrorMessage = "Location permissions are not granted.";
                    tcs.SetResult(response);
                }
                return await tcs.Task;
            }
            // Handle iOS versions less than 14 using CaptiveNetwork
            else
            {
                if (CaptiveNetwork.TryGetSupportedInterfaces(out string[] supportedInterfaces) == StatusCode.OK)
                {
                    if (supportedInterfaces != null)
                    {
                        foreach (var interfaceName in supportedInterfaces)
                        {
                            if (CaptiveNetwork.TryCopyCurrentNetworkInfo(interfaceName, out NSDictionary? info) == StatusCode.OK)
                            {
                                response.ErrorCode = WifiErrorCodes.Success;
                                response.Data = new NetworkData
                                {
                                    StatusId = 1,
                                    Ssid = info?[CaptiveNetwork.NetworkInfoKeySSID]?.ToString(),
                                    Bssid = info?[CaptiveNetwork.NetworkInfoKeyBSSID]?.ToString(),
                                    NativeObject = info
                                };

                                UpdateNetworkData(response.Data);

                                break; // Use the first available network
                            }
                        }
                    }
                }
                else
                {
                    response.ErrorCode = WifiErrorCodes.NetworkUnavailable;
                    response.ErrorMessage = "No network is currently connected.";
                }
            }
            return response;
        }

        /// <summary>
        /// OpenWifiSetting
        /// For iOS 8 and 9, we can navigate automatically to the settings
        /// App-Pre0fs:root=WIFI is forbidden by the app store guidelines
        /// </summary>
        public async Task<bool> OpenWifiSetting()
        {
            return await OpenSettings();
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
            var wifiNetworks = new List<NetworkData>();
            Debug.WriteLine($"ScanWifiNetworks is not supported on iOS.");
            response.ErrorCode = WifiErrorCodes.WifiNotEnabled;
            response.ErrorMessage = "ScanWifiNetworks is not supported on iOS.";
            return response;
        }

        public async Task<bool> OpenWirelessSetting()
        {
            return await OpenSettings();
        }

        private static async Task<bool> OpenSettings()
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion(8, 0))
            {
                try
                {
                    var url = new NSUrl(UIApplication.OpenSettingsUrlString);

                    if (UIApplication.SharedApplication.CanOpenUrl(url))
                    {
                        var success = await UIApplication.SharedApplication.OpenUrlAsync(url, new UIApplicationOpenUrlOptions());
                        if (!success)
                        {
                            Debug.WriteLine("Failed to open app settings.");
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Cannot open app settings URL.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error: {ex.Message}");
                    return false;
                }
            }
            else
            {
                Debug.WriteLine("OpenWirelessSetting is not supported on this version of iOS.");
                return false;
            }
        }
    }
}
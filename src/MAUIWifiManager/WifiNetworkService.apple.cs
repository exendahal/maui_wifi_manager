using CoreLocation;
using Foundation;
using NetworkExtension;
using Plugin.MauiWifiManager.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SystemConfiguration;
using UIKit;

namespace Plugin.MauiWifiManager
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
        public async Task<NetworkData> ConnectWifi(string ssid, string password)
        {
            var networkData = new NetworkData();

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
                    networkData = await GetNetworkInfo();
                }
                else if (error.LocalizedDescription == "already associated.")
                {
                    // Already connected
                    Debug.WriteLine("Already associated with the network.");
                    networkData = await GetNetworkInfo();
                }
                else
                {
                    // Connection failed
                    Debug.WriteLine($"Connection failed: {error.LocalizedDescription}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error connecting to WiFi: {ex.Message}");
            }

            return networkData;
        }

        /// <summary>
        /// Disconnect Wi-Fi
        /// </summary>
        public void DisconnectWifi(string ssid)
        {
            NEHotspotConfigurationManager.SharedManager.RemoveConfiguration(ssid);
        }

        /// <summary>
        /// Get Wi-Fi Network Info
        /// </summary>
        public async Task<NetworkData> GetNetworkInfo()
        {
            var networkData = new NetworkData();
            var locationManager = new CLLocationManager();

            // Request location permissions for iOS 8+
            if (OperatingSystem.IsIOSVersionAtLeast(8))
                locationManager.RequestWhenInUseAuthorization();

            // Handle iOS 14+ using NEHotspotNetwork
            if (OperatingSystem.IsIOSVersionAtLeast(14))
            {
                var tcs = new TaskCompletionSource<NetworkData>();
                NEHotspotNetwork.FetchCurrent(hotspotNetwork =>
                {
                    if (hotspotNetwork != null)
                    {
                        networkData.StatusId = 1;
                        networkData.Ssid = hotspotNetwork.Ssid;
                        networkData.Bssid = hotspotNetwork.Bssid;
                        networkData.SignalStrength = hotspotNetwork.SignalStrength;
                        if (UIDevice.CurrentDevice.CheckSystemVersion(15, 0))
                        {
                            networkData.SecurityType = hotspotNetwork.SecurityType;
                        }
                        networkData.NativeObject = hotspotNetwork;
                    }
                    else
                    {
                        networkData.StatusId = -1; // No network available
                    }
                    tcs.SetResult(networkData);
                });

                return await tcs.Task;
            }
            // Handle iOS versions less than 14 using CaptiveNetwork
            else
            {
                if (locationManager.AuthorizationStatus == CLAuthorizationStatus.Authorized ||
                    locationManager.AuthorizationStatus == CLAuthorizationStatus.AuthorizedAlways ||
                    locationManager.AuthorizationStatus == CLAuthorizationStatus.AuthorizedWhenInUse)
                {
                    if (CaptiveNetwork.TryGetSupportedInterfaces(out string[] supportedInterfaces) == StatusCode.OK)
                    {
                        if (supportedInterfaces != null)
                        {
                            foreach (var interfaceName in supportedInterfaces)
                            {
                                if (CaptiveNetwork.TryCopyCurrentNetworkInfo(interfaceName, out NSDictionary? info) == StatusCode.OK)
                                {
                                    networkData = new NetworkData
                                    {
                                        StatusId = 1,
                                        Ssid = info?[CaptiveNetwork.NetworkInfoKeySSID]?.ToString(),
                                        Bssid = info?[CaptiveNetwork.NetworkInfoKeyBSSID]?.ToString(),
                                        NativeObject = info
                                    };
                                    break; // Use the first available network
                                }
                            }
                        }
                    }
                    else
                    {
                        networkData.StatusId = -1; // No network available
                    }
                }
                else
                {
                    networkData.StatusId = -2; // Location permissions not granted
                }
            }

            return networkData;
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
        public async Task<List<NetworkData>> ScanWifiNetworks()
        {
            var wifiNetworks = new List<NetworkData>();
            // ScanWifiNetworks is not supported on iOS
            return await Task.FromResult(wifiNetworks);
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
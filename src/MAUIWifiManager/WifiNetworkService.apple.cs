using CoreLocation;
using Foundation;
using NetworkExtension;
using Plugin.MauiWifiManager.Abstractions;
using System;
using System.Collections.Generic;
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
                    Console.WriteLine("Successfully connected to the network.");
                    networkData = await GetNetworkInfo();
                }
                else if (error.LocalizedDescription == "already associated.")
                {
                    // Already connected
                    Console.WriteLine("Already associated with the network.");
                    networkData = await GetNetworkInfo();
                }
                else
                {
                    // Connection failed
                    Console.WriteLine($"Connection failed: {error.LocalizedDescription}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to WiFi: {ex.Message}");
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
        public Task<NetworkData> GetNetworkInfo()
        {
            NetworkData networkData = new NetworkData();
            var manager = new CLLocationManager();

            // Request location permissions if iOS version is 8.0+
            if (OperatingSystem.IsIOSVersionAtLeast(8))
                manager.RequestWhenInUseAuthorization();

            if (OperatingSystem.IsIOSVersionAtLeast(14))
            {
                if (manager.AuthorizationStatus == CLAuthorizationStatus.Authorized ||
                    manager.AuthorizationStatus == CLAuthorizationStatus.AuthorizedAlways ||
                    manager.AuthorizationStatus == CLAuthorizationStatus.AuthorizedWhenInUse)
                {
                    // Use NEHotspotNetwork for iOS 14.0+                  
                    NEHotspotNetwork.FetchCurrent(hotspotNetwork =>
                    {
                        if (hotspotNetwork != null && !string.IsNullOrEmpty(hotspotNetwork.Ssid))
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
                    });
                }

            }
            else
            {
                if (CLLocationManager.Status is CLAuthorizationStatus.AuthorizedAlways ||
                CLLocationManager.Status is CLAuthorizationStatus.AuthorizedWhenInUse)
                {
                    if (CaptiveNetwork.TryGetSupportedInterfaces(out string[] supportedInterfaces) == StatusCode.OK)
                    {
                        if (supportedInterfaces != null)
                        {
                            foreach (var item in supportedInterfaces)
                            {
                                if (CaptiveNetwork.TryCopyCurrentNetworkInfo(item, out NSDictionary? info) == StatusCode.OK)
                                {
                                    networkData.StatusId = 1;
                                    networkData.Ssid = info?[CaptiveNetwork.NetworkInfoKeySSID]?.ToString();
                                    networkData.Bssid = info?[CaptiveNetwork.NetworkInfoKeyBSSID]?.ToString();
                                    networkData.NativeObject = info;
                                    break; // Get the first available network
                                }
                            }
                        }
                    }
                }

            }
            return Task.FromResult(networkData);
        }

        /// <summary>
        /// OpenWifiSetting
        /// For iOS 8 and 9, we can navigate automatically to the settings
        /// App-Pre0fs:root=WIFI is forbidden by the app store guidelines
        /// </summary>
        public Task<bool> OpenWifiSetting()
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion(8, 0))
            {
                NSUrl url = new NSUrl(UIApplication.OpenSettingsUrlString);
                return Task.FromResult(UIApplication.SharedApplication.OpenUrl(url));
            }
            return Task.FromResult(false);
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
            Console.WriteLine($"ScanWifiNetworks is not supported on iOS");
            return await Task.FromResult(new List<NetworkData>());
        }

        public Task<bool> OpenWirelessSetting()
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion(8, 0))
            {
                NSUrl url = new NSUrl(UIApplication.OpenSettingsUrlString);
                return Task.FromResult(UIApplication.SharedApplication.OpenUrl(url));
            }
            return Task.FromResult(false);
        }

        public async Task<NetworkData> StartLocalHotspot()
        {
            Console.WriteLine($"StartLocalHotspot is not supported on iOS");
            return await Task.FromResult(new NetworkData());
        }

        public async Task<bool> StopLocalHotspot()
        {
            Console.WriteLine($"StopLocalHotspot is not supported on iOS");
            return await Task.FromResult(false);
        }
    }
}

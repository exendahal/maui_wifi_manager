using CoreLocation;
using Foundation;
using NetworkExtension;
using ObjCRuntime;
using Plugin.MauiWifiManager.Abstractions;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        public WifiNetworkService()
        {
        }
        public async Task<NetworkData> ConnectWifi(string ssid, string password)
        {
            NetworkData networkData = new NetworkData();
            NEHotspotConfigurationManager.SharedManager.RemoveConfiguration(ssid);
            var config = new NEHotspotConfiguration(ssid, password, isWep: false);
            config.JoinOnce = false;
            var tcs = new TaskCompletionSource<NSError>();
            NEHotspotConfigurationManager.SharedManager.ApplyConfiguration(config, err => tcs.TrySetResult(err));
            var error = await tcs.Task;

            if (error != null)
            {
                if (error?.LocalizedDescription == "already associated.")
                {
                    
                }
                else
                {
                    Console.WriteLine("Not Connected");
                }
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
            if (UIDevice.CurrentDevice.CheckSystemVersion(8, 0))
                manager.RequestWhenInUseAuthorization();

            if (CLLocationManager.Status is CLAuthorizationStatus.Authorized ||
                    CLLocationManager.Status is CLAuthorizationStatus.AuthorizedAlways ||
                    CLLocationManager.Status is CLAuthorizationStatus.AuthorizedWhenInUse)
            {
                if (CaptiveNetwork.TryGetSupportedInterfaces(out string[] supportedInterfaces) == StatusCode.OK)
                {
                    foreach (var item in supportedInterfaces)
                    {
                        if (CaptiveNetwork.TryCopyCurrentNetworkInfo(item, out NSDictionary? info) == StatusCode.OK)
                        {
                            networkData.Ssid = info?[CaptiveNetwork.NetworkInfoKeySSID].ToString();                            
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
            List<NetworkData> wifiNetworks = new List<NetworkData>();
            // Check if Wi-Fi is available on the device
            if (Reachability.LocalWifiConnectionStatus() == NetworkStatus.NotReachable)
            {
                Console.WriteLine("Wi-Fi is not reachable.");
                return wifiNetworks;
            }

            // Get the list of available networks
            var interfaces = CNCopySupportedInterfaces();
            if (interfaces != null)
            {
                var interfaceArray = NSArray.ArrayFromHandle<NSString>(interfaces);
                foreach (var interfaceName in interfaceArray)
                {
                    NSDictionary info;
                    var status = CaptiveNetwork.TryCopyCurrentNetworkInfo(interfaceName, out info);
                    if (status != StatusCode.OK)
                    {
                        continue;
                    }
                    var ssid = info[CaptiveNetwork.NetworkInfoKeySSID].ToString();
                    var bssid = info[CaptiveNetwork.NetworkInfoKeyBSSID].ToString();
                    if (ssid != null && bssid != null)
                    {
                        wifiNetworks.Add(new NetworkData { Ssid = ssid.ToString(), Bssid = bssid.ToString() });
                    }
                }
            }
            return wifiNetworks;
        }
        // Import CoreFoundation
        [DllImport(Constants.CoreFoundationLibrary)]
        extern static IntPtr CNCopySupportedInterfaces();

        [DllImport(Constants.SystemConfigurationLibrary)]
        extern static IntPtr CNCopyCurrentNetworkInfo(IntPtr interfaceName);

        // Reachability class to check Wi-Fi connection status
        public class Reachability
        {
            public static NetworkStatus LocalWifiConnectionStatus()
            {
                NetworkReachabilityFlags flags;
                bool defaultNetworkAvailable = IsNetworkAvailable(out flags);
                if (defaultNetworkAvailable && ((flags & NetworkReachabilityFlags.IsDirect) != 0))
                    return NetworkStatus.NotReachable;
                else if ((flags & NetworkReachabilityFlags.IsWWAN) != 0)
                    return NetworkStatus.ReachableViaCarrierDataNetwork;
                else if (flags == 0)
                    return NetworkStatus.NotReachable;
                return NetworkStatus.ReachableViaWiFiNetwork;
            }

            static bool IsNetworkAvailable(out NetworkReachabilityFlags flags)
            {
                using (var h = new NetworkReachability("www.apple.com"))
                {
                    if (h == null)
                    {
                        flags = 0;
                        return false;
                    }

                    return h.TryGetFlags(out flags) && IsReachableWithoutRequiringConnection(flags);
                }
            }

            static bool IsReachableWithoutRequiringConnection(NetworkReachabilityFlags flags)
            {
                return (flags & NetworkReachabilityFlags.Reachable) != 0;
            }
        }

        // Enum for network status
        public enum NetworkStatus
        {
            NotReachable,
            ReachableViaCarrierDataNetwork,
            ReachableViaWiFiNetwork
        }
    }
}

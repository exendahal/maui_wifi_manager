using CoreFoundation;
using CoreLocation;
using Foundation;
using NetworkExtension;
using Plugin.MauiWifiManager.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
                    networkData = await GetNetworkInfo();
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
            var networkData = new NetworkData();

            if (UIDevice.CurrentDevice.CheckSystemVersion(14, 0))
            {
                // Request current Wi-Fi networks using NEHotspotNetwork
                var hotspotHelperOptions = new NEHotspotHelperOptions();
                var configuration = new NSDictionary();

                NEHotspotHelper.Register(hotspotHelperOptions, DispatchQueue.MainQueue, (cmd) =>
                {
                    var networks = cmd.NetworkList;
                    if (networks != null)
                    {
                        foreach (var network in networks)
                        {
                            networkData.StausId = 1;
                            networkData.Ssid = network.Ssid;
                            networkData.Bssid = network.Bssid;
                            networkData.SignalStrength = network.SignalStrength;
                            networkData.NativeObject = network;  // Store the native object for further use.
                        }
                    }
                });
            }
            else
            {
                // Fallback for older iOS versions
                if (CaptiveNetwork.TryGetSupportedInterfaces(out string[] supportedInterfaces) == StatusCode.OK)
                {
                    foreach (var item in supportedInterfaces)
                    {
                        if (CaptiveNetwork.TryCopyCurrentNetworkInfo(item, out NSDictionary? info) == StatusCode.OK)
                        {
                            networkData.StausId = 1;
                            networkData.Ssid = info?[CaptiveNetwork.NetworkInfoKeySSID].ToString();
                            networkData.Bssid = info?[CaptiveNetwork.NetworkInfoKeyBSSID].ToString();
                            networkData.NativeObject = info;
                        }
                    }
                }
            }

            // Get device's IP address
            IPAddress[] ipAddresses = Dns.GetHostAddresses(Dns.GetHostName());
            IPAddress ipAddress = ipAddresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            if (ipAddress != null)
            {
                networkData.IpAddress = BitConverter.ToInt32(ipAddress.GetAddressBytes(), 0);
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
            return wifiNetworks;
        }

    }
}

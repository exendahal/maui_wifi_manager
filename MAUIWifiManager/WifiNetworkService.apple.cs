using CoreFoundation;
using CoreLocation;
using Foundation;
using NetworkExtension;
using Plugin.MauiWifiManager.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
                            networkData.Bssid = info?[CaptiveNetwork.NetworkInfoKeyBSSID].ToString();
                            networkData.NativeObject = info;
                        }
                    }
                    IPAddress[] ipAddresses = Dns.GetHostAddresses(Dns.GetHostName());
                    IPAddress ipAddress = ipAddresses.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    networkData.IpAddress = BitConverter.ToInt32(ipAddress.GetAddressBytes(), 0);

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

            var queue = new DispatchQueue("com.BajraTech.MauiWifiManager");
            var options = new NEHotspotHelperOptions { DisplayName = (NSString)"Maui Wifi Manager" };
            var handler = new NEHotspotHelperHandler(async (cmd) =>
            {
                if (cmd.CommandType == NEHotspotHelperCommandType.FilterScanList)
                {
                    foreach (var network in cmd.NetworkList)
                    {
                        wifiNetworks.Add(new NetworkData
                        {
                            Ssid = network.Ssid,
                            Bssid = network.Bssid,
                            SignalStrength = network.SignalStrength
                        });
                    }
                    await Task.Delay(1000); // wait for the scan to complete
                    var response = cmd.CreateResponse(NEHotspotHelperResult.Success);
                    response.SetNetworkList(cmd.NetworkList);
                    response.Deliver();
                }
                else if (cmd.CommandType == NEHotspotHelperCommandType.Evaluate)
                {
                    // Evaluate the network and set the confidence level
                    var network = cmd.Network;
                    network.SetConfidence(NEHotspotHelperConfidence.High);
                    var response = cmd.CreateResponse(NEHotspotHelperResult.Success);
                    response.SetNetwork(network);
                    response.Deliver();
                }
                else if (cmd.CommandType == NEHotspotHelperCommandType.Authenticate)
                {
                    // Perform custom authentication and deliver the result
                    var response = cmd.CreateResponse(NEHotspotHelperResult.Success);
                    response.Deliver();
                }
            });

            var success = NEHotspotHelper.Register(options, queue, handler);
            if (success)
            {
                Console.WriteLine("Registered successfully");
            }
            else
            {
                Console.WriteLine("Registration failed");
            }

            return wifiNetworks;
        }

    }
}

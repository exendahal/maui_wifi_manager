using Plugin.MauiWifiManager.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Windows.Devices.WiFi;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;
using Windows.Security.Credentials;
using Windows.System;

namespace Plugin.MauiWifiManager
{
    /// <summary>
    /// Interface for Wi-FiNetworkService
    /// </summary>
    public class WifiNetworkService : IWifiNetworkService
    {
        private NetworkOperatorTetheringManager _networkOperatorTetheringManager;
        public WifiNetworkService()
        {
        }

        /// <summary>
        /// Connect Wi-Fi
        /// </summary>
        public async Task<NetworkData> ConnectWifi(string ssid, string password)
        {
            NetworkData networkData = new NetworkData();
            var credential = new PasswordCredential();
            credential.Password = password;
            WiFiAdapter adapter;
            var access = await WiFiAdapter.RequestAccessAsync();
            if (access != WiFiAccessStatus.Allowed)
            {
                Console.WriteLine("No Wi-Fi Access Status");
            }
            else
            {
                var result = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(WiFiAdapter.GetDeviceSelector());
                if (result.Count >= 1)
                {
                    adapter = await WiFiAdapter.FromIdAsync(result[0].Id);
                    if (adapter != null)
                    {
                        await adapter.ScanAsync();
                        WiFiAvailableNetwork wiFiAvailableNetwork = null;
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
                                ConnectionProfile InternetConnectionProfile = NetworkInformation.GetInternetConnectionProfile();
                                var hostname = NetworkInformation.GetHostNames().FirstOrDefault(hn => hn.IPInformation?.NetworkAdapter != null && hn.IPInformation.NetworkAdapter.NetworkAdapterId == InternetConnectionProfile?.NetworkAdapter.NetworkAdapterId);
                                networkData.Ssid = hostname.ToString();
                                Console.WriteLine("OK");
                            }
                            else if (status.ConnectionStatus == WiFiConnectionStatus.InvalidCredential)
                            {
                                Console.WriteLine("Invalid Credential");
                            }
                            else if (status.ConnectionStatus == WiFiConnectionStatus.Timeout)
                            {
                                Console.WriteLine("Timeout");
                            }
                        }
                    }
                }
            }
            return networkData;
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
        public Task<NetworkData> GetNetworkInfo()
        {
            NetworkData data = new NetworkData();
            ConnectionProfile profile = NetworkInformation.GetInternetConnectionProfile();
            data.StatusId = (int)profile.GetNetworkConnectivityLevel();
            if (profile.IsWlanConnectionProfile)
            {
                data.Ssid = profile.WlanConnectionProfileDetails.GetConnectedSsid();
            }
            var hostNames = NetworkInformation.GetHostNames();
            var ipAddressString = hostNames.FirstOrDefault(h => h.Type == HostNameType.Ipv4)?.DisplayName;
            IPAddress ipAddress = IPAddress.Parse(ipAddressString);
            data.IpAddress = BitConverter.ToInt32(ipAddress.GetAddressBytes(), 0);
            data.Bssid = profile.NetworkAdapter.NetworkAdapterId;
            data.NativeObject = profile;
            data.SignalStrength = profile.GetSignalBars();           
            return Task.FromResult(data);
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
        public async Task<List<NetworkData>> ScanWifiNetworks()
        {
            List<NetworkData> wifiNetworks = new List<NetworkData>();

            var accessStatus = await WiFiAdapter.RequestAccessAsync();
            if (accessStatus == WiFiAccessStatus.Allowed)
            {
                var result = await WiFiAdapter.FindAllAdaptersAsync();
                if (result.Count > 0)
                {
                    var wifiAdapter = result[0];
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
            }
            return wifiNetworks;
        }


        /// <summary>
        /// Open Network and Internet Setting
        /// </summary>
        public async Task<bool> OpenWirelessSetting()
        {
            return await Launcher.LaunchUriAsync(new Uri("ms-settings:network"));
        }

        public async Task<NetworkData> StartLocalHotspot()
        {
            var networkData = new NetworkData();
            try
            {
                _networkOperatorTetheringManager = TryGetTetheringManager();

                if (_networkOperatorTetheringManager != null)
                {
                    bool isTethered = _networkOperatorTetheringManager.TetheringOperationalState == TetheringOperationalState.On;
                    if (isTethered)
                    {
                        NetworkOperatorTetheringAccessPointConfiguration configuration = _networkOperatorTetheringManager.GetCurrentAccessPointConfiguration();
                        networkData.Ssid = configuration.Ssid;
                        networkData.Password = configuration.Passphrase;
                    }
                    else
                    {
                        var result = await _networkOperatorTetheringManager.StartTetheringAsync();
                        if (result.Status == TetheringOperationStatus.Success)
                        {
                            NetworkOperatorTetheringAccessPointConfiguration configuration = _networkOperatorTetheringManager.GetCurrentAccessPointConfiguration();
                            networkData.Ssid = configuration.Ssid;
                            networkData.Password = configuration.Passphrase;
                        }
                        else
                        {
                            switch (result.Status)
                            {
                                case TetheringOperationStatus.WiFiDeviceOff:
                                    Console.WriteLine("Wi-Fi adapter is either turned off or missing.");
                                    break;
                                default:
                                    Console.WriteLine($"Failed to start tethering: {result.Status}");
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to start tethering");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Failed to start tethering: {ex.Message}");
            }
            return networkData;
        }

        public async Task<bool> StopLocalHotspot()
        {
            try
            {
                bool isTethered = _networkOperatorTetheringManager.TetheringOperationalState == TetheringOperationalState.On;
                if (isTethered)
                {
                    var result = await _networkOperatorTetheringManager.StopTetheringAsync();
                    if (result.Status == TetheringOperationStatus.Success)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine($"No active hostpot.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping hotspot: {ex.Message}");                
            }
            return false;
        }
        NetworkOperatorTetheringManager TryGetTetheringManager()
        {
            ConnectionProfile internetConnectionProfile = NetworkInformation.GetInternetConnectionProfile();
            if (internetConnectionProfile != null)
            {
                Console.WriteLine($"Internet connection profile found: {internetConnectionProfile.ProfileName}");
                TetheringCapability tetheringCapability =
                    NetworkOperatorTetheringManager.GetTetheringCapabilityFromConnectionProfile(internetConnectionProfile);

                HandleTetheringCapability(tetheringCapability, internetConnectionProfile.ProfileName);

                if (tetheringCapability == TetheringCapability.Enabled)
                {
                    try
                    {
                        return NetworkOperatorTetheringManager.CreateFromConnectionProfile(internetConnectionProfile);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to create TetheringManager for internet profile: {ex.Message}");
                    }
                }
            }
            else
            {
                Console.WriteLine("No internet connection profile found.");
            }
            var connectionProfiles = NetworkInformation.GetConnectionProfiles();
            foreach (var profile in connectionProfiles)
            {
                TetheringCapability tetheringCapability =
                    NetworkOperatorTetheringManager.GetTetheringCapabilityFromConnectionProfile(profile);

                HandleTetheringCapability(tetheringCapability, profile.ProfileName);

                if (tetheringCapability == TetheringCapability.Enabled)
                {
                    try
                    {
                        return NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to create TetheringManager for profile {profile.ProfileName}: {ex.Message}");
                    }
                }
            }

            Console.WriteLine("No tethering-enabled profiles found.");
            return null;
        }
        void HandleTetheringCapability(TetheringCapability tetheringCapability, string profileName)
        {
            string message;
            switch (tetheringCapability)
            {
                case TetheringCapability.Enabled:
                    message = $"Tethering is enabled for profile: {profileName}.";
                    break;
                case TetheringCapability.DisabledByGroupPolicy:
                    message = $"Tethering is disabled for profile '{profileName}' due to group policy.";
                    break;
                case TetheringCapability.DisabledByHardwareLimitation:
                    message = $"Tethering is not available for profile '{profileName}' due to hardware limitations.";
                    break;
                case TetheringCapability.DisabledByOperator:
                    message = $"Tethering is disabled for profile '{profileName}' by the network operator.";
                    break;
                case TetheringCapability.DisabledByRequiredAppNotInstalled:
                    message = $"Tethering is disabled for profile '{profileName}' because a required application is not installed.";
                    break;
                case TetheringCapability.DisabledBySku:
                    message = $"Tethering is not supported by the account services for profile '{profileName}'.";
                    break;
                case TetheringCapability.DisabledBySystemCapability:
                    message = $"Tethering is disabled for profile '{profileName}' due to missing system capabilities.";
                    break;
                default:
                    message = $"Tethering is disabled for profile '{profileName}'. (Code: {(int)tetheringCapability})";
                    break;
            }
            Console.WriteLine(message);
        }

    }
}

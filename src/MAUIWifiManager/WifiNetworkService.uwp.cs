using Plugin.MauiWifiManager.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Windows.Devices.WiFi;
using Windows.Devices.WiFiDirect;
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
                _networkOperatorTetheringManager = TryGetCurrentNetworkOperatorTetheringManager();

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
        NetworkOperatorTetheringManager TryGetCurrentNetworkOperatorTetheringManager()
        {
            ConnectionProfile currentConnectionProfile = NetworkInformation.GetInternetConnectionProfile();
            if (currentConnectionProfile == null)
            {
                Console.WriteLine("System is not connected to the Internet.");
            }
            TetheringCapability tetheringCapability =
              NetworkOperatorTetheringManager.GetTetheringCapabilityFromConnectionProfile(currentConnectionProfile);
            if (tetheringCapability != TetheringCapability.Enabled)
            {
                string message;
                switch (tetheringCapability)
                {
                    case TetheringCapability.DisabledByGroupPolicy:
                        message = "Tethering is disabled due to group policy.";
                        break;
                    case TetheringCapability.DisabledByHardwareLimitation:
                        message = "Tethering is not available due to hardware limitations.";
                        break;
                    case TetheringCapability.DisabledByOperator:
                        message = "Tethering operations are disabled for this account by the network operator.";
                        break;
                    case TetheringCapability.DisabledByRequiredAppNotInstalled:
                        message = "An application required for tethering operations is not available.";
                        break;
                    case TetheringCapability.DisabledBySku:
                        message = "Tethering is not supported by the current account services.";
                        break;
                    case TetheringCapability.DisabledBySystemCapability:
                        // This will occur if the "wiFiControl" capability is missing from the App.
                        message = "This app is not configured to access Wi-Fi devices on this machine.";
                        break;
                    default:
                        message = $"Tethering is disabled on this machine. (Code {(int)tetheringCapability}).";
                        break;
                }
                Console.WriteLine(message);
            }
            const int E_NOT_FOUND = unchecked((int)0x80070490);

            try
            {
                return NetworkOperatorTetheringManager.CreateFromConnectionProfile(currentConnectionProfile);
            }
            catch (Exception ex) when (ex.HResult == E_NOT_FOUND)
            {
                Console.WriteLine("System has no Wi-Fi adapters.");
                return null;
            }
        }
    }
}

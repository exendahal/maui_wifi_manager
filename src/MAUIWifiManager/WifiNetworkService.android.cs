using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Runtime;
using MauiWifiManager.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Android.Provider.Settings;
using Context = Android.Content.Context;

namespace MauiWifiManager
{
    /// <summary>
    /// Interface for WiFiNetworkService
    /// </summary>
    /// 
    public class WifiNetworkService : IWifiNetworkService
    {
        private static Context _context = null!;
        private static ConnectivityManager? _connectivityManager;
        private static bool _requested; 
        public WifiNetworkService() 
        {
            
        }
        public static void CheckInit(Context context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(_context), "Please call WifiNetworkService.Init(this) inside the MainActivity's OnCreate function.");
        }
        public static void Init(Context context)
        {
            if (context != null)
            {
                CheckInit(context);
                _context = context;     
            }
            else
                throw new NullReferenceException("Context is null. Initialization cannot proceed.");

        }

        /// <summary>
        /// Connect Wi-Fi
        /// </summary>
        public async Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password)
        {
            var response = new WifiManagerResponse<NetworkData>();
            var networkData = new NetworkData();

            var wifiManager = _context.GetSystemService(Context.WifiService) as WifiManager;

            if (wifiManager == null)
            {
                System.Diagnostics.Debug.WriteLine($"Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.");
                response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                response.ErrorMessage = "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.";
                return response;
            }

            try
            {
                // Check Android version for different connection methods
                if (!OperatingSystem.IsAndroidVersionAtLeast(29))
                {
                    // Android version is less than 29 (Android 10)
                    if (!wifiManager.IsWifiEnabled)
                    {
                        wifiManager.SetWifiEnabled(true); // Enable Wi-Fi if not already enabled
                    }
                    string wifiSsid = wifiManager.ConnectionInfo?.SSID?.ToString() ?? string.Empty;
                    if (wifiSsid != string.Format("\"{0}\"", ssid))
                    {
                        System.Diagnostics.Debug.WriteLine("Wi-Fi connection initiated successfully.");
                        WifiConfiguration wifiConfig = new WifiConfiguration
                        {
                            Ssid = string.Format("\"{0}\"", ssid),
                            PreSharedKey = string.Format("\"{0}\"", password)
                        };
                        int netId = wifiManager.AddNetwork(wifiConfig);
                        wifiManager.Disconnect();
                        wifiManager.EnableNetwork(netId, true);
                        wifiManager.Reconnect();
                        networkData.Ssid = wifiConfig.Ssid;
                        networkData.StatusId = (int)WifiErrorCodes.Success;                        
                        response.ErrorCode = WifiErrorCodes.Success;
                        response.ErrorMessage = "Wi-Fi connection initiated successfully.";
                        response.Data = networkData;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Cannot find a valid SSID to connect.");
                        response.ErrorCode = WifiErrorCodes.NoConnection;
                        response.ErrorMessage = "Cannot find a valid SSID to connect.";
                        response.Data = networkData;
                    }
                }               
                else if (OperatingSystem.IsAndroidVersionAtLeast(29) && !OperatingSystem.IsAndroidVersionAtLeast(30))
                {
                    //Android version is 29(Android 10)
                    response = await RequestNetwork(wifiManager, ssid, password);
                }
                else
                {
                    //Android version is greater than 29(Android 10)
                    response = await AddWifiSuggestion(wifiManager, ssid, password);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error connecting to Wi-Fi: {ex.Message}");
                response.ErrorCode = WifiErrorCodes.UnknownError;
                response.ErrorMessage = $"Error connecting to Wi-Fi: {ex.Message}";
                response.Data = networkData;
            }
            return response;
           
        }

        /// <summary>
        /// Disconnect Wi-Fi
        /// From Android Q (Android 10) you can't enable/disable Wi-Fi programmatically anymore. 
        /// So, use Settings Panel to toggle Wi-Fi connectivity
        /// </summary>
        public void DisconnectWifi(string? ssid)
        {
            if (_context != null)
            {
                CheckInit(_context);
                if (OperatingSystem.IsAndroidVersionAtLeast(29))
                {
                    Intent panelIntent = new Intent(Panel.ActionWifi);
                    _context.StartActivity(panelIntent);
                }
                else
                {
                    var wifiManager = _context.GetSystemService(Context.WifiService) as WifiManager;
                    if (wifiManager != null)
                    {
                        wifiManager.SetWifiEnabled(false); // Disable Wi-Fi
                        wifiManager.SetWifiEnabled(true); // Enable Wi-Fi
                    }
                    else
                        throw new NullReferenceException("wifiManager is null.");
                }
            }
            else
                throw new NullReferenceException("Context is null. Disconnect Wi-Fi cannot proceed.");

        }

        /// <summary>
        /// Get Wi-Fi Network Info
        /// </summary>
        public async Task<WifiManagerResponse<NetworkData>> GetNetworkInfo()
        {
            var response = new WifiManagerResponse<NetworkData>();
            var networkData = new NetworkData();
            if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                var wifiManager = _context.GetSystemService(Context.WifiService) as WifiManager;

                if (wifiManager == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.");
                    response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                    response.ErrorMessage = "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.";
                    return response;
                }
                if (!wifiManager.IsWifiEnabled)
                {
                    System.Diagnostics.Debug.WriteLine("Wi-Fi is turned off. Please enable Wi-Fi to proceed.");
                    response.ErrorCode = WifiErrorCodes.WifiNotEnabled;
                    response.ErrorMessage = "Wi-Fi is turned off. Please enable Wi-Fi to proceed.";
                    return response;
                }
                if (wifiManager.ConnectionInfo == null )
                {
                    System.Diagnostics.Debug.WriteLine("Invalid ConnectionInfo.");
                    response.ErrorCode = WifiErrorCodes.NoConnection;
                    response.ErrorMessage = "Invalid ConnectionInfo.";
                    return response;
                }

                if (wifiManager.ConnectionInfo.SupplicantState == SupplicantState.Completed)
                {
                    System.Diagnostics.Debug.WriteLine($"Fetched Wi-Fi connection info successfully.");
                    networkData.StatusId = (int)WifiErrorCodes.Success;
                    networkData.Ssid = wifiManager.ConnectionInfo?.SSID?.Trim(new char[] { '"', '\"' });
                    networkData.Bssid = wifiManager.ConnectionInfo?.BSSID;
                    networkData.SignalStrength = wifiManager.ConnectionInfo?.Rssi;
                    networkData.IpAddress = wifiManager.DhcpInfo?.IpAddress ?? 0;
                    networkData.GatewayAddress = wifiManager.DhcpInfo?.Gateway.ToString();
                    networkData.NativeObject = wifiManager.ConnectionInfo;

                    response.ErrorCode = WifiErrorCodes.Success;
                    response.ErrorMessage = "Fetched Wi-Fi connection info successfully.";
                    response.Data = networkData;
                    return response;
                }
               
            }
            else
            {
                // For Android 12+ (API level 31 or higher), use connectivity manager to fetch network details
                TaskCompletionSource<NetworkData> tcs = new TaskCompletionSource<NetworkData>();
                ConnectivityManager? connectivityManager = _context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
                if (connectivityManager == null)
                {
                    System.Diagnostics.Debug.WriteLine("Connectivity service is not available on this device.");
                    response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                    response.ErrorMessage = "Connectivity service is not available on this device.";
                    return response;
                }

                NetworkInfo activeNetworkInfo = connectivityManager.ActiveNetworkInfo;
                if (activeNetworkInfo == null)
                {
                    System.Diagnostics.Debug.WriteLine("No active network info.");
                    response.ErrorCode = WifiErrorCodes.NetworkUnavailable;
                    response.ErrorMessage = "No active network info.";
                    return response;

                }

                NetworkCallbackFlags flagIncludeLocationInfo = NetworkCallbackFlags.IncludeLocationInfo;
                NetworkCallback networkCallback = new NetworkCallback((int)flagIncludeLocationInfo)
                {
                    OnNetworkCapabilitiesChanged = (network, networkCapabilities) =>
                    {
                        WifiInfo wifiInfo = (WifiInfo)networkCapabilities.TransportInfo;

                        if (wifiInfo != null && wifiInfo.SupplicantState == SupplicantState.Completed)
                        {
                            networkData.StatusId = 1;
                            networkData.Ssid = wifiInfo?.SSID?.Trim(new char[] { '"', '\"' });
                            networkData.Bssid = wifiInfo?.BSSID;
                            networkData.IpAddress = wifiInfo?.IpAddress ?? 0;
                            networkData.NativeObject = wifiInfo;
                            networkData.SignalStrength = wifiInfo?.Rssi;
                            tcs.TrySetResult(networkData);
                        }
                    },
                    NetworkUnavailable = () =>
                    {
                        tcs.TrySetResult(new NetworkData());
                    }
                };
                var request = new NetworkRequest.Builder().AddTransportType(transportType: TransportType.Wifi).Build();
                connectivityManager.RequestNetwork(request, networkCallback);
                connectivityManager.RegisterNetworkCallback(request, networkCallback);
               
                networkData = await tcs.Task;
                if (networkData != null && networkData.StatusId == 1)
                {
                    System.Diagnostics.Debug.WriteLine($"Fetched Wi-Fi connection info successfully.");
                    response.ErrorCode = WifiErrorCodes.Success;
                    response.ErrorMessage = "Fetched Wi-Fi connection info successfully.";
                    response.Data = networkData;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to fetch Wi-Fi connection info.");
                    response.ErrorCode = WifiErrorCodes.UnknownError;
                    response.ErrorMessage = "Failed to fetch Wi-Fi connection info.";
                }

            }           
            return response;
        }

        /// <summary>
        /// Open Wi-Fi Setting
        /// </summary>
        public Task<bool> OpenWifiSetting()
        {
            CheckInit(_context);
            var taskCompletionSource = new TaskCompletionSource<bool>();
            Intent panelIntent;
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
                panelIntent = new Intent(Panel.ActionWifi);
            else
                panelIntent = new Intent(ActionWifiSettings);
            _context.StartActivity(panelIntent);
            taskCompletionSource.TrySetResult(true);
            return taskCompletionSource.Task;
        }

        /// <summary>
        /// Open Wireless Setting
        /// </summary>
        public Task<bool> OpenWirelessSetting()
        {
            CheckInit(_context);
            var taskCompletionSource = new TaskCompletionSource<bool>();
            var panelIntent = new Intent(ActionWirelessSettings);
            _context.StartActivity(panelIntent);
            taskCompletionSource.TrySetResult(true);
            return taskCompletionSource.Task;
        }

        /// <summary>
        /// Scan Wi-Fi Networks
        /// </summary>
        public async Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworks()
        {
            var response = new WifiManagerResponse<List<NetworkData>>();
            try
            {
                var wifiManager = _context.GetSystemService(Context.WifiService) as WifiManager;
                if (wifiManager == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.");
                    response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                    response.ErrorMessage = "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.";
                    return response;
                }

                if (!wifiManager.IsWifiEnabled)
                {
                    System.Diagnostics.Debug.WriteLine($"Wi-Fi is turned off. Please enable Wi-Fi to proceed.");
                    response.ErrorCode = WifiErrorCodes.WifiNotEnabled;
                    response.ErrorMessage = "Wi-Fi is turned off. Please enable Wi-Fi to proceed.";
                    return response;
                }

                List<NetworkData> wifiNetworks = new List<NetworkData>();
                System.Diagnostics.Debug.WriteLine($"Wi-Fi Scan started.");
                wifiManager?.StartScan();
                var scanResults = wifiManager?.ScanResults;
                foreach (var result in scanResults)
                {
                    wifiNetworks.Add(new NetworkData()
                    {
                        Bssid = result.Bssid,
                        Ssid = result.Ssid,
                        NativeObject = result
                    });
                }
                System.Diagnostics.Debug.WriteLine($"Wi-Fi Scan complete.");
                response.ErrorCode = WifiErrorCodes.Success;
                response.ErrorMessage = $"Wi-Fi Scan complete.";
                response.Data = wifiNetworks;
            }
            catch(Exception ex)
            {

                System.Diagnostics.Debug.WriteLine($"Error while scanning Wi-Fi: {ex.Message}");
                response.ErrorCode = WifiErrorCodes.UnknownError;
                response.ErrorMessage = $"Error while scanning Wi-Fi: {ex.Message}";
            }
            return response;
        }

        private async Task<WifiManagerResponse<NetworkData>> AddWifiSuggestion(WifiManager wifiManager, string ssid, string psk)
        {
            var response = new WifiManagerResponse<NetworkData>();
            var networkData = new NetworkData();

            if (OperatingSystem.IsAndroidVersionAtLeast(30)) // Android 11 (API level 30) or later
            {
                try
                {
                    TaskCompletionSource<NetworkData> tcs = new TaskCompletionSource<NetworkData>();
                    var suggestions = new List<IParcelable>
                                    {
                                       new WifiNetworkSuggestion.Builder()
                                        .SetSsid(ssid)
                                        .SetWpa2Passphrase(psk)
                                        .SetIsUserInteractionRequired(true)
                                        .Build()
                                    };
                    // Open the Wi-Fi settings
                    var wifiSettingsResponse = await OpenWifiSetting();
                    if (!wifiSettingsResponse)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to open Wi - Fi settings.");
                        response.ErrorCode = WifiErrorCodes.UnknownError;
                        return response;
                    }

                    var bundle = new Bundle();
                    bundle.PutParcelableArrayList("android.provider.extra.WIFI_NETWORK_LIST", suggestions);
                    var intent = new Intent("android.settings.WIFI_ADD_NETWORKS");
                    intent.PutExtras(bundle);
                    _context.StartActivity(intent);

                    var connectivityManager = _context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
                    var networkRequest = new NetworkRequest.Builder().AddTransportType(TransportType.Wifi).Build();
                    ConnectivityManager.NetworkCallback networkCallback;
                    if (OperatingSystem.IsAndroidVersionAtLeast(31)) // Android 12 (API level 31) or later
                    {
                        NetworkCallbackFlags flagIncludeLocationInfo = NetworkCallbackFlags.IncludeLocationInfo;
                        networkCallback = new NetworkCallback((int)flagIncludeLocationInfo)
                        {
                            OnNetworkCapabilitiesChanged = (network, networkCapabilities) =>
                            {
                                WifiInfo wifiInfo = (WifiInfo)networkCapabilities.TransportInfo;
                                if (wifiInfo != null)
                                {
                                    if (wifiInfo.SupplicantState == SupplicantState.Completed)
                                    {
                                        networkData.StatusId = (int)WifiErrorCodes.Success;
                                        networkData.Ssid = wifiInfo.SSID?.Trim(new char[] { '"', '\"' });
                                        networkData.Bssid = wifiInfo.BSSID;
                                        networkData.IpAddress = wifiInfo?.IpAddress ?? 0;
                                        networkData.NativeObject = wifiInfo;
                                        networkData.SignalStrength = wifiInfo.Rssi;
                                        tcs.TrySetResult(networkData);
                                    }
                                    else if (wifiInfo.SupplicantState == SupplicantState.Invalid)
                                    {
                                        tcs.TrySetResult(new NetworkData());
                                    }                                       
                                }
                            },
                            NetworkUnavailable = () =>
                            {
                                tcs.TrySetResult(new NetworkData());
                            }
                        };
                    }
                    else
                    {
                        networkCallback = new NetworkCallback
                        {
                            NetworkAvailable = network =>
                            {
                                // Handle network available
                            },
                            OnNetworkCapabilitiesChanged = (network, networkCapabilities) =>
                            {
                                if (networkCapabilities.HasCapability(NetCapability.Validated))
                                {
                                    if (wifiManager != null)
                                    {
                                        if (OperatingSystem.IsAndroidVersionAtLeast(30) && !OperatingSystem.IsAndroidVersionAtLeast(31))
                                        {
                                            networkData.StatusId = (int)WifiErrorCodes.Success;
                                            networkData.Ssid = wifiManager.ConnectionInfo?.SSID?.Trim(new char[] { '"', '\"' });
                                            networkData.Bssid = wifiManager.ConnectionInfo?.BSSID;
                                            networkData.SignalStrength = wifiManager.ConnectionInfo?.Rssi;
                                            networkData.IpAddress = wifiManager.DhcpInfo?.IpAddress ?? 0;
                                            networkData.GatewayAddress = wifiManager.DhcpInfo?.Gateway.ToString();
                                            networkData.NativeObject = wifiManager.ConnectionInfo;
                                        }
                                    }
                                    tcs.TrySetResult(networkData);
                                }
                            },
                            NetworkUnavailable = () =>
                            {
                                tcs.TrySetResult(new NetworkData());
                            }
                        };
                    }
                    connectivityManager?.RegisterNetworkCallback(networkRequest, networkCallback);

                    // Set a timeout to prevent hanging
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15));
                    var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        // Timed out, return default result
                        tcs.TrySetResult(new NetworkData());
                    }

                    // Ensure to unregister the callback when done
                    connectivityManager?.UnregisterNetworkCallback(networkCallback);
                    networkData = await tcs.Task;

                    if (networkData != null && networkData.StatusId == (int)WifiErrorCodes.Success)
                    {
                        System.Diagnostics.Debug.WriteLine($"Wi-Fi network suggestion added successfully.");
                        response.ErrorCode = WifiErrorCodes.Success;
                        response.ErrorMessage = "Wi-Fi network suggestion added successfully.";
                        response.Data = networkData;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to add Wi-Fi network suggestion.");
                        response.ErrorCode = WifiErrorCodes.UnknownError;
                        response.ErrorMessage = "Failed to add Wi-Fi network suggestion.";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error while adding Wi-Fi suggestion: {ex.Message}");
                    response.ErrorCode = WifiErrorCodes.UnknownError;
                    response.ErrorMessage = $"Error while adding Wi-Fi suggestion: {ex.Message}";
                }                
            }
            return response;
        }      

        public async Task<WifiManagerResponse<NetworkData>> RequestNetwork(WifiManager wifiManager, string ssid, string password) 
        {
            var response = new WifiManagerResponse<NetworkData>();
            var networkData = new NetworkData();

            if (wifiManager == null)
            {
                System.Diagnostics.Debug.WriteLine($"Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.");
                response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                response.ErrorMessage = "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.";
                return response;
            }

            if (!wifiManager.IsWifiEnabled)
            {
                System.Diagnostics.Debug.WriteLine($"Wi-Fi is turned off. Please enable Wi-Fi to proceed.");
                response.ErrorCode = WifiErrorCodes.WifiNotEnabled;
                response.ErrorMessage = "Wi-Fi is turned off. Please enable Wi-Fi to proceed.";
                return response;
            }

            try
            {
                TaskCompletionSource<NetworkData> tcs = new TaskCompletionSource<NetworkData>();
                if (OperatingSystem.IsAndroidVersionAtLeast(29) && !OperatingSystem.IsAndroidVersionAtLeast(30))
                {
                    // Creating a connection using this API does not provide an internet connection to the app or to the device.
                    var specifier = new WifiNetworkSpecifier.Builder().SetSsid(ssid).SetWpa2Passphrase(password).Build();
                    var request = new NetworkRequest.Builder()?
                        .AddTransportType(TransportType.Wifi)?
                        .SetNetworkSpecifier(specifier)?
                        .Build();
                    var networkCallback = new NetworkCallback
                    {
                        NetworkAvailable = network =>
                        {

                        },
                        NetworkUnavailable = () =>
                        {
                            tcs.TrySetResult(new NetworkData());
                        },
                        OnNetworkCapabilitiesChanged = (network, networkCapabilities) =>
                        {
                            if (!OperatingSystem.IsAndroidVersionAtLeast(31))
                            {
                                if (networkCapabilities.HasCapability(NetCapability.Validated))
                                {
                                    networkData.StatusId = (int)WifiErrorCodes.Success;
                                    networkData.Ssid = wifiManager.ConnectionInfo?.SSID?.Trim(new char[] { '"', '\"' });
                                    networkData.Bssid = wifiManager.ConnectionInfo?.BSSID;
                                    networkData.SignalStrength = wifiManager.ConnectionInfo?.Rssi;
                                    networkData.IpAddress = wifiManager.DhcpInfo?.IpAddress ?? 0;
                                    networkData.GatewayAddress = wifiManager.DhcpInfo?.Gateway.ToString();
                                    networkData.NativeObject = wifiManager.ConnectionInfo;
                                    tcs.TrySetResult(networkData);
                                }
                            }

                        }

                    };
                    UnregisterNetworkCallback(networkCallback);

                    _connectivityManager = _context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
                    if (_requested)
                        _connectivityManager?.UnregisterNetworkCallback(networkCallback);
                    if (request != null)
                    {
                        _connectivityManager?.RequestNetwork(request, networkCallback);
                        _requested = true;
                    }
                    // Await the task and set response accordingly
                    networkData = await tcs.Task;
                    if (networkData != null && networkData.StatusId == 1)
                    {
                        System.Diagnostics.Debug.WriteLine($"Wi-Fi connected successfully.");
                        response.ErrorCode = WifiErrorCodes.Success;
                        response.ErrorMessage = "Wi-Fi connected successfully.";
                        response.Data = networkData;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Wi-Fi connection failed.");
                        response.ErrorCode = WifiErrorCodes.UnknownError;
                        response.ErrorMessage = "Wi-Fi connection failed.";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error while requesting Wi-Fi network: {ex.Message}");
                response.ErrorCode = WifiErrorCodes.UnknownError;
                response.ErrorMessage = "Error while requesting Wi-Fi network: {ex.Message}";
            }
            return response;
        }

        private void UnregisterNetworkCallback(NetworkCallback networkCallback)
        {
            if (networkCallback != null)
            {
                try
                {
                    _connectivityManager = _context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
                    _connectivityManager?.UnregisterNetworkCallback(networkCallback);
                }
                catch
                {
                    networkCallback = null;
                }
            }
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {

        }
       
    }
    public class NetworkCallback : ConnectivityManager.NetworkCallback
    {
        public Action<Network> NetworkAvailable { get; set; }
        public Action NetworkUnavailable { get; set; }
        public Action<Network, NetworkCapabilities> OnNetworkCapabilitiesChanged { get; set; }
        public NetworkCallback(int flags) : base(flags)
        {
        }
        public NetworkCallback()
        {
        }
        public override void OnAvailable(Network network)
        {
            base.OnAvailable(network);
            NetworkAvailable?.Invoke(network);
        }

        public override void OnUnavailable()
        {
            base.OnUnavailable();
            NetworkUnavailable?.Invoke();
        }
        public override void OnCapabilitiesChanged(Network network, NetworkCapabilities networkCapabilities)
        {
            base.OnCapabilitiesChanged(network, networkCapabilities);
            OnNetworkCapabilitiesChanged?.Invoke(network, networkCapabilities);
        }
    }

    [Flags]
    public enum NetworkCallbackFlags
    {
        //
        // Summary:
        //     To be added.
        [IntDefinition(null, JniField = "")]
        None = 0x0,
        //
        // Summary:
        //     To be added.
        [IntDefinition("Android.Net.ConnectivityManager.NetworkCallback.FlagIncludeLocationInfo", JniField = "android/net/ConnectivityManager$NetworkCallback.FLAG_INCLUDE_LOCATION_INFO")]
        IncludeLocationInfo = 0x1
    }

    public class WifiScanReceiver : BroadcastReceiver
    {
        public List<ScanResult> ScanResults { get; private set; }
        public WifiScanReceiver(WifiNetworkService wifiScanner)
        {
            ScanResults = new List<ScanResult>();
        }
        public override void OnReceive(Context context, Intent intent)
        {
        }
    }

}

using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Runtime;
using Plugin.MauiWifiManager.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Android.Provider.Settings;
using Context = Android.Content.Context;

namespace Plugin.MauiWifiManager
{
    /// <summary>
    /// Interface for WiFiNetworkService
    /// </summary>
    /// 
    public class WifiNetworkService : IWifiNetworkService
    {
        private static NetworkData _networkData = null!;
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
                _networkData = new NetworkData();          
            }
            else
                throw new NullReferenceException("Context is null. Initialization cannot proceed.");

        }

        /// <summary>
        /// Connect Wi-Fi
        /// </summary>
        public async Task<NetworkData> ConnectWifi(string ssid, string password)
        {
            var wifiManager = _context.GetSystemService(Context.WifiService) as WifiManager;
            if (wifiManager != null)
            {
                if (!OperatingSystem.IsAndroidVersionAtLeast(29))
                {
                    if (!wifiManager.IsWifiEnabled)
                        wifiManager.SetWifiEnabled(true);
                    string wifiSsid = wifiManager.ConnectionInfo?.SSID?.ToString() ?? string.Empty;
                    if (wifiSsid != string.Format("\"{0}\"", ssid))
                    {
                        WifiConfiguration wifiConfig = new WifiConfiguration
                        {
                            Ssid = string.Format("\"{0}\"", ssid),
                            PreSharedKey = string.Format("\"{0}\"", password)
                        };
                        int netId = wifiManager.AddNetwork(wifiConfig);
                        wifiManager.Disconnect();
                        wifiManager.EnableNetwork(netId, true);
                        wifiManager.Reconnect();
                        _networkData.Ssid = wifiConfig.Ssid;

                    }
                    else
                        Console.WriteLine("Cannot find valid SSID");
                }
                else if (OperatingSystem.IsAndroidVersionAtLeast(29) && !OperatingSystem.IsAndroidVersionAtLeast(30))
                    _networkData = await RequestNetwork(ssid, password);
                else
                    await AddWifiSuggestion(ssid, password);
                return _networkData;
            }
            else
                throw new InvalidOperationException("Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.");

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
        public async Task<NetworkData> GetNetworkInfo()
        {         
            if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                var wifiManager = _context.GetSystemService(Context.WifiService) as WifiManager;
                if (wifiManager != null && wifiManager.IsWifiEnabled)
                {
                    _networkData.StatusId = 1;
                    _networkData.Ssid = wifiManager.ConnectionInfo?.SSID?.Trim(new char[] { '"', '\"' });
                    _networkData.Bssid = wifiManager.ConnectionInfo?.BSSID;
                    _networkData.SignalStrength = wifiManager.ConnectionInfo?.Rssi;
                    _networkData.IpAddress = wifiManager.DhcpInfo?.IpAddress ?? 0;
                    _networkData.GatewayAddress = wifiManager.DhcpInfo?.Gateway.ToString();
                    _networkData.NativeObject = wifiManager.ConnectionInfo;
                }
                else
                {
                    if (wifiManager == null)
                        throw new InvalidOperationException("Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.");
                    else if (!wifiManager.IsWifiEnabled)
                        throw new InvalidOperationException("Wi-Fi is turned off. Please enable Wi-Fi to proceed.");
                }
            }
            else
            {
                TaskCompletionSource<NetworkData> tcs = new TaskCompletionSource<NetworkData>();
                ConnectivityManager? connectivityManager = _context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
                if (connectivityManager == null)
                {
                    throw new InvalidOperationException("Connectivity service is not available on this device.");
                }
                else
                {
                    NetworkInfo activeNetworkInfo = connectivityManager.ActiveNetworkInfo;
                    if (activeNetworkInfo != null)
                    {
                        NetworkCallbackFlags flagIncludeLocationInfo = NetworkCallbackFlags.IncludeLocationInfo;
                        NetworkCallback networkCallback = new NetworkCallback((int)flagIncludeLocationInfo)
                        {
                            OnNetworkCapabilitiesChanged = (network, networkCapabilities) =>
                            {
                                WifiInfo wifiInfo = (WifiInfo)networkCapabilities.TransportInfo;

                                if (wifiInfo != null && wifiInfo.SupplicantState == SupplicantState.Completed)
                                {
                                    _networkData.StatusId = 1;
                                    _networkData.Ssid = wifiInfo?.SSID?.Trim(new char[] { '"', '\"' });
                                    _networkData.Bssid = wifiInfo?.BSSID;
                                    _networkData.IpAddress = wifiInfo?.IpAddress ?? 0;
                                    _networkData.NativeObject = wifiInfo;
                                    _networkData.SignalStrength = wifiInfo?.Rssi;
                                    tcs.TrySetResult(_networkData);
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
                        return await tcs.Task;
                    }
                    else
                        return new NetworkData();
                }
               
            }           
            return _networkData;
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
        public async Task<List<NetworkData>> ScanWifiNetworks()
        {
            var wifiManager = _context.GetSystemService(Context.WifiService) as WifiManager;
            List<NetworkData> wifiNetworks = new List<NetworkData>();
            if (wifiManager != null && wifiManager.IsWifiEnabled)
            {
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
            }
            else
                throw new InvalidOperationException("Wi-Fi is turned off. Please enable Wi-Fi to proceed.");
            return wifiNetworks;
        }

        private async Task<NetworkData> AddWifiSuggestion(string ssid, string psk)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30)) // Android 11 (API level 30) or later
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
                var response = await OpenWifiSetting();
                if (response)
                {
                    var bundle = new Bundle();
                    bundle.PutParcelableArrayList("android.provider.extra.WIFI_NETWORK_LIST", suggestions);
                    var intent = new Intent("android.settings.WIFI_ADD_NETWORKS");
                    intent.PutExtras(bundle);
                    _context.StartActivity(intent);
                    var connectivityManager = _context.GetSystemService(Context.ConnectivityService) as ConnectivityManager; ;
                    var networkRequest = new NetworkRequest.Builder().AddTransportType(TransportType.Wifi).Build();

                    ConnectivityManager.NetworkCallback networkCallback;
                    if (OperatingSystem.IsAndroidVersionAtLeast(31)) // Android 12 (API level 31) or later
                    {
                        NetworkCallbackFlags flagIncludeLocationInfo = NetworkCallbackFlags.IncludeLocationInfo;
                        networkCallback = new NetworkCallback(((int)flagIncludeLocationInfo))
                        {
                            OnNetworkCapabilitiesChanged = (network, networkCapabilities) =>
                            {
                                WifiInfo wifiInfo = (WifiInfo)networkCapabilities.TransportInfo;
                                if (wifiInfo != null)
                                {
                                    if (wifiInfo.SupplicantState == SupplicantState.Completed)
                                    {
                                        _networkData.StatusId = 1;
                                        _networkData.Ssid = wifiInfo?.SSID?.Trim(new char[] { '"', '\"' });
                                        _networkData.Bssid = wifiInfo?.BSSID;
                                        _networkData.IpAddress = wifiInfo?.IpAddress ?? 0;
                                        _networkData.NativeObject = wifiInfo;
                                        _networkData.SignalStrength = wifiInfo?.Rssi;
                                        tcs.TrySetResult(_networkData);
                                    }
                                    else if (wifiInfo.SupplicantState == SupplicantState.Invalid)
                                        tcs.TrySetResult(new NetworkData());
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
                        var wifiManager = _context.GetSystemService(Context.WifiService) as WifiManager;
                        networkCallback = new NetworkCallback
                        {
                            NetworkAvailable = network =>
                            {

                            },
                            OnNetworkCapabilitiesChanged = (network, networkCapabilities) =>
                            {
                                if (networkCapabilities.HasCapability(NetCapability.Validated))
                                {
                                    if (wifiManager != null)
                                    {
                                        if (OperatingSystem.IsAndroidVersionAtLeast(30) && !OperatingSystem.IsAndroidVersionAtLeast(31))
                                        {
                                            _networkData.StatusId = 1;
                                            _networkData.Ssid = wifiManager.ConnectionInfo?.SSID?.Trim(new char[] { '"', '\"' });
                                            _networkData.Bssid = wifiManager.ConnectionInfo?.BSSID;
                                            _networkData.SignalStrength = wifiManager.ConnectionInfo?.Rssi;
                                            _networkData.IpAddress = wifiManager.DhcpInfo?.IpAddress ?? 0;
                                            _networkData.GatewayAddress = wifiManager.DhcpInfo?.Gateway.ToString();
                                            _networkData.NativeObject = wifiManager.ConnectionInfo;
                                        }
                                       
                                    }                                    
                                    tcs.TrySetResult(_networkData);
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
                    return await tcs.Task;
                }
                return new NetworkData();
            }
            return new NetworkData();
        }      

        public async Task<NetworkData> RequestNetwork(string ssid, string password) 
        {            
            var wifiManager = _context.GetSystemService(Context.WifiService) as WifiManager;
            if (wifiManager == null)
                throw new InvalidOperationException("Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.");
            if (!wifiManager.IsWifiEnabled)
                throw new InvalidOperationException("Wi-Fi is turned off. Please enable Wi-Fi to proceed.");
            TaskCompletionSource<NetworkData> tcs = new TaskCompletionSource<NetworkData>();
            if (OperatingSystem.IsAndroidVersionAtLeast(29) && !OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var specifier = new WifiNetworkSpecifier.Builder().SetSsid(ssid).SetWpa2Passphrase(password).Build();
                var request = new NetworkRequest.Builder()?.AddTransportType(TransportType.Wifi)?.SetNetworkSpecifier(specifier)?.Build();
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
                                _networkData.StatusId = 1;
                                _networkData.Ssid = wifiManager.ConnectionInfo?.SSID?.Trim(new char[] { '"', '\"' });
                                _networkData.Bssid = wifiManager.ConnectionInfo?.BSSID;
                                _networkData.SignalStrength = wifiManager.ConnectionInfo?.Rssi;
                                _networkData.IpAddress = wifiManager.DhcpInfo?.IpAddress ?? 0;
                                _networkData.GatewayAddress = wifiManager.DhcpInfo?.Gateway.ToString();
                                _networkData.NativeObject = wifiManager.ConnectionInfo;
                                tcs.TrySetResult(_networkData);
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
            }            
            return await tcs.Task;
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

using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Runtime;
using MauiWifiManager.Abstractions;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using static Android.Provider.Settings;
using Context = Android.Content.Context;
using WifiSecurity = MauiWifiManager.Abstractions.WifiSecurityType;

namespace MauiWifiManager
{
    public class WifiNetworkService : IWifiNetworkService
    {
        private static Context _Context = null!;
        private static ConnectivityManager? _ConnectivityManager;
        private static bool _Requested;
        private static readonly char[] _TrimChars = new char[] { '"', '\"' };
        private EventHandler<WifiNetworkChangedEventArgs>? _WifiNetworkChanged;
        private EventHandler<NetworkData>? _DeviceDiscovered;
        private readonly object _MonitorLock = new();
        private readonly object _ScanLock = new();
        private NetworkData? _LastKnownNetwork;
        private CancellationTokenSource? _ScanSessionCts;
        private Task? _ScanSessionTask;
        private readonly HashSet<string> _DiscoveredNetworkKeys = new(StringComparer.OrdinalIgnoreCase);
        private bool _IsMonitoring;

        public bool IsScanning { get; private set; }

        public event EventHandler<WifiNetworkChangedEventArgs>? WifiNetworkChanged
        {
            add
            {
                _WifiNetworkChanged += value;
                EnsureMonitoringStarted();
            }
            remove => _WifiNetworkChanged -= value;
        }

        public event EventHandler<NetworkData>? DeviceDiscovered
        {
            add => _DeviceDiscovered += value;
            remove => _DeviceDiscovered -= value;
        }

        public WifiNetworkService() { }

        public static void CheckInit(Context context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(_Context), "Please call WifiNetworkService.Init(this) inside the MainActivity's OnCreate function.");
        }

        public static void Init(Context? context)
        {
            if (context != null)
            {
                CheckInit(context);
                _Context = context;
            }
            else
                throw new NullReferenceException("Context is null. Initialization cannot proceed.");
        }

        [Obsolete("Use ConnectWifiAsync(string ssid, string password, CancellationToken cancellationToken = default) or ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default) instead.")]
        public Task<WifiManagerResponse<NetworkData>> ConnectWifi(string ssid, string password, string? bssid = null)
        {
            return ConnectWifiAsync(ssid, password, bssid, CancellationToken.None);
        }

        public Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, CancellationToken cancellationToken = default)
        {
            return ConnectWifiAsync(ssid, password, new WifiConnectionOptions(), cancellationToken);
        }

        public Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, string? bssid, CancellationToken cancellationToken = default)
        {
            return ConnectWifiAsync(ssid, password, new WifiConnectionOptions { Bssid = bssid }, cancellationToken);
        }

        public async Task<WifiManagerResponse<NetworkData>> ConnectWifiAsync(string ssid, string password, WifiConnectionOptions options, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return CreateCanceledResponse<NetworkData>(nameof(ConnectWifiAsync));

            var response = new WifiManagerResponse<NetworkData>();
            var networkData = new NetworkData();
            var wifiManager = _Context.GetSystemService(Context.WifiService) as WifiManager;

            if (wifiManager == null)
            {
                response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                response.ErrorMessage = "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.";
                return response;
            }

            try
            {
                if (!OperatingSystem.IsAndroidVersionAtLeast(29))
                {
                    if (!wifiManager.IsWifiEnabled)
                        wifiManager.SetWifiEnabled(true);

                    string wifiSsid = wifiManager.ConnectionInfo?.SSID?.ToString() ?? string.Empty;
                    if (wifiSsid != string.Format("\"{0}\"", ssid))
                    {
                        var wifiConfig = new WifiConfiguration
                        {
                            Ssid = string.Format("\"{0}\"", ssid),
                            PreSharedKey = string.Format("\"{0}\"", password)
                        };
                        if (!string.IsNullOrWhiteSpace(options.Bssid))
                            wifiConfig.Bssid = options.Bssid;
                        // Hidden network support for legacy API
                        if (options.IsHidden)
                            wifiConfig.HiddenSSID = true;

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
                        response.ErrorCode = WifiErrorCodes.NoConnection;
                        response.ErrorMessage = "Cannot find a valid SSID to connect.";
                        response.Data = networkData;
                    }
                }
                else if (OperatingSystem.IsAndroidVersionAtLeast(29) && !OperatingSystem.IsAndroidVersionAtLeast(30))
                {
                    response = await RequestNetwork(wifiManager, ssid, password, options, cancellationToken);
                }
                else
                {
                    response = await AddWifiSuggestion(wifiManager, ssid, password, options, cancellationToken);
                }
            }
            catch (System.OperationCanceledException)
            {
                return CreateCanceledResponse<NetworkData>(nameof(ConnectWifiAsync));
            }
            catch (Exception ex)
            {
                response.ErrorCode = WifiErrorCodes.UnknownError;
                response.ErrorMessage = $"Error connecting to Wi-Fi: {ex.Message}";
                response.Data = networkData;
            }
            return response;
        }

        public void DisconnectWifi(string? ssid)
        {
            if (_Context != null)
            {
                CheckInit(_Context);
                if (OperatingSystem.IsAndroidVersionAtLeast(29))
                {
                    Intent panelIntent = new Intent(Panel.ActionWifi);
                    _Context.StartActivity(panelIntent);
                }
                else
                {
                    var wifiManager = _Context.GetSystemService(Context.WifiService) as WifiManager;
                    if (wifiManager != null)
                    {
                        wifiManager.SetWifiEnabled(false);
                        wifiManager.SetWifiEnabled(true);
                    }
                    else
                        throw new NullReferenceException("wifiManager is null.");
                }
            }
            else
                throw new NullReferenceException("Context is null. Disconnect Wi-Fi cannot proceed.");
        }

        [Obsolete("Use GetNetworkInfoAsync(CancellationToken cancellationToken = default) instead.")]
        public Task<WifiManagerResponse<NetworkData>> GetNetworkInfo()
        {
            return GetNetworkInfoAsync(CancellationToken.None);
        }

        public async Task<WifiManagerResponse<NetworkData>> GetNetworkInfoAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return CreateCanceledResponse<NetworkData>(nameof(GetNetworkInfo));

            var response = new WifiManagerResponse<NetworkData>();
            var networkData = new NetworkData();

            if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                var wifiManager = _Context.GetSystemService(Context.WifiService) as WifiManager;

                if (wifiManager == null)
                {
                    response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                    response.ErrorMessage = "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.";
                    return response;
                }
                if (!wifiManager.IsWifiEnabled)
                {
                    response.ErrorCode = WifiErrorCodes.WifiNotEnabled;
                    response.ErrorMessage = "Wi-Fi is turned off. Please enable Wi-Fi to proceed.";
                    return response;
                }
                if (wifiManager.ConnectionInfo == null)
                {
                    response.ErrorCode = WifiErrorCodes.NoConnection;
                    response.ErrorMessage = "Invalid ConnectionInfo.";
                    return response;
                }

                if (wifiManager.ConnectionInfo.SupplicantState == SupplicantState.Completed)
                {
                    networkData.StatusId = (int)WifiErrorCodes.Success;
                    networkData.Ssid = wifiManager.ConnectionInfo?.SSID?.Trim(_TrimChars);
                    networkData.Bssid = wifiManager.ConnectionInfo?.BSSID;
                    networkData.SignalStrength = wifiManager.ConnectionInfo?.Rssi;
                    networkData.RssiDbm = wifiManager.ConnectionInfo?.Rssi;
                    networkData.LinkSpeedMbps = wifiManager.ConnectionInfo?.LinkSpeed;
                    networkData.IpAddress = wifiManager.DhcpInfo?.IpAddress ?? 0;
                    networkData.GatewayAddress = wifiManager.DhcpInfo?.Gateway ?? 0;
                    networkData.DhcpServerAddress = wifiManager.DhcpInfo?.ServerAddress ?? 0;
                    networkData.NativeObject = wifiManager.ConnectionInfo;

                    int freq = wifiManager.ConnectionInfo?.Frequency ?? 0;
                    if (freq > 0)
                    {
                        networkData.FrequencyBand = GetBandFromFrequency(freq);
                        networkData.ChannelNumber = GetChannelFromFrequency(freq);
                    }

                    var networkList = wifiManager?.ScanResults;
                    if (networkList != null)
                    {
                        foreach (var list in networkList)
                        {
                            if (list.Bssid == wifiManager?.ConnectionInfo?.BSSID)
                            {
                                networkData.SecurityType = list.Capabilities;
                                break;
                            }
                        }
                    }

                    PopulateNetworkInterfaceExtendedData(networkData);

                    response.ErrorCode = WifiErrorCodes.Success;
                    response.ErrorMessage = "Fetched Wi-Fi connection info successfully.";
                    response.Data = networkData;
                    return response;
                }
            }
            else
            {
                TaskCompletionSource<NetworkData> tcs = new();
                ConnectivityManager? connectivityManager = _Context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
                if (connectivityManager == null)
                {
                    response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                    response.ErrorMessage = "Connectivity service is not available on this device.";
                    return response;
                }
                var activeNetwork = connectivityManager?.ActiveNetwork;
                if (activeNetwork == null)
                {
                    response.ErrorCode = WifiErrorCodes.NetworkUnavailable;
                    response.ErrorMessage = "No active network.";
                    return response;
                }

                NetworkCallbackFlags flagIncludeLocationInfo = NetworkCallbackFlags.IncludeLocationInfo;
                NetworkCallback networkCallback = new((int)flagIncludeLocationInfo)
                {
                    OnNetworkCapabilitiesChanged = (network, networkCapabilities) =>
                    {
                        if (OperatingSystem.IsAndroidVersionAtLeast(29))
                        {
                            WifiInfo? wifiInfo = networkCapabilities?.TransportInfo as WifiInfo;

                            if (wifiInfo != null && wifiInfo.SupplicantState == SupplicantState.Completed)
                            {
                                networkData.StatusId = 1;
                                networkData.Ssid = wifiInfo?.SSID?.Trim(_TrimChars);
                                networkData.Bssid = wifiInfo?.BSSID;
                                networkData.SignalStrength = wifiInfo?.Rssi;
                                networkData.RssiDbm = wifiInfo?.Rssi;
                                networkData.LinkSpeedMbps = wifiInfo?.LinkSpeed;
                                networkData.NativeObject = wifiInfo;

                                int freq = wifiInfo?.Frequency ?? 0;
                                if (freq > 0)
                                {
                                    networkData.FrequencyBand = GetBandFromFrequency(freq);
                                    networkData.ChannelNumber = GetChannelFromFrequency(freq);
                                }

                                if (OperatingSystem.IsAndroidVersionAtLeast(31))
                                {
                                    var linkProperties = connectivityManager?.GetLinkProperties(network);
                                    var inetAddress = linkProperties?.LinkAddresses
                                        .Select(la => la.Address)
                                        .FirstOrDefault(addr => addr is Java.Net.Inet4Address);

                                    if (inetAddress != null)
                                    {
                                        networkData.IpAddress = GetIpAddressFromInetAddress(inetAddress);
                                        networkData.DhcpServerAddress = GetIpAddressFromInetAddress(linkProperties?.DhcpServerAddress);
                                        if (linkProperties?.Routes != null)
                                        {
                                            foreach (var route in linkProperties.Routes)
                                            {
                                                if (route.IsDefaultRoute && route.Gateway != null)
                                                    networkData.GatewayAddress = GetIpAddressFromInetAddress(route.Gateway);
                                            }
                                        }
                                    }

                                    // IPv6
                                    var ipv6 = linkProperties?.LinkAddresses
                                        .Select(la => la.Address)
                                        .FirstOrDefault(a => a is Java.Net.Inet6Address && !a.IsLoopbackAddress && !a.IsLinkLocalAddress);
                                    networkData.IPv6Address = ipv6?.HostAddress;

                                    // DNS
                                    if (linkProperties?.DnsServers != null)
                                    {
                                        networkData.DnsAddresses = linkProperties.DnsServers
                                            .Where(d => d.HostAddress != null)
                                            .Select(d => d.HostAddress!)
                                            .ToList();
                                    }

                                    // Subnet mask
                                    var ipv4LinkAddr = linkProperties?.LinkAddresses
                                        .FirstOrDefault(la => la.Address is Java.Net.Inet4Address);
                                    if (ipv4LinkAddr != null)
                                        networkData.SubnetMask = GetSubnetMaskFromPrefixLength(ipv4LinkAddr.PrefixLength);
                                }
                                else
                                {
                                    networkData.IpAddress = wifiInfo?.IpAddress ?? 0;
                                    PopulateNetworkInterfaceExtendedData(networkData);
                                }

                                var wifiManager = _Context.GetSystemService(Context.WifiService) as WifiManager;
                                var networkList = wifiManager?.ScanResults;
                                if (networkList != null)
                                {
                                    foreach (var list in networkList)
                                    {
                                        if (list.Bssid == wifiInfo?.BSSID)
                                        {
                                            networkData.SecurityType = list.Capabilities;
                                            break;
                                        }
                                    }
                                }
                                tcs.TrySetResult(networkData);
                            }
                        }
                    },
                    NetworkUnavailable = () =>
                    {
                        tcs.TrySetResult(new NetworkData());
                    }
                };

                NetworkRequest? request = new NetworkRequest.Builder()?.AddTransportType(transportType: TransportType.Wifi)?.Build();
                if (request != null)
                {
                    connectivityManager?.RequestNetwork(request, networkCallback);
                    connectivityManager?.RegisterNetworkCallback(request, networkCallback);

                    try
                    {
                        networkData = await tcs.Task.WaitAsync(cancellationToken);
                    }
                    catch (System.OperationCanceledException)
                    {
                        return CreateCanceledResponse<NetworkData>(nameof(GetNetworkInfo));
                    }

                    if (networkData != null && networkData.StatusId == 1)
                    {
                        response.ErrorCode = WifiErrorCodes.Success;
                        response.ErrorMessage = "Fetched Wi-Fi connection info successfully.";
                        response.Data = networkData;
                    }
                    else
                    {
                        response.ErrorCode = WifiErrorCodes.UnknownError;
                        response.ErrorMessage = "Failed to fetch Wi-Fi connection info.";
                    }
                }
                else
                {
                    response.ErrorCode = WifiErrorCodes.UnknownError;
                    response.ErrorMessage = "Network request is null.";
                }
            }
            return response;
        }

        public Task<bool> OpenWifiSetting()
        {
            CheckInit(_Context);
            var taskCompletionSource = new TaskCompletionSource<bool>();
            Intent panelIntent;
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
                panelIntent = new Intent(Panel.ActionWifi);
            else
                panelIntent = new Intent(ActionWifiSettings);
            _Context.StartActivity(panelIntent);
            taskCompletionSource.TrySetResult(true);
            return taskCompletionSource.Task;
        }

        public Task<bool> OpenWirelessSetting()
        {
            CheckInit(_Context);
            var taskCompletionSource = new TaskCompletionSource<bool>();
            var panelIntent = new Intent(ActionWirelessSettings);
            _Context.StartActivity(panelIntent);
            taskCompletionSource.TrySetResult(true);
            return taskCompletionSource.Task;
        }

        [Obsolete("Use ScanWifiNetworksAsync(CancellationToken cancellationToken = default) instead.")]
        public Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworks()
        {
            return ScanWifiNetworksAsync(CancellationToken.None);
        }

        public Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworksAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ScanWifiNetworksInternal(cancellationToken));
        }

        private WifiManagerResponse<List<NetworkData>> ScanWifiNetworksInternal(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return CreateCanceledResponse<List<NetworkData>>(nameof(ScanWifiNetworks));

            var response = new WifiManagerResponse<List<NetworkData>>();
            try
            {
                var wifiManager = _Context.GetSystemService(Context.WifiService) as WifiManager;
                if (wifiManager == null)
                {
                    response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                    response.ErrorMessage = "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.";
                    return response;
                }

                if (!wifiManager.IsWifiEnabled)
                {
                    response.ErrorCode = WifiErrorCodes.WifiNotEnabled;
                    response.ErrorMessage = "Wi-Fi is turned off. Please enable Wi-Fi to proceed.";
                    return response;
                }

                List<NetworkData> wifiNetworks = new();
                if (!OperatingSystem.IsAndroidVersionAtLeast(28))
                    wifiManager.StartScan();

                var scanResults = wifiManager?.ScanResults;
                if (scanResults != null)
                {
                    foreach (var result in scanResults)
                    {
                        var rawSsid = OperatingSystem.IsAndroidVersionAtLeast(33)
                            ? result.WifiSsid?.ToString()
                            : result.Ssid;

                        int freq = result.Frequency;
                        wifiNetworks.Add(new NetworkData
                        {
                            Bssid = result.Bssid,
                            Ssid = rawSsid?.Trim(_TrimChars),
                            SignalStrength = result.Level,
                            RssiDbm = result.Level,
                            SecurityType = result.Capabilities,
                            FrequencyBand = freq > 0 ? GetBandFromFrequency(freq) : WifiFrequencyBand.Unknown,
                            ChannelNumber = freq > 0 ? GetChannelFromFrequency(freq) : null,
                            NativeObject = result
                        });
                    }
                }

                response.ErrorCode = WifiErrorCodes.Success;
                response.ErrorMessage = "Wi-Fi Scan complete.";
                response.Data = wifiNetworks;
            }
            catch (Exception ex)
            {
                response.ErrorCode = WifiErrorCodes.UnknownError;
                response.ErrorMessage = $"Error while scanning Wi-Fi: {ex.Message}";
            }
            return response;
        }

        public Task<WifiManagerResponse<bool>> StartScanningForDevicesAsync(CancellationToken cancellationToken = default)
        {
            CheckInit(_Context);
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.OperationCanceled, "StartScanningForDevicesAsync operation was canceled."));

            lock (_ScanLock)
            {
                if (IsScanning)
                    return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(true, "Scan session is already running."));

                _DiscoveredNetworkKeys.Clear();
                _ScanSessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                IsScanning = true;
                _ScanSessionTask = RunScanSessionAsync(_ScanSessionCts.Token);
            }

            return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(true, "Wi-Fi scan session started."));
        }

        public async Task<WifiManagerResponse<bool>> StopScanningAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CancellationTokenSource? cts;
            Task? scanTask;

            lock (_ScanLock)
            {
                if (!IsScanning)
                    return WifiManagerResponse<bool>.SuccessResponse(false, "No active scan session.");

                cts = _ScanSessionCts;
                scanTask = _ScanSessionTask;
                IsScanning = false;
                _ScanSessionCts = null;
                _ScanSessionTask = null;
            }

            try
            {
                cts?.Cancel();
                if (scanTask != null)
                    await scanTask;
            }
            catch (System.OperationCanceledException) { }
            finally
            {
                cts?.Dispose();
            }

            return WifiManagerResponse<bool>.SuccessResponse(true, "Wi-Fi scan session stopped.");
        }

        public Task<WifiManagerResponse<bool>> IsInternetAvailableAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(CreateCanceledResponse<bool>(nameof(IsInternetAvailableAsync)));

            try
            {
                var connectivityManager = _Context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
                var activeNetwork = connectivityManager?.ActiveNetwork;
                if (activeNetwork == null)
                    return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(false, "No active network."));

                var capabilities = connectivityManager?.GetNetworkCapabilities(activeNetwork);
                bool hasInternet = capabilities != null
                    && capabilities.HasCapability(NetCapability.Internet)
                    && capabilities.HasCapability(NetCapability.Validated);

                return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(
                    hasInternet,
                    hasInternet ? "Internet is available." : "No internet access."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, ex.Message));
            }
        }

        public Task<WifiManagerResponse<bool>> IsCaptivePortalDetectedAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(CreateCanceledResponse<bool>(nameof(IsCaptivePortalDetectedAsync)));

            try
            {
                var connectivityManager = _Context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
                var activeNetwork = connectivityManager?.ActiveNetwork;
                if (activeNetwork == null)
                    return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(false, "No active network."));

                var capabilities = connectivityManager?.GetNetworkCapabilities(activeNetwork);
                bool isCaptive = capabilities?.HasCapability(NetCapability.CaptivePortal) == true;

                return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(
                    isCaptive,
                    isCaptive ? "Captive portal detected." : "No captive portal."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, ex.Message));
            }
        }

        private async Task<WifiManagerResponse<NetworkData>> AddWifiSuggestion(WifiManager wifiManager, string ssid, string psk, WifiConnectionOptions options, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return CreateCanceledResponse<NetworkData>(nameof(AddWifiSuggestion));

            var response = new WifiManagerResponse<NetworkData>();
            var networkData = new NetworkData();

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                try
                {
                    TaskCompletionSource<NetworkData> tcs = new();
                    var suggestionBuilder = new WifiNetworkSuggestion.Builder()
                        .SetSsid(ssid)
                        .SetIsUserInteractionRequired(true);

                    if (!string.IsNullOrWhiteSpace(options.Bssid))
                        suggestionBuilder.SetBssid(MacAddress.FromString(options.Bssid));

                    if (options.IsHidden)
                        suggestionBuilder.SetIsHiddenSsid(true);

                    if (options.SecurityType == WifiSecurity.Wpa3Sae)
                        suggestionBuilder.SetWpa3Passphrase(psk);
                    else if (options.SecurityType == WifiSecurity.Open)
                        ; // no passphrase for open networks
                    else
                        suggestionBuilder.SetWpa2Passphrase(psk);

                    var suggestions = new List<IParcelable> { suggestionBuilder.Build() };

                    var wifiSettingsResponse = await OpenWifiSetting();
                    if (!wifiSettingsResponse)
                    {
                        response.ErrorCode = WifiErrorCodes.UnknownError;
                        return response;
                    }

                    var bundle = new Bundle();
                    bundle.PutParcelableArrayList("android.provider.extra.WIFI_NETWORK_LIST", suggestions);
                    var intent = new Intent("android.settings.WIFI_ADD_NETWORKS");
                    intent.PutExtras(bundle);
                    _Context.StartActivity(intent);

                    var connectivityManager = _Context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
                    var networkRequest = new NetworkRequest.Builder()?.AddTransportType(TransportType.Wifi)?.Build();
                    ConnectivityManager.NetworkCallback networkCallback;
                    var activeNetwork = connectivityManager?.ActiveNetwork;
                    var linkProperties = connectivityManager?.GetLinkProperties(activeNetwork);

                    if (OperatingSystem.IsAndroidVersionAtLeast(31))
                    {
                        NetworkCallbackFlags flagIncludeLocationInfo = NetworkCallbackFlags.IncludeLocationInfo;
                        networkCallback = new NetworkCallback((int)flagIncludeLocationInfo)
                        {
                            OnNetworkCapabilitiesChanged = (network, networkCapabilities) =>
                            {
                                if (OperatingSystem.IsAndroidVersionAtLeast(29))
                                {
                                    WifiInfo? wifiInfo = networkCapabilities?.TransportInfo as WifiInfo;
                                    if (wifiInfo != null)
                                    {
                                        if (wifiInfo.SupplicantState == SupplicantState.Completed)
                                        {
                                            var currentSsid = wifiInfo.SSID?.Trim(_TrimChars);
                                            if (currentSsid == ssid && (string.IsNullOrWhiteSpace(options.Bssid) || string.Equals(wifiInfo.BSSID, options.Bssid, StringComparison.OrdinalIgnoreCase)))
                                            {
                                                networkData.StatusId = (int)WifiErrorCodes.Success;
                                                networkData.Ssid = currentSsid;
                                                networkData.Bssid = wifiInfo.BSSID;
                                                networkData.SignalStrength = wifiInfo.Rssi;
                                                networkData.RssiDbm = wifiInfo.Rssi;
                                                networkData.LinkSpeedMbps = wifiInfo.LinkSpeed;
                                                networkData.NativeObject = wifiInfo;

                                                if (OperatingSystem.IsAndroidVersionAtLeast(31))
                                                {
                                                    var inetAddress = linkProperties?.LinkAddresses
                                                        .Select(la => la.Address)
                                                        .FirstOrDefault(addr => addr is Java.Net.Inet4Address);
                                                    if (inetAddress != null)
                                                    {
                                                        var bytes = inetAddress.GetAddress();
                                                        networkData.IpAddress = bytes != null ? GetIpAddressFromBytes(bytes) : 0;
                                                    }
                                                }
                                                else
                                                    networkData.IpAddress = wifiInfo?.IpAddress ?? 0;

                                                tcs.TrySetResult(networkData);
                                            }
                                        }
                                        else if (wifiInfo.SupplicantState == SupplicantState.Invalid)
                                        {
                                            tcs.TrySetResult(new NetworkData());
                                        }
                                    }
                                }
                            },
                            NetworkUnavailable = () => tcs.TrySetResult(new NetworkData())
                        };
                    }
                    else
                    {
                        networkCallback = new NetworkCallback
                        {
                            NetworkAvailable = network => { },
                            OnNetworkCapabilitiesChanged = (network, networkCapabilities) =>
                            {
                                if (OperatingSystem.IsAndroidVersionAtLeast(23) && networkCapabilities.HasCapability(NetCapability.Validated))
                                {
                                    if (OperatingSystem.IsAndroidVersionAtLeast(30) && !OperatingSystem.IsAndroidVersionAtLeast(31))
                                    {
                                        var currentSsid = wifiManager.ConnectionInfo?.SSID?.Trim(_TrimChars);
                                        if (currentSsid == ssid && (string.IsNullOrWhiteSpace(options.Bssid) || string.Equals(wifiManager.ConnectionInfo?.BSSID, options.Bssid, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            networkData.StatusId = (int)WifiErrorCodes.Success;
                                            networkData.Ssid = currentSsid;
                                            networkData.Bssid = wifiManager.ConnectionInfo?.BSSID;
                                            networkData.SignalStrength = wifiManager.ConnectionInfo?.Rssi;
                                            networkData.RssiDbm = wifiManager.ConnectionInfo?.Rssi;
                                            networkData.LinkSpeedMbps = wifiManager.ConnectionInfo?.LinkSpeed;
                                            networkData.IpAddress = wifiManager.DhcpInfo?.IpAddress ?? 0;
                                            networkData.GatewayAddress = wifiManager.DhcpInfo?.Gateway ?? 0;
                                            networkData.DhcpServerAddress = wifiManager.DhcpInfo?.ServerAddress ?? 0;
                                            networkData.NativeObject = wifiManager.ConnectionInfo;
                                            tcs.TrySetResult(networkData);
                                        }
                                    }
                                }
                            },
                            NetworkUnavailable = () => tcs.TrySetResult(new NetworkData())
                        };
                    }

                    if (networkRequest != null)
                        connectivityManager?.RegisterNetworkCallback(networkRequest, networkCallback);

                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                    var networkTask = tcs.Task.WaitAsync(cancellationToken);
                    var completedTask = await Task.WhenAny(networkTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        response.ErrorCode = WifiErrorCodes.OperationTimeout;
                        response.ErrorMessage = "Wi-Fi network suggestion Timeout.";
                        tcs.TrySetResult(new NetworkData());
                    }

                    connectivityManager?.UnregisterNetworkCallback(networkCallback);
                    networkData = await networkTask;

                    if (networkData != null && networkData.StatusId == (int)WifiErrorCodes.Success)
                    {
                        response.ErrorCode = WifiErrorCodes.Success;
                        response.ErrorMessage = "Wi-Fi network suggestion added successfully.";
                        response.Data = networkData;
                    }
                }
                catch (System.OperationCanceledException)
                {
                    return CreateCanceledResponse<NetworkData>(nameof(AddWifiSuggestion));
                }
                catch (Exception ex)
                {
                    response.ErrorCode = WifiErrorCodes.UnknownError;
                    response.ErrorMessage = $"Error while adding Wi-Fi suggestion: {ex.Message}";
                }
            }
            return response;
        }

        public async Task<WifiManagerResponse<NetworkData>> RequestNetwork(WifiManager wifiManager, string ssid, string password, WifiConnectionOptions options, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return CreateCanceledResponse<NetworkData>(nameof(RequestNetwork));

            var response = new WifiManagerResponse<NetworkData>();
            var networkData = new NetworkData();

            if (wifiManager == null)
            {
                response.ErrorCode = WifiErrorCodes.UnsupportedHardware;
                response.ErrorMessage = "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.";
                return response;
            }

            if (!wifiManager.IsWifiEnabled)
            {
                response.ErrorCode = WifiErrorCodes.WifiNotEnabled;
                response.ErrorMessage = "Wi-Fi is turned off. Please enable Wi-Fi to proceed.";
                return response;
            }

            try
            {
                TaskCompletionSource<NetworkData> tcs = new();
                if (OperatingSystem.IsAndroidVersionAtLeast(29) && !OperatingSystem.IsAndroidVersionAtLeast(30))
                {
                    var specifierBuilder = new WifiNetworkSpecifier.Builder()
                        .SetSsid(ssid);

                    if (options.IsHidden)
                        specifierBuilder.SetIsHiddenSsid(true);

                    if (!string.IsNullOrWhiteSpace(options.Bssid))
                        specifierBuilder.SetBssid(MacAddress.FromString(options.Bssid));

                    if (options.SecurityType == WifiSecurity.Wpa3Sae)
                        specifierBuilder.SetWpa3Passphrase(password);
                    else if (options.SecurityType == WifiSecurity.Open)
                        ; // open network, no passphrase
                    else
                        specifierBuilder.SetWpa2Passphrase(password);

                    var specifier = specifierBuilder.Build();
                    var request = new NetworkRequest.Builder()?
                        .AddTransportType(TransportType.Wifi)?
                        .SetNetworkSpecifier(specifier)?
                        .Build();

                    var networkCallback = new NetworkCallback
                    {
                        NetworkAvailable = network => { },
                        NetworkUnavailable = () => tcs.TrySetResult(new NetworkData()),
                        OnNetworkCapabilitiesChanged = (network, networkCapabilities) =>
                        {
                            if (!OperatingSystem.IsAndroidVersionAtLeast(31) && OperatingSystem.IsAndroidVersionAtLeast(23))
                            {
                                if (networkCapabilities.HasCapability(NetCapability.Validated) && (string.IsNullOrWhiteSpace(options.Bssid) || string.Equals(wifiManager.ConnectionInfo?.BSSID, options.Bssid, StringComparison.OrdinalIgnoreCase)))
                                {
                                    networkData.StatusId = (int)WifiErrorCodes.Success;
                                    networkData.Ssid = wifiManager.ConnectionInfo?.SSID?.Trim(_TrimChars);
                                    networkData.Bssid = wifiManager.ConnectionInfo?.BSSID;
                                    networkData.SignalStrength = wifiManager.ConnectionInfo?.Rssi;
                                    networkData.RssiDbm = wifiManager.ConnectionInfo?.Rssi;
                                    networkData.LinkSpeedMbps = wifiManager.ConnectionInfo?.LinkSpeed;
                                    networkData.IpAddress = wifiManager.DhcpInfo?.IpAddress ?? 0;
                                    networkData.GatewayAddress = wifiManager.DhcpInfo?.Gateway ?? 0;
                                    networkData.DhcpServerAddress = wifiManager.DhcpInfo?.ServerAddress ?? 0;
                                    networkData.NativeObject = wifiManager.ConnectionInfo;
                                    tcs.TrySetResult(networkData);
                                }
                            }
                        }
                    };

                    UnregisterNetworkCallback(networkCallback);

                    _ConnectivityManager = _Context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
                    if (_Requested)
                        _ConnectivityManager?.UnregisterNetworkCallback(networkCallback);

                    if (request != null)
                    {
                        _ConnectivityManager?.RequestNetwork(request, networkCallback);
                        _Requested = true;
                    }

                    networkData = await tcs.Task.WaitAsync(cancellationToken);
                    if (networkData != null && networkData.StatusId == 1)
                    {
                        response.ErrorCode = WifiErrorCodes.Success;
                        response.ErrorMessage = "Wi-Fi connected successfully.";
                        response.Data = networkData;
                    }
                    else
                    {
                        response.ErrorCode = WifiErrorCodes.UnknownError;
                        response.ErrorMessage = "Wi-Fi connection failed.";
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                return CreateCanceledResponse<NetworkData>(nameof(RequestNetwork));
            }
            catch (Exception ex)
            {
                response.ErrorCode = WifiErrorCodes.UnknownError;
                response.ErrorMessage = $"Error while requesting Wi-Fi network: {ex.Message}";
            }
            return response;
        }

        private void UnregisterNetworkCallback(NetworkCallback? networkCallback)
        {
            if (networkCallback != null)
            {
                try
                {
                    _ConnectivityManager = _Context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
                    _ConnectivityManager?.UnregisterNetworkCallback(networkCallback);
                }
                catch
                {
                    networkCallback = null;
                }
            }
        }

        public void Dispose()
        {
            _ = StopScanningAsync();
            StopMonitoring();
        }

        private async Task RunScanSessionAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var scanResponse = ScanWifiNetworksInternal(cancellationToken);
                    if (scanResponse.ErrorCode == WifiErrorCodes.Success && scanResponse.Data != null)
                    {
                        foreach (var network in scanResponse.Data)
                        {
                            var key = BuildNetworkKey(network);
                            if (string.IsNullOrWhiteSpace(key)) continue;

                            if (_DiscoveredNetworkKeys.Add(key))
                                _DeviceDiscovered?.Invoke(this, CloneNetworkData(network)!);
                        }
                    }
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
            catch (System.OperationCanceledException) { }
            finally
            {
                lock (_ScanLock)
                {
                    IsScanning = false;
                    _ScanSessionCts?.Dispose();
                    _ScanSessionCts = null;
                    _ScanSessionTask = null;
                    _DiscoveredNetworkKeys.Clear();
                }
            }
        }

        private static string BuildNetworkKey(NetworkData network)
        {
            var ssid = network.Ssid?.Trim() ?? string.Empty;
            var bssid = network.Bssid?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ssid) && string.IsNullOrWhiteSpace(bssid)) return string.Empty;
            return string.Concat(ssid, "|", bssid);
        }

        private static WifiFrequencyBand GetBandFromFrequency(int frequencyMhz)
        {
            if (frequencyMhz >= 2400 && frequencyMhz < 2500) return WifiFrequencyBand.Band2_4GHz;
            if (frequencyMhz >= 4900 && frequencyMhz < 5925) return WifiFrequencyBand.Band5GHz;
            if (frequencyMhz >= 5925 && frequencyMhz < 7125) return WifiFrequencyBand.Band6GHz;
            return WifiFrequencyBand.Unknown;
        }

        private static int? GetChannelFromFrequency(int frequencyMhz)
        {
            if (frequencyMhz == 2484) return 14;
            if (frequencyMhz >= 2412 && frequencyMhz <= 2484) return (frequencyMhz - 2412) / 5 + 1;
            if (frequencyMhz >= 5180 && frequencyMhz <= 5885) return (frequencyMhz - 5000) / 5;
            if (frequencyMhz >= 5955 && frequencyMhz <= 7115) return (frequencyMhz - 5955) / 5 + 1;
            return null;
        }

        private static string? GetSubnetMaskFromPrefixLength(int prefixLength)
        {
            if (prefixLength < 0 || prefixLength > 32) return null;
            uint mask = prefixLength == 0 ? 0u : ~((1u << (32 - prefixLength)) - 1);
            return $"{(mask >> 24) & 0xFF}.{(mask >> 16) & 0xFF}.{(mask >> 8) & 0xFF}.{mask & 0xFF}";
        }

        private static void PopulateNetworkInterfaceExtendedData(NetworkData networkData)
        {
            try
            {
                var wifiInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(iface =>
                        iface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                        (iface.OperationalStatus == OperationalStatus.Up || iface.OperationalStatus == OperationalStatus.Unknown));

                if (wifiInterface == null) return;

                var ipProps = wifiInterface.GetIPProperties();

                var ipv6 = ipProps.UnicastAddresses
                    .Where(u => u.Address.AddressFamily == AddressFamily.InterNetworkV6
                             && !u.Address.IsIPv6LinkLocal
                             && !IPAddress.IsLoopback(u.Address))
                    .Select(u => u.Address.ToString())
                    .FirstOrDefault();
                networkData.IPv6Address ??= ipv6;

                // DnsAddresses is not supported via NetworkInterface on Android.
                // DNS is populated via LinkProperties in the API 31+ GetNetworkInfoAsync path.

                if (networkData.SubnetMask == null)
                {
                    var ipv4 = ipProps.UnicastAddresses
                        .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);
                    if (ipv4?.IPv4Mask != null)
                        networkData.SubnetMask = ipv4.IPv4Mask.ToString();
                }
            }
            catch { }
        }

        private static int GetIpAddressFromBytes(byte[] address)
        {
            try
            {
                return (address[3] & 0xFF) << 24 |
                       (address[2] & 0xFF) << 16 |
                       (address[1] & 0xFF) << 8 |
                       (address[0] & 0xFF);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Invalid IP address: {ex.Message}");
            }
            return 0;
        }

        private static int GetIpAddressFromInetAddress(Java.Net.InetAddress? address)
        {
            var bytes = address?.GetAddress();
            return bytes is { Length: 4 } ? GetIpAddressFromBytes(bytes) : 0;
        }

        private static WifiManagerResponse<T> CreateCanceledResponse<T>(string operationName)
        {
            return WifiManagerResponse<T>.ErrorResponse(
                WifiErrorCodes.OperationCanceled,
                $"{operationName} operation was canceled.");
        }

        private void EnsureMonitoringStarted()
        {
            lock (_MonitorLock)
            {
                if (_IsMonitoring) return;
                NetworkChange.NetworkAddressChanged += OnNetworkChanged;
                NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
                _IsMonitoring = true;
            }
            _ = RefreshSnapshotAsync(raiseEvent: false);
        }

        private void StopMonitoring()
        {
            lock (_MonitorLock)
            {
                if (!_IsMonitoring) return;
                NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
                NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
                _IsMonitoring = false;
                _LastKnownNetwork = null;
            }
        }

        private void OnNetworkChanged(object? sender, EventArgs e) => _ = RefreshSnapshotAsync(raiseEvent: true);
        private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) => _ = RefreshSnapshotAsync(raiseEvent: true);

        private async Task RefreshSnapshotAsync(bool raiseEvent)
        {
            NetworkData? currentNetwork = null;
            try
            {
                var info = await GetNetworkInfoAsync();
                if (info.ErrorCode == WifiErrorCodes.Success && info.Data != null)
                    currentNetwork = CloneNetworkData(info.Data);
            }
            catch { currentNetwork = null; }

            NetworkData? oldNetwork;
            bool changed;
            lock (_MonitorLock)
            {
                oldNetwork = CloneNetworkData(_LastKnownNetwork);
                changed = !AreSameNetwork(_LastKnownNetwork, currentNetwork);
                _LastKnownNetwork = CloneNetworkData(currentNetwork);
            }

            if (raiseEvent && changed)
                _WifiNetworkChanged?.Invoke(this, new WifiNetworkChangedEventArgs(oldNetwork, CloneNetworkData(currentNetwork)));
        }

        private static bool AreSameNetwork(NetworkData? first, NetworkData? second)
        {
            if (first == null && second == null) return true;
            if (first == null || second == null) return false;
            return string.Equals(first.Ssid, second.Ssid, StringComparison.Ordinal)
                && string.Equals(first.Bssid?.ToString(), second.Bssid?.ToString(), StringComparison.Ordinal)
                && first.IpAddress == second.IpAddress;
        }

        private static NetworkData? CloneNetworkData(NetworkData? source)
        {
            if (source == null) return null;
            return new NetworkData
            {
                StatusId = source.StatusId,
                Ssid = source.Ssid,
                IpAddress = source.IpAddress,
                GatewayAddress = source.GatewayAddress,
                DhcpServerAddress = source.DhcpServerAddress,
                NativeObject = source.NativeObject,
                Bssid = source.Bssid,
                SignalStrength = source.SignalStrength,
                SecurityType = source.SecurityType,
                IPv6Address = source.IPv6Address,
                DnsAddresses = source.DnsAddresses != null ? new List<string>(source.DnsAddresses) : null,
                SubnetMask = source.SubnetMask,
                RssiDbm = source.RssiDbm,
                FrequencyBand = source.FrequencyBand,
                ChannelNumber = source.ChannelNumber,
                LinkSpeedMbps = source.LinkSpeedMbps,
            };
        }
    }

    public class NetworkCallback : ConnectivityManager.NetworkCallback
    {
        public Action<Network>? NetworkAvailable { get; set; }
        public Action? NetworkUnavailable { get; set; }
        public Action<Network, NetworkCapabilities>? OnNetworkCapabilitiesChanged { get; set; }

        [SupportedOSPlatform("android31.0")]
        public NetworkCallback(int flags) : base(flags) { }

        public NetworkCallback() { }

        public override void OnAvailable(Network network)
        {
            base.OnAvailable(network);
            NetworkAvailable?.Invoke(network);
        }

        public override void OnUnavailable()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
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
        [IntDefinition(null, JniField = "")]
        None = 0x0,
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
        public override void OnReceive(Context? context, Intent? intent) { }
    }
}

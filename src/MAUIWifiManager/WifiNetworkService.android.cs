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
using WifiStandard = MauiWifiManager.Abstractions.WifiStandard;
using WifiEapMethod = MauiWifiManager.Abstractions.WifiEapMethod;
using WifiPhase2Method = MauiWifiManager.Abstractions.WifiPhase2Method;

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
        private EventHandler<NetworkData>? _DeviceLost;
        private readonly object _MonitorLock = new();
        private readonly object _ScanLock = new();
        private NetworkData? _LastKnownNetwork;
        private CancellationTokenSource? _ScanSessionCts;
        private Task? _ScanSessionTask;
        private bool _IsMonitoring;
        private EventHandler<WifiStateChangedEventArgs>? _WifiStateChanged;
        private EventHandler<WifiSignalChangedEventArgs>? _SignalStrengthChanged;
        private IntentReceiver? _WifiStateReceiver;
        private IntentReceiver? _RssiReceiver;
        private ConnectivityManager.NetworkCallback? _LocalOnlyCallback;
        private EventHandler<WifiScanResultsEventArgs>? _ScanResultsAvailable;
        private IntentReceiver? _ScanResultsReceiver;
        // Times of scans this app started, to tell when Android's 4-per-2-minutes quota is used up.
        private static readonly Queue<DateTime> _ScanRequests = new();
        // Android 10 has no API to read back suggestions, so the ones added in this process are kept to support removal.
        [SupportedOSPlatform("android29.0")]
        private static readonly Dictionary<string, WifiNetworkSuggestion> _AddedSuggestions = new();

        private static readonly TimeSpan DefaultScanSessionDuration = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ScanQuotaWindow = TimeSpan.FromMinutes(2);
        private const int ScanQuota = 4;
        private static readonly TimeSpan ScanResultsTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan FreshScanInterval = TimeSpan.FromSeconds(30);

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

        public event EventHandler<NetworkData>? DeviceLost
        {
            add => _DeviceLost += value;
            remove => _DeviceLost -= value;
        }

        public event EventHandler<WifiStateChangedEventArgs>? WifiStateChanged
        {
            add
            {
                lock (_MonitorLock)
                {
                    _WifiStateChanged += value;
                    _WifiStateReceiver ??= RegisterIntentReceiver(WifiManager.WifiStateChangedAction, OnWifiStateIntent);
                }
            }
            remove
            {
                lock (_MonitorLock)
                {
                    _WifiStateChanged -= value;
                    if (_WifiStateChanged == null)
                        UnregisterIntentReceiver(ref _WifiStateReceiver);
                }
            }
        }

        public event EventHandler<WifiSignalChangedEventArgs>? SignalStrengthChanged
        {
            add
            {
                lock (_MonitorLock)
                {
                    _SignalStrengthChanged += value;
                    _RssiReceiver ??= RegisterIntentReceiver(WifiManager.RssiChangedAction, OnRssiIntent);
                }
            }
            remove
            {
                lock (_MonitorLock)
                {
                    _SignalStrengthChanged -= value;
                    if (_SignalStrengthChanged == null)
                        UnregisterIntentReceiver(ref _RssiReceiver);
                }
            }
        }

        public event EventHandler<WifiScanResultsEventArgs>? ScanResultsAvailable
        {
            add
            {
                lock (_MonitorLock)
                {
                    _ScanResultsAvailable += value;
                    _ScanResultsReceiver ??= RegisterIntentReceiver(WifiManager.ScanResultsAvailableAction, OnScanResultsIntent);
                }
            }
            remove
            {
                lock (_MonitorLock)
                {
                    _ScanResultsAvailable -= value;
                    if (_ScanResultsAvailable == null)
                        UnregisterIntentReceiver(ref _ScanResultsReceiver);
                }
            }
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

            if (options.IsSsidPrefix)
            {
                var resolved = await ResolveSsidPrefixAsync(ssid, cancellationToken);
                if (resolved == null)
                    return WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, $"No network starting with \"{ssid}\" was found.");
                ssid = resolved;
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
                    if (OperatingSystem.IsAndroidVersionAtLeast(30) && wifiManager.ConnectionInfo is { } connectionInfo)
                        networkData.WifiStandard = (WifiStandard)(int)connectionInfo.WifiStandard;

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
                                if (OperatingSystem.IsAndroidVersionAtLeast(30))
                                    networkData.WifiStandard = (WifiStandard)(int)wifiInfo!.WifiStandard;

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

        public async Task<WifiManagerResponse<List<NetworkData>>> ScanWifiNetworksAsync(CancellationToken cancellationToken = default)
        {
            CheckInit(_Context);
            bool fresh = true;
            try
            {
                if (_Context.GetSystemService(Context.WifiService) is WifiManager { IsWifiEnabled: true } wifiManager)
                    fresh = await RequestFreshScanAsync(wifiManager, cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                return CreateCanceledResponse<List<NetworkData>>(nameof(ScanWifiNetworks));
            }
            var response = ScanWifiNetworksInternal(cancellationToken);
            // Still a success so existing callers keep working; the message tells them the list may be old.
            if (!fresh && response.ErrorCode == WifiErrorCodes.Success)
                response.ErrorMessage = "Android did not run a new scan (throttled or not allowed), so these are the previous results. Check CanStartScanAsync for the reason.";
            return response;
        }

        /// <summary>
        /// Starts a scan and waits for SCAN_RESULTS_AVAILABLE so ScanResults is fresh. StartScan only queues the scan;
        /// reading ScanResults right after it returns the previous (cached) list.
        /// When the OS throttles the request (4 per 2 min in foreground) the cached results are used as-is.
        /// </summary>
        private static async Task<bool> RequestFreshScanAsync(WifiManager wifiManager, CancellationToken cancellationToken)
        {
            var scanCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var receiver = new WifiScanReceiver(() => scanCompleted.TrySetResult());
            var filter = new IntentFilter(WifiManager.ScanResultsAvailableAction);
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
                _Context.RegisterReceiver(receiver, filter, ReceiverFlags.NotExported);
            else
                _Context.RegisterReceiver(receiver, filter);

            try
            {
#pragma warning disable CA1422 // Deprecated in API 28 but still the only way to request a scan.
                if (!wifiManager.StartScan())
                    return false;
#pragma warning restore CA1422
                lock (_ScanRequests) _ScanRequests.Enqueue(DateTime.UtcNow);
                await scanCompleted.Task.WaitAsync(ScanResultsTimeout, cancellationToken);
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
            finally
            {
                _Context.UnregisterReceiver(receiver);
            }
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
                            ChannelWidthMhz = OperatingSystem.IsAndroidVersionAtLeast(23) ? GetChannelWidthMhz((int)result.ChannelWidth) : null,
                            WifiStandard = OperatingSystem.IsAndroidVersionAtLeast(30) ? (WifiStandard)(int)result.WifiStandard : WifiStandard.Unknown,
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

                _ScanSessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (!cancellationToken.CanBeCanceled)
                    _ScanSessionCts.CancelAfter(DefaultScanSessionDuration);
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

        public Task<WifiManagerResponse<bool>> IsWifiEnabledAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(CreateCanceledResponse<bool>(nameof(IsWifiEnabledAsync)));

            if (_Context.GetSystemService(Context.WifiService) is not WifiManager wifiManager)
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability."));

            bool enabled = wifiManager.IsWifiEnabled;
            return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(enabled, enabled ? "Wi-Fi is enabled." : "Wi-Fi is disabled."));
        }

        public async Task<WifiManagerResponse<bool>> RequestPermissionsAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return CreateCanceledResponse<bool>(nameof(RequestPermissionsAsync));

            try
            {
                var status = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.LocationWhenInUse>);
                if (status != PermissionStatus.Granted)
                    return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.PermissionDenied, "Location permission was denied.");

                if (OperatingSystem.IsAndroidVersionAtLeast(33))
                {
                    status = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.NearbyWifiDevices>);
                    if (status != PermissionStatus.Granted)
                        return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.PermissionDenied, "Nearby Wi-Fi devices permission was denied.");
                }

                // SSIDs and scan results stay hidden while location services are off, even with the permission granted.
                if (!IsLocationEnabled())
                    return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.PermissionDenied, "Location services are turned off. Turn them on to scan and read Wi-Fi details.");

                return WifiManagerResponse<bool>.SuccessResponse(true, "All Wi-Fi permissions are granted.");
            }
            catch (PermissionException ex)
            {
                // Thrown when the permission is missing from AndroidManifest.xml.
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.PermissionDenied, ex.Message);
            }
        }

        public Task<WifiManagerResponse<bool>> IsMeteredConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(CreateCanceledResponse<bool>(nameof(IsMeteredConnectionAsync)));

            if (_Context.GetSystemService(Context.ConnectivityService) is not ConnectivityManager connectivityManager)
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "Connectivity service is not available on this device."));

            bool metered = connectivityManager.IsActiveNetworkMetered;
            return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(metered, metered ? "Connection is metered." : "Connection is not metered."));
        }

        public Task<WifiManagerResponse<List<string>>> GetSavedNetworksAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(CreateCanceledResponse<List<string>>(nameof(GetSavedNetworksAsync)));

            if (_Context.GetSystemService(Context.WifiService) is not WifiManager wifiManager)
                return Task.FromResult(WifiManagerResponse<List<string>>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability."));

            try
            {
                IEnumerable<string?> ssids;
                if (OperatingSystem.IsAndroidVersionAtLeast(30))
#pragma warning disable CA1416 // Lambda runs inside the API 30 check above.
                    ssids = wifiManager.NetworkSuggestions.Select(s => s.Ssid);
#pragma warning restore CA1416
                else if (OperatingSystem.IsAndroidVersionAtLeast(29))
                    lock (_AddedSuggestions) ssids = _AddedSuggestions.Keys.ToList();
                else
#pragma warning disable CA1422 // Deprecated in API 29; the only way to list networks on older versions.
                    ssids = wifiManager.ConfiguredNetworks?.Select(c => c.Ssid) ?? Enumerable.Empty<string?>();
#pragma warning restore CA1422

                var result = ssids
                    .Select(s => s?.Trim(_TrimChars))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => s!)
                    .Distinct()
                    .ToList();
                return Task.FromResult(WifiManagerResponse<List<string>>.SuccessResponse(result, $"Found {result.Count} saved network(s)."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(WifiManagerResponse<List<string>>.ErrorResponse(WifiErrorCodes.UnknownError, $"Error while reading saved networks: {ex.Message}"));
            }
        }

        public Task<WifiManagerResponse<bool>> ForgetNetworkAsync(string ssid, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(CreateCanceledResponse<bool>(nameof(ForgetNetworkAsync)));

            if (_Context.GetSystemService(Context.WifiService) is not WifiManager wifiManager)
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability."));

            try
            {
                bool removed;
                if (OperatingSystem.IsAndroidVersionAtLeast(29))
                    removed = RemoveSuggestions(wifiManager, ssid);
                else
                {
#pragma warning disable CA1422 // Deprecated in API 29; the only way to remove networks on older versions.
                    var config = wifiManager.ConfiguredNetworks?.FirstOrDefault(c => c.Ssid?.Trim(_TrimChars) == ssid);
                    removed = config != null && wifiManager.RemoveNetwork(config.NetworkId);
                    if (removed)
                        wifiManager.SaveConfiguration();
#pragma warning restore CA1422
                }

                return Task.FromResult(removed
                    ? WifiManagerResponse<bool>.SuccessResponse(true, "Network removed.")
                    : WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, "No network with this SSID was saved by this app."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, $"Error while removing network: {ex.Message}"));
            }
        }

        public async Task<WifiManagerResponse<bool>> AddNetworkSuggestionAsync(string ssid, string password, WifiConnectionOptions options, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return CreateCanceledResponse<bool>(nameof(AddNetworkSuggestionAsync));

            if (!OperatingSystem.IsAndroidVersionAtLeast(29))
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "Network suggestions require Android 10 (API 29) or later.");

            if (_Context.GetSystemService(Context.WifiService) is not WifiManager wifiManager)
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.");

            if (options.IsSsidPrefix)
            {
                var resolved = await ResolveSsidPrefixAsync(ssid, cancellationToken);
                if (resolved == null)
                    return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, $"No network starting with \"{ssid}\" was found.");
                ssid = resolved;
            }

            try
            {
                return SaveSuggestion(wifiManager, ssid, CreateSuggestionBuilder(ssid, password, options).Build());
            }
            catch (Exception ex)
            {
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, $"Error while adding network suggestion: {ex.Message}");
            }
        }

        public async Task<WifiManagerResponse<NetworkData>> ConnectEnterpriseWifiAsync(string ssid, WifiEnterpriseOptions options, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return CreateCanceledResponse<NetworkData>(nameof(ConnectEnterpriseWifiAsync));

            if (_Context.GetSystemService(Context.WifiService) is not WifiManager wifiManager)
                return WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.");

            try
            {
                var enterpriseConfig = CreateEnterpriseConfig(options);
                if (OperatingSystem.IsAndroidVersionAtLeast(29))
                {
                    var builder = new WifiNetworkSuggestion.Builder().SetSsid(ssid);
                    if (options.IsHidden)
                        builder.SetIsHiddenSsid(true);
                    if (!options.UseWpa3)
                        builder.SetWpa2EnterpriseConfig(enterpriseConfig);
                    else if (OperatingSystem.IsAndroidVersionAtLeast(31))
                        builder.SetWpa3EnterpriseStandardModeConfig(enterpriseConfig);
                    else
#pragma warning disable CA1422 // Replaced by SetWpa3EnterpriseStandardModeConfig in API 31.
                        builder.SetWpa3EnterpriseConfig(enterpriseConfig);
#pragma warning restore CA1422

                    var saved = SaveSuggestion(wifiManager, ssid, builder.Build());
                    if (saved.ErrorCode != WifiErrorCodes.Success)
                        return WifiManagerResponse<NetworkData>.ErrorResponse(saved.ErrorCode ?? WifiErrorCodes.UnknownError, saved.ErrorMessage ?? string.Empty);
                }
                else
                {
#pragma warning disable CA1422 // Deprecated in API 29; the only way to add networks on older versions.
                    var config = new WifiConfiguration
                    {
                        Ssid = $"\"{ssid}\"",
                        HiddenSSID = options.IsHidden,
                        EnterpriseConfig = enterpriseConfig
                    };
                    config.AllowedKeyManagement?.Set((int)KeyManagementType.WpaEap);
                    config.AllowedKeyManagement?.Set((int)KeyManagementType.Ieee8021x);
                    int netId = wifiManager.AddNetwork(config);
                    if (netId == -1)
                        return WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.UnknownError, "Android rejected the enterprise network configuration.");
                    wifiManager.Disconnect();
                    wifiManager.EnableNetwork(netId, true);
                    wifiManager.Reconnect();
#pragma warning restore CA1422
                }

                return await WaitForConnectionAsync(wifiManager, ssid, cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                return CreateCanceledResponse<NetworkData>(nameof(ConnectEnterpriseWifiAsync));
            }
            catch (Exception ex)
            {
                return WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.UnknownError, $"Error connecting to enterprise Wi-Fi: {ex.Message}");
            }
        }

        public async Task<WifiManagerResponse<NetworkData>> ConnectLocalOnlyAsync(string ssid, string password, WifiConnectionOptions options, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return CreateCanceledResponse<NetworkData>(nameof(ConnectLocalOnlyAsync));

            if (!OperatingSystem.IsAndroidVersionAtLeast(23))
                return WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "Binding to a local-only network requires Android 6.0 (API 23) or later.");

            if (_Context.GetSystemService(Context.ConnectivityService) is not ConnectivityManager connectivityManager)
                return WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "Connectivity service is not available on this device.");

            if (options.IsSsidPrefix)
            {
                var resolved = await ResolveSsidPrefixAsync(ssid, cancellationToken);
                if (resolved == null)
                    return WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, $"No network starting with \"{ssid}\" was found.");
                ssid = resolved;
            }

            await ReleaseLocalOnlyAsync();

            try
            {
                // Removing INTERNET lets the request match an access point that has no internet access.
                var requestBuilder = new NetworkRequest.Builder()
                    .AddTransportType(TransportType.Wifi)!
                    .RemoveCapability(NetCapability.Internet)!;

                if (OperatingSystem.IsAndroidVersionAtLeast(29))
                    requestBuilder.SetNetworkSpecifier(CreateSpecifier(ssid, password, options));
                else
                {
                    // ponytail: no specifiers before Android 10, so join the legacy way and bind to whichever Wi-Fi network comes up.
                    var joined = await ConnectWifiAsync(ssid, password, options, cancellationToken);
                    if (joined.ErrorCode != WifiErrorCodes.Success)
                        return joined;
                }

                var tcs = new TaskCompletionSource<Network?>(TaskCreationOptions.RunContinuationsAsynchronously);
                var callback = new NetworkCallback
                {
                    NetworkAvailable = network =>
                    {
#pragma warning disable CA1416 // Lambda runs after the API 23 check above.
                        connectivityManager.BindProcessToNetwork(network);
#pragma warning restore CA1416
                        tcs.TrySetResult(network);
                    },
                    NetworkUnavailable = () => tcs.TrySetResult(null)
                };
                _LocalOnlyCallback = callback;
                connectivityManager.RequestNetwork(requestBuilder.Build()!, callback);

                Network? network;
                bool timedOut = false;
                try
                {
                    network = await tcs.Task.WaitAsync(options.Timeout ?? ConnectTimeout, cancellationToken);
                }
                catch (TimeoutException)
                {
                    network = null;
                    timedOut = true;
                }

                if (network == null)
                {
                    await ReleaseLocalOnlyAsync();
                    return timedOut
                        ? WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.OperationTimeout, "Timed out waiting for the network; it may be out of range or the user has not approved the request.")
                        : WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, "The network was not found or the user declined the connection request.");
                }

                var networkData = new NetworkData { StatusId = (int)WifiErrorCodes.Success, Ssid = ssid };
                if (OperatingSystem.IsAndroidVersionAtLeast(29) && connectivityManager.GetNetworkCapabilities(network)?.TransportInfo is WifiInfo wifiInfo)
                {
                    networkData.Bssid = wifiInfo.BSSID;
                    networkData.SignalStrength = wifiInfo.Rssi;
                    networkData.RssiDbm = wifiInfo.Rssi;
                    networkData.LinkSpeedMbps = wifiInfo.LinkSpeed;
                    networkData.NativeObject = wifiInfo;
                }
                var linkProperties = connectivityManager.GetLinkProperties(network);
                networkData.IpAddress = GetIpAddressFromInetAddress(linkProperties?.LinkAddresses
                    .Select(la => la.Address)
                    .FirstOrDefault(a => a is Java.Net.Inet4Address));
                var gateway = linkProperties?.Routes.FirstOrDefault(r => r.IsDefaultRoute && r.Gateway is Java.Net.Inet4Address)?.Gateway;
                networkData.GatewayAddress = GetIpAddressFromInetAddress(gateway);

                return WifiManagerResponse<NetworkData>.SuccessResponse(networkData, "Connected; app traffic now goes through this network.");
            }
            catch (System.OperationCanceledException)
            {
                await ReleaseLocalOnlyAsync();
                return CreateCanceledResponse<NetworkData>(nameof(ConnectLocalOnlyAsync));
            }
            catch (Exception ex)
            {
                await ReleaseLocalOnlyAsync();
                return WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.UnknownError, $"Error connecting to local-only network: {ex.Message}");
            }
        }

        public Task<WifiManagerResponse<bool>> ReleaseLocalOnlyAsync(CancellationToken cancellationToken = default)
        {
            var callback = Interlocked.Exchange(ref _LocalOnlyCallback, null);
            if (callback == null)
                return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(false, "No local-only network to release."));

            var connectivityManager = _Context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
            if (OperatingSystem.IsAndroidVersionAtLeast(23))
                connectivityManager?.BindProcessToNetwork(null);
            try
            {
                // Unregistering drops the request, so Android returns to the previous network.
                connectivityManager?.UnregisterNetworkCallback(callback);
            }
            catch (Java.Lang.IllegalArgumentException) { }

            return Task.FromResult(WifiManagerResponse<bool>.SuccessResponse(true, "Local-only network released."));
        }

        private static async Task<WifiManagerResponse<NetworkData>> WaitForConnectionAsync(WifiManager wifiManager, string ssid, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ConnectTimeout);
            try
            {
                while (true)
                {
#pragma warning disable CA1422 // Deprecated in API 31 but still reports the connected SSID; avoids registering a callback per poll.
                    var info = wifiManager.ConnectionInfo;
#pragma warning restore CA1422
                    if (info != null && info.SupplicantState == SupplicantState.Completed && info.SSID?.Trim(_TrimChars) == ssid)
                    {
                        var data = new NetworkData
                        {
                            StatusId = (int)WifiErrorCodes.Success,
                            Ssid = ssid,
                            Bssid = info.BSSID,
                            SignalStrength = info.Rssi,
                            RssiDbm = info.Rssi,
                            LinkSpeedMbps = info.LinkSpeed,
                            NativeObject = info
                        };
                        PopulateNetworkInterfaceExtendedData(data);
                        return WifiManagerResponse<NetworkData>.SuccessResponse(data, "Connected to enterprise Wi-Fi.");
                    }
                    await Task.Delay(TimeSpan.FromSeconds(1), timeoutCts.Token);
                }
            }
            catch (System.OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.OperationTimeout,
                    "Network saved, but the device has not connected yet. Android connects once the user allows the suggestion and the network is in range.");
            }
        }

        private static WifiEnterpriseConfig CreateEnterpriseConfig(WifiEnterpriseOptions options)
        {
            var config = new WifiEnterpriseConfig
            {
                EapMethod = options.EapMethod switch
                {
                    WifiEapMethod.Ttls => Android.Net.Wifi.WifiEapMethod.Ttls,
                    WifiEapMethod.Pwd => Android.Net.Wifi.WifiEapMethod.Pwd,
                    _ => Android.Net.Wifi.WifiEapMethod.Peap
                },
                Phase2Method = options.Phase2Method switch
                {
                    WifiPhase2Method.None => Android.Net.Wifi.WifiPhase2Method.None,
                    WifiPhase2Method.Pap => Android.Net.Wifi.WifiPhase2Method.Pap,
                    WifiPhase2Method.Gtc => Android.Net.Wifi.WifiPhase2Method.Gtc,
                    _ => Android.Net.Wifi.WifiPhase2Method.Mschapv2
                },
                Identity = options.Identity,
                Password = options.Password
            };
            if (!string.IsNullOrEmpty(options.AnonymousIdentity))
                config.AnonymousIdentity = options.AnonymousIdentity;
            if (!string.IsNullOrEmpty(options.ServerDomain) && OperatingSystem.IsAndroidVersionAtLeast(23))
                config.DomainSuffixMatch = options.ServerDomain;
            if (options.CaCertificate != null)
            {
                using var stream = new MemoryStream(options.CaCertificate.RawData);
                config.CaCertificate = Java.Security.Cert.CertificateFactory.GetInstance("X.509")?
                    .GenerateCertificate(stream)?
                    .JavaCast<Java.Security.Cert.X509Certificate>();
            }
            return config;
        }

        [SupportedOSPlatform("android29.0")]
        private static WifiNetworkSuggestion.Builder CreateSuggestionBuilder(string ssid, string psk, WifiConnectionOptions options)
        {
            var builder = new WifiNetworkSuggestion.Builder().SetSsid(ssid);
            if (!string.IsNullOrWhiteSpace(options.Bssid))
                builder.SetBssid(MacAddress.FromString(options.Bssid));
            if (options.IsHidden)
                builder.SetIsHiddenSsid(true);
            if (options.SecurityType == WifiSecurity.Wpa3Sae)
                builder.SetWpa3Passphrase(psk);
            else if (options.SecurityType != WifiSecurity.Open)
                builder.SetWpa2Passphrase(psk);
            return builder;
        }

        [SupportedOSPlatform("android29.0")]
        private static WifiNetworkSpecifier CreateSpecifier(string ssid, string password, WifiConnectionOptions options)
        {
            var builder = new WifiNetworkSpecifier.Builder().SetSsid(ssid);
            if (options.IsHidden)
                builder.SetIsHiddenSsid(true);
            if (!string.IsNullOrWhiteSpace(options.Bssid))
                builder.SetBssid(MacAddress.FromString(options.Bssid));
            if (options.SecurityType == WifiSecurity.Wpa3Sae)
                builder.SetWpa3Passphrase(password);
            else if (options.SecurityType != WifiSecurity.Open)
                builder.SetWpa2Passphrase(password);
            return builder.Build();
        }

        [SupportedOSPlatform("android29.0")]
        private static WifiManagerResponse<bool> SaveSuggestion(WifiManager wifiManager, string ssid, WifiNetworkSuggestion suggestion)
        {
            // Replace any earlier suggestion for this SSID; Android 10 rejects duplicates.
            RemoveSuggestions(wifiManager, ssid);
            int status = (int)wifiManager.AddNetworkSuggestions(new List<WifiNetworkSuggestion> { suggestion });
            if (status == 0)
            {
                lock (_AddedSuggestions) _AddedSuggestions[ssid] = suggestion;
                return WifiManagerResponse<bool>.SuccessResponse(true, "Network suggestion saved. Android connects to it automatically when in range.");
            }

            // STATUS_NETWORK_SUGGESTIONS_ERROR_* codes.
            return status switch
            {
                2 => WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.PermissionDenied, "The user has disallowed network suggestions from this app."),
                4 => WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, "The app has reached the maximum number of network suggestions."),
                _ => WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, $"Android rejected the network suggestion (status {status}).")
            };
        }

        [SupportedOSPlatform("android29.0")]
        private static bool RemoveSuggestions(WifiManager wifiManager, string ssid)
        {
            List<WifiNetworkSuggestion> matches;
            lock (_AddedSuggestions)
            {
                matches = OperatingSystem.IsAndroidVersionAtLeast(30)
#pragma warning disable CA1416 // Lambda runs inside the API 30 check.
                    ? wifiManager.NetworkSuggestions.Where(s => s.Ssid?.Trim(_TrimChars) == ssid).ToList()
#pragma warning restore CA1416
                    : _AddedSuggestions.TryGetValue(ssid, out var added) ? new List<WifiNetworkSuggestion> { added } : new List<WifiNetworkSuggestion>();
                _AddedSuggestions.Remove(ssid);
            }
            return matches.Count > 0 && (int)wifiManager.RemoveNetworkSuggestions(matches) == 0;
        }

        private static IntentReceiver RegisterIntentReceiver(string? action, Action<BroadcastReceiver, Intent> onReceive)
        {
            var receiver = new IntentReceiver(onReceive);
            var filter = new IntentFilter(action);
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
                _Context.RegisterReceiver(receiver, filter, ReceiverFlags.NotExported);
            else
                _Context.RegisterReceiver(receiver, filter);
            return receiver;
        }

        private static void UnregisterIntentReceiver(ref IntentReceiver? receiver)
        {
            if (receiver == null) return;
            try { _Context.UnregisterReceiver(receiver); }
            catch (Java.Lang.IllegalArgumentException) { }
            receiver = null;
        }

        private void OnWifiStateIntent(BroadcastReceiver receiver, Intent intent)
        {
            // Registering delivers the last (sticky) state right away; only report real changes.
            if (receiver.IsInitialStickyBroadcast) return;
            int state = intent.GetIntExtra(WifiManager.ExtraWifiState, -1);
            if (state is not (1 or 3)) return; // WIFI_STATE_DISABLED / WIFI_STATE_ENABLED; skip the transitional states
            _WifiStateChanged?.Invoke(this, new WifiStateChangedEventArgs(state == 3));
        }

        private void OnRssiIntent(BroadcastReceiver receiver, Intent intent)
        {
            int rssi = intent.GetIntExtra(WifiManager.ExtraNewRssi, int.MinValue);
            if (rssi == int.MinValue) return;
            _SignalStrengthChanged?.Invoke(this, new WifiSignalChangedEventArgs(rssi, WifiSignal.GetSignalLevel(rssi), WifiSignal.GetSignalPercentage(rssi)));
        }

        public Task<WifiManagerResponse<WifiScanAvailability>> CanStartScanAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(CreateCanceledResponse<WifiScanAvailability>(nameof(CanStartScanAsync)));

            if (_Context.GetSystemService(Context.WifiService) is not WifiManager wifiManager)
                return Task.FromResult(WifiManagerResponse<WifiScanAvailability>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability."));

            var (state, message) =
                !wifiManager.IsWifiEnabled ? (WifiScanAvailability.WifiDisabled, "Wi-Fi is turned off.")
                : !HasScanPermission() ? (WifiScanAvailability.PermissionDenied, "Location (or nearby Wi-Fi devices) permission is not granted.")
                : !IsLocationEnabled() ? (WifiScanAvailability.LocationServicesDisabled, "Location services are turned off.")
                : IsScanQuotaUsed(wifiManager) ? (WifiScanAvailability.Throttled, "This app used its 4 scans in the last 2 minutes; Android will return cached results.")
                : (WifiScanAvailability.Ready, "A scan can start now.");
            return Task.FromResult(WifiManagerResponse<WifiScanAvailability>.SuccessResponse(state, message));
        }

        private static bool HasScanPermission()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(23))
                return true;
            if (_Context.CheckSelfPermission(Android.Manifest.Permission.AccessFineLocation) == Android.Content.PM.Permission.Granted)
                return true;
            return OperatingSystem.IsAndroidVersionAtLeast(33)
                && _Context.CheckSelfPermission(Android.Manifest.Permission.NearbyWifiDevices) == Android.Content.PM.Permission.Granted;
        }

        private static bool IsLocationEnabled()
        {
            var locationManager = _Context.GetSystemService(Context.LocationService) as Android.Locations.LocationManager;
            return OperatingSystem.IsAndroidVersionAtLeast(28)
                ? locationManager?.IsLocationEnabled == true
                : locationManager?.IsProviderEnabled(Android.Locations.LocationManager.GpsProvider) == true
                  || locationManager?.IsProviderEnabled(Android.Locations.LocationManager.NetworkProvider) == true;
        }

        /// <summary>
        /// Android 9+ allows 4 foreground scans per 2 minutes. Only scans started by this app are counted,
        /// which is the quota Android applies per app.
        /// </summary>
        private static bool IsScanQuotaUsed(WifiManager wifiManager)
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(28))
                return false;
            if (OperatingSystem.IsAndroidVersionAtLeast(30) && !wifiManager.IsScanThrottleEnabled)
                return false; // Throttling turned off in developer options.
            lock (_ScanRequests)
            {
                while (_ScanRequests.Count > 0 && DateTime.UtcNow - _ScanRequests.Peek() > ScanQuotaWindow)
                    _ScanRequests.Dequeue();
                return _ScanRequests.Count >= ScanQuota;
            }
        }

        private void OnScanResultsIntent(BroadcastReceiver receiver, Intent intent)
        {
            bool fresh = !OperatingSystem.IsAndroidVersionAtLeast(23) || intent.GetBooleanExtra(WifiManager.ExtraResultsUpdated, true);
            var scan = ScanWifiNetworksInternal();
            if (scan.ErrorCode == WifiErrorCodes.Success && scan.Data != null)
                _ScanResultsAvailable?.Invoke(this, new WifiScanResultsEventArgs(scan.Data, fresh));
        }

        /// <summary>Scans and returns the strongest network whose SSID starts with <paramref name="prefix"/>.</summary>
        private async Task<string?> ResolveSsidPrefixAsync(string prefix, CancellationToken cancellationToken)
        {
            var scan = await ScanWifiNetworksAsync(cancellationToken);
            return scan.Data?
                .Where(n => n.Ssid?.StartsWith(prefix, StringComparison.Ordinal) == true)
                .OrderByDescending(n => n.RssiDbm ?? int.MinValue)
                .FirstOrDefault()?.Ssid;
        }

        public async Task<WifiManagerResponse<bool>> SetWifiEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return CreateCanceledResponse<bool>(nameof(SetWifiEnabledAsync));

            if (_Context.GetSystemService(Context.WifiService) is not WifiManager wifiManager)
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "Wi-Fi Manager is unavailable. Please ensure the device has Wi-Fi capability.");

            if (wifiManager.IsWifiEnabled == enabled)
                return WifiManagerResponse<bool>.SuccessResponse(true, $"Wi-Fi is already {(enabled ? "on" : "off")}.");

            if (!OperatingSystem.IsAndroidVersionAtLeast(29))
            {
#pragma warning disable CA1422 // Deprecated in API 29; still works on older versions.
                bool changed = wifiManager.SetWifiEnabled(enabled);
#pragma warning restore CA1422
                return changed
                    ? WifiManagerResponse<bool>.SuccessResponse(true, $"Wi-Fi turned {(enabled ? "on" : "off")}.")
                    : WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, "Android refused to change the Wi-Fi state.");
            }

            await OpenWifiSetting();
            return WifiManagerResponse<bool>.SuccessResponse(false, "Android 10+ does not let apps switch Wi-Fi, so the Wi-Fi panel was opened for the user.");
        }

        private static int? GetChannelWidthMhz(int channelWidth) => channelWidth switch
        {
            0 => 20,
            1 => 40,
            2 => 80,
            3 or 4 => 160, // 160 MHz or 80+80 MHz
            5 => 320,
            _ => null
        };

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
                    var suggestionBuilder = CreateSuggestionBuilder(ssid, psk, options)
                        .SetIsUserInteractionRequired(true);

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

                    var timeoutTask = Task.Delay(options.Timeout ?? ConnectTimeout);
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
                    var specifier = CreateSpecifier(ssid, password, options);
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

                    try
                    {
                        networkData = await tcs.Task.WaitAsync(options.Timeout ?? Timeout.InfiniteTimeSpan, cancellationToken);
                    }
                    catch (TimeoutException)
                    {
                        UnregisterNetworkCallback(networkCallback);
                        return WifiManagerResponse<NetworkData>.ErrorResponse(WifiErrorCodes.OperationTimeout, "Timed out waiting for the Wi-Fi connection.");
                    }
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
            _ = ReleaseLocalOnlyAsync();
            lock (_MonitorLock)
            {
                UnregisterIntentReceiver(ref _WifiStateReceiver);
                UnregisterIntentReceiver(ref _RssiReceiver);
                UnregisterIntentReceiver(ref _ScanResultsReceiver);
            }
        }

        private async Task RunScanSessionAsync(CancellationToken cancellationToken)
        {
            var known = new Dictionary<string, NetworkData>(StringComparer.OrdinalIgnoreCase);
            var lastFreshScan = DateTime.MinValue;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Request a real scan only every FreshScanInterval so the OS quota (4 per 2 min) lasts the whole
                    // session; in between, read the cached results, which also pick up scans made by the system.
                    WifiManagerResponse<List<NetworkData>> scanResponse;
                    if (DateTime.UtcNow - lastFreshScan >= FreshScanInterval)
                    {
                        lastFreshScan = DateTime.UtcNow;
                        scanResponse = await ScanWifiNetworksAsync(cancellationToken);
                    }
                    else
                        scanResponse = ScanWifiNetworksInternal(cancellationToken);
                    // Wi-Fi turned off means every network is gone; other errors keep the current list.
                    var networks = scanResponse.ErrorCode == WifiErrorCodes.Success ? scanResponse.Data
                        : scanResponse.ErrorCode == WifiErrorCodes.WifiNotEnabled ? new List<NetworkData>()
                        : null;
                    if (networks != null)
                    {
                        var current = new Dictionary<string, NetworkData>(StringComparer.OrdinalIgnoreCase);
                        foreach (var network in networks)
                        {
                            var key = BuildNetworkKey(network);
                            if (!string.IsNullOrWhiteSpace(key))
                                current[key] = network;
                        }

                        foreach (var (key, network) in known)
                            if (!current.ContainsKey(key))
                                _DeviceLost?.Invoke(this, network);

                        foreach (var (key, network) in current)
                            if (!known.ContainsKey(key))
                                _DeviceDiscovered?.Invoke(this, CloneNetworkData(network)!);

                        known = current;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
            catch (System.OperationCanceledException) { }
            finally
            {
                lock (_ScanLock)
                {
                    // StopScanningAsync may already have cleared this session and a new one may be running.
                    if (_ScanSessionCts != null && _ScanSessionCts.Token == cancellationToken)
                    {
                        IsScanning = false;
                        _ScanSessionCts.Dispose();
                        _ScanSessionCts = null;
                        _ScanSessionTask = null;
                    }
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
                WifiStandard = source.WifiStandard,
                ChannelWidthMhz = source.ChannelWidthMhz,
            };
        }
    }

    internal sealed class IntentReceiver : BroadcastReceiver
    {
        private readonly Action<BroadcastReceiver, Intent> _OnReceive;

        public IntentReceiver(Action<BroadcastReceiver, Intent> onReceive) => _OnReceive = onReceive;

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent != null)
                _OnReceive(this, intent);
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
        private readonly Action? _OnScanCompleted;

        public List<ScanResult> ScanResults { get; private set; }
        public WifiScanReceiver(WifiNetworkService wifiScanner)
        {
            ScanResults = new List<ScanResult>();
        }

        public WifiScanReceiver(Action onScanCompleted) : this((WifiNetworkService)null!)
        {
            _OnScanCompleted = onScanCompleted;
        }

        public override void OnReceive(Context? context, Intent? intent) => _OnScanCompleted?.Invoke();
    }
}

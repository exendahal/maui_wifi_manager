using MauiWifiManager;
using MauiWifiManager.Abstractions;
using System.Diagnostics;

namespace DemoApp;

/// <summary>
/// Runs the library's features against the real device and logs PASS/FAIL/INFO lines.
/// Started from the Tools tab, or headless for device testing:
/// <list type="bullet">
/// <item>Android: <c>adb shell am start -n &lt;activity&gt; --es selftest basic</c>; read with <c>adb logcat -s DOTNET</c>.</item>
/// <item>Windows: write the mode to <c>%USERPROFILE%\wifimanager-selftest.txt</c> and start the app; results go to <c>wifimanager-selftest.log</c> next to it.</item>
/// </list>
/// Modes: <c>basic</c>, <c>scan</c>, <c>mdns[:type]</c>, <c>speed</c>, <c>hotspot-hold:&lt;seconds&gt;</c>, <c>connect-prefix:&lt;prefix&gt;:&lt;password&gt;</c>, <c>timeout</c>, <c>wifi-toggle</c>.
/// </summary>
public static class SelfTest
{
    public const string LogPrefix = "WIFISELFTEST";

    /// <summary>Mode requested at launch (Android intent extra or Windows trigger file), consumed once.</summary>
    public static string? PendingMode { get; set; }

    public static string TriggerFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "wifimanager-selftest.txt");

    public static string LogFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "wifimanager-selftest.log");

    /// <summary>Windows: picks up the trigger file written before launch.</summary>
    public static void ReadTriggerFile()
    {
        if (DeviceInfo.Current.Platform != DevicePlatform.WinUI || !File.Exists(TriggerFile))
            return;
        PendingMode = File.ReadAllText(TriggerFile).Trim();
        File.Delete(TriggerFile);
    }

    public static async Task RunPendingAsync()
    {
        var mode = PendingMode;
        PendingMode = null;
        if (string.IsNullOrWhiteSpace(mode))
            return;
        if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
            File.WriteAllText(LogFile, string.Empty);
        await RunAsync(mode, line =>
        {
            if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
                File.AppendAllText(LogFile, line + Environment.NewLine);
        });
    }

    public static async Task RunAsync(string mode, Action<string>? output = null)
    {
        int pass = 0, fail = 0;
        void Log(string status, string name, string detail)
        {
            if (status == "PASS") pass++;
            if (status == "FAIL") fail++;
            var line = $"{LogPrefix} {status} {name}: {detail}";
            Console.WriteLine(line);
            Debug.WriteLine(line);
            output?.Invoke(line);
        }
        void Check(string name, bool ok, string detail) => Log(ok ? "PASS" : "FAIL", name, detail);
        async Task Step(string name, Func<Task> body)
        {
            try { await body(); }
            catch (Exception ex) { Log("FAIL", name, $"threw {ex.GetType().Name}: {ex.Message}"); }
        }

        Log("INFO", "start", $"mode={mode} platform={DeviceInfo.Current.Platform} os={DeviceInfo.Current.VersionString}");
        var parts = mode.Split(':');
        switch (parts[0])
        {
            case "basic":
                await RunBasicAsync(Log, Check, Step);
                break;
            case "scan":
                await Step("scan", async () =>
                {
                    for (int i = 1; i <= 3; i++)
                    {
                        var sw = Stopwatch.StartNew();
                        var scan = await CrossWifiManager.ScanWifiNetworksAsync();
                        Check($"scan #{i}", scan.ErrorCode == WifiErrorCodes.Success && scan.Data!.Count > 0,
                            $"{scan.ErrorCode} {scan.Data?.Count} networks in {sw.Elapsed.TotalSeconds:0.0}s; {scan.ErrorMessage}");
                    }
                });
                break;
            case "mdns":
                await Step("mdns", async () =>
                {
                    var type = parts.Length > 1 ? parts[1] : "_services._dns-sd._udp";
                    var r = await CrossWifiManager.BrowseServicesAsync(type, TimeSpan.FromSeconds(5));
                    Check("mdns", r.ErrorCode == WifiErrorCodes.Success && r.Data!.Count > 0, $"{r.ErrorMessage}");
                    foreach (var s in r.Data ?? [])
                        Log("INFO", "mdns-service", $"{s.ServiceType} '{s.Name}' {s.HostName} {s.IpAddress}:{s.Port} {string.Join(",", s.Properties.Select(p => p.Key + "=" + p.Value))}");
                });
                break;
            case "speed":
                await Step("speed", async () =>
                {
                    var r = await CrossWifiManager.MeasureSpeedAsync();
                    Check("speed-test", r.ErrorCode == WifiErrorCodes.Success && r.Data!.DownloadMbps > 0,
                        $"{r.ErrorMessage} down={r.Data?.DownloadMbps}Mbps up={r.Data?.UploadMbps}Mbps latency={r.Data?.LatencyMs}ms");

                    // Reference: plain HttpClient download on a pool thread, outside the library.
                    var reference = await Task.Run(async () =>
                    {
                        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                        var sw = Stopwatch.StartNew();
                        var bytes = await client.GetByteArrayAsync("https://speed.cloudflare.com/__down?bytes=10000000");
                        return bytes.Length * 8 / sw.Elapsed.TotalSeconds / 1_000_000;
                    });
                    Log("INFO", "speed-reference", $"plain HttpClient download {reference:0.00} Mbps");
                });
                break;
            case "hotspot-hold":
                await Step("hotspot-hold", async () =>
                {
                    var started = await CrossWifiManager.StartHotspotAsync();
                    Check("hotspot-start", started.ErrorCode == WifiErrorCodes.Success, $"{started.ErrorCode} {started.ErrorMessage} ssid={started.Data?.Ssid} password={started.Data?.Password}");
                    if (started.ErrorCode != WifiErrorCodes.Success) return;
                    int seconds = parts.Length > 1 && int.TryParse(parts[1], out var s) ? s : 120;
                    for (int elapsed = 0; elapsed < seconds; elapsed += 10)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));
                        var clients = await CrossWifiManager.GetHotspotClientsAsync();
                        if (clients.ErrorCode == WifiErrorCodes.Success)
                            Log("INFO", "hotspot-clients", string.Join(", ", clients.Data!.Select(c => $"{c.HostName ?? "?"} {c.MacAddress}")));
                    }
                    var stopped = await CrossWifiManager.StopHotspotAsync();
                    Check("hotspot-stop", stopped.ErrorCode == WifiErrorCodes.Success && stopped.Data, $"{stopped.ErrorCode} {stopped.ErrorMessage}");
                });
                break;
            case "connect-prefix":
                await Step("connect-prefix", async () =>
                {
                    var prefix = parts[1];
                    var password = string.Join(':', parts.Skip(2));
                    var connected = await CrossWifiManager.ConnectLocalOnlyAsync(prefix, password,
                        new WifiConnectionOptions { IsSsidPrefix = true, Timeout = TimeSpan.FromSeconds(60) });
                    Check("prefix-connect", connected.ErrorCode == WifiErrorCodes.Success && connected.Data?.Ssid?.StartsWith(prefix) == true,
                        $"{connected.ErrorCode} {connected.ErrorMessage} ssid={connected.Data?.Ssid}");
                    if (connected.ErrorCode != WifiErrorCodes.Success) return;

                    // The local-only connection's own details; on Android the default network may still be the old one.
                    var joinedNetwork = connected.Data!;
                    Log("INFO", "prefix-network", $"ssid={joinedNetwork.Ssid} ip={Ip(joinedNetwork.IpAddress)} gateway={Ip(joinedNetwork.GatewayAddress)}");
                    var gateway = Ip(joinedNetwork.GatewayAddress);
                    if (gateway != null)
                    {
                        var ping = await CrossWifiManager.PingAsync(gateway, 3);
                        Check("prefix-ping-gateway", ping.Data?.Received > 0, $"{ping.ErrorMessage} avg={ping.Data?.AverageMs:0.0}ms");
                    }
                    var devices = await CrossWifiManager.DiscoverDevicesAsync();
                    Check("prefix-discover", devices.Data?.Count > 0, $"{devices.ErrorMessage} {string.Join(", ", devices.Data?.Select(d => d.IpAddress) ?? [])}");

                    var released = await CrossWifiManager.ReleaseLocalOnlyAsync();
                    Check("prefix-release", released.ErrorCode == WifiErrorCodes.Success, $"{released.ErrorMessage}");
                    if (connected.Data?.Ssid is string joined)
                    {
                        var forgot = await CrossWifiManager.ForgetNetworkAsync(joined);
                        Log("INFO", "prefix-forget", $"{forgot.ErrorCode} {forgot.ErrorMessage}");
                    }
                });
                break;
            case "timeout":
                await Step("timeout", async () =>
                {
                    // A real target with a timeout far too short to finish must come back as OperationTimeout.
                    var target = parts.Length > 1 ? parts[1] : "SelfTestNoSuchNetwork";
                    var password = parts.Length > 2 ? string.Join(':', parts.Skip(2)) : "password123";
                    var sw = Stopwatch.StartNew();
                    var response = await CrossWifiManager.ConnectLocalOnlyAsync(target, password,
                        new WifiConnectionOptions { IsSsidPrefix = parts.Length > 1, Timeout = TimeSpan.FromSeconds(3) });
                    Check("connect-timeout", response.ErrorCode == WifiErrorCodes.OperationTimeout && sw.Elapsed < TimeSpan.FromSeconds(20),
                        $"{response.ErrorCode} {response.ErrorMessage} after {sw.Elapsed.TotalSeconds:0.0}s");
                    await CrossWifiManager.ReleaseLocalOnlyAsync();
                });
                break;
            case "wifi-toggle":
                await Step("wifi-toggle", async () =>
                {
                    var off = await CrossWifiManager.SetWifiEnabledAsync(false);
                    Log("INFO", "wifi-off", $"{off.ErrorCode} data={off.Data} {off.ErrorMessage}");
                    if (off.Data)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3));
                        var state = await CrossWifiManager.IsWifiEnabledAsync();
                        Check("wifi-off-state", state.Data == false, $"enabled={state.Data}");
                        var on = await CrossWifiManager.SetWifiEnabledAsync(true);
                        Check("wifi-on", on.ErrorCode == WifiErrorCodes.Success && on.Data, $"{on.ErrorMessage}");
                        await Task.Delay(TimeSpan.FromSeconds(3));
                        state = await CrossWifiManager.IsWifiEnabledAsync();
                        Check("wifi-on-state", state.Data == true, $"enabled={state.Data}");
                    }
                    else
                        Check("wifi-toggle-panel", off.ErrorCode == WifiErrorCodes.Success, "Android 10+ opened the Wi-Fi panel instead");
                });
                break;
            default:
                Log("FAIL", "mode", $"Unknown mode '{mode}'.");
                break;
        }
        Log("INFO", "done", $"{pass} passed, {fail} failed");
    }

    private static async Task RunBasicAsync(Action<string, string, string> log, Action<string, bool, string> check, Func<string, Func<Task>, Task> step)
    {
        await step("permissions", async () =>
        {
            var r = await CrossWifiManager.RequestPermissionsAsync();
            check("permissions", r.ErrorCode == WifiErrorCodes.Success, $"{r.ErrorCode} {r.ErrorMessage}");
        });

        await step("connection-types", async () =>
        {
            var r = await CrossWifiManager.GetConnectionTypesAsync();
            check("connection-types", r.ErrorCode == WifiErrorCodes.Success && r.Data!.Contains(NetworkConnectionType.WiFi), string.Join(", ", r.Data ?? []));
            EventHandler<ConnectionTypeChangedEventArgs> handler = (_, _) => { };
            CrossWifiManager.ConnectionTypeChanged += handler;
            CrossWifiManager.ConnectionTypeChanged -= handler;
            check("connection-type-event", true, "subscribe/unsubscribe ok");
        });

        await step("scan-availability", async () =>
        {
            var r = await CrossWifiManager.CanStartScanAsync();
            check("scan-availability", r.ErrorCode == WifiErrorCodes.Success, $"{r.Data} - {r.ErrorMessage}");
        });

        await step("scan-results-event", async () =>
        {
            var tcs = new TaskCompletionSource<WifiScanResultsEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<WifiScanResultsEventArgs> handler = (_, e) => tcs.TrySetResult(e);
            CrossWifiManager.ScanResultsAvailable += handler;
            try
            {
                await Task.Delay(500); // the Windows adapter subscription is set up asynchronously
                var scan = await CrossWifiManager.ScanWifiNetworksAsync();
                check("scan", scan.ErrorCode == WifiErrorCodes.Success && scan.Data!.Count > 0,
                    $"{scan.Data?.Count} networks; {scan.ErrorMessage}");
                var standards = scan.Data?.GroupBy(n => n.WifiStandard).Select(g => $"{g.Key}={g.Count()}");
                log("INFO", "scan-standards", string.Join(", ", standards ?? []));
                var e = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(20));
                check("scan-results-event", e.Networks.Count > 0, $"{e.Networks.Count} networks, fresh={e.IsFresh}");
            }
            catch (TimeoutException)
            {
                check("scan-results-event", false, "no event within 20 s");
            }
            finally
            {
                CrossWifiManager.ScanResultsAvailable -= handler;
            }
        });

        string? gateway = null;
        await step("network-info", async () =>
        {
            var r = await CrossWifiManager.GetNetworkInfoAsync();
            gateway = Ip(r.Data?.GatewayAddress ?? 0);
            check("broadcast-address", r.Data?.BroadcastAddress != null && r.Data.NetworkPrefixLength != null,
                $"ssid={r.Data?.Ssid} ip={Ip(r.Data?.IpAddress ?? 0)} mask={r.Data?.SubnetMask} broadcast={r.Data?.BroadcastAddress} prefix=/{r.Data?.NetworkPrefixLength} gateway={gateway}");
        });

        await step("custom-probe", async () =>
        {
            var r = await CrossWifiManager.IsInternetAvailableAsync(new Uri("https://www.google.com/generate_204"));
            check("custom-probe", r.ErrorCode == WifiErrorCodes.Success && r.Data, $"{r.ErrorMessage}");
        });

        await step("ping", async () =>
        {
            foreach (var host in new[] { gateway ?? "192.168.1.1", "8.8.8.8", "one.one.one.one" })
            {
                var r = await CrossWifiManager.PingAsync(host, 3);
                check($"ping {host}", r.Data?.Received > 0,
                    $"{r.ErrorCode} {r.ErrorMessage} min/avg/max={r.Data?.MinMs}/{r.Data?.AverageMs:0.0}/{r.Data?.MaxMs}ms loss={r.Data?.PacketLossPercent:0}%");
            }
        });

        await step("discover-devices", async () =>
        {
            var sw = Stopwatch.StartNew();
            var r = await CrossWifiManager.DiscoverDevicesAsync();
            check("discover-devices", r.Data?.Count > 0, $"{r.ErrorMessage} in {sw.Elapsed.TotalSeconds:0.0}s");
            foreach (var d in r.Data ?? [])
                log("INFO", "device", $"{d.IpAddress} {d.HostName} {d.RoundTripMs}ms");
        });

        await step("mdns", async () =>
        {
            var types = await CrossWifiManager.BrowseServicesAsync();
            check("mdns-types", types.ErrorCode == WifiErrorCodes.Success, $"{types.ErrorMessage} {string.Join(", ", types.Data?.Select(t => t.Name) ?? [])}");
            foreach (var type in types.Data?.Take(4) ?? [])
            {
                var services = await CrossWifiManager.BrowseServicesAsync(type.Name);
                foreach (var s in services.Data ?? [])
                    log("INFO", "mdns-service", $"{s.ServiceType} '{s.Name}' {s.HostName} {s.IpAddress}:{s.Port} txt={s.Properties.Count}");
            }
        });

        await step("speed-test", async () =>
        {
            var r = await CrossWifiManager.MeasureSpeedAsync();
            check("speed-test", r.ErrorCode == WifiErrorCodes.Success && r.Data!.DownloadMbps > 0,
                $"{r.ErrorMessage} down={r.Data?.DownloadMbps}Mbps up={r.Data?.UploadMbps}Mbps latency={r.Data?.LatencyMs}ms");
        });

        await step("prefix-not-found", async () =>
        {
            var r = await CrossWifiManager.ConnectWifiAsync("ZzSelfTestNoSuchPrefix-", "password123", new WifiConnectionOptions { IsSsidPrefix = true });
            check("prefix-not-found", r.ErrorCode is WifiErrorCodes.NetworkUnavailable or WifiErrorCodes.NoConnection, $"{r.ErrorCode} {r.ErrorMessage}");
        });

        await step("hotspot", async () =>
        {
            var started = await CrossWifiManager.StartHotspotAsync();
            log(started.ErrorCode == WifiErrorCodes.Success ? "PASS" : "INFO", "hotspot-start",
                $"{started.ErrorCode} {started.ErrorMessage} ssid={started.Data?.Ssid} localOnly={started.Data?.IsLocalOnly}");
            if (started.ErrorCode != WifiErrorCodes.Success) return;
            var clients = await CrossWifiManager.GetHotspotClientsAsync();
            log("INFO", "hotspot-clients", $"{clients.ErrorCode} {clients.ErrorMessage}");
            var stopped = await CrossWifiManager.StopHotspotAsync();
            check("hotspot-stop", stopped.ErrorCode == WifiErrorCodes.Success && stopped.Data, $"{stopped.ErrorMessage}");
        });
    }

    private static string? Ip(int address) =>
        address == 0 ? null : new System.Net.IPAddress(BitConverter.GetBytes(address)).ToString();
}

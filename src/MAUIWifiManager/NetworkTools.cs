using MauiWifiManager.Abstractions;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace MauiWifiManager
{
    /// <summary>
    /// Platform-independent network tools backing the default implementations on <see cref="IWifiNetworkService"/>.
    /// </summary>
    internal static class NetworkTools
    {
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan SweepPingTimeout = TimeSpan.FromMilliseconds(600);
        private const int SweepConcurrency = 64;
        public static readonly TimeSpan DefaultBrowseDuration = TimeSpan.FromSeconds(3);

        public static async Task<WifiManagerResponse<bool>> ProbeInternetAsync(Uri probeUrl, CancellationToken cancellationToken)
        {
            try
            {
                using var handler = new HttpClientHandler { AllowAutoRedirect = false };
                using var client = new HttpClient(handler) { Timeout = ProbeTimeout };
                using var response = await client.GetAsync(probeUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                // A redirect usually means a captive portal intercepted the request.
                bool available = response.IsSuccessStatusCode;
                return WifiManagerResponse<bool>.SuccessResponse(available,
                    available ? "Internet is available." : $"Probe returned HTTP {(int)response.StatusCode}.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return WifiManagerResponse<bool>.SuccessResponse(false, $"Probe failed: {ex.Message}");
            }
        }

        public static async Task<WifiManagerResponse<PingStatistics>> PingAsync(string host, int count, CancellationToken cancellationToken)
        {
            if (count < 1)
                return WifiManagerResponse<PingStatistics>.ErrorResponse(WifiErrorCodes.UnknownError, "Count must be at least 1.");

            var times = new List<long>();
            try
            {
                using var ping = new Ping();
                for (int i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var reply = await ping.SendPingAsync(host, TimeSpan.FromSeconds(2), cancellationToken: cancellationToken).ConfigureAwait(false);
                    if (reply.Status == IPStatus.Success)
                        times.Add(reply.RoundtripTime);
                    if (i < count - 1)
                        await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return WifiManagerResponse<PingStatistics>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");
            }
            catch (PingException ex)
            {
                return WifiManagerResponse<PingStatistics>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, ex.InnerException?.Message ?? ex.Message);
            }

            var stats = new PingStatistics
            {
                Host = host,
                Sent = count,
                Received = times.Count,
                MinMs = times.Count > 0 ? times.Min() : null,
                AverageMs = times.Count > 0 ? times.Average() : null,
                MaxMs = times.Count > 0 ? times.Max() : null
            };
            return WifiManagerResponse<PingStatistics>.SuccessResponse(stats, $"{stats.Received}/{stats.Sent} replies.");
        }

        public static async Task<WifiManagerResponse<SpeedTestResult>> MeasureSpeedAsync(SpeedTestOptions options, CancellationToken cancellationToken)
        {
            try
            {
                using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

                var (download, latency) = await MeasureDownloadAsync(client, options, cancellationToken).ConfigureAwait(false);
                double? upload = options.UploadUrl == null ? null : await MeasureUploadAsync(client, options, cancellationToken).ConfigureAwait(false);

                var result = new SpeedTestResult { DownloadMbps = download, UploadMbps = upload, LatencyMs = latency };
                return WifiManagerResponse<SpeedTestResult>.SuccessResponse(result, "Speed test complete.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return WifiManagerResponse<SpeedTestResult>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");
            }
            catch (HttpRequestException ex)
            {
                return WifiManagerResponse<SpeedTestResult>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, ex.Message);
            }
        }

        /// <summary>
        /// Returns throughput plus latency: the fastest time to first byte of a one-byte ranged request,
        /// measured after a warm-up request so DNS and the TLS handshake are not counted.
        /// </summary>
        private static async Task<(double Mbps, double LatencyMs)> MeasureDownloadAsync(HttpClient client, SpeedTestOptions options, CancellationToken cancellationToken)
        {
            double latency = double.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, options.DownloadUrl);
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                var probe = Stopwatch.StartNew();
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                if (i > 0)
                    latency = Math.Min(latency, probe.Elapsed.TotalMilliseconds);
            }

            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(options.MaxDurationPerDirection);
            var buffer = new byte[81920];
            long total = 0;
            var sw = Stopwatch.StartNew();
            try
            {
                using var response = await client.GetAsync(options.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, limit.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                using var stream = await response.Content.ReadAsStreamAsync(limit.Token).ConfigureAwait(false);
                int read;
                while ((read = await stream.ReadAsync(buffer, limit.Token).ConfigureAwait(false)) > 0)
                    total += read;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Hit the time limit: measure what arrived so far.
            }
            return (ToMbps(total, sw.Elapsed), Math.Round(latency, 1));
        }

        private static async Task<double> MeasureUploadAsync(HttpClient client, SpeedTestOptions options, CancellationToken cancellationToken)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(options.MaxDurationPerDirection);
            var content = new CountingContent(new byte[options.UploadBytes]);
            var sw = Stopwatch.StartNew();
            try
            {
                using var response = await client.PostAsync(options.UploadUrl, content, limit.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
            return ToMbps(content.BytesSent, sw.Elapsed);
        }

        private static double ToMbps(long bytes, TimeSpan elapsed) =>
            elapsed.TotalSeconds <= 0 ? 0 : Math.Round(bytes * 8 / elapsed.TotalSeconds / 1_000_000, 2);

        /// <summary>Upload body that counts bytes as they are written, so a time-limited upload can still be measured.</summary>
        private sealed class CountingContent : HttpContent
        {
            private readonly byte[] _Data;
            public long BytesSent;

            public CountingContent(byte[] data) => _Data = data;

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            {
                const int chunk = 65536;
                for (int offset = 0; offset < _Data.Length; offset += chunk)
                {
                    int size = Math.Min(chunk, _Data.Length - offset);
                    await stream.WriteAsync(_Data.AsMemory(offset, size)).ConfigureAwait(false);
                    Interlocked.Add(ref BytesSent, size);
                }
            }

            protected override bool TryComputeLength(out long length)
            {
                length = _Data.Length;
                return true;
            }
        }

        /// <param name="subnet">Network to sweep; defaults to the device's Wi-Fi (or first active) IPv4 network.</param>
        public static async Task<WifiManagerResponse<List<LanDevice>>> DiscoverDevicesAsync(CancellationToken cancellationToken, (IPAddress Address, int PrefixLength)? subnet = null)
        {
            var local = subnet ?? GetLocalIpv4();
            if (local == null)
                return WifiManagerResponse<List<LanDevice>>.ErrorResponse(WifiErrorCodes.NoConnection, "No active IPv4 network.");

            var hosts = GetSweepHosts(local.Value.Address, local.Value.PrefixLength);
            var found = new System.Collections.Concurrent.ConcurrentBag<(IPAddress Address, long Ms)>();
            using var gate = new SemaphoreSlim(SweepConcurrency);
            try
            {
                await Task.WhenAll(hosts.Select(async host =>
                {
                    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        using var ping = new Ping();
                        var reply = await ping.SendPingAsync(host, SweepPingTimeout, cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (reply.Status == IPStatus.Success)
                            found.Add((host, reply.RoundtripTime));
                    }
                    catch (PingException) { }
                    finally
                    {
                        gate.Release();
                    }
                })).ConfigureAwait(false);

                var devices = await Task.WhenAll(found.Select(async f => new LanDevice
                {
                    IpAddress = f.Address.ToString(),
                    HostName = await TryReverseLookupAsync(f.Address, cancellationToken).ConfigureAwait(false),
                    RoundTripMs = f.Ms
                })).ConfigureAwait(false);
                var sorted = devices.OrderBy(d => IPAddress.Parse(d.IpAddress).GetAddressBytes()[3]).ToList();
                return WifiManagerResponse<List<LanDevice>>.SuccessResponse(sorted, $"Found {sorted.Count} device(s) on {hosts.Count} addresses.");
            }
            catch (OperationCanceledException)
            {
                return WifiManagerResponse<List<LanDevice>>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");
            }
        }

        private static async Task<string?> TryReverseLookupAsync(IPAddress address, CancellationToken cancellationToken)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                var entry = await Dns.GetHostEntryAsync(address.ToString(), timeout.Token).ConfigureAwait(false);
                return entry.HostName == address.ToString() ? null : entry.HostName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Every host address in the subnet. Subnets larger than /24 are limited to the /24 containing the
        /// device, so a sweep stays at 254 pings. ponytail: widen with a smarter (ARP/mDNS-first) scan if needed.
        /// </summary>
        internal static List<IPAddress> GetSweepHosts(IPAddress localAddress, int prefixLength)
        {
            prefixLength = Math.Clamp(prefixLength, 24, 30);
            uint ip = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(localAddress.GetAddressBytes());
            uint mask = uint.MaxValue << (32 - prefixLength);
            uint network = ip & mask;
            uint broadcast = network | ~mask;
            var hosts = new List<IPAddress>();
            for (uint host = network + 1; host < broadcast; host++)
            {
                if (host == ip) continue;
                var bytes = new byte[4];
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes, host);
                hosts.Add(new IPAddress(bytes));
            }
            return hosts;
        }

        private static (IPAddress Address, int PrefixLength)? GetLocalIpv4()
        {
            // Prefer Wi-Fi, then any other interface that is up and has a gateway.
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus is OperationalStatus.Up or OperationalStatus.Unknown
                         && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderByDescending(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
            foreach (var nic in candidates)
            {
                var props = nic.GetIPProperties();
                var unicast = props.UnicastAddresses.FirstOrDefault(u =>
                    u.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(u.Address)
                    && !u.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal));
                if (unicast == null) continue;
                int prefix = unicast.PrefixLength > 0 ? unicast.PrefixLength : 24;
                return (unicast.Address, prefix);
            }
            return null;
        }

        private static readonly IPAddress MdnsGroup = IPAddress.Parse("224.0.0.251");
        private const int MdnsPort = 5353;

        /// <summary>
        /// Joins the mDNS group on port 5353 (shared with the OS responder) so multicast answers arrive.
        /// If 5353 can't be bound, falls back to an ephemeral port: per RFC 6762 §6.7 responders then answer
        /// directly, though not every responder (or firewall) cooperates.
        /// </summary>
        private static UdpClient CreateMdnsSocket()
        {
            var local = GetLocalIpv4()?.Address;
            UdpClient udp;
            try
            {
                udp = new UdpClient { ExclusiveAddressUse = false };
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));
                udp.JoinMulticastGroup(MdnsGroup, local ?? IPAddress.Any);
            }
            catch (SocketException)
            {
                udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            }
            if (local != null)
                udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
            udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 255);
            return udp;
        }

        public static async Task<WifiManagerResponse<List<NetworkServiceInfo>>> BrowseServicesAsync(string serviceType, TimeSpan duration, CancellationToken cancellationToken)
        {
            var type = serviceType.Trim().TrimEnd('.');
            if (type.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
                type = type[..^".local".Length];

            try
            {
                using var udp = CreateMdnsSocket();
                var query = Mdns.BuildPtrQuery(type + ".local");
                var target = new IPEndPoint(MdnsGroup, MdnsPort);
                var records = new List<Mdns.Record>();

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(duration);
                // Multicast UDP can drop packets; a second query costs nothing and catches slow responders.
                await udp.SendAsync(query, target, timeout.Token).ConfigureAwait(false);
                await Task.Delay(100, timeout.Token).ConfigureAwait(false);
                await udp.SendAsync(query, target, timeout.Token).ConfigureAwait(false);
                try
                {
                    while (true)
                    {
                        var result = await udp.ReceiveAsync(timeout.Token).ConfigureAwait(false);
                        records.AddRange(Mdns.ParseRecords(result.Buffer));
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Browse duration elapsed.
                }

                var services = Mdns.BuildServices(type, records);
                return WifiManagerResponse<List<NetworkServiceInfo>>.SuccessResponse(services, $"Found {services.Count} service(s).");
            }
            catch (OperationCanceledException)
            {
                return WifiManagerResponse<List<NetworkServiceInfo>>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");
            }
            catch (SocketException ex)
            {
                return WifiManagerResponse<List<NetworkServiceInfo>>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, $"mDNS query failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Connection type and change events, built on MAUI Essentials <see cref="Microsoft.Maui.Networking.Connectivity"/>.
    /// </summary>
    internal static class ConnectionMonitor
    {
        private static readonly object _Lock = new();
        private static readonly List<(object Sender, EventHandler<ConnectionTypeChangedEventArgs> Handler)> _Handlers = new();

        public static WifiManagerResponse<List<NetworkConnectionType>> GetConnectionTypes()
        {
            try
            {
                var types = Map(Microsoft.Maui.Networking.Connectivity.Current.ConnectionProfiles);
                return WifiManagerResponse<List<NetworkConnectionType>>.SuccessResponse(types,
                    types.Count == 0 ? "Not connected." : string.Join(", ", types));
            }
            catch (Exception ex) when (ex is NotImplementedException or NotSupportedException)
            {
                return WifiManagerResponse<List<NetworkConnectionType>>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, "Connection type is not available on this platform.");
            }
        }

        public static void Add(object sender, EventHandler<ConnectionTypeChangedEventArgs>? handler)
        {
            if (handler == null) return;
            lock (_Lock)
            {
                if (_Handlers.Count == 0 && !TrySubscribe())
                    return;
                _Handlers.Add((sender, handler));
            }
        }

        public static void Remove(EventHandler<ConnectionTypeChangedEventArgs>? handler)
        {
            lock (_Lock)
            {
                int index = _Handlers.FindIndex(h => h.Handler == handler);
                if (index < 0) return;
                _Handlers.RemoveAt(index);
                if (_Handlers.Count == 0)
                    Microsoft.Maui.Networking.Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
            }
        }

        private static bool TrySubscribe()
        {
            try
            {
                Microsoft.Maui.Networking.Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
                return true;
            }
            catch (Exception ex) when (ex is NotImplementedException or NotSupportedException)
            {
                return false; // No connectivity API on this target (e.g. plain net10.0).
            }
        }

        private static void OnConnectivityChanged(object? sender, Microsoft.Maui.Networking.ConnectivityChangedEventArgs e)
        {
            var args = new ConnectionTypeChangedEventArgs(Map(e.ConnectionProfiles),
                e.NetworkAccess == Microsoft.Maui.Networking.NetworkAccess.Internet);
            List<(object Sender, EventHandler<ConnectionTypeChangedEventArgs> Handler)> handlers;
            lock (_Lock) handlers = _Handlers.ToList();
            foreach (var (owner, handler) in handlers)
                handler(owner, args);
        }

        internal static List<NetworkConnectionType> Map(IEnumerable<Microsoft.Maui.Networking.ConnectionProfile> profiles) =>
            profiles.Select(p => p switch
            {
                Microsoft.Maui.Networking.ConnectionProfile.WiFi => NetworkConnectionType.WiFi,
                Microsoft.Maui.Networking.ConnectionProfile.Cellular => NetworkConnectionType.Cellular,
                Microsoft.Maui.Networking.ConnectionProfile.Ethernet => NetworkConnectionType.Ethernet,
                Microsoft.Maui.Networking.ConnectionProfile.Bluetooth => NetworkConnectionType.Bluetooth,
                _ => NetworkConnectionType.Unknown
            }).Distinct().ToList();
    }

    /// <summary>
    /// Minimal mDNS/DNS-SD message builder and parser: PTR queries plus PTR, SRV, TXT and A answers.
    /// </summary>
    internal static class Mdns
    {
        public const ushort TypeA = 1, TypePtr = 12, TypeTxt = 16, TypeSrv = 33;

        public sealed record Record(string Name, ushort Type, string? Target = null, int Port = 0, string? Address = null,
            IReadOnlyDictionary<string, string>? Properties = null);

        public static byte[] BuildPtrQuery(string name)
        {
            var bytes = new List<byte> { 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 }; // id 0, flags 0, one question
            WriteName(bytes, name);
            bytes.AddRange(new byte[] { 0, (byte)TypePtr, 0, 1 }); // QTYPE PTR, QCLASS IN
            return bytes.ToArray();
        }

        private static void WriteName(List<byte> bytes, string name)
        {
            foreach (var label in name.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                var encoded = Encoding.UTF8.GetBytes(label);
                bytes.Add((byte)encoded.Length);
                bytes.AddRange(encoded);
            }
            bytes.Add(0);
        }

        /// <summary>Parses every resource record in a response; returns nothing for malformed packets.</summary>
        public static List<Record> ParseRecords(byte[] message)
        {
            var records = new List<Record>();
            try
            {
                if (message.Length < 12) return records;
                int questions = ReadUInt16(message, 4);
                int recordCount = ReadUInt16(message, 6) + ReadUInt16(message, 8) + ReadUInt16(message, 10);
                int offset = 12;
                for (int i = 0; i < questions; i++)
                {
                    ReadName(message, ref offset);
                    offset += 4;
                }
                for (int i = 0; i < recordCount; i++)
                {
                    string name = ReadName(message, ref offset);
                    ushort type = ReadUInt16(message, offset);
                    int length = ReadUInt16(message, offset + 8);
                    int data = offset + 10;
                    if (data + length > message.Length) break;
                    int cursor = data;
                    records.Add(type switch
                    {
                        TypePtr => new Record(name, type, Target: ReadName(message, ref cursor)),
                        TypeSrv => ReadSrv(message, name, data),
                        TypeTxt => new Record(name, type, Properties: ReadTxt(message, data, length)),
                        TypeA when length == 4 => new Record(name, type, Address: new IPAddress(message.AsSpan(data, 4)).ToString()),
                        _ => new Record(name, type)
                    });
                    offset = data + length;
                }
            }
            catch (IndexOutOfRangeException) { }
            catch (ArgumentException) { }
            return records;
        }

        private static Record ReadSrv(byte[] message, string name, int data)
        {
            int port = ReadUInt16(message, data + 4);
            int cursor = data + 6;
            return new Record(name, TypeSrv, Target: ReadName(message, ref cursor), Port: port);
        }

        private static Dictionary<string, string> ReadTxt(byte[] message, int data, int length)
        {
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int end = data + length;
            while (data < end)
            {
                int size = message[data++];
                if (size == 0 || data + size > end) { data += size; continue; }
                var entry = Encoding.UTF8.GetString(message, data, size);
                int eq = entry.IndexOf('=');
                properties[eq < 0 ? entry : entry[..eq]] = eq < 0 ? string.Empty : entry[(eq + 1)..];
                data += size;
            }
            return properties;
        }

        private static ushort ReadUInt16(byte[] message, int offset) => (ushort)(message[offset] << 8 | message[offset + 1]);

        /// <summary>Reads a possibly compressed domain name, advancing <paramref name="offset"/> past it.</summary>
        internal static string ReadName(byte[] message, ref int offset)
        {
            var labels = new List<string>();
            int position = offset;
            bool jumped = false;
            for (int hops = 0; hops < 64; hops++)
            {
                int length = message[position];
                if (length == 0)
                {
                    if (!jumped) offset = position + 1;
                    return string.Join('.', labels);
                }
                if ((length & 0xC0) == 0xC0)
                {
                    int pointer = (length & 0x3F) << 8 | message[position + 1];
                    if (!jumped) offset = position + 2;
                    jumped = true;
                    position = pointer;
                    continue;
                }
                labels.Add(Encoding.UTF8.GetString(message, position + 1, length));
                position += length + 1;
            }
            throw new ArgumentException("Name compression loop.");
        }

        /// <summary>
        /// Joins PTR, SRV, TXT and A records into services of <paramref name="serviceType"/>.
        /// For the <c>_services._dns-sd._udp</c> meta query, each PTR target is a service type.
        /// </summary>
        public static List<NetworkServiceInfo> BuildServices(string serviceType, IEnumerable<Record> records)
        {
            var all = records.ToList();
            string fullType = serviceType + ".local";
            var ptrs = all.Where(r => r.Type == TypePtr && string.Equals(r.Name, fullType, StringComparison.OrdinalIgnoreCase) && r.Target != null)
                .Select(r => r.Target!)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            if (serviceType.Equals("_services._dns-sd._udp", StringComparison.OrdinalIgnoreCase))
                return ptrs.Select(t => TrimLocal(t)).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(t => new NetworkServiceInfo { Name = t, ServiceType = t }).OrderBy(s => s.Name).ToList();

            var services = new List<NetworkServiceInfo>();
            foreach (var instance in ptrs)
            {
                var srv = all.FirstOrDefault(r => r.Type == TypeSrv && string.Equals(r.Name, instance, StringComparison.OrdinalIgnoreCase));
                var txt = all.FirstOrDefault(r => r.Type == TypeTxt && string.Equals(r.Name, instance, StringComparison.OrdinalIgnoreCase));
                var a = srv?.Target == null ? null
                    : all.FirstOrDefault(r => r.Type == TypeA && string.Equals(r.Name, srv.Target, StringComparison.OrdinalIgnoreCase));
                services.Add(new NetworkServiceInfo
                {
                    Name = instance.EndsWith("." + fullType, StringComparison.OrdinalIgnoreCase) ? instance[..^(fullType.Length + 1)] : instance,
                    ServiceType = serviceType,
                    HostName = srv?.Target,
                    Port = srv?.Port ?? 0,
                    IpAddress = a?.Address,
                    Properties = txt?.Properties ?? new Dictionary<string, string>()
                });
            }
            return services.OrderBy(s => s.Name).ToList();
        }

        private static string TrimLocal(string name) =>
            name.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ? name[..^".local".Length] : name;
    }
}

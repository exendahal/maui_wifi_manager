using MauiWifiManager.Abstractions;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace MauiWifiManager
{
    /// <summary>
    /// Traceroute, DNS lookup, port check and Wake-on-LAN, backing the default implementations on
    /// <see cref="IWifiNetworkService"/>. Shared .NET code, so they work on every platform.
    /// </summary>
    internal static class NetworkDiagnostics
    {
        private static readonly TimeSpan HopTimeout = TimeSpan.FromMilliseconds(1500);
        private static readonly TimeSpan DnsTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan PortTimeout = TimeSpan.FromMilliseconds(800);
        private const int PortConcurrency = 32;
        private const int WakeOnLanPort = 9;
        private static readonly IPAddress FallbackDnsServer = IPAddress.Parse("1.1.1.1");

        #region Traceroute

        public static Task<WifiManagerResponse<List<TracerouteHop>>> TracerouteAsync(string host, int maxHops, IProgress<TracerouteHop>? progress,
            CancellationToken cancellationToken) =>
            Task.Run(() => TracerouteCoreAsync(host, maxHops, progress, cancellationToken));

        private static async Task<WifiManagerResponse<List<TracerouteHop>>> TracerouteCoreAsync(string host, int maxHops, IProgress<TracerouteHop>? progress,
            CancellationToken cancellationToken)
        {
            if (maxHops is < 1 or > 64)
                return WifiManagerResponse<List<TracerouteHop>>.ErrorResponse(WifiErrorCodes.UnknownError, "maxHops must be between 1 and 64.");

            var hops = new List<TracerouteHop>();
            try
            {
                var target = await ResolveAsync(host, cancellationToken).ConfigureAwait(false);
                if (target == null)
                    return WifiManagerResponse<List<TracerouteHop>>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, $"Could not resolve {host}.");

                using var ping = new Ping();
                long overheadMs = await MeasurePingOverheadAsync(ping, cancellationToken).ConfigureAwait(false);
                for (int ttl = 1; ttl <= maxHops; ttl++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Each hop is timed here: some platforms report 0 ms for a "TTL expired" reply.
                    // Default payload and no don't-fragment flag: without raw sockets (Android, Linux)
                    // .NET runs the system ping utility, which rejects a custom payload.
                    var stopwatch = Stopwatch.StartNew();
                    var reply = await ping.SendPingAsync(target, HopTimeout, null, new PingOptions(ttl, dontFragment: false), cancellationToken)
                        .ConfigureAwait(false);
                    stopwatch.Stop();

                    bool answered = reply.Status is IPStatus.Success or IPStatus.TtlExpired or IPStatus.TimeExceeded;
                    var address = answered && !reply.Address.Equals(IPAddress.Any) ? reply.Address : null;
                    var hop = new TracerouteHop
                    {
                        Hop = ttl,
                        Address = address?.ToString(),
                        HostName = address == null ? null : await NetworkTools.TryReverseLookupAsync(address, cancellationToken).ConfigureAwait(false),
                        RoundTripMs = !answered ? null
                            : reply.RoundtripTime > 0 ? reply.RoundtripTime
                            : Math.Max(0, stopwatch.ElapsedMilliseconds - overheadMs),
                        IsDestination = reply.Status == IPStatus.Success
                    };
                    hops.Add(hop);
                    progress?.Report(hop);
                    if (hop.IsDestination)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                return WifiManagerResponse<List<TracerouteHop>>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");
            }
            catch (PingException ex)
            {
                return WifiManagerResponse<List<TracerouteHop>>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, ex.InnerException?.Message ?? ex.Message);
            }
            catch (PlatformNotSupportedException ex)
            {
                return WifiManagerResponse<List<TracerouteHop>>.ErrorResponse(WifiErrorCodes.UnsupportedHardware, ex.Message);
            }

            bool reached = hops.Count > 0 && hops[^1].IsDestination;
            return WifiManagerResponse<List<TracerouteHop>>.SuccessResponse(hops,
                reached ? $"Reached {host} in {hops.Count} hop(s)." : $"Did not reach {host} within {maxHops} hops.");
        }

        /// <summary>
        /// Time a loopback ping takes, i.e. the cost of sending one rather than the network's. Where .NET
        /// runs the system ping utility (Android, Linux) that is a process start, tens of milliseconds,
        /// and a "TTL expired" reply carries no time of its own, so it is subtracted from each hop.
        /// </summary>
        private static async Task<long> MeasurePingOverheadAsync(Ping ping, CancellationToken cancellationToken)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await ping.SendPingAsync(IPAddress.Loopback, HopTimeout, null, null, cancellationToken).ConfigureAwait(false);
                return stopwatch.ElapsedMilliseconds;
            }
            catch (PingException)
            {
                return 0;
            }
        }

        private static async Task<IPAddress?> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            if (IPAddress.TryParse(host, out var address))
                return address;
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
                return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses.FirstOrDefault();
            }
            catch (SocketException)
            {
                return null;
            }
        }

        #endregion

        #region DNS

        public static Task<WifiManagerResponse<DnsLookupResult>> LookupDnsAsync(string name, DnsRecordType type, string? server,
            CancellationToken cancellationToken) =>
            Task.Run(() => LookupDnsCoreAsync(name, type, server, cancellationToken));

        private static async Task<WifiManagerResponse<DnsLookupResult>> LookupDnsCoreAsync(string name, DnsRecordType type, string? server,
            CancellationToken cancellationToken)
        {
            name = name.Trim().TrimEnd('.');
            if (name.Length == 0)
                return WifiManagerResponse<DnsLookupResult>.ErrorResponse(WifiErrorCodes.UnknownError, "Enter a name to look up.");

            IPAddress serverAddress;
            if (string.IsNullOrWhiteSpace(server))
                serverAddress = GetSystemDnsServer() ?? FallbackDnsServer;
            else if (!IPAddress.TryParse(server, out serverAddress!))
                return WifiManagerResponse<DnsLookupResult>.ErrorResponse(WifiErrorCodes.UnknownError, "The DNS server must be an IP address.");

            var id = (ushort)Random.Shared.Next(ushort.MaxValue);
            var query = BuildDnsQuery(id, name, type);
            try
            {
                using var client = new UdpClient(serverAddress.AddressFamily);
                var stopwatch = Stopwatch.StartNew();

                // One retry: a single lost UDP datagram should not fail the lookup.
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    await client.SendAsync(query, new IPEndPoint(serverAddress, 53), cancellationToken).ConfigureAwait(false);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(DnsTimeout);
                    try
                    {
                        while (true)
                        {
                            var answer = await client.ReceiveAsync(timeout.Token).ConfigureAwait(false);
                            if (answer.Buffer.Length < 12 || ReadUInt16(answer.Buffer, 0) != id)
                                continue;
                            stopwatch.Stop();
                            var (notFound, records) = ParseDnsAnswer(answer.Buffer);
                            var result = new DnsLookupResult
                            {
                                Name = name,
                                Type = type,
                                Server = serverAddress.ToString(),
                                Records = records,
                                NameNotFound = notFound,
                                ElapsedMs = stopwatch.ElapsedMilliseconds
                            };
                            return WifiManagerResponse<DnsLookupResult>.SuccessResponse(result,
                                notFound ? $"{name} does not exist." : $"{records.Count} record(s).");
                        }
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
                }
                return WifiManagerResponse<DnsLookupResult>.ErrorResponse(WifiErrorCodes.OperationTimeout, $"{serverAddress} did not answer.");
            }
            catch (OperationCanceledException)
            {
                return WifiManagerResponse<DnsLookupResult>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");
            }
            catch (SocketException ex)
            {
                return WifiManagerResponse<DnsLookupResult>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, ex.Message);
            }
        }

        /// <summary>The first IPv4 DNS server of an active interface with a gateway, or null (Android often hides them).</summary>
        private static IPAddress? GetSystemDnsServer()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Select(n => n.GetIPProperties())
                    .Where(p => p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
                    .SelectMany(p => p.DnsAddresses)
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            }
            catch (NetworkInformationException)
            {
                return null;
            }
        }

        internal static byte[] BuildDnsQuery(ushort id, string name, DnsRecordType type)
        {
            // Header: id, flags 0x0100 (recursion desired), one question.
            var bytes = new List<byte> { (byte)(id >> 8), (byte)id, 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 0 };
            foreach (var label in name.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                var encoded = Encoding.UTF8.GetBytes(label);
                bytes.Add((byte)encoded.Length);
                bytes.AddRange(encoded);
            }
            bytes.Add(0);
            bytes.AddRange(new byte[] { (byte)((int)type >> 8), (byte)type, 0, 1 }); // QTYPE, QCLASS IN
            return bytes.ToArray();
        }

        /// <summary>Reads the answer section of a DNS response; NXDOMAIN gives an empty list.</summary>
        internal static (bool NameNotFound, List<DnsRecord> Records) ParseDnsAnswer(byte[] message)
        {
            var records = new List<DnsRecord>();
            bool notFound = (message[3] & 0x0F) == 3;
            try
            {
                int questions = ReadUInt16(message, 4);
                int answers = ReadUInt16(message, 6);
                int offset = 12;
                for (int i = 0; i < questions; i++)
                {
                    Mdns.ReadName(message, ref offset);
                    offset += 4;
                }
                for (int i = 0; i < answers; i++)
                {
                    string owner = Mdns.ReadName(message, ref offset);
                    var type = (DnsRecordType)ReadUInt16(message, offset);
                    int ttl = (int)((uint)ReadUInt16(message, offset + 4) << 16 | ReadUInt16(message, offset + 6));
                    int length = ReadUInt16(message, offset + 8);
                    int data = offset + 10;
                    if (data + length > message.Length) break;
                    int cursor = data;
                    string? value = type switch
                    {
                        DnsRecordType.A when length == 4 => new IPAddress(message.AsSpan(data, 4)).ToString(),
                        DnsRecordType.Aaaa when length == 16 => new IPAddress(message.AsSpan(data, 16)).ToString(),
                        DnsRecordType.Cname or DnsRecordType.Ns => Mdns.ReadName(message, ref cursor),
                        DnsRecordType.Mx => ReadMx(message, data),
                        DnsRecordType.Txt => ReadTxt(message, data, length),
                        _ => null
                    };
                    if (value != null)
                        records.Add(new DnsRecord { Type = type, Name = owner, Value = value, TimeToLiveSeconds = ttl });
                    offset = data + length;
                }
            }
            catch (IndexOutOfRangeException) { }
            catch (ArgumentException) { }
            return (notFound, records);
        }

        private static string ReadMx(byte[] message, int data)
        {
            int preference = ReadUInt16(message, data);
            int cursor = data + 2;
            return $"{preference} {Mdns.ReadName(message, ref cursor)}";
        }

        private static string ReadTxt(byte[] message, int data, int length)
        {
            // A TXT record is one or more length-prefixed strings; long values are split across them.
            var text = new StringBuilder();
            int end = data + length;
            while (data < end)
            {
                int size = message[data++];
                text.Append(Encoding.UTF8.GetString(message, data, Math.Min(size, end - data)));
                data += size;
            }
            return text.ToString();
        }

        private static ushort ReadUInt16(byte[] message, int offset) => (ushort)(message[offset] << 8 | message[offset + 1]);

        #endregion

        #region Ports

        public static Task<WifiManagerResponse<List<PortCheckResult>>> CheckPortsAsync(string host, IEnumerable<int> ports,
            IProgress<PortCheckResult>? progress, CancellationToken cancellationToken) =>
            Task.Run(() => CheckPortsCoreAsync(host, ports.Distinct().ToList(), progress, cancellationToken));

        private static async Task<WifiManagerResponse<List<PortCheckResult>>> CheckPortsCoreAsync(string host, List<int> ports,
            IProgress<PortCheckResult>? progress, CancellationToken cancellationToken)
        {
            if (ports.Count == 0 || ports.Any(p => p is < 1 or > 65535))
                return WifiManagerResponse<List<PortCheckResult>>.ErrorResponse(WifiErrorCodes.UnknownError, "Ports must be between 1 and 65535.");
            try
            {
                var target = await ResolveAsync(host, cancellationToken).ConfigureAwait(false);
                if (target == null)
                    return WifiManagerResponse<List<PortCheckResult>>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, $"Could not resolve {host}.");

                using var gate = new SemaphoreSlim(PortConcurrency);
                var results = await Task.WhenAll(ports.Select(async port =>
                {
                    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        var result = new PortCheckResult
                        {
                            Port = port,
                            IsOpen = await IsPortOpenAsync(target, port, cancellationToken).ConfigureAwait(false),
                            Service = LanDeviceDetails.KnownPorts.TryGetValue(port, out var service) ? service : null
                        };
                        progress?.Report(result);
                        return result;
                    }
                    finally
                    {
                        gate.Release();
                    }
                })).ConfigureAwait(false);

                var sorted = results.OrderBy(r => r.Port).ToList();
                return WifiManagerResponse<List<PortCheckResult>>.SuccessResponse(sorted, $"{sorted.Count(r => r.IsOpen)} of {sorted.Count} open.");
            }
            catch (OperationCanceledException)
            {
                return WifiManagerResponse<List<PortCheckResult>>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");
            }
        }

        private static async Task<bool> IsPortOpenAsync(IPAddress address, int port, CancellationToken cancellationToken)
        {
            using var client = new TcpClient(address.AddressFamily);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(PortTimeout);
            try
            {
                await client.ConnectAsync(address, port, timeout.Token).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        #endregion

        #region Wake-on-LAN

        public static async Task<WifiManagerResponse<bool>> WakeOnLanAsync(string macAddress, string? broadcastAddress, CancellationToken cancellationToken)
        {
            if (!TryParseMac(macAddress, out var mac))
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, "Enter a MAC address like AA:BB:CC:DD:EE:FF.");

            IPAddress? subnetBroadcast = null;
            if (!string.IsNullOrWhiteSpace(broadcastAddress) && !IPAddress.TryParse(broadcastAddress, out subnetBroadcast))
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.UnknownError, "The broadcast address must be an IPv4 address.");

            var packet = BuildMagicPacket(mac);
            try
            {
                using var client = new UdpClient { EnableBroadcast = true };

                // Both the subnet's broadcast and the limited broadcast: some routers drop one or the other.
                foreach (var target in new[] { subnetBroadcast, IPAddress.Broadcast }.OfType<IPAddress>().Distinct())
                    await client.SendAsync(packet, new IPEndPoint(target, WakeOnLanPort), cancellationToken).ConfigureAwait(false);

                return WifiManagerResponse<bool>.SuccessResponse(true, "Magic packet sent.");
            }
            catch (OperationCanceledException)
            {
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.OperationCanceled, "Operation was canceled.");
            }
            catch (SocketException ex)
            {
                return WifiManagerResponse<bool>.ErrorResponse(WifiErrorCodes.NetworkUnavailable, ex.Message);
            }
        }

        /// <summary>Six 0xFF bytes followed by the MAC address sixteen times.</summary>
        internal static byte[] BuildMagicPacket(byte[] mac)
        {
            var packet = new byte[6 + 16 * 6];
            Array.Fill(packet, (byte)0xFF, 0, 6);
            for (int i = 0; i < 16; i++)
                Array.Copy(mac, 0, packet, 6 + i * 6, 6);
            return packet;
        }

        internal static bool TryParseMac(string? text, out byte[] mac)
        {
            mac = Array.Empty<byte>();
            var hex = new string((text ?? string.Empty).Where(Uri.IsHexDigit).ToArray());
            if (hex.Length != 12 || (text ?? string.Empty).Any(c => !Uri.IsHexDigit(c) && c is not (':' or '-' or '.' or ' ')))
                return false;
            mac = Convert.FromHexString(hex);
            return true;
        }

        #endregion
    }
}

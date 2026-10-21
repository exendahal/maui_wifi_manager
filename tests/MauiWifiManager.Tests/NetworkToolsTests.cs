using MauiWifiManager.Abstractions;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MauiWifiManager.Tests;

public class NetworkDataAddressTests
{
    private static int Ip(string address) => BitConverter.ToInt32(IPAddress.Parse(address).GetAddressBytes());

    [Theory]
    [InlineData("192.168.1.95", "255.255.255.0", "192.168.1.255", 24)]
    [InlineData("10.20.30.40", "255.255.0.0", "10.20.255.255", 16)]
    [InlineData("172.16.5.9", "255.255.255.252", "172.16.5.11", 30)]
    [InlineData("192.168.4.2", "255.255.254.0", "192.168.5.255", 23)]
    public void BroadcastAndPrefix_AreCalculatedFromIpAndMask(string ip, string mask, string broadcast, int prefix)
    {
        var data = new NetworkData { IpAddress = Ip(ip), SubnetMask = mask };

        Assert.Equal(broadcast, data.BroadcastAddress);
        Assert.Equal(prefix, data.NetworkPrefixLength);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-mask")]
    [InlineData("ffff::")]
    public void BroadcastAndPrefix_InvalidMask_AreNull(string? mask)
    {
        var data = new NetworkData { IpAddress = Ip("192.168.1.95"), SubnetMask = mask };

        Assert.Null(data.BroadcastAddress);
        Assert.Null(data.NetworkPrefixLength);
    }

    [Fact]
    public void BroadcastAndPrefix_NoIpAddress_AreNull()
    {
        var data = new NetworkData { SubnetMask = "255.255.255.0" };

        Assert.Null(data.BroadcastAddress);
        Assert.Null(data.NetworkPrefixLength);
    }
}

public class SweepHostTests
{
    [Fact]
    public void Slash24_ListsEveryHostExceptSelf()
    {
        var hosts = NetworkTools.GetSweepHosts(IPAddress.Parse("192.168.1.95"), 24);

        Assert.Equal(253, hosts.Count);
        Assert.Equal("192.168.1.1", hosts[0].ToString());
        Assert.Equal("192.168.1.254", hosts[^1].ToString());
        Assert.DoesNotContain(hosts, h => h.ToString() == "192.168.1.95");
    }

    [Fact]
    public void LargerSubnet_IsLimitedToSurroundingSlash24()
    {
        var hosts = NetworkTools.GetSweepHosts(IPAddress.Parse("10.1.2.3"), 8);

        Assert.Equal(253, hosts.Count);
        Assert.All(hosts, h => Assert.StartsWith("10.1.2.", h.ToString()));
    }

    [Fact]
    public void Slash30_HasOneOtherHost()
    {
        var hosts = NetworkTools.GetSweepHosts(IPAddress.Parse("172.16.5.9"), 30);

        Assert.Equal(new[] { "172.16.5.10" }, hosts.Select(h => h.ToString()));
    }
}

public class MdnsTests
{
    [Fact]
    public void BuildPtrQuery_EncodesQuestion()
    {
        var query = Mdns.BuildPtrQuery("_http._tcp.local");

        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 }, query[..12]);
        int offset = 12;
        Assert.Equal("_http._tcp.local", Mdns.ReadName(query, ref offset));
        Assert.Equal(new byte[] { 0, 12, 0, 1 }, query[offset..]);
    }

    [Fact]
    public void ParseAndBuild_JoinsPtrSrvTxtAndARecords()
    {
        var response = new ResponseBuilder()
            .Ptr("_http._tcp.local", "Office Printer._http._tcp.local")
            .Srv("Office Printer._http._tcp.local", "printer.local", 8080)
            .Txt("Office Printer._http._tcp.local", "path=/admin", "secure")
            .A("printer.local", "192.168.1.50")
            .Build();

        var services = Mdns.BuildServices("_http._tcp", Mdns.ParseRecords(response));

        var service = Assert.Single(services);
        Assert.Equal("Office Printer", service.Name);
        Assert.Equal("_http._tcp", service.ServiceType);
        Assert.Equal("printer.local", service.HostName);
        Assert.Equal(8080, service.Port);
        Assert.Equal("192.168.1.50", service.IpAddress);
        Assert.Equal("/admin", service.Properties["path"]);
        Assert.Equal(string.Empty, service.Properties["secure"]);
    }

    [Fact]
    public void BuildServices_IgnoresOtherServiceTypes()
    {
        var response = new ResponseBuilder()
            .Ptr("_ipp._tcp.local", "Printer._ipp._tcp.local")
            .Build();

        Assert.Empty(Mdns.BuildServices("_http._tcp", Mdns.ParseRecords(response)));
    }

    [Fact]
    public void BuildServices_MetaQuery_ReturnsServiceTypes()
    {
        var response = new ResponseBuilder()
            .Ptr("_services._dns-sd._udp.local", "_googlecast._tcp.local")
            .Ptr("_services._dns-sd._udp.local", "_http._tcp.local")
            .Build();

        var services = Mdns.BuildServices("_services._dns-sd._udp", Mdns.ParseRecords(response));

        Assert.Equal(new[] { "_googlecast._tcp", "_http._tcp" }, services.Select(s => s.Name));
    }

    [Fact]
    public void ServiceWithoutSrv_StillListedWithoutHost()
    {
        var response = new ResponseBuilder().Ptr("_http._tcp.local", "Bare._http._tcp.local").Build();

        var service = Assert.Single(Mdns.BuildServices("_http._tcp", Mdns.ParseRecords(response)));
        Assert.Equal("Bare", service.Name);
        Assert.Null(service.HostName);
        Assert.Equal(0, service.Port);
    }

    [Fact]
    public void ReadName_FollowsCompressionPointer()
    {
        // "local" at offset 0, then "printer" + pointer to offset 0.
        var message = new byte[] { 5, (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'l', 0,
                                   7, (byte)'p', (byte)'r', (byte)'i', (byte)'n', (byte)'t', (byte)'e', (byte)'r', 0xC0, 0 };
        int offset = 7;

        Assert.Equal("printer.local", Mdns.ReadName(message, ref offset));
        Assert.Equal(message.Length, offset);
    }

    [Fact]
    public void ReadName_PointerLoop_Throws()
    {
        var message = new byte[] { 0xC0, 0 };
        int offset = 0;

        Assert.Throws<ArgumentException>(() => Mdns.ReadName(message, ref offset));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0, 0, 0x84, 0, 0, 0, 0, 5, 0, 0, 0, 0 })] // claims 5 answers, has none
    [InlineData(new byte[] { 0, 0, 0x84, 0, 0, 0, 0, 1, 0, 0, 0, 0, 3, 1, 2 })] // truncated name
    public void ParseRecords_MalformedPacket_ReturnsWhatItCanWithoutThrowing(byte[] message)
    {
        Assert.Empty(Mdns.ParseRecords(message));
    }

    /// <summary>Builds an mDNS response with uncompressed names.</summary>
    private sealed class ResponseBuilder
    {
        private readonly List<byte[]> _Records = new();

        public ResponseBuilder Ptr(string name, string target) => Add(name, Mdns.TypePtr, Name(target));

        public ResponseBuilder Srv(string name, string target, int port) =>
            Add(name, Mdns.TypeSrv, new byte[] { 0, 0, 0, 0, (byte)(port >> 8), (byte)port }.Concat(Name(target)).ToArray());

        public ResponseBuilder Txt(string name, params string[] entries) =>
            Add(name, Mdns.TypeTxt, entries.SelectMany(e => new[] { (byte)Encoding.UTF8.GetByteCount(e) }.Concat(Encoding.UTF8.GetBytes(e))).ToArray());

        public ResponseBuilder A(string name, string address) => Add(name, Mdns.TypeA, IPAddress.Parse(address).GetAddressBytes());

        public byte[] Build()
        {
            var header = new byte[] { 0, 0, 0x84, 0, 0, 0, (byte)(_Records.Count >> 8), (byte)_Records.Count, 0, 0, 0, 0 };
            return header.Concat(_Records.SelectMany(r => r)).ToArray();
        }

        private ResponseBuilder Add(string name, ushort type, byte[] data)
        {
            var record = Name(name)
                .Concat(new byte[] { (byte)(type >> 8), (byte)type, 0x80, 1, 0, 0, 0x11, 0x94, (byte)(data.Length >> 8), (byte)data.Length })
                .Concat(data)
                .ToArray();
            _Records.Add(record);
            return this;
        }

        private static byte[] Name(string name) =>
            name.Split('.').SelectMany(l => new[] { (byte)Encoding.UTF8.GetByteCount(l) }.Concat(Encoding.UTF8.GetBytes(l))).Append((byte)0).ToArray();
    }
}

public class ConnectionMonitorTests
{
    [Fact]
    public void Map_TranslatesProfilesAndRemovesDuplicates()
    {
        var types = ConnectionMonitor.Map(new[]
        {
            Microsoft.Maui.Networking.ConnectionProfile.WiFi,
            Microsoft.Maui.Networking.ConnectionProfile.Cellular,
            Microsoft.Maui.Networking.ConnectionProfile.WiFi,
            Microsoft.Maui.Networking.ConnectionProfile.Ethernet,
            Microsoft.Maui.Networking.ConnectionProfile.Bluetooth,
            Microsoft.Maui.Networking.ConnectionProfile.Unknown,
        });

        Assert.Equal(new[]
        {
            NetworkConnectionType.WiFi, NetworkConnectionType.Cellular, NetworkConnectionType.Ethernet,
            NetworkConnectionType.Bluetooth, NetworkConnectionType.Unknown
        }, types);
    }

    [Fact]
    public void GetConnectionTypes_WithoutPlatform_ReportsUnsupported()
    {
        Assert.Equal(WifiErrorCodes.UnsupportedHardware, ConnectionMonitor.GetConnectionTypes().ErrorCode);
    }

    [Fact]
    public void Event_WithoutPlatform_SubscribeAndUnsubscribeDoNotThrow()
    {
        IWifiNetworkService service = new WifiNetworkService();
        EventHandler<ConnectionTypeChangedEventArgs> handler = (_, _) => { };

        service.ConnectionTypeChanged += handler;
        service.ConnectionTypeChanged -= handler;
    }
}

public class NetworkToolsTests
{
    [Fact]
    public async Task Ping_Localhost_Replies()
    {
        var response = await NetworkTools.PingAsync("127.0.0.1", 2, CancellationToken.None);

        Assert.Equal(WifiErrorCodes.Success, response.ErrorCode);
        Assert.Equal(2, response.Data!.Sent);
        Assert.Equal(2, response.Data.Received);
        Assert.Equal(0, response.Data.PacketLossPercent);
        Assert.NotNull(response.Data.AverageMs);
    }

    [Fact]
    public async Task Ping_InvalidCount_IsRejected()
    {
        var response = await NetworkTools.PingAsync("127.0.0.1", 0, CancellationToken.None);

        Assert.NotEqual(WifiErrorCodes.Success, response.ErrorCode);
    }

    [Fact]
    public void PingStatistics_PacketLoss()
    {
        Assert.Equal(25, new PingStatistics { Sent = 4, Received = 3 }.PacketLossPercent);
        Assert.Equal(0, new PingStatistics().PacketLossPercent);
    }

    [Theory]
    [InlineData(204, true)]
    [InlineData(200, true)]
    [InlineData(302, false)] // captive portals redirect
    [InlineData(503, false)]
    public async Task ProbeInternet_UsesStatusCode(int status, bool expected)
    {
        using var server = new TestServer.LocalServer(context =>
        {
            context.Response.StatusCode = status;
            if (status == 302) context.Response.RedirectLocation = "http://portal.example/";
        });

        var response = await NetworkTools.ProbeInternetAsync(server.Url, CancellationToken.None);

        Assert.Equal(WifiErrorCodes.Success, response.ErrorCode);
        Assert.Equal(expected, response.Data);
    }

    [Fact]
    public async Task ProbeInternet_Unreachable_ReportsFalse()
    {
        var response = await NetworkTools.ProbeInternetAsync(new Uri($"http://127.0.0.1:{TestServer.FreePort()}/"), CancellationToken.None);

        Assert.Equal(WifiErrorCodes.Success, response.ErrorCode);
        Assert.False(response.Data);
    }

    [Fact]
    public async Task MeasureSpeed_AgainstLocalServer_ReportsAllFigures()
    {
        using var server = new TestServer.LocalServer(context =>
        {
            if (context.Request.HttpMethod == "POST")
            {
                context.Request.InputStream.CopyTo(Stream.Null);
                return;
            }
            context.Response.ContentLength64 = 2_000_000;
            context.Response.OutputStream.Write(new byte[2_000_000]);
        });

        var response = await NetworkTools.MeasureSpeedAsync(new SpeedTestOptions
        {
            DownloadUrl = server.Url,
            UploadUrl = server.Url,
            UploadBytes = 1_000_000
        }, CancellationToken.None);

        Assert.Equal(WifiErrorCodes.Success, response.ErrorCode);
        Assert.True(response.Data!.DownloadMbps > 0);
        Assert.True(response.Data.UploadMbps > 0);
        Assert.True(response.Data.LatencyMs >= 0);
    }

    [Fact]
    public async Task MeasureSpeed_NoUploadUrl_SkipsUpload()
    {
        using var server = new TestServer.LocalServer(context => context.Response.OutputStream.Write(new byte[1000]));

        var response = await NetworkTools.MeasureSpeedAsync(new SpeedTestOptions { DownloadUrl = server.Url, UploadUrl = null }, CancellationToken.None);

        Assert.Equal(WifiErrorCodes.Success, response.ErrorCode);
        Assert.Null(response.Data!.UploadMbps);
    }

    [Fact]
    public async Task MeasureSpeed_ServerError_ReportsNetworkUnavailable()
    {
        using var server = new TestServer.LocalServer(context => context.Response.StatusCode = 500);

        var response = await NetworkTools.MeasureSpeedAsync(new SpeedTestOptions { DownloadUrl = server.Url, UploadUrl = null }, CancellationToken.None);

        Assert.Equal(WifiErrorCodes.NetworkUnavailable, response.ErrorCode);
    }
}

/// <summary>Local HTTP server and free-port helper shared by the network tests.</summary>
internal static class TestServer
{
    public static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>Minimal HTTP server on localhost that runs <paramref name="handle"/> for every request.</summary>
    public sealed class LocalServer : IDisposable
    {
        private readonly HttpListener _Listener = new();

        public LocalServer(Action<HttpListenerContext> handle)
        {
            Url = new Uri($"http://localhost:{FreePort()}/");
            _Listener.Prefixes.Add(Url.ToString());
            _Listener.Start();
            _ = Task.Run(async () =>
            {
                while (_Listener.IsListening)
                {
                    HttpListenerContext context;
                    try { context = await _Listener.GetContextAsync(); }
                    catch { return; }
                    try { handle(context); }
                    catch { }
                    finally { try { context.Response.Close(); } catch { } }
                }
            });
        }

        public Uri Url { get; }

        public void Dispose() => _Listener.Close();
    }
}

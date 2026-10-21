using MauiWifiManager.Abstractions;
using System.Text;

namespace MauiWifiManager.Tests;

public class DeviceProbeTests
{
    [Fact]
    public void NetBiosQuery_IsWildcardNodeStatusRequest()
    {
        var query = DeviceProbe.BuildNetBiosQuery();

        Assert.Equal(50, query.Length);                       // 12 header + 34 name + 4 type/class
        Assert.Equal(new byte[] { 0, 1 }, query[4..6]);       // one question
        Assert.Equal(0x20, query[12]);                        // 32-byte encoded name
        Assert.Equal("CKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", Encoding.ASCII.GetString(query, 13, 32)); // "*" + 15 zero bytes
        Assert.Equal(new byte[] { 0, 0, 0x21, 0, 1 }, query[45..]); // end of name, NBSTAT, IN
    }

    [Fact]
    public void NetBiosResponse_ReturnsWorkstationNameAndMac()
    {
        var response = BuildNetBiosResponse(
            new[] { ("WORKGROUP", (byte)0x00, true), ("OFFICE-PC", (byte)0x20, false), ("OFFICE-PC", (byte)0x00, false) },
            new byte[] { 0xA4, 0x5E, 0x60, 0x12, 0x34, 0x56 });

        var (name, mac) = DeviceProbe.ParseNetBiosResponse(response);

        Assert.Equal("OFFICE-PC", name);
        Assert.Equal("A4:5E:60:12:34:56", mac);
    }

    [Fact]
    public void NetBiosResponse_ZeroUnitId_HasNoMac()
    {
        var response = BuildNetBiosResponse(new[] { ("NAS", (byte)0x00, false) }, new byte[6]);

        var (name, mac) = DeviceProbe.ParseNetBiosResponse(response);

        Assert.Equal("NAS", name);
        Assert.Null(mac);
    }

    [Fact]
    public void NetBiosResponse_Truncated_ReturnsNothing()
    {
        Assert.Equal((null, null), DeviceProbe.ParseNetBiosResponse(new byte[20]));
    }

    [Fact]
    public void SsdpResponse_LocationHeaderIsCaseInsensitive()
    {
        var response = "HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age=1800\r\nLocation: http://192.168.1.1:1900/igd.xml\r\nST: upnp:rootdevice\r\n\r\n";

        Assert.Equal("http://192.168.1.1:1900/igd.xml", DeviceProbe.ParseSsdpLocation(response));
        Assert.Null(DeviceProbe.ParseSsdpLocation("HTTP/1.1 200 OK\r\n\r\n"));
    }

    [Fact]
    public void UpnpDescription_ReadsDeviceFields()
    {
        const string xml = """
            <?xml version="1.0"?>
            <root xmlns="urn:schemas-upnp-org:device-1-0">
              <specVersion><major>1</major><minor>0</minor></specVersion>
              <device>
                <deviceType>urn:schemas-upnp-org:device:InternetGatewayDevice:1</deviceType>
                <friendlyName> Archer C6 </friendlyName>
                <manufacturer>TP-Link</manufacturer>
                <modelName>Archer C6</modelName>
                <modelNumber>2.0</modelNumber>
                <deviceList><device><friendlyName>WAN Device</friendlyName></device></deviceList>
              </device>
            </root>
            """;

        var info = DeviceProbe.ParseUpnpDescription(xml);

        Assert.Equal("Archer C6", info.FriendlyName);
        Assert.Equal("TP-Link", info.Manufacturer);
        Assert.Equal("Archer C6", info.ModelName);
        Assert.Equal("2.0", info.ModelNumber);
    }

    [Fact]
    public void UpnpDescription_MissingFields_AreNull()
    {
        var info = DeviceProbe.ParseUpnpDescription("<root><device><friendlyName></friendlyName></device></root>");

        Assert.Null(info.FriendlyName);
        Assert.Null(info.Manufacturer);
    }

    [Theory]
    [InlineData("A4:5E:60:12:34:56", false)] // universally administered (vendor) address
    [InlineData("DA:A1:19:00:11:22", true)]  // locally administered: private/randomized
    [InlineData("02:00:00:00:00:00", true)]
    [InlineData(null, null)]
    public void IsRandomizedMac_UsesLocallyAdministeredBit(string? mac, bool? expected)
    {
        Assert.Equal(expected, new LanDeviceDetails { MacAddress = mac }.IsRandomizedMac);
    }

    [Fact]
    public void KnownPorts_CoverCommonServices()
    {
        Assert.Equal("Web (HTTP)", LanDeviceDetails.KnownPorts[80]);
        Assert.Equal("Printing (raw)", LanDeviceDetails.KnownPorts[9100]);
        Assert.Contains(8009, LanDeviceDetails.KnownPorts.Keys);
    }

    [Fact]
    public async Task WebPage_ReadsTitleAndServer()
    {
        using var server = new TestServer.LocalServer(context =>
        {
            context.Response.Headers["Server"] = "TestServer/1.0";
            context.Response.OutputStream.Write(Encoding.UTF8.GetBytes("<html><head><title> Printer &amp; Scanner </title></head></html>"));
        });

        var (serverHeader, title) = await DeviceProbe.ReadWebPageAsync(server.Url, CancellationToken.None);

        Assert.Equal("Printer & Scanner", title);
        Assert.Contains("TestServer", serverHeader);
    }

    [Fact]
    public async Task WebPage_EmptyTitle_FollowsMetaRefreshOnce()
    {
        using var server = new TestServer.LocalServer(context =>
        {
            var body = context.Request.Url!.AbsolutePath == "/cgi-bin/luci/web"
                ? "<title>Mi Router</title>"
                : "<title></title><noscript><meta http-equiv=\"refresh\" content=\"0; url=/cgi-bin/luci/web\" /></noscript>";
            context.Response.OutputStream.Write(Encoding.UTF8.GetBytes(body));
        });

        var (_, title) = await DeviceProbe.ReadWebPageAsync(server.Url, CancellationToken.None);

        Assert.Equal("Mi Router", title);
    }

    [Fact]
    public async Task WebPage_Unreachable_ReturnsNothing()
    {
        var result = await DeviceProbe.ReadWebPageAsync(new Uri($"http://127.0.0.1:{TestServer.FreePort()}/"), CancellationToken.None);

        Assert.Equal((null, null), result);
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("fe80::1")]
    public async Task GetDetails_RejectsNonIpv4(string address)
    {
        var response = await DeviceProbe.GetDetailsAsync(address, CancellationToken.None);

        Assert.NotEqual(WifiErrorCodes.Success, response.ErrorCode);
    }

    [Fact]
    public async Task GetDetails_Localhost_ReturnsPing()
    {
        var response = await DeviceProbe.GetDetailsAsync("127.0.0.1", CancellationToken.None);

        Assert.Equal(WifiErrorCodes.Success, response.ErrorCode);
        Assert.Equal("127.0.0.1", response.Data!.IpAddress);
        Assert.True(response.Data.Ping!.Received > 0);
    }

    /// <summary>Builds a NetBIOS node status response with the given (name, suffix, group) entries and unit ID.</summary>
    private static byte[] BuildNetBiosResponse((string Name, byte Suffix, bool Group)[] names, byte[] unitId)
    {
        var bytes = new List<byte> { 0x13, 0x37, 0x84, 0x00, 0, 0, 0, 1, 0, 0, 0, 0, 0x20 };
        bytes.AddRange(Encoding.ASCII.GetBytes(new string('A', 32)));
        bytes.AddRange(new byte[] { 0, 0, 0x21, 0, 1, 0, 0, 0, 0 });     // end of name, type, class, TTL
        int dataLength = 1 + names.Length * 18 + 46;
        bytes.Add((byte)(dataLength >> 8));
        bytes.Add((byte)dataLength);
        bytes.Add((byte)names.Length);
        foreach (var (name, suffix, group) in names)
        {
            bytes.AddRange(Encoding.ASCII.GetBytes(name.PadRight(15)));
            bytes.Add(suffix);
            bytes.Add(group ? (byte)0x84 : (byte)0x04);
            bytes.Add(0);
        }
        bytes.AddRange(unitId);
        bytes.AddRange(new byte[40]); // remaining statistics
        return bytes.ToArray();
    }
}

using MauiWifiManager.Abstractions;
using System.Net;
using System.Net.Sockets;

namespace MauiWifiManager.Tests;

public class NetworkDiagnosticsTests
{
    [Fact]
    public void BuildDnsQuery_EncodesHeaderLabelsAndType()
    {
        var query = NetworkDiagnostics.BuildDnsQuery(0x1234, "example.com", DnsRecordType.Mx);

        byte[] expected =
        [
            0x12, 0x34, 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 0,
            7, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e',
            3, (byte)'c', (byte)'o', (byte)'m', 0,
            0, 15, 0, 1
        ];
        Assert.Equal(expected, query);
    }

    [Fact]
    public void ParseDnsAnswer_ReadsCnameAndAWithCompressedNames()
    {
        var message = Response(0x00, 2,
            // www.example.com CNAME example.com (pointer to offset 16, "example" in the question).
            [0xC0, 12, 0, 5, 0, 1, 0, 0, 0x0E, 0x10, 0, 2, 0xC0, 16],
            // example.com A 93.184.216.34, TTL 300.
            [0xC0, 16, 0, 1, 0, 1, 0, 0, 0x01, 0x2C, 0, 4, 93, 184, 216, 34]);

        var (notFound, records) = NetworkDiagnostics.ParseDnsAnswer(message);

        Assert.False(notFound);
        Assert.Equal(2, records.Count);
        Assert.Equal(DnsRecordType.Cname, records[0].Type);
        Assert.Equal("www.example.com", records[0].Name);
        Assert.Equal("example.com", records[0].Value);
        Assert.Equal(3600, records[0].TimeToLiveSeconds);
        Assert.Equal("93.184.216.34", records[1].Value);
        Assert.Equal(300, records[1].TimeToLiveSeconds);
    }

    [Fact]
    public void ParseDnsAnswer_ReadsMxAndSplitTxt()
    {
        var message = Response(0x00, 2,
            [0xC0, 12, 0, 15, 0, 1, 0, 0, 0, 60, 0, 4, 0, 10, 0xC0, 16],
            [0xC0, 12, 0, 16, 0, 1, 0, 0, 0, 60, 0, 6, 2, (byte)'a', (byte)'b', 2, (byte)'c', (byte)'d']);

        var (_, records) = NetworkDiagnostics.ParseDnsAnswer(message);

        Assert.Equal("10 example.com", records[0].Value);
        Assert.Equal("abcd", records[1].Value);
    }

    [Fact]
    public void ParseDnsAnswer_NxDomain_ReportsNotFound()
    {
        var (notFound, records) = NetworkDiagnostics.ParseDnsAnswer(Response(0x03, 0));

        Assert.True(notFound);
        Assert.Empty(records);
    }

    [Fact]
    public void ParseDnsAnswer_Truncated_KeepsWhatWasRead()
    {
        var message = Response(0x00, 2, [0xC0, 16, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 1, 2, 3, 4]);

        var (_, records) = NetworkDiagnostics.ParseDnsAnswer(message);

        Assert.Single(records);
    }

    [Theory]
    [InlineData("AA:BB:CC:DD:EE:FF")]
    [InlineData("aa-bb-cc-dd-ee-ff")]
    [InlineData("aabb.ccdd.eeff")]
    [InlineData("AABBCCDDEEFF")]
    public void TryParseMac_AcceptsCommonFormats(string text)
    {
        Assert.True(NetworkDiagnostics.TryParseMac(text, out var mac));
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF }, mac);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AA:BB:CC:DD:EE")]
    [InlineData("AA:BB:CC:DD:EE:FF:00")]
    [InlineData("GG:BB:CC:DD:EE:FF")]
    public void TryParseMac_RejectsMalformed(string? text)
    {
        Assert.False(NetworkDiagnostics.TryParseMac(text, out _));
    }

    [Fact]
    public void BuildMagicPacket_IsSyncStreamThenMacSixteenTimes()
    {
        byte[] mac = [1, 2, 3, 4, 5, 6];

        var packet = NetworkDiagnostics.BuildMagicPacket(mac);

        Assert.Equal(102, packet.Length);
        Assert.All(packet.Take(6), b => Assert.Equal(0xFF, b));
        for (int i = 0; i < 16; i++)
            Assert.Equal(mac, packet.Skip(6 + i * 6).Take(6));
    }

    [Fact]
    public async Task WakeOnLanAsync_InvalidMac_ReturnsError()
    {
        var result = await NetworkDiagnostics.WakeOnLanAsync("nope", null, CancellationToken.None);

        Assert.NotEqual(WifiErrorCodes.Success, result.ErrorCode);
    }

    [Fact]
    public async Task CheckPortsAsync_FindsListeningPortAndClosedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int open = ((IPEndPoint)listener.LocalEndpoint).Port;
        int closed = FreePort();
        var reported = new List<PortCheckResult>();

        var result = await NetworkDiagnostics.CheckPortsAsync("127.0.0.1", [closed, open, open],
            new SyncProgress<PortCheckResult>(reported.Add), CancellationToken.None);

        Assert.Equal(WifiErrorCodes.Success, result.ErrorCode);
        Assert.Equal(2, result.Data!.Count);
        Assert.True(result.Data.Single(r => r.Port == open).IsOpen);
        Assert.False(result.Data.Single(r => r.Port == closed).IsOpen);
        Assert.Equal(2, reported.Count);
    }

    [Fact]
    public async Task CheckPortsAsync_OutOfRangePort_ReturnsError()
    {
        var result = await NetworkDiagnostics.CheckPortsAsync("127.0.0.1", [0], null, CancellationToken.None);

        Assert.NotEqual(WifiErrorCodes.Success, result.ErrorCode);
    }

    /// <summary>A DNS response for "www.example.com" (type A) with the given rcode and answer records.</summary>
    private static byte[] Response(byte rcode, int answerCount, params byte[][] answers)
    {
        var message = NetworkDiagnostics.BuildDnsQuery(1, "www.example.com", DnsRecordType.A);
        message[2] = 0x81;
        message[3] = (byte)(0x80 | rcode);
        message[7] = (byte)answerCount;
        return message.Concat(answers.SelectMany(a => a)).ToArray();
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    /// <summary>Progress that reports on the calling thread, so the test sees every report before the task ends.</summary>
    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value)
        {
            lock (this) report(value);
        }
    }
}

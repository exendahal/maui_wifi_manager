using MauiWifiManager.Abstractions;
using System.Net;
using System.Net.Sockets;

namespace MauiWifiManager.Tests;

public class SpeedTestProgressTests
{
    [Fact]
    public async Task MeasureSpeed_ReportsEachPhaseWhileItRuns()
    {
        string prefix = $"http://127.0.0.1:{FreePort()}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        var server = ServeAsync(listener);

        var reports = new List<SpeedTestProgress>();
        var options = new SpeedTestOptions
        {
            LatencyUrl = new Uri(prefix + "latency"),
            DownloadUrl = new Uri(prefix + "download"),
            UploadUrl = new Uri(prefix + "upload"),
            UploadBytes = 2_000_000,
            MaxDurationPerDirection = TimeSpan.FromSeconds(5)
        };

        var result = await NetworkTools.MeasureSpeedCoreAsync(options, new SyncProgress<SpeedTestProgress>(reports.Add), CancellationToken.None);
        listener.Stop();
        await server;

        Assert.Equal(WifiErrorCodes.Success, result.ErrorCode);
        Assert.True(result.Data!.DownloadMbps > 0);
        Assert.Contains(reports, r => r.Phase == SpeedTestPhase.Latency && r.LatencyMs != null);
        Assert.Contains(reports, r => r.Phase == SpeedTestPhase.Download && r.Mbps > 0 && r.Fraction is > 0 and < 1);
        Assert.Contains(reports, r => r.Phase == SpeedTestPhase.Upload);
        Assert.All(reports, r => Assert.InRange(r.Fraction, 0, 1));

        // Phases arrive in order and never go back.
        var phases = reports.Select(r => r.Phase).ToList();
        Assert.Equal(phases.OrderBy(p => p), phases);
    }

    [Fact]
    public void Snapshot_UsesBytesWhenTheSizeIsKnownAndTimeOtherwise()
    {
        var options = new SpeedTestOptions { MaxDurationPerDirection = TimeSpan.FromSeconds(10) };

        var bySize = NetworkTools.Snapshot(SpeedTestPhase.Download, 750_000, 1_000_000, TimeSpan.FromSeconds(1), options, 12);
        var byTime = NetworkTools.Snapshot(SpeedTestPhase.Download, 750_000, 0, TimeSpan.FromSeconds(2), options, 12);

        Assert.Equal(0.75, bySize.Fraction, 3);
        Assert.Equal(6, bySize.Mbps, 3);
        Assert.Equal(0.2, byTime.Fraction, 3);
        Assert.Equal(12, bySize.LatencyMs);
    }

    /// <summary>Latency: empty reply. Download: 1.5 MB, sent slowly. Upload: reads the body.</summary>
    private static async Task ServeAsync(HttpListener listener)
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            string path = context.Request.Url!.AbsolutePath;
            if (path == "/download")
            {
                var chunk = new byte[50_000];
                context.Response.ContentLength64 = chunk.Length * 30;
                for (int i = 0; i < 30; i++)
                {
                    await context.Response.OutputStream.WriteAsync(chunk);
                    await Task.Delay(30);
                }
            }
            else if (path == "/upload")
            {
                await context.Request.InputStream.CopyToAsync(Stream.Null);
            }
            context.Response.Close();
        }
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value)
        {
            lock (this) report(value);
        }
    }
}

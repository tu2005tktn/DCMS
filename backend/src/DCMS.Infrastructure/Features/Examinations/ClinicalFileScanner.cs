using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using DCMS.Application.Features.Examinations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DCMS.Infrastructure.Features.Examinations;

public sealed class ClamAvClinicalFileScanner(IConfiguration config) : IClinicalFileScanner
{
    public async Task<string> ScanAsync(byte[] content, CancellationToken ct)
    {
        var host = config["ClinicalDocuments:ClamAvHost"];
        if (string.IsNullOrWhiteSpace(host)) return "PendingScan";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try {
            using var client = new TcpClient(); await client.ConnectAsync(host, config.GetValue("ClinicalDocuments:ClamAvPort", 3310), timeout.Token);
            await using var stream = client.GetStream(); await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);
            var length = new byte[4];
            for (var offset = 0; offset < content.Length; offset += 65536) {
                var count = Math.Min(65536, content.Length - offset); BinaryPrimitives.WriteInt32BigEndian(length, count);
                await stream.WriteAsync(length, timeout.Token); await stream.WriteAsync(content.AsMemory(offset, count), timeout.Token);
            }
            await stream.WriteAsync(new byte[4], timeout.Token);
            var buffer = new byte[4096]; var answer = new StringBuilder();
            while (answer.Length < 4096) {
                var n = await stream.ReadAsync(buffer, timeout.Token); if (n == 0) break;
                answer.Append(Encoding.UTF8.GetString(buffer, 0, n)); if (answer.ToString().Contains('\0')) break;
            }
            var result = answer.ToString().TrimEnd('\0', '\n', '\r');
            return result == "stream: OK" ? "Ready" : result.StartsWith("stream: ") && result.EndsWith(" FOUND") ? "Rejected" : "PendingScan";
        } catch (Exception ex) when (ex is SocketException or IOException || ex is OperationCanceledException && !ct.IsCancellationRequested) { return "PendingScan"; }
    }
}

public sealed class ClinicalScanWorker(IServiceScopeFactory scopes, ILogger<ClinicalScanWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<IClinicalDocumentService>().ScanPendingAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Clinical document scan deferred; pending files remain unavailable."); }
        }
    }
}

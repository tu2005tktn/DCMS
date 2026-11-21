using DCMS.Application.Features.Examinations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DCMS.Infrastructure.Features.Examinations;

// No virus scanning (ClamAV removed 20/11/2026). Files already passed MIME, extension, content-signature and size
// checks at upload; they become Ready and are audited as ScanSkipped. See docs/specs/S19-S20-DCMS-112.md.
public sealed class DisabledClinicalFileScanner : IClinicalFileScanner
{
    public bool Enabled => false;
    public Task<string> ScanAsync(byte[] content, CancellationToken ct) => Task.FromResult("Ready");
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

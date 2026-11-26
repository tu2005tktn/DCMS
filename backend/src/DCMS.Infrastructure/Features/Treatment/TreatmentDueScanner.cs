using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed class TreatmentDueScanWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<TreatmentDueScanWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<CareTaskIngestionService>().IngestAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Treatment due scan failed; the next scan will retry"); }
            try { await Task.Delay(NextDelay(clock.GetUtcNow()), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    // Hourly, and right after the clinic day (UTC+7) changes, when the previous day's missed bookings become due.
    private static TimeSpan NextDelay(DateTimeOffset now)
    {
        var local = now.ToOffset(TimeSpan.FromHours(7));
        var untilTomorrow = local.Date.AddDays(1) - local.DateTime + TimeSpan.FromSeconds(5);
        return untilTomorrow < TimeSpan.FromHours(1) ? untilTomorrow : TimeSpan.FromHours(1);
    }
}

using DCMS.Application.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DCMS.Infrastructure.Features.Treatment;

// Reconciles pending links even if the browser is closed or a webhook cannot reach a local API.
public sealed class PayOsOrderWorker(IServiceScopeFactory scopes, ILogger<PayOsOrderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
                    var orders = await db.DossierPayOsOrders.AsNoTracking().Where(x => x.State == "Pending")
                        .OrderBy(x => x.CreatedAt).Take(30)
                        .Select(x => new { x.Id, x.DossierId, x.CreatedBy }).ToArrayAsync(stoppingToken);
                    foreach (var order in orders)
                    {
                        try
                        {
                            using var itemScope = scopes.CreateScope();
                            await itemScope.ServiceProvider.GetRequiredService<IPayOsOrderService>()
                                .GetAsync(order.CreatedBy, order.DossierId, order.Id, stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                        catch (Exception ex) { logger.LogWarning(ex, "PayOS reconciliation failed for order {OrderId}", order.Id); }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex) { logger.LogWarning(ex, "PayOS reconciliation cycle failed"); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}

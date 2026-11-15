using System.Text.Json;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Api.Features.Finance;

public sealed class PendingTreatmentPayOsRow
{
    public string Target { get; set; } = "";
    public string Payload { get; set; } = "";
}

public sealed class TreatmentPayOsWorker(IServiceScopeFactory scopes, ILogger<TreatmentPayOsWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var gateway = scope.ServiceProvider.GetRequiredService<TreatmentPayOsGateway>();
                if (gateway.Configured)
                {
                    var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
                    var cutoff = DateTimeOffset.UtcNow.AddHours(-2);
                    var pending = await db.Database.SqlQuery<PendingTreatmentPayOsRow>($"""
                        SELECT orders."Target" AS "Target", orders."PayloadJson"::text AS "Payload"
                        FROM "AUDIT_LOG" orders
                        WHERE orders."AuditType" = 'FinancePayOsOrder' AND orders."At" >= {cutoff}
                          AND (orders."PayloadJson"->>'expiresAt')::timestamptz > now() - interval '30 minutes'
                          AND NOT EXISTS (SELECT 1 FROM "AUDIT_LOG" ledger
                              WHERE ledger."AuditType" = 'FinanceLedger'
                                AND ledger."Action" IN ('TreatmentTransferPayment', 'AdvanceTransfer')
                                AND ledger."PayloadJson"->>'orderId' = orders."StorageId"::text)
                        ORDER BY orders."At" DESC LIMIT 20
                        """).ToArrayAsync(stoppingToken);
                    foreach (var row in pending)
                    {
                        if (!Guid.TryParse(row.Target, out var dossierId)) continue;
                        var order = JsonSerializer.Deserialize<TreatmentPayOsOrder>(row.Payload,
                            new JsonSerializerOptions(JsonSerializerDefaults.Web));
                        if (order is null) continue;
                        // A query to payOS is authoritative. PENDING never changes the ledger.
                        await TreatmentPayOsEndpoints.Settle(order, dossierId, db, gateway, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning(exception, "Could not reconcile pending treatment payOS orders."); }
            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}

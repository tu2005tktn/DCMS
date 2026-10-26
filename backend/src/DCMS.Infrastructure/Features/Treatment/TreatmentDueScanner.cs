using System.Data;
using System.Text.Json;
using DCMS.Domain.Features.Treatment;
using DCMS.Application.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed class TreatmentDueScanner(DcmsDbContext db, ITreatmentLabReadinessPort lab, TimeProvider clock)
{
    public async Task<int> ScanAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({DCMS.Infrastructure.Features.Scheduling.WorkingScheduleService.TransactionLock})", ct);
        var due = await db.FollowUpInstructions.AsNoTracking().Where(x => x.Status == "NeedsBooking"
            && x.TargetOn <= today).ToListAsync(ct);
        var inserted = 0;
        foreach (var instruction in due)
        {
            // A version can change without changing the clinical return window (for example booking/cancel).
            var stable = $"followup-due:{instruction.Id}:{instruction.EarliestOn:yyyyMMdd}:{instruction.TargetOn:yyyyMMdd}:{instruction.LatestOn:yyyyMMdd}";
            if (await db.TreatmentProgressionEvents.AnyAsync(x => x.StableKey == stable, ct)) continue;
            db.TreatmentProgressionEvents.Add(new TreatmentProgressionEvent
            {
                AggregateId = instruction.Id, Kind = "followup.due", StableKey = stable,
                ActorId = 0, OccurredAt = clock.GetUtcNow(),
                PayloadJson = JsonSerializer.Serialize(new { instruction.Id, instruction.PatientId,
                    instruction.BranchId, instruction.TargetOn, instruction.LatestOn, instruction.Version })
            });
            inserted++;
        }
        var patients = await db.TreatmentDossiers.AsNoTracking().Where(x => x.State == "Open"
            && x.PlanState == "Committed")
            .ToDictionaryAsync(x => x.Id, x => x.PatientId, ct);
        var dossierIds = patients.Keys.ToArray();
        var steps = await db.TreatmentSteps.AsNoTracking().Where(x => dossierIds.Contains(x.DossierId))
            .ToListAsync(ct);
        var byId = steps.ToDictionary(x => x.Id);
        foreach (var step in steps)
        {
            if (step.Outcome == "Completed") continue;
            if (!patients.TryGetValue(step.DossierId, out var patientId)) continue;
            var predecessors = JsonSerializer.Deserialize<Guid[]>(step.PredecessorsJson) ?? [];
            if (predecessors.Any(id => !byId.TryGetValue(id, out var parent) || parent.Outcome != "Completed"))
            {
                var stable = $"prerequisite-blocked:{step.Id}:{step.PlanVersion}";
                if (!await db.TreatmentProgressionEvents.AnyAsync(x => x.StableKey == stable, ct))
                {
                    db.TreatmentProgressionEvents.Add(new TreatmentProgressionEvent
                    {
                        AggregateId = step.Id, Kind = "step.prerequisite_blocked", StableKey = stable,
                        ActorId = 0, OccurredAt = clock.GetUtcNow(),
                        PayloadJson = JsonSerializer.Serialize(new { step.Id, patientId, step.PlanVersion, predecessors })
                    });
                    inserted++;
                }
            }
            if (step.LabRequirement != null)
            {
                var result = await lab.CheckAsync(patientId, step.Id, step.LabRequirement, step.PlanVersion, ct);
                if (!result.Available || result.IsAccepted || string.IsNullOrWhiteSpace(result.BlockerEpisodeKey)) continue;
                var stable = $"lab-blocked:{step.Id}:{step.PlanVersion}:{result.BlockerEpisodeKey}";
                if (stable.Length > 160 || await db.TreatmentProgressionEvents.AnyAsync(x => x.StableKey == stable, ct)) continue;
                db.TreatmentProgressionEvents.Add(new TreatmentProgressionEvent
                {
                    AggregateId = step.Id, Kind = "step.lab_blocked", StableKey = stable,
                    ActorId = 0, OccurredAt = clock.GetUtcNow(),
                    PayloadJson = JsonSerializer.Serialize(new { step.Id, patientId, step.PlanVersion,
                        result.BlockerEpisodeKey })
                });
                inserted++;
            }
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return inserted;
    }
}

public sealed class TreatmentDueScanWorker(IServiceScopeFactory scopes, ILogger<TreatmentDueScanWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<TreatmentDueScanner>().ScanAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<CareTaskIngestionService>().IngestAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Treatment due scan failed; the next scan will retry"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

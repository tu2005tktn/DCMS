using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

// Docs: docs/specs/WORK-SERVICE-STATISTICS.md. Aggregated in memory at single-clinic scale; one group per treatment dossier.
public sealed class WorkStatisticsService(DcmsDbContext db, TimeProvider clock, DCMS.Application.Features.Settings.ISettingsAccess access) : IWorkStatisticsService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Snapshot(Guid ActivityId, int SheetVersion, ActivityDto Activity);
    private static BusinessException Bad() => new(400, SystemMessages.Get("MSG04"));

    public async Task<WorkStatisticsPage> ListAsync(int actorId, DateOnly? from, DateOnly? to, int? branchId, string? q, int page, int pageSize, CancellationToken ct)
    {
        var scope = await access.RequireAsync(actorId, "finance.work-statistics.read", ct);
        if (branchId.HasValue && !scope.BranchIds.Contains(branchId.Value))
            throw new BusinessException(403, SystemMessages.Get("MSG06"));
        if (pageSize is not (20 or 50 or 100) || page < 1 || page > int.MaxValue / pageSize || q?.Length > 160 || from > to) throw Bad();
        var dossierQuery = db.TreatmentDossiers.AsNoTracking().Where(d => d.State != "Deleted" && scope.BranchIds.Contains(d.BranchId));
        if (branchId.HasValue) dossierQuery = dossierQuery.Where(d => d.BranchId == branchId);
        if (from.HasValue) dossierQuery = dossierQuery.Where(d => d.StartedOn >= from);
        if (to.HasValue) dossierQuery = dossierQuery.Where(d => d.StartedOn <= to);
        var dossiers = await dossierQuery.ToListAsync(ct);
        var dossierIds = dossiers.Select(d => d.Id).ToArray();
        var source = await db.ClinicalActivities.AsNoTracking().Where(x => x.State == "Posted" && x.DossierId != null && dossierIds.Contains(x.DossierId.Value)).ToListAsync(ct);
        // Latest sheet edit/delete per activity wins (same rule as the treatment sheet).
        var events = await db.TreatmentEvents.AsNoTracking().Where(e => dossierIds.Contains(e.DossierId) &&
            (e.Kind == "SheetActivityEdited" || e.Kind == "SheetActivityDeleted")).OrderBy(e => e.DossierVersion).ToListAsync(ct);
        var overrides = new Dictionary<Guid, ActivityDto>();
        foreach (var e in events)
            if (JsonSerializer.Deserialize<Snapshot>(e.PayloadJson, Json) is { } s) overrides[s.ActivityId] = s.Activity;
        var activities = source.Select(x => overrides.TryGetValue(x.Id, out var o) ? o : new ActivityDto(x.Id, x.VisitId, x.PatientId, x.BranchId, x.DossierId,
                x.ItemId, x.StepId, x.PlanVersion, x.ServiceId, x.Region, x.Unit, x.Quantity, x.PerformedAt, x.ElapsedMinutes, x.PerformerId, x.Outcome,
                x.NextSteps, x.ChargingBasis, x.ChargingBasisEvidence, x.PriceSnapshotVnd, x.ReviewId, x.State, x.Version, x.CreatedAt, x.PostedAt,
                x.FeeStatus, x.DiscountSnapshotVnd, x.AssistantId, x.RecordedBy, x.SourceKind, x.ServiceNameSnapshot, x.LabWorkSnapshot))
            .Where(a => !a.SheetDeleted).ToList();

        var patientIds = dossiers.Select(d => d.PatientId).Distinct().ToArray();
        var patients = await db.Patients.AsNoTracking().Where(p => patientIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var serviceIds = activities.Select(a => a.ServiceId).Distinct().ToArray();
        var services = await db.Set<DCMS.Domain.Features.Scheduling.SchedulingService>().AsNoTracking().Where(s => serviceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var staffIds = activities.Where(a => a.AssistantId.HasValue).Select(a => a.AssistantId!.Value).Concat(dossiers.Select(d => d.DentistId)).Distinct().ToArray();
        var staff = await db.Employees.AsNoTracking().Where(e => staffIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName, ct);

        // Money follows the treatment sheet itself: price x quantity - discount. No payments are recorded while the finance module is removed.

        var now = clock.GetUtcNow();
        var upcoming = await db.Set<Appointment>().AsNoTracking().Where(x => x.PatientId != null && patientIds.Contains(x.PatientId.Value) && x.Status == "Confirmed" && x.StartAt >= now)
            .GroupBy(x => x.PatientId!.Value).Select(g => new { PatientId = g.Key, At = g.Min(x => x.StartAt) }).ToDictionaryAsync(x => x.PatientId, x => x.At, ct);

        var search = q?.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant() ?? "";
        var groups = new List<(WorkStatisticsDossier Group, DateTimeOffset Created)>();
        foreach (var d in dossiers)
        {
            var own = activities.Where(a => a.DossierId == d.Id).OrderBy(a => a.PerformedAt).ThenBy(a => a.CreatedAt).ThenBy(a => a.Id).ToList();
            if (own.Count == 0) continue;
            var patient = patients.GetValueOrDefault(d.PatientId);
            var lines = own.Select(a =>
            {
                var gross = checked((long)Math.Round(a.Quantity * a.PriceSnapshotVnd, MidpointRounding.AwayFromZero));
                var cost = a.ChargingBasis == "Included" ? 0 : gross - Math.Min(gross, a.DiscountSnapshotVnd);
                return new WorkStatisticsLine(a.Id, a.ServiceNameSnapshot ?? services.GetValueOrDefault(a.ServiceId, ""), cost, 0, 0);
            }).ToArray();
            if (search.Length > 0 && !(patient?.PatientCode.ToUpperInvariant().Contains(search) == true || patient?.FullName.ToUpperInvariant().Contains(search) == true
                || lines.Any(l => l.ServiceName.ToUpperInvariant().Contains(search)))) continue;
            var assistants = own.Where(a => a.AssistantId.HasValue).Select(a => staff.GetValueOrDefault(a.AssistantId!.Value, "")).Where(n => n.Length > 0).Distinct().ToArray();
            groups.Add((new(d.Id, d.Code, d.StartedOn, d.PatientId, patient?.PatientCode ?? "", patient?.FullName ?? "", upcoming.TryGetValue(d.PatientId, out var next) ? next : null,
                staff.GetValueOrDefault(d.DentistId, ""), assistants, lines.Sum(l => l.CostVnd), lines.Sum(l => l.PaidVnd), lines.Sum(l => l.DueVnd), lines), d.CreatedAt));
        }
        var ordered = groups.OrderByDescending(x => x.Group.StartedOn).ThenByDescending(x => x.Created).ThenBy(x => x.Group.DossierId).Select(x => x.Group).ToArray();
        var totals = new WorkStatisticsTotals(ordered.Sum(x => x.CostVnd), ordered.Sum(x => x.PaidVnd), ordered.Sum(x => x.DueVnd));
        return new(ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), ordered.Length, page, pageSize, totals, ordered.Sum(x => x.Lines.Count));
    }
}

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

        // Cost follows the treatment sheet itself: price x quantity - discount. Paid follows the dossier payment screen.
        var paid = await PaidByActivityAsync(dossierIds, ct);
        var creatorIds = await db.TreatmentEvents.AsNoTracking().Where(e => dossierIds.Contains(e.DossierId) && e.Kind == "DossierCreated")
            .Select(e => new { e.DossierId, e.ActorId }).ToListAsync(ct);
        var creatorByDossier = creatorIds.GroupBy(x => x.DossierId).ToDictionary(g => g.Key, g => g.First().ActorId);
        var actorIds = creatorByDossier.Values.Distinct().ToArray();
        var actorNames = await db.Users.AsNoTracking().Where(u => actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

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
                var paidVnd = Math.Clamp(paid.GetValueOrDefault(a.Id), 0, cost);
                var discount = gross - cost;
                // A cancelled dossier keeps what was collected; the unpaid part is written off (same rule as the finance statistics).
                if (d.State == "Cancelled") cost = paidVnd;
                return new WorkStatisticsLine(a.Id, a.ServiceNameSnapshot ?? services.GetValueOrDefault(a.ServiceId, ""), cost, paidVnd, cost - paidVnd,
                    a.PerformedAt, a.Quantity, checked((long)Math.Round(a.PriceSnapshotVnd, MidpointRounding.AwayFromZero)), discount);
            }).ToArray();
            if (search.Length > 0 && !(patient?.PatientCode.ToUpperInvariant().Contains(search) == true || patient?.FullName.ToUpperInvariant().Contains(search) == true
                || lines.Any(l => l.ServiceName.ToUpperInvariant().Contains(search)))) continue;
            var assistants = own.Where(a => a.AssistantId.HasValue).Select(a => staff.GetValueOrDefault(a.AssistantId!.Value, "")).Where(n => n.Length > 0).Distinct().ToArray();
            groups.Add((new(d.Id, d.Code, d.StartedOn, d.PatientId, patient?.PatientCode ?? "", patient?.FullName ?? "", upcoming.TryGetValue(d.PatientId, out var next) ? next : null,
                staff.GetValueOrDefault(d.DentistId, ""), assistants, lines.Sum(l => l.CostVnd), lines.Sum(l => l.PaidVnd), lines.Sum(l => l.DueVnd), lines,
                d.State, d.Note, creatorByDossier.TryGetValue(d.Id, out var creator) ? actorNames.GetValueOrDefault(creator, "") : ""), d.CreatedAt));
        }
        var ordered = groups.OrderByDescending(x => x.Group.StartedOn).ThenByDescending(x => x.Created).ThenBy(x => x.Group.DossierId).Select(x => x.Group).ToArray();
        var totals = new WorkStatisticsTotals(ordered.Sum(x => x.CostVnd), ordered.Sum(x => x.PaidVnd), ordered.Sum(x => x.DueVnd));
        return new(ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), ordered.Length, page, pageSize, totals, ordered.Sum(x => x.Lines.Count));
    }

    // Same sources and guards as the dossier payment screen (TreatmentPaymentEndpoints.ContextAsync), read for many dossiers at once.
    // Keep the two in step: archived payments of the removed finance tables plus the FinanceLedger cash, transfer and applied-deposit entries, less the lab priority share.
    private async Task<Dictionary<Guid, long>> PaidByActivityAsync(Guid[] dossierIds, CancellationToken ct)
    {
        if (dossierIds.Length == 0) return [];
        var rows = await db.Database.SqlQuery<PaidRow>($"""
            WITH d AS (
                SELECT "Id"::text AS id, "PatientId" AS patient_id, "BranchId" AS branch_id
                FROM "TREATMENT_DOSSIER" WHERE "Id" = ANY({dossierIds})
            ), archive AS (
                SELECT a."Target" AS source, item.row
                FROM "AUDIT_LOG" a CROSS JOIN LATERAL jsonb_array_elements(a."PayloadJson"->'rows') item(row)
                WHERE a."AuditType" = 'FinanceLegacyArchive'
                  AND a."Target" IN ('TREATMENT_DOSSIER_PAYMENT', 'TREATMENT_DOSSIER_PAYMENT_LINE',
                      'PAYMENT_RECEIPT', 'PAYMENT_ALLOCATION', 'FINANCE_CHARGE',
                      'FINANCE_ADVANCE_ALLOCATION', 'FINANCE_INVOICE_LINE')
            ), sources AS (
                SELECT (l.row->>'ActivityId')::uuid AS activity_id, (l.row->>'AmountVnd')::bigint AS amount
                FROM archive l JOIN archive p ON p.source = 'TREATMENT_DOSSIER_PAYMENT'
                    AND p.row->>'Id' = l.row->>'PaymentId'
                JOIN d ON d.id = p.row->>'DossierId' AND (p.row->>'PatientId')::integer = d.patient_id
                    AND (p.row->>'BranchId')::integer = d.branch_id
                WHERE l.source = 'TREATMENT_DOSSIER_PAYMENT_LINE'
                UNION ALL
                SELECT (c.row->>'ActivityId')::uuid, (a.row->>'AmountVnd')::bigint
                FROM archive a JOIN archive c ON c.source = 'FINANCE_CHARGE' AND c.row->>'Id' = a.row->>'ChargeId'
                JOIN archive r ON r.source = 'PAYMENT_RECEIPT' AND r.row->>'Id' = a.row->>'PaymentId'
                JOIN d ON d.id = c.row->>'DossierId' AND (r.row->>'PatientId')::integer = d.patient_id
                WHERE a.source = 'PAYMENT_ALLOCATION' AND r.row->>'State' = 'Posted'
                UNION ALL
                SELECT (c.row->>'ActivityId')::uuid, (a.row->>'AmountVnd')::bigint
                FROM archive a JOIN archive i ON i.source = 'FINANCE_INVOICE_LINE' AND i.row->>'Id' = a.row->>'InvoiceLineId'
                JOIN archive c ON c.source = 'FINANCE_CHARGE' AND c.row->>'Id' = i.row->>'ChargeId'
                JOIN d ON d.id = c.row->>'DossierId'
                WHERE a.source = 'FINANCE_ADVANCE_ALLOCATION'
                UNION ALL
                SELECT (line.row->>'activityId')::uuid, (line.row->>'amountVnd')::bigint
                  - COALESCE((SELECT sum((priority.row->>'amountVnd')::bigint)
                      FROM jsonb_array_elements(COALESCE(entry."PayloadJson"->'labPriority', '[]'::jsonb)) priority(row)
                      JOIN "LAB_ORDER" lab ON lab."Id" = (priority.row->>'labOrderId')::uuid
                      WHERE lab."ActivityId" = (line.row->>'activityId')::uuid), 0)
                FROM "AUDIT_LOG" entry
                JOIN d ON d.id = entry."Target" AND entry."PatientId" = d.patient_id AND entry."BranchId" = d.branch_id
                CROSS JOIN LATERAL jsonb_array_elements(entry."PayloadJson"->'lines') line(row)
                WHERE entry."AuditType" = 'FinanceLedger'
                  AND entry."Action" IN ('TreatmentCashPayment', 'TreatmentTransferPayment', 'AdvanceApplied')
            )
            SELECT activity_id AS "ActivityId", sum(amount)::bigint AS "PaidVnd"
            FROM sources WHERE activity_id IS NOT NULL GROUP BY activity_id
            """).ToListAsync(ct);
        return rows.ToDictionary(x => x.ActivityId, x => x.PaidVnd);
    }

    private sealed class PaidRow
    {
        public Guid ActivityId { get; set; }
        public long PaidVnd { get; set; }
    }
}

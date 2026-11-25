using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

// Lab work management (list, history, status). See docs/specs/LAB-ORDER-MANAGEMENT.md.
public sealed partial class TreatmentDossierService
{
    private sealed class LabPatientPaymentRow
    {
        public Guid LabOrderId { get; set; }
        public long PaidVnd { get; set; }
    }

    private async Task<Dictionary<Guid, long>> LabPatientPaymentsAsync(Guid[] labOrderIds, CancellationToken ct)
    {
        if (labOrderIds.Length == 0) return [];
        // FinanceLedger contains only confirmed cash payments and settled PayOS transfers.
        // Deposits allocated to a particular lab order are included; pending QR orders are not.
        var rows = await db.Database.SqlQuery<LabPatientPaymentRow>($"""
            SELECT lab."Id" AS "LabOrderId", sum((line.row->>'amountVnd')::bigint)::bigint AS "PaidVnd"
            FROM "AUDIT_LOG" ledger
            CROSS JOIN LATERAL jsonb_array_elements(
                COALESCE(ledger."PayloadJson"->'labPriority', '[]'::jsonb)
                || COALESCE(ledger."PayloadJson"->'labLines', '[]'::jsonb)) line(row)
            JOIN "LAB_ORDER" lab ON lab."Id" = (line.row->>'labOrderId')::uuid
            JOIN "CLINICAL_ACTIVITY" act ON act."Id" = lab."ActivityId"
            JOIN "TREATMENT_DOSSIER" dos ON dos."Id" = act."DossierId"
            WHERE ledger."AuditType" = 'FinanceLedger'
              AND ledger."Action" IN ('TreatmentCashPayment', 'TreatmentTransferPayment',
                  'AdvanceCash', 'AdvanceTransfer', 'AdvanceApplied')
              AND lab."Id" = ANY({labOrderIds})
              AND ledger."Target" = dos."Id"::text
              AND ledger."PatientId" = dos."PatientId"
              AND ledger."BranchId" = dos."BranchId"
            GROUP BY lab."Id"
            """).ToArrayAsync(ct);
        return rows.ToDictionary(x => x.LabOrderId, x => x.PaidVnd);
    }

    private static LabWorkItem WithPatientPayment(LabWorkItem item, IReadOnlyDictionary<Guid, long> amounts)
    {
        var paid = Math.Clamp(amounts.GetValueOrDefault(item.Id), 0, item.TotalFeeVnd);
        var status = item.TotalFeeVnd <= 0 ? "NoFee" : paid >= item.TotalFeeVnd ? "Paid" :
            paid > 0 ? "Partial" : "Unpaid";
        return item with { PatientPaidVnd = paid, PatientPaymentStatus = status };
    }

    private static LabWorkItem ToWorkItem(LabOrder x, Guid dossierId, string dossierCode, int patientId, string patientCode,
        string patientName, string dentistName, string? labName, DateOnly today, IReadOnlyDictionary<Guid, long> amounts) =>
        WithPatientPayment(new(x.Id, x.Code, dossierId, dossierCode, x.ActivityId, patientId, patientCode,
            patientName, x.WorkName, x.LabProviderId, labName, x.DentistId, dentistName, x.OrderedOn, x.DueOn, x.Spec.SentOn,
            x.Spec.ReceivedOn, x.DeliveredOn, x.Quantity, x.Spec.Material, x.Spec.Teeth, x.Note, x.TotalFeeVnd, x.PaidVnd, x.Status,
            IsOverdue(x.Status, x.DueOn, today), x.Version), amounts);

    private DateOnly VnToday() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);

    // BranchIds is already limited to the actor's own active branches (owner: all active; others: membership ∩ access-group branches).
    private IQueryable<LabOrder> LabScope(Application.Features.Settings.PermissionState scope, int? branchId = null) =>
        from x in db.LabOrders
        join a in db.ClinicalActivities on x.ActivityId equals a.Id
        join d in db.TreatmentDossiers on a.DossierId equals d.Id
        where scope.BranchIds.Contains(d.BranchId) && (!branchId.HasValue || d.BranchId == branchId.Value)
        select x;

    private static bool IsOverdue(string status, DateOnly? due, DateOnly today) =>
        due is { } d && d < today && status is LabOrderStatus.NotSent or LabOrderStatus.Sent or LabOrderStatus.Rework;

    public async Task<LabWorkPage> LabWorkAsync(int actorId, string? status, int? branchId, Guid? labProviderId, int? dentistId,
        DateOnly? from, DateOnly? to, string? q, int page, int pageSize, CancellationToken ct)
    {
        var scope = await access.RequireAsync(actorId, "treatment.lab.read", ct);
        q = Clean(q);
        if (page < 1 || pageSize is not (20 or 50 or 100) || page > int.MaxValue / pageSize || q?.Length > 160 || from > to
            || (status != null && status != "Overdue" && !LabOrderStatus.All.Contains(status))) throw Error("MSG04");
        if (branchId is { } b && !scope.BranchIds.Contains(b)) throw Error("MSG05", 403);
        var today = VnToday();
        var baseQuery = LabScope(scope, branchId);
        var labRows = await (from x in baseQuery join p in db.Set<CatalogProvider>() on x.LabProviderId equals p.Id select new { p.Id, p.Name }).Distinct().ToListAsync(ct);
        var labs = labRows.OrderBy(x => x.Name).Select(x => new LabOrderLab(x.Id, x.Name)).ToList();
        var dentistRows = await (from x in baseQuery join e in db.Employees on x.DentistId equals e.Id select new { e.Id, e.FullName }).Distinct().ToListAsync(ct);
        var dentists = dentistRows.OrderBy(x => x.FullName).Select(x => new LabOrderDentist(x.Id, x.FullName)).ToList();
        if (labProviderId.HasValue) baseQuery = baseQuery.Where(x => x.LabProviderId == labProviderId.Value);
        if (dentistId.HasValue) baseQuery = baseQuery.Where(x => x.DentistId == dentistId.Value);
        if (from.HasValue) baseQuery = baseQuery.Where(x => x.DueOn >= from.Value);
        if (to.HasValue) baseQuery = baseQuery.Where(x => x.DueOn <= to.Value);
        if (q != null)
        {
            var like = $"%{q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            var patientActivityIds = from a in db.ClinicalActivities
                join d in db.TreatmentDossiers on a.DossierId equals d.Id
                join p in db.Patients on d.PatientId equals p.Id
                where EF.Functions.ILike(p.FullName, like) || EF.Functions.ILike(p.PatientCode, like)
                select a.Id;
            baseQuery = baseQuery.Where(x => EF.Functions.ILike(x.WorkName, like) || (x.Code != null && EF.Functions.ILike(x.Code, like)) || patientActivityIds.Contains(x.ActivityId));
        }
        var overdue = baseQuery.Where(x => x.DueOn < today && (x.Status == LabOrderStatus.NotSent || x.Status == LabOrderStatus.Sent || x.Status == LabOrderStatus.Rework));
        var byStatus = await baseQuery.GroupBy(x => x.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        int N(string s) => byStatus.FirstOrDefault(x => x.Key == s)?.N ?? 0;
        var counts = new LabWorkCounts(N(LabOrderStatus.NotSent), N(LabOrderStatus.Sent), N(LabOrderStatus.Received), N(LabOrderStatus.Delivered),
            N(LabOrderStatus.Rework), await overdue.CountAsync(ct));
        var filtered = status == "Overdue" ? overdue : status != null ? baseQuery.Where(x => x.Status == status) : baseQuery;
        var total = await filtered.CountAsync(ct);
        var rows = await (from x in filtered
            join a in db.ClinicalActivities on x.ActivityId equals a.Id
            join d in db.TreatmentDossiers on a.DossierId equals d.Id
            join pt in db.Patients on d.PatientId equals pt.Id
            join e in db.Employees on x.DentistId equals e.Id
            join pr in db.Set<CatalogProvider>() on x.LabProviderId equals pr.Id into prs
            from lab in prs.DefaultIfEmpty()
            orderby x.DueOn == null, x.DueOn, x.OrderedOn descending, x.Id
            select new { x, DossierId = d.Id, DossierCode = d.Code, d.PatientId, pt.PatientCode, PatientName = pt.FullName, DentistName = e.FullName, LabName = lab == null ? null : lab.Name })
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var patientPayments = await LabPatientPaymentsAsync(rows.Select(r => r.x.Id).ToArray(), ct);
        var items = rows.Select(r => ToWorkItem(r.x, r.DossierId, r.DossierCode, r.PatientId, r.PatientCode, r.PatientName, r.DentistName, r.LabName, today, patientPayments)).ToList();
        return new(items, total, page, pageSize, counts, labs, dentists);
    }

    private async Task<LabOrder> ScopedLabOrderAsync(Application.Features.Settings.PermissionState scope, Guid id, bool track, CancellationToken ct) =>
        await (track ? db.LabOrders : db.LabOrders.AsNoTracking())
            .SingleOrDefaultAsync(x => x.Id == id && (
                from a in db.ClinicalActivities
                join d in db.TreatmentDossiers on a.DossierId equals d.Id
                where a.Id == x.ActivityId && scope.BranchIds.Contains(d.BranchId)
                select d.Id).Any(), ct) ?? throw Error("MSG42", 404);

    public async Task<IReadOnlyList<LabStatusLogItem>> LabWorkHistoryAsync(int actorId, Guid id, CancellationToken ct)
    {
        var scope = await access.RequireAsync(actorId, "treatment.lab.read", ct);
        await ScopedLabOrderAsync(scope, id, false, ct);
        var logs = (await db.LabOrders.AsNoTracking().SingleAsync(x => x.Id == id, ct)).StatusLogs;
        var actorIds = logs.Select(x => x.ChangedBy).Distinct().ToArray();
        var names = await db.Employees.AsNoTracking().Where(x => actorIds.Contains(x.AccountId))
            .ToDictionaryAsync(x => x.AccountId, x => x.FullName, ct);
        return logs.OrderByDescending(x => x.ChangedAt).ThenBy(x => x.Id)
            .Select(x => new LabStatusLogItem(x.FromStatus, x.ToStatus, x.ChangedAt, names.GetValueOrDefault(x.ChangedBy, ""), x.Note)).ToList();
    }

    public async Task<LabWorkItem> ChangeLabStatusAsync(int actorId, Guid id, ChangeLabStatusRequest r, CancellationToken ct)
    {
        var scope = await access.RequireAsync(actorId, "treatment.lab.track", ct);
        var note = Clean(r.Note);
        if (!LabOrderStatus.All.Contains(r.Status) || note?.Length > 300) throw Error("MSG04");
        var order = await ScopedLabOrderAsync(scope, id, true, ct);
        if (order.Version != r.ExpectedVersion) throw Error("MSG41", 409);
        if (!LabOrderStatus.CanMove(order.Status, r.Status)) throw Error("MSG04");
        var today = VnToday(); var now = clock.GetUtcNow();
        order.StatusLogs.Add(new LabOrderStatusLog { LabOrderId = id, FromStatus = order.Status, ToStatus = r.Status, ChangedAt = now, ChangedBy = actorId, Note = note });
        var spec = order.Spec;
        var (sentOn, receivedOn) = r.Status switch
        {
            LabOrderStatus.Sent => (today, (DateOnly?)null),
            LabOrderStatus.Received => (spec.SentOn, today),
            LabOrderStatus.Rework => (spec.SentOn, (DateOnly?)null),
            _ => (spec.SentOn, spec.ReceivedOn)
        };
        if (r.Status == LabOrderStatus.Sent || r.Status == LabOrderStatus.Rework) order.DeliveredOn = null;
        else if (r.Status == LabOrderStatus.Delivered) order.DeliveredOn ??= today;
        if (sentOn != spec.SentOn || receivedOn != spec.ReceivedOn)
            order.Spec = new LabOrderSpec
            {
                MarginOuter = spec.MarginOuter, MarginInner = spec.MarginInner, MarginSide = spec.MarginSide,
                BridgeShapes = spec.BridgeShapes, IsChild = spec.IsChild, Teeth = spec.Teeth, Shade = spec.Shade,
                Material = spec.Material, Warranty = spec.Warranty, SentOn = sentOn, ReceivedOn = receivedOn
            };
        order.Status = r.Status; order.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Error("MSG41", 409); }
        return await SingleLabItemAsync(id, ct);
    }

    private async Task<LabWorkItem> SingleLabItemAsync(Guid id, CancellationToken ct)
    {
        var today = VnToday();
        var r = await (from x in db.LabOrders.AsNoTracking() where x.Id == id
            join a in db.ClinicalActivities on x.ActivityId equals a.Id
            join d in db.TreatmentDossiers on a.DossierId equals d.Id
            join pt in db.Patients on d.PatientId equals pt.Id
            join e in db.Employees on x.DentistId equals e.Id
            join pr in db.Set<CatalogProvider>() on x.LabProviderId equals pr.Id into prs
            from lab in prs.DefaultIfEmpty()
            select new { x, DossierId = d.Id, DossierCode = d.Code, d.PatientId, pt.PatientCode, PatientName = pt.FullName, DentistName = e.FullName, LabName = lab == null ? null : lab.Name })
            .SingleAsync(ct);
        var patientPayments = await LabPatientPaymentsAsync([id], ct);
        return ToWorkItem(r.x, r.DossierId, r.DossierCode, r.PatientId, r.PatientCode, r.PatientName, r.DentistName, r.LabName, today, patientPayments);
    }
}

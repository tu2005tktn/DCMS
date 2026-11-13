using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

// Lab work management (list, history, status). See docs/specs/LAB-ORDER-MANAGEMENT.md.
public sealed partial class TreatmentDossierService
{
    private DateOnly VnToday() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);

    // BranchIds is already limited to the actor's own active branches (owner: all active; others: membership ∩ access-group branches).
    private IQueryable<LabOrder> LabScope(Application.Features.Settings.PermissionState scope) =>
        db.LabOrders.Where(x => scope.BranchIds.Contains(x.BranchId));

    private static bool IsOverdue(string status, DateOnly? due, DateOnly today) =>
        due is { } d && d < today && status is LabOrderStatus.NotSent or LabOrderStatus.Sent or LabOrderStatus.Rework;

    public async Task<LabWorkPage> LabWorkAsync(int actorId, string? status, int? branchId, Guid? labProviderId, int? dentistId,
        DateOnly? from, DateOnly? to, string? q, int page, int pageSize, CancellationToken ct)
    {
        var scope = await access.RequireAsync(actorId, "treatment.lab.read", ct);
        q = Clean(q);
        if (page < 1 || pageSize is not (20 or 50 or 100) || page > int.MaxValue / pageSize || q?.Length > 160 || from > to
            || (status != null && status != "Overdue" && !LabOrderStatus.All.Contains(status))) throw Error("MSG04");
        if (branchId is { } b && !scope.BranchIds.Contains(b)) throw Error("MSG06", 403);
        var today = VnToday();
        var baseQuery = LabScope(scope);
        if (branchId.HasValue) baseQuery = baseQuery.Where(x => x.BranchId == branchId.Value);
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
            var patientIds = db.Patients.Where(p => EF.Functions.ILike(p.FullName, like) || EF.Functions.ILike(p.PatientCode, like)).Select(p => p.Id);
            baseQuery = baseQuery.Where(x => EF.Functions.ILike(x.WorkName, like) || (x.Code != null && EF.Functions.ILike(x.Code, like)) || patientIds.Contains(x.PatientId));
        }
        var overdue = baseQuery.Where(x => x.DueOn < today && (x.Status == LabOrderStatus.NotSent || x.Status == LabOrderStatus.Sent || x.Status == LabOrderStatus.Rework));
        var byStatus = await baseQuery.GroupBy(x => x.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        int N(string s) => byStatus.FirstOrDefault(x => x.Key == s)?.N ?? 0;
        var counts = new LabWorkCounts(N(LabOrderStatus.NotSent), N(LabOrderStatus.Sent), N(LabOrderStatus.Received), N(LabOrderStatus.Delivered),
            N(LabOrderStatus.Rework), await overdue.CountAsync(ct));
        var filtered = status == "Overdue" ? overdue : status != null ? baseQuery.Where(x => x.Status == status) : baseQuery;
        var total = await filtered.CountAsync(ct);
        var rows = await (from x in filtered
            join d in db.TreatmentDossiers on x.DossierId equals d.Id
            join pt in db.Patients on x.PatientId equals pt.Id
            join e in db.Employees on x.DentistId equals e.Id
            join pr in db.Set<CatalogProvider>() on x.LabProviderId equals pr.Id into prs
            from lab in prs.DefaultIfEmpty()
            orderby x.DueOn == null, x.DueOn, x.OrderedOn descending, x.Id
            select new { x, DossierCode = d.Code, pt.PatientCode, PatientName = pt.FullName, DentistName = e.FullName, LabName = lab == null ? null : lab.Name })
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = rows.Select(r => new LabWorkItem(r.x.Id, r.x.Code, r.x.DossierId, r.DossierCode, r.x.ActivityId, r.x.PatientId, r.PatientCode,
            r.PatientName, r.x.WorkName, r.x.LabProviderId, r.LabName, r.x.DentistId, r.DentistName, r.x.OrderedOn, r.x.DueOn, r.x.SentOn,
            r.x.ReceivedOn, r.x.DeliveredOn, r.x.Quantity, r.x.Material, r.x.Teeth, r.x.Note, r.x.TotalFeeVnd, r.x.PaidVnd, r.x.Status,
            IsOverdue(r.x.Status, r.x.DueOn, today), r.x.Version)).ToList();
        return new(items, total, page, pageSize, counts, labs, dentists);
    }

    private async Task<LabOrder> ScopedLabOrderAsync(Application.Features.Settings.PermissionState scope, Guid id, bool track, CancellationToken ct)
    {
        var order = await (track ? db.LabOrders : db.LabOrders.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);
        if (!scope.BranchIds.Contains(order.BranchId)) throw Error("MSG113", 404);
        return order;
    }

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
        if (order.Version != r.ExpectedVersion) throw Error("MSG111", 409);
        if (!LabOrderStatus.CanMove(order.Status, r.Status)) throw Error("MSG04");
        var today = VnToday(); var now = clock.GetUtcNow();
        order.StatusLogs.Add(new LabOrderStatusLog { LabOrderId = id, FromStatus = order.Status, ToStatus = r.Status, ChangedAt = now, ChangedBy = actorId, Note = note });
        switch (r.Status)
        {
            case LabOrderStatus.Sent: order.SentOn = today; order.ReceivedOn = null; order.DeliveredOn = null; break;
            case LabOrderStatus.Received: order.ReceivedOn = today; break;
            case LabOrderStatus.Delivered: order.DeliveredOn ??= today; break;
            case LabOrderStatus.Rework: order.ReceivedOn = null; order.DeliveredOn = null; break;
        }
        order.Status = r.Status; order.Version++; order.UpdatedAt = now; order.UpdatedBy = actorId;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Error("MSG111", 409); }
        return await SingleLabItemAsync(id, ct);
    }

    private async Task<LabWorkItem> SingleLabItemAsync(Guid id, CancellationToken ct)
    {
        var today = VnToday();
        var r = await (from x in db.LabOrders.AsNoTracking() where x.Id == id
            join d in db.TreatmentDossiers on x.DossierId equals d.Id
            join pt in db.Patients on x.PatientId equals pt.Id
            join e in db.Employees on x.DentistId equals e.Id
            join pr in db.Set<CatalogProvider>() on x.LabProviderId equals pr.Id into prs
            from lab in prs.DefaultIfEmpty()
            select new { x, DossierCode = d.Code, pt.PatientCode, PatientName = pt.FullName, DentistName = e.FullName, LabName = lab == null ? null : lab.Name })
            .SingleAsync(ct);
        return new(r.x.Id, r.x.Code, r.x.DossierId, r.DossierCode, r.x.ActivityId, r.x.PatientId, r.PatientCode, r.PatientName, r.x.WorkName,
            r.x.LabProviderId, r.LabName, r.x.DentistId, r.DentistName, r.x.OrderedOn, r.x.DueOn, r.x.SentOn, r.x.ReceivedOn, r.x.DeliveredOn,
            r.x.Quantity, r.x.Material, r.x.Teeth, r.x.Note, r.x.TotalFeeVnd, r.x.PaidVnd, r.x.Status, IsOverdue(r.x.Status, r.x.DueOn, today), r.x.Version);
    }
}

using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

// Payments recorded against posted activities of a dossier. See docs/specs/TREATMENT-DOSSIER-PAYMENTS.md.
public sealed partial class TreatmentDossierService
{
    private const string PaymentCommand = "dossier-payment";
    private const long MaxPaymentVnd = 9_000_000_000_000_000L;

    private async Task<List<ActivityDto>> PaymentActivitiesAsync(int actorId, Guid dossierId, CancellationToken ct)
    {
        var result = new List<ActivityDto>();
        for (var page = 1; ; page++)
        {
            var batch = await ActivitiesAsync(actorId, dossierId, null, null, null, page, 100, ct);
            result.AddRange(batch.Items);
            if (result.Count >= batch.Total || batch.Items.Count == 0) return result;
        }
    }

    private async Task<long> PaidForActivityAsync(Guid activityId, CancellationToken ct) =>
        await db.Set<DossierPaymentLine>().AsNoTracking().Where(x => x.ActivityId == activityId).SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;

    private static long ActivityCost(ActivityDto activity) => checked((long)Math.Round(activity.Quantity * activity.PriceSnapshotVnd, MidpointRounding.AwayFromZero));

    private async Task<(List<DossierPaymentContextLine> Lines, long Paid)> PaymentLinesAsync(
        int actorId, Guid dossierId, CancellationToken ct)
    {
        var activities = await PaymentActivitiesAsync(actorId, dossierId, ct);
        var paidByActivity = (await db.Set<DossierPaymentLine>().AsNoTracking().Where(x =>
            db.Set<DossierPayment>().Any(p => p.Id == x.PaymentId && p.DossierId == dossierId))
            .GroupBy(x => x.ActivityId).Select(g => new { g.Key, Total = g.Sum(x => x.AmountVnd) }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Total);
        var performerIds = activities.Select(x => x.PerformerId).Distinct().ToArray();
        var names = await db.Employees.AsNoTracking().Where(x => performerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var lines = activities.OrderBy(x => x.PerformedAt).ThenBy(x => x.Id).Select(x =>
        {
            var cost = ActivityCost(x);
            var discount = Math.Min(cost, x.DiscountSnapshotVnd);
            var paid = paidByActivity.GetValueOrDefault(x.Id);
            var due = x.ChargingBasis == "Included" ? 0 : Math.Max(0, cost - discount - paid);
            return new DossierPaymentContextLine(x.Id, x.PerformedAt, names.GetValueOrDefault(x.PerformerId) ?? "—",
                x.ServiceNameSnapshot, cost, discount, paid, due);
        }).ToList();
        return (lines, paidByActivity.Values.Sum());
    }

    public async Task<DossierPaymentContext> PaymentContextAsync(int actorId, Guid dossierId, CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        var (lines, paid) = await PaymentLinesAsync(actorId, dossierId, ct);
        var collectors = await (from member in db.Set<SchedulingMember>().AsNoTracking()
            join staff in db.Employees.AsNoTracking() on member.EmployeeId equals staff.Id
            where member.BranchId == dossier.BranchId && member.Active
            orderby staff.FullName
            select new DossierPaymentCollector(staff.Id, staff.FullName)).ToListAsync(ct);
        var self = await db.Employees.AsNoTracking().Where(x => x.AccountId == actorId).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
        return new(paid, lines, collectors, collectors.Any(x => x.Id == self) ? self : null);
    }

    public async Task<IReadOnlyList<DossierPaymentDto>> PaymentsAsync(int actorId, Guid dossierId, CancellationToken ct)
    {
        await DossierAsync(actorId, dossierId, ct);
        var payments = await db.Set<DossierPayment>().AsNoTracking().Include(x => x.Lines).Where(x => x.DossierId == dossierId)
            .OrderByDescending(x => x.PaidOn).ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
        var activityNames = (await PaymentActivitiesAsync(actorId, dossierId, ct)).ToDictionary(x => x.Id, x => x.ServiceNameSnapshot);
        var collectorIds = payments.Select(x => x.CollectorEmployeeId).Distinct().ToArray();
        var collectors = await db.Employees.AsNoTracking().Where(x => collectorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        return payments.Select(x => ToDto(x, collectors.GetValueOrDefault(x.CollectorEmployeeId) ?? "—", activityNames)).ToList();
    }

    private static DossierPaymentDto ToDto(DossierPayment payment, string collector, IReadOnlyDictionary<Guid, string?> names) =>
        new(payment.Id, payment.PaidOn, payment.Method, payment.CollectorEmployeeId, collector, payment.Note, payment.TotalVnd,
            payment.CreatedAt, payment.Lines.Select(l => new DossierPaymentLineDto(l.ActivityId,
                names.GetValueOrDefault(l.ActivityId), l.AmountVnd)).ToList());

    public Task<DossierPaymentDto> CreatePaymentAsync(int actorId, Guid dossierId, CreateDossierPaymentRequest request, CancellationToken ct)
    {
        if (request.Lines is not { Count: > 0 and <= 500 } || request.Method is not ("Cash" or "Transfer")
            || request.Note?.Length > 1000 || request.CollectorId < 1
            || request.Lines.Any(x => x.AmountVnd <= 0 || x.AmountVnd > MaxPaymentVnd)
            || request.Lines.Select(x => x.ActivityId).Distinct().Count() != request.Lines.Count)
            throw Error("MSG04");
        return SheetCommandAsync(actorId, dossierId, request.RequestKey, PaymentCommand, request, async dossier =>
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
            if (request.PaidOn > today || request.PaidOn.Year < 2000) throw Error("MSG04");
            var collector = await (from member in db.Set<SchedulingMember>().AsNoTracking()
                join staff in db.Employees.AsNoTracking() on member.EmployeeId equals staff.Id
                where member.BranchId == dossier.BranchId && member.Active && staff.Id == request.CollectorId
                select staff.FullName).FirstOrDefaultAsync(ct) ?? throw Error("MSG04");
            var (lines, _) = await PaymentLinesAsync(actorId, dossierId, ct);
            var byActivity = lines.ToDictionary(x => x.ActivityId);
            foreach (var line in request.Lines)
            {
                if (!byActivity.TryGetValue(line.ActivityId, out var source)) throw Error("MSG04");
                if (line.AmountVnd > source.DueVnd) throw Error("MSG111", 409);
            }
            var payment = new DossierPayment { DossierId = dossier.Id, PatientId = dossier.PatientId, BranchId = dossier.BranchId,
                PaidOn = request.PaidOn, Method = request.Method, CollectorEmployeeId = request.CollectorId, Note = Clean(request.Note),
                TotalVnd = checked(request.Lines.Sum(x => x.AmountVnd)), CreatedBy = actorId, CreatedAt = clock.GetUtcNow() };
            payment.Lines = request.Lines.Select(x => new DossierPaymentLine { PaymentId = payment.Id, ActivityId = x.ActivityId, AmountVnd = x.AmountVnd }).ToList();
            db.Add(payment);
            return ToDto(payment, collector, byActivity.ToDictionary(x => x.Key, x => x.Value.ServiceName));
        }, ct);
    }
}

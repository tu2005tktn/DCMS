using DCMS.Application.Features.Finance;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Finance;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.Settings.SettingsAccess;

namespace DCMS.Infrastructure.Features.Finance;

public sealed class RevenueService(DcmsDbContext db, ISettingsAccess access, TimeProvider clock) : IRevenueService
{
    private static readonly TimeZoneInfo ClinicZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public async Task<RevenueReport> GetAsync(int actor, DateOnly? from, DateOnly? to, int? branchId,
        int page, int pageSize, CancellationToken ct)
    {
        var scope = await access.RequireAsync(actor, "finance.revenue.read", ct);
        if (branchId.HasValue && !scope.BranchIds.Contains(branchId.Value)) throw Error("MSG06", 403);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), ClinicZone).DateTime);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;
        if (start > end || end.DayNumber - start.DayNumber > 365 || page < 1 || pageSize is < 1 or > 100) throw Error("MSG04");
        var lower = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
        var upper = new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
        var branches = branchId.HasValue ? new[] { branchId.Value } : scope.BranchIds;

        var receipts = await db.Set<PaymentReceipt>().AsNoTracking()
            .Where(x => branches.Contains(x.BranchId) && x.State == "Posted" && x.ConfirmedAt >= lower && x.ConfirmedAt < upper)
            .Select(x => new { x.Id, x.VisitId, x.DossierId, x.PatientId, x.PatientName, x.BranchId, x.BranchName,
                ReceivedAt = x.ConfirmedAt!.Value, x.Method, x.AmountVnd }).ToArrayAsync(ct);
        var treatment = await db.Set<DossierPayment>().AsNoTracking()
            .Where(x => branches.Contains(x.BranchId) && x.CreatedAt >= lower && x.CreatedAt < upper)
            .Select(x => new { x.Id, x.DossierId, x.PatientId, x.BranchId, x.CreatedAt, x.Method, x.TotalVnd }).ToArrayAsync(ct);
        var patientIds = treatment.Select(x => x.PatientId).Distinct().ToArray();
        var patients = await db.Set<PatientProfile>().AsNoTracking().Where(x => patientIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FullName }).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var names = await db.Set<SchedulingBranch>().AsNoTracking().Where(x => branches.Contains(x.Id))
            .Select(x => new { x.Id, x.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var items = receipts.Select(x => new RevenueItem(x.Id, "Finance", x.VisitId ?? x.DossierId, x.PatientId,
                x.PatientName, x.BranchId, x.BranchName, x.ReceivedAt, x.Method, x.AmountVnd))
            .Concat(treatment.Select(x => new RevenueItem(x.Id, "Treatment", x.DossierId, x.PatientId,
                patients.GetValueOrDefault(x.PatientId) ?? "—", x.BranchId, names.GetValueOrDefault(x.BranchId) ?? "—",
                x.CreatedAt, x.Method, x.TotalVnd)))
            .OrderByDescending(x => x.ReceivedAt).ThenByDescending(x => x.Id).ToArray();
        var pendingReceipts = await db.Set<PaymentReceipt>().AsNoTracking()
            .Where(x => branches.Contains(x.BranchId) && x.State == "Pending" && x.CreatedAt >= lower && x.CreatedAt < upper)
            .SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;
        var payOsTableReady = await db.Database.SqlQueryRaw<bool>("SELECT to_regclass('\"TREATMENT_PAYOS_ORDER\"') IS NOT NULL AS \"Value\"").SingleAsync(ct);
        var pendingPayOs = payOsTableReady ? await db.Set<DossierPayOsOrder>().AsNoTracking()
            .Where(x => branches.Contains(x.BranchId) && x.State == "Pending" && x.CreatedAt >= lower && x.CreatedAt < upper)
            .SumAsync(x => (long?)x.AmountVnd, ct) ?? 0 : 0;
        var cash = items.Where(x => x.Method == "Cash").Sum(x => x.AmountVnd);
        var transfer = items.Where(x => x.Method == "Transfer").Sum(x => x.AmountVnd);
        var daily = items.GroupBy(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x.ReceivedAt, ClinicZone).DateTime))
            .OrderBy(x => x.Key).Select(x => new RevenueDay(x.Key,
                x.Where(i => i.Method == "Cash").Sum(i => i.AmountVnd),
                x.Where(i => i.Method == "Transfer").Sum(i => i.AmountVnd))).ToArray();
        return new(start, end, new(cash + transfer, cash, transfer, pendingReceipts + pendingPayOs),
            daily, items.Length, items.Skip((page - 1) * pageSize).Take(pageSize).ToArray());
    }
}

using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Finance;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Finance;

public sealed class TreatmentFinancialSummaryPort(DcmsDbContext db, TimeProvider clock) : ITreatmentFinancialSummaryPort
{
    public async Task<TreatmentFinancialSummary?> ReadAsync(int patientId, int branchId, Guid dossierId, CancellationToken ct)
    {
        var charges = await (from c in db.Set<FinanceCharge>().AsNoTracking()
            join a in db.ClinicalActivities.AsNoTracking() on c.ActivityId equals a.Id
            where a.DossierId == dossierId && c.PatientId == patientId && c.BranchId == branchId
            select new { c.Id, c.ActivityId, c.Quantity, c.UnitPriceVnd, c.AmountVnd }).ToArrayAsync(ct);
        var ids = charges.Select(x => x.Id).ToArray();
        var paid = await (from a in db.Set<PaymentAllocation>().AsNoTracking()
            join p in db.Set<PaymentReceipt>().AsNoTracking() on a.PaymentId equals p.Id
            where ids.Contains(a.ChargeId) && p.State == "Posted"
            group a by a.ChargeId into g select new { Id = g.Key, Amount = g.Sum(x => x.AmountVnd) })
            .ToDictionaryAsync(x => x.Id, x => x.Amount, ct);
        var gross = charges.Sum(x => checked((long)(x.Quantity * x.UnitPriceVnd)));
        var net = charges.Sum(x => x.AmountVnd);
        var allocated = charges.Sum(x => paid.GetValueOrDefault(x.Id));
        var version = await db.Set<FinancialAccount>().AsNoTracking()
            .Where(x => x.PatientId == patientId && x.BranchId == branchId).Select(x => x.Version).SingleOrDefaultAsync(ct);
        return new(true, 0, net, allocated, allocated, net - allocated, version.ToString(),
            gross, gross - net, 0, 0, clock.GetUtcNow(), 0,
            charges.Select(x => new TreatmentChargeAllocation(x.ActivityId!.Value, x.Id, x.AmountVnd,
                paid.GetValueOrDefault(x.Id), x.AmountVnd - paid.GetValueOrDefault(x.Id), false)).ToArray());
    }
}

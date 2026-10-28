using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Finance;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace DCMS.Infrastructure.Features.Finance;
// Runs inside ClinicalWorkService's transaction and advisory lock; all clinical eligibility gates still apply.
public sealed class ClinicalChargePort(DcmsDbContext db, FinanceService finance, TimeProvider clock,DCMS.Infrastructure.Features.Treatment.OutboxClinicalChargePort outbox) : IClinicalChargePort
{
    public Task<ClinicalFeeResult> PostInTransactionAsync(ClinicalFeeRequest request,CancellationToken ct)=>outbox.PostInTransactionAsync(request,ct);
    public async Task<bool> PostInTransactionAsync(int patientId, int branchId, Guid activityId, decimal quantity, decimal priceSnapshotVnd, int postingActor, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) return false;
        var row = await db.ClinicalActivities.SingleOrDefaultAsync(x => x.Id == activityId && x.PatientId == patientId && x.BranchId == branchId, ct);
        if (row == null || !row.VisitId.HasValue || row.State != "Draft" || row.ChargingBasis != "Separate" || row.Quantity != quantity || row.PriceSnapshotVnd != priceSnapshotVnd || quantity <= 0 || priceSnapshotVnd < 0 || priceSnapshotVnd != decimal.Truncate(priceSnapshotVnd) || priceSnapshotVnd > FinanceService.MaxAmount) return false;
        var amount = quantity * priceSnapshotVnd; if (amount != decimal.Truncate(amount) || amount > FinanceService.MaxAmount) return false;
        if (await db.Set<FinanceCharge>().AnyAsync(x => x.SourceKind == "ClinicalActivity" && x.SourceId == activityId, ct)) return false;
        if (!await db.Set<DCMS.Domain.Features.Visits.Visit>().AnyAsync(x => x.Id == row.VisitId && x.PatientId == patientId && x.BranchId == branchId && x.Status == "InProgress", ct)) return false;
        var service = await db.Set<DCMS.Domain.Features.Scheduling.SchedulingService>().SingleAsync(x => x.Id == row.ServiceId, ct);
        var actor = postingActor;
        await finance.AddCharge(new FinanceCharge { VisitId = row.VisitId.Value, PatientId = patientId, BranchId = branchId, SourceKind = "ClinicalActivity", SourceId = row.Id, ActivityId = row.Id, ServiceId = row.ServiceId, Name = service.Name, Unit = row.Unit, Quantity = quantity, UnitPriceVnd = (long)priceSnapshotVnd, AmountVnd = (long)amount, PriceVersion = service.PriceVersion, PostedBy = actor, PostedAt = clock.GetUtcNow() }, ct);
        db.Add(new FinanceAudit { ActorId = actor, BranchId = branchId, VisitId = row.VisitId.Value, Action = "PostClinicalCharge", Target = row.Id.ToString(), At = clock.GetUtcNow() }); return true;
    }
}

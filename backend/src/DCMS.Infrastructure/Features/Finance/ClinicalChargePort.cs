using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Finance;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace DCMS.Infrastructure.Features.Finance;
// Runs inside ClinicalWorkService's transaction and advisory lock; all clinical eligibility gates still apply.
public sealed class ClinicalChargePort(DcmsDbContext db, FinanceService finance, TimeProvider clock) : IClinicalChargePort
{
    public async Task<ClinicalFeeResult> PostInTransactionAsync(ClinicalFeeRequest request,CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Fee must share the actual transaction.");
        var row = await db.ClinicalActivities.SingleOrDefaultAsync(x => x.Id == request.ActivityId, ct);
        if (row == null || row.State != "Draft" || row.PatientId != request.PatientId || row.BranchId != request.BranchId
            || row.DossierId != request.DossierId || row.ItemId != request.ItemId || row.PlanVersion != request.PlanVersion
            || row.ChargingBasis != "Separate" || request.ChargingBasis != "Separate" || row.Quantity != request.Quantity
            || row.Unit != request.Unit || row.PriceSnapshotVnd != request.AgreedUnitPriceVnd
            || row.DiscountSnapshotVnd != request.ApprovedDiscountVnd
            || request.RecorderAccountId == null || request.Quantity <= 0 || request.AgreedUnitPriceVnd <= 0
            || request.ApprovedDiscountVnd < 0 || request.GrossVnd < request.ApprovedDiscountVnd
            || request.GrossVnd != request.Quantity * request.AgreedUnitPriceVnd
            || request.GrossVnd > FinanceService.MaxAmount || row.Unit == "gói")
            throw new InvalidOperationException("Invalid clinical fee snapshot.");
        var existing = await db.Set<FinanceCharge>().SingleOrDefaultAsync(x => x.SourceKind == "ClinicalActivity" && x.SourceId == row.Id, ct);
        if (existing != null) return new("Posted", existing.Id);
        if (row.VisitId.HasValue)
        {
            if (!await db.Visits.AnyAsync(x => x.Id == row.VisitId && x.PatientId == row.PatientId && x.BranchId == row.BranchId && x.Status == "InProgress", ct))
                throw new InvalidOperationException("Visit is not eligible for clinical billing.");
        }
        else if (!row.DossierId.HasValue || !await db.TreatmentDossiers.AnyAsync(x => x.Id == row.DossierId && x.PatientId == row.PatientId && x.BranchId == row.BranchId && x.State == "Open", ct))
            throw new InvalidOperationException("Dossier is not eligible for clinical billing.");
        var service = await db.Set<DCMS.Domain.Features.Scheduling.SchedulingService>().SingleAsync(x => x.Id == row.ServiceId && x.BranchId == row.BranchId, ct);
        var charge = new FinanceCharge { VisitId = row.VisitId, DossierId = row.VisitId.HasValue ? null : row.DossierId,
            PatientId = row.PatientId, BranchId = row.BranchId, SourceKind = "ClinicalActivity", SourceId = row.Id,
            ActivityId = row.Id, ServiceId = row.ServiceId, Name = row.ServiceNameSnapshot ?? service.Name, Unit = row.Unit,
            Quantity = row.Quantity, UnitPriceVnd = request.AgreedUnitPriceVnd,
            AmountVnd = request.GrossVnd - request.ApprovedDiscountVnd, PriceVersion = service.PriceVersion,
            PostedBy = request.RecorderAccountId.Value, PostedAt = clock.GetUtcNow() };
        await finance.AddCharge(charge, ct);
        db.Add(new FinanceAudit { ActorId = charge.PostedBy, BranchId = charge.BranchId, VisitId = charge.VisitId,
            Action = "PostClinicalCharge", Target = row.Id.ToString(), At = clock.GetUtcNow() });
        return new("Posted", charge.Id);
    }
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

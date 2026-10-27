using System.Text.Json;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed class ClinicalConsentPort(DcmsDbContext db) : IClinicalConsentPort
{
    public async Task<bool> ValidAsync(int patientId, Guid? itemId, Guid? stepId, int? planVersion,
        int serviceId, CancellationToken ct)
    {
        if (!itemId.HasValue || !planVersion.HasValue) return false;
        var item = await db.TreatmentItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == itemId, ct);
        if (item == null || item.ServiceId != serviceId || item.Decision != "Agreed"
            || item.DecisionPlanVersion is null || item.DecisionPlanVersion > planVersion
            || string.IsNullOrWhiteSpace(item.Counseling) || string.IsNullOrWhiteSpace(item.DecisionMaker)) return false;
        var dossier = await db.TreatmentDossiers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == item.DossierId
            && x.PatientId == patientId && x.State == "Open" && x.PlanVersion == planVersion, ct);
        if (dossier == null) return false;
        if (stepId.HasValue && !await db.TreatmentSteps.AnyAsync(x => x.Id == stepId && x.ItemId == item.Id
            && x.DossierId == dossier.Id, ct)) return false;
        return !item.ConsentDocumentId.HasValue || item.ConsentPlanVersion == item.DecisionPlanVersion
            && await db.Set<ClinicalDocument>().AnyAsync(x => x.Id == item.ConsentDocumentId && x.PatientId == patientId
                && x.DossierId == dossier.Id && x.PlanId == dossier.PlanId && x.FileState == "Ready", ct);
    }
}

// Only writes the outbox owned by the posting transaction. DCMS-101 consumes the source key.
public sealed class OutboxClinicalChargePort(DcmsDbContext db, TimeProvider clock) : IClinicalChargePort
{
    public async Task<ClinicalFeeResult> PostInTransactionAsync(ClinicalFeeRequest request, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Fee request must share the actual transaction.");
        if (!await db.TreatmentProgressionEvents.AnyAsync(x => x.StableKey == request.SourceKey, ct))
            db.TreatmentProgressionEvents.Add(new TreatmentProgressionEvent { AggregateId = request.ActivityId,
                ActorId = request.RecorderAccountId ?? throw new InvalidOperationException("Recorder is required."),
                Kind = "clinical.fee.requested", StableKey = request.SourceKey, OccurredAt = clock.GetUtcNow(),
                PayloadJson = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        return new("Pending");
    }
}

using System.Text.Json;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

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

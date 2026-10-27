using System.Text.Json;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed partial class TreatmentDossierService
{
    private sealed record SheetActivitySnapshot(Guid ActivityId, int SheetVersion, ActivityDto Activity);

    private async Task<Dictionary<Guid, ActivityDto>> SheetActivityOverridesAsync(Guid dossierId, CancellationToken ct)
    {
        var entries = await db.TreatmentEvents.AsNoTracking().Where(e => e.DossierId == dossierId &&
            (e.Kind == "SheetActivityEdited" || e.Kind == "SheetActivityDeleted")).OrderBy(e => e.DossierVersion).ToListAsync(ct);
        var result = new Dictionary<Guid, ActivityDto>();
        foreach (var e in entries)
        {
            var snapshot = JsonSerializer.Deserialize<SheetActivitySnapshot>(e.PayloadJson, Json) ?? throw Error("MSG144", 503);
            result[snapshot.ActivityId] = snapshot.Activity;
        }
        return result;
    }

    private async Task<(ClinicalActivity Source, ActivityDto Current)> EditableSheetActivityAsync(int actorId,
        TreatmentDossier dossier, Guid id, int expectedVersion, int expectedActivityVersion, int expectedSheetVersion, CancellationToken ct)
    {
        if (await DentistAsync(actorId, dossier.BranchId, ct) != dossier.DentistId) throw Error("MSG06", 403);
        if (dossier.Version != expectedVersion) throw Error("MSG111", 409);
        var source = await db.ClinicalActivities.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id && a.DossierId == dossier.Id
            && a.PatientId == dossier.PatientId && a.BranchId == dossier.BranchId && a.State == "Posted", ct) ?? throw Error("MSG113", 404);
        var overrides = await SheetActivityOverridesAsync(dossier.Id, ct);
        var current = overrides.GetValueOrDefault(id) ?? DirectActivityDto(source);
        if (current.ServiceNameSnapshot == null)
            current = current with { ServiceNameSnapshot = (await ActivitySourceNamesAsync([source], dossier, ct)).GetValueOrDefault(id) };
        if (source.Version != expectedActivityVersion || current.SheetVersion != expectedSheetVersion) throw Error("MSG111", 409);
        return (source, current);
    }

    public Task<ConfirmSheetActualResult> EditSheetActivityAsync(int actorId, Guid dossierId, Guid activityId, EditSheetActivityRequest request, CancellationToken ct)
        => SheetCommandAsync(actorId, dossierId, request.RequestKey, "SheetActivityEdit", new { dossierId, activityId, request }, async dossier =>
        {
            var (source, before) = await EditableSheetActivityAsync(actorId, dossier, activityId, request.ExpectedVersion,
                request.ExpectedActivityVersion, request.ExpectedSheetVersion, ct);
            var after = await EditSheetActivityCoreAsync(actorId, dossier, source, before, ReadRow(request.Row), ct);
            return new ConfirmSheetActualResult(await DetailAsync(dossier, ct), after);
        }, ct);

    private async Task<ActivityDto> EditSheetActivityCoreAsync(int actorId, TreatmentDossier dossier, ClinicalActivity source,
        ActivityDto before, SheetRow row, CancellationToken ct)
    {
            if (row.Quantity != source.Quantity || row.DentistId != dossier.DentistId) throw Error("MSG04");
            var service = await db.Set<SchedulingService>().AsNoTracking().SingleOrDefaultAsync(s => s.Id == row.ServiceId && s.BranchId == dossier.BranchId && (s.Active || s.Id == before.ServiceId), ct) ?? throw Error("MSG04");
            if (!TreatmentSheetRules.ValidQuantity(row.Quantity, service.Unit)) throw Error("MSG04");
            if (row.AssistantId.HasValue && !await db.Set<SchedulingMember>().AnyAsync(m => m.EmployeeId == row.AssistantId && m.BranchId == dossier.BranchId && m.Active && !m.CanDentist, ct)) throw Error("MSG04");
            var price = row.UnitPriceVnd ?? checked((long)(row.CostVnd / row.Quantity));
            if (TreatmentPlanRules.Gross(row.Quantity, price) != row.CostVnd) throw Error("MSG04");
            var type = row.DiscountType == "VND" ? "Fixed" : row.DiscountType;
            long discount;
            if (row.DiscountValue < 0 || row.DiscountValue > 0 && type is not ("Fixed" or "Percent")) throw Error("MSG252");
            try { discount = row.DiscountValue > 0 ? TreatmentPlanRules.Discount(row.CostVnd, type!, row.DiscountValue) : 0; }
            catch (ArgumentOutOfRangeException) { throw Error("MSG252"); }
            var moneyChanged = before.SheetDeleted || before.ServiceId != row.ServiceId || before.PriceSnapshotVnd != price || before.DiscountSnapshotVnd != discount;
            if (moneyChanged && discount > 0 && Clean(row.DiscountNote) == null) throw Error("MSG252");
            var after = before with { ServiceId = service.Id, ServiceNameSnapshot = service.Id == before.ServiceId ? before.ServiceNameSnapshot ?? service.Name : service.Name, Unit = service.Unit,
                Region = Clean(string.Join(", ", row.Regions ?? [])), Outcome = Clean(row.Note) ?? "", PerformerId = row.DentistId,
                AssistantId = row.AssistantId, LabWorkSnapshot = Clean(row.LabWork), PriceSnapshotVnd = price,
                DiscountSnapshotVnd = discount, SheetDiscountType = type, SheetDiscountValue = row.DiscountValue, SheetDiscountReason = Clean(row.DiscountNote),
                PerformedAt = row.Date.HasValue ? new DateTimeOffset(row.Date.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime() : before.PerformedAt,
                SheetVersion = before.SheetVersion + 1, SheetDeleted = false, FeeStatus = moneyChanged ? "NeedsAdjustment" : before.FeeStatus };
            dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
            Event(dossier, actorId, "SheetActivityEdited", new { activityId = source.Id, after.SheetVersion, activity = after, before });
            if (moneyChanged) RecordSheetFeeCorrection(actorId, dossier, before, after);
            await db.SaveChangesAsync(ct);
            return after;
    }

    public Task<ConfirmSheetActualResult> DeleteSheetActivityAsync(int actorId, Guid dossierId, Guid activityId, SheetActivityActionRequest request, CancellationToken ct)
        => SheetCommandAsync(actorId, dossierId, request.RequestKey, "SheetActivityDelete", new { dossierId, activityId, request }, async dossier =>
        {
            var (_, before) = await EditableSheetActivityAsync(actorId, dossier, activityId, request.ExpectedVersion,
                request.ExpectedActivityVersion, request.ExpectedSheetVersion, ct);
            var after = before with { SheetVersion = before.SheetVersion + 1, SheetDeleted = true };
            dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
            Event(dossier, actorId, "SheetActivityDeleted", new { activityId, after.SheetVersion, activity = after, before });
            if (before.PriceSnapshotVnd > 0 && !before.SheetDeleted) RecordSheetFeeCorrection(actorId, dossier, before, after);
            await db.SaveChangesAsync(ct);
            return new ConfirmSheetActualResult(await DetailAsync(dossier, ct), after);
        }, ct);

    private void RecordSheetFeeCorrection(int actorId, TreatmentDossier dossier, ActivityDto before, ActivityDto after)
    {
        var sourceKey = $"clinical-fee-correction:{after.Id}:{after.SheetVersion}";
        db.TreatmentProgressionEvents.Add(new TreatmentProgressionEvent {
            AggregateId = after.Id, ActorId = actorId, Kind = "clinical.fee.correction-requested", StableKey = sourceKey,
            OccurredAt = clock.GetUtcNow(), PayloadJson = JsonSerializer.Serialize(new {
                sourceKey, activityId = after.Id, activityVersion = after.Version, sheetVersion = after.SheetVersion,
                dossierId = dossier.Id, dossier.PatientId, dossier.BranchId, before.ServiceId,
                previousGrossVnd = TreatmentPlanRules.Gross(before.Quantity, checked((long)before.PriceSnapshotVnd)),
                previousDiscountVnd = before.DiscountSnapshotVnd, proposedServiceId = after.ServiceId,
                proposedGrossVnd = after.SheetDeleted ? 0 : TreatmentPlanRules.Gross(after.Quantity, checked((long)after.PriceSnapshotVnd)),
                proposedDiscountVnd = after.SheetDeleted ? 0 : after.DiscountSnapshotVnd,
                after.SheetDeleted, discountReason = after.SheetDiscountReason, recorderAccountId = actorId
            }, Json)
        });
    }
}

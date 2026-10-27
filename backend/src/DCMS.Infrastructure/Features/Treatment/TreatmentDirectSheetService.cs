using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed partial class TreatmentDossierService
{
    private async Task<TreatmentItem> EditableSheetItemAsync(int actorId, TreatmentDossier dossier, Guid itemId, int version, CancellationToken ct)
    {
        var employee = await DentistAsync(actorId, dossier.BranchId, ct);
        if (employee != dossier.DentistId) throw Error("MSG06", 403);
        if (dossier.Version != version) throw Error("MSG111", 409);
        return dossier.Items.SingleOrDefault(x => x.Id == itemId && !x.SheetDeleted) ?? throw Error("MSG113", 404);
    }

    public Task<TreatmentChange> MarkSheetItemAsync(int actorId, Guid dossierId, Guid itemId, MarkSheetItemRequest request, CancellationToken ct)
        => SheetCommandAsync(actorId, dossierId, request.RequestKey, "SheetItemMark", new { dossierId, itemId, request }, async dossier =>
        {
            var item = await EditableSheetItemAsync(actorId, dossier, itemId, request.ExpectedVersion, ct);
            item.DisplayCompleted = request.Completed;
            dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
            Event(dossier, actorId, "SheetItemMarked", new { itemId, request.Completed });
            await db.SaveChangesAsync(ct);
            return new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "hạng mục", value: dossier.Code));
        }, ct);

    public Task<TreatmentChange> DeleteSheetItemAsync(int actorId, Guid dossierId, Guid itemId, SheetItemActionRequest request, CancellationToken ct)
        => SheetCommandAsync(actorId, dossierId, request.RequestKey, "SheetItemDelete", new { dossierId, itemId, request }, async dossier =>
        {
            var item = await EditableSheetItemAsync(actorId, dossier, itemId, request.ExpectedVersion, ct);
            var before = ItemDto(item);
            item.SheetDeleted = true;
            dossier.PlanVersion++; dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
            await CarryForwardUnchangedProgressionAsync(dossier, actorId, ct);
            Event(dossier, actorId, "SheetItemDeleted", new { itemId, before, dossier.PlanVersion });
            RecordDirectPlanSnapshot(dossier, actorId);
            await db.SaveChangesAsync(ct);
            return new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "hạng mục", value: dossier.Code));
        }, ct);

    public Task<TreatmentChange> EditSheetItemAsync(int actorId, Guid dossierId, Guid itemId, EditSheetItemRequest request, CancellationToken ct)
        => SheetCommandAsync(actorId, dossierId, request.RequestKey, "SheetItemEdit", new { dossierId, itemId, request }, async dossier =>
        {
            var item = await EditableSheetItemAsync(actorId, dossier, itemId, request.ExpectedVersion, ct);
            if (dossier.State != "Open") throw Error("MSG230", 409);
            var row = ReadRow(request.Row);
            if (row.Quantity != item.Quantity || row.DentistId != dossier.DentistId) throw Error("MSG04");
            var service = await db.Set<SchedulingService>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == row.ServiceId && x.BranchId == dossier.BranchId && x.Active, ct)
                ?? throw Error("MSG04");
            if (!TreatmentSheetRules.ValidQuantity(row.Quantity, service.Unit)) throw Error("MSG04");
            if (row.AssistantId.HasValue && !await db.Set<SchedulingMember>().AnyAsync(x => x.EmployeeId == row.AssistantId && x.BranchId == dossier.BranchId && x.Active && !x.CanDentist, ct)) throw Error("MSG04");
            var unitPrice = row.UnitPriceVnd ?? checked((long)(row.CostVnd / row.Quantity));
            if (TreatmentPlanRules.Gross(row.Quantity, unitPrice) != row.CostVnd) throw Error("MSG04");
            var type = row.DiscountType == "VND" ? "Fixed" : row.DiscountType;
            if (row.DiscountValue < 0 || row.DiscountValue > 0 && (type is not ("Fixed" or "Percent") || Clean(row.DiscountNote) == null)) throw Error("MSG252");
            try { if (row.DiscountValue > 0) _ = TreatmentPlanRules.Discount(row.CostVnd, type!, row.DiscountValue); }
            catch (ArgumentOutOfRangeException) { throw Error("MSG252"); }
            var before = ItemDto(item);
            if (item.ServiceId != service.Id) { item.ReferenceUnitPriceVnd = service.ReferenceUnitPriceVnd; item.ReferencePriceVersion = service.PriceVersion; }
            item.ServiceId = service.Id; item.ServiceName = service.Name; item.Unit = service.Unit;
            item.Region = Clean(string.Join(", ", row.Regions ?? [])); item.Note = Clean(row.Note);
            item.EstimatedStart = row.Date; item.EstimatedMinutes = row.EstimatedMinutes;
            item.EstimatedMinMinutes = null; item.EstimatedMaxMinutes = null; item.ReservedMinutes = null;
            item.ResponsibleDentistId = row.DentistId; item.AssistantId = row.AssistantId; item.AgreedUnitPriceVnd = unitPrice;
            item.DiscountType = row.DiscountValue > 0 ? type : null; item.DiscountValue = row.DiscountValue > 0 ? row.DiscountValue : null;
            item.DiscountReason = row.DiscountValue > 0 ? Clean(row.DiscountNote) : null;
            item.DiscountStatus = row.DiscountValue > 0 ? "Approved" : null; item.DiscountQualifyingItemId = null;
            item.DiscountDecidedBy = row.DiscountValue > 0 ? actorId : null; item.DiscountDecidedAt = row.DiscountValue > 0 ? clock.GetUtcNow() : null;
            item.DiscountBasisHash = row.DiscountValue > 0 ? DiscountBasis(item, null) : null;
            item.EntrySource = "ClinicianEntry"; item.EntryRecordedBy = actorId; item.EntryRecordedAt = clock.GetUtcNow();
            dossier.PlanVersion++; dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
            await CarryForwardUnchangedProgressionAsync(dossier, actorId, ct);
            Event(dossier, actorId, "SheetItemEdited", new { before, after = ItemDto(item), dossier.PlanVersion });
            RecordDirectPlanSnapshot(dossier, actorId);
            await db.SaveChangesAsync(ct);
            return new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "hạng mục", value: dossier.Code));
        }, ct);

    public Task<ConfirmSheetActualResult> PerformSheetItemAsync(int actorId, Guid dossierId, Guid itemId, PerformSheetItemRequest request, CancellationToken ct)
        => SheetCommandAsync(actorId, dossierId, request.RequestKey, "SheetItemPerform", new { dossierId, itemId, request }, async dossier =>
        {
            var item = await EditableSheetItemAsync(actorId, dossier, itemId, request.ExpectedVersion, ct);
            if (dossier.State != "Open") throw Error("MSG230", 409);
            var old = await db.ClinicalActivities.SingleOrDefaultAsync(x => x.ItemId == itemId && x.IsPlanTransfer, ct);
            ActivityDto activity;
            var input = request.Row.HasValue ? ReadRow(request.Row.Value) : null;
            if (old != null)
            {
                var current = (await SheetActivityOverridesAsync(dossierId, ct)).GetValueOrDefault(old.Id) ?? DirectActivityDto(old);
                activity = input == null ? current : await EditSheetActivityCoreAsync(actorId, dossier, old, current, input, ct);
            }
            else activity = await RecordDossierActivityAsync(actorId, dossier, item, Guid.NewGuid(), input?.Date,
                input?.Note ?? item.Note, true, request.RequestKey, ct, input?.LabWork, input);
            item.DisplayCompleted = true;
            dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
            Event(dossier, actorId, "SheetItemPerformed", new { itemId, activity.Id });
            await db.SaveChangesAsync(ct);
            return new ConfirmSheetActualResult(await DetailAsync(dossier, ct), activity);
        }, ct);

    private void RecordDirectPlanSnapshot(TreatmentDossier dossier, int actorId) =>
        Event(dossier, actorId, "PlanCommitted", new { dossier.PlanId, dossier.PlanVersion, dossier.Goals,
            dossier.PaymentTermsJson, Items = dossier.Items.OrderBy(x => x.PlannedOrder).Select(ItemDto).ToArray() });

    private static long GrossInput(decimal quantity, long price) => TreatmentPlanRules.Gross(quantity, price);

    private static ActivityDto DirectActivityDto(ClinicalActivity x) => new(x.Id, x.VisitId, x.PatientId, x.BranchId,
        x.DossierId, x.ItemId, x.StepId, x.PlanVersion, x.ServiceId, x.Region, x.Unit, x.Quantity, x.PerformedAt,
        x.ElapsedMinutes, x.PerformerId, x.Outcome, x.NextSteps, x.ChargingBasis, x.ChargingBasisEvidence,
        x.PriceSnapshotVnd, x.ReviewId, x.State, x.Version, x.CreatedAt, x.PostedAt, x.FeeStatus,
        x.DiscountSnapshotVnd, x.AssistantId, x.RecordedBy, x.SourceKind, x.ServiceNameSnapshot, x.LabWorkSnapshot);

    private async Task<ActivityDto> RecordDossierActivityAsync(int actorId, TreatmentDossier dossier, TreatmentItem item,
        Guid activityId, DateOnly? date, string? note, bool transfer, Guid key, CancellationToken ct, string? labWork = null, SheetRow? input = null)
    {
        var employee = await DentistAsync(actorId, dossier.BranchId, ct);
        if (employee != dossier.DentistId || item.ResponsibleDentistId.HasValue && item.ResponsibleDentistId != employee) throw Error("MSG06", 403);
        if (item.SheetDeleted || note?.Length > 1000) throw Error("MSG04");
        if (await db.TreatmentSteps.AnyAsync(x => x.ItemId == item.Id && x.HoldReason != null, ct)) throw Error("MSG241", 409);
        var service = input == null ? null : await db.Set<SchedulingService>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.ServiceId && x.BranchId == dossier.BranchId && (x.Active || x.Id == item.ServiceId), ct) ?? (input == null ? null : throw Error("MSG04"));
        var qty = input?.Quantity ?? item.Quantity;
        var price = input?.UnitPriceVnd ?? (input != null ? checked((long)(input.CostVnd / qty)) : item.AgreedUnitPriceVnd);
        if (input != null && (input.DentistId != employee || qty != item.Quantity || !TreatmentSheetRules.ValidQuantity(qty, service?.Unit ?? item.Unit) || GrossInput(qty, price) != input.CostVnd)) throw Error("MSG04");
        var discountType = input == null ? item.DiscountType : input.DiscountType == "VND" ? "Fixed" : input.DiscountType;
        var discountValue = input?.DiscountValue ?? item.DiscountValue ?? 0;
        var discountReason = input?.DiscountNote ?? item.DiscountReason;
        if (discountValue < 0 || discountValue > 0 && (discountType is not ("Fixed" or "Percent") || Clean(discountReason) == null)) throw Error("MSG252");
        long reduction;
        try { reduction = discountValue > 0 ? TreatmentPlanRules.Discount(GrossInput(qty, price), discountType!, discountValue) : 0; }
        catch (ArgumentOutOfRangeException) { throw Error("MSG252"); }
        var assistant = input == null ? item.AssistantId : input.AssistantId;
        if (assistant.HasValue && !await db.Set<SchedulingMember>().AnyAsync(x => x.BranchId == dossier.BranchId && x.EmployeeId == assistant && x.Active && !x.CanDentist, ct)) throw Error("MSG04");
        var activity = new ClinicalActivity { Id = activityId, SourceKind = "Dossier", ServiceNameSnapshot = service == null || service.Id == item.ServiceId ? item.ServiceName : service.Name,
            LabWorkSnapshot = Clean(labWork), IsPlanTransfer = transfer, PatientId = dossier.PatientId, BranchId = dossier.BranchId, DossierId = dossier.Id,
            ItemId = item.Id, PlanVersion = dossier.PlanVersion, ServiceId = service?.Id ?? item.ServiceId, Unit = service?.Unit ?? item.Unit,
            Quantity = qty, Region = input == null ? item.Region : Clean(string.Join(", ", input.Regions ?? [])), PerformerId = employee, AssistantId = assistant,
            RecordedBy = actorId, Outcome = Clean(note) ?? "", ChargingBasis = "Separate", PriceSnapshotVnd = price, DiscountSnapshotVnd = reduction,
            PerformedAt = date.HasValue ? new DateTimeOffset(date.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime() : clock.GetUtcNow(),
            CreatedAt = clock.GetUtcNow(), FeeStatus = "NotRequested" };
        db.ClinicalActivities.Add(activity);
        await db.SaveChangesAsync(ct);
        return await clinicalWork.PostWithinTransactionAsync(actorId, activity.Id,
            new PostActivityRequest(activity.Version, 0, null, null, key), ct,
            input == null ? null : new ClinicalFeeRequest($"clinical-fee:{activity.Id}", activity.Id, activity.Version + 1,
                activity.PatientId, activity.BranchId, activity.DossierId, activity.ItemId, activity.PlanVersion, "Separate",
                qty, activity.Unit, price, GrossInput(qty, price), reduction, discountType, discountValue, Clean(discountReason), null,
                reduction > 0 ? actorId : null, reduction > 0 ? clock.GetUtcNow() : null, actorId));
    }
}

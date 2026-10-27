using System.Data;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed partial class TreatmentDossierService
{
    private sealed record SheetRow(DateOnly? Date, int ServiceId, int DentistId, string[]? Regions,
        decimal Quantity, long CostVnd, string? Note, int? AssistantId, Guid? SourceItemId,
        string? DiscountType, decimal DiscountValue, string? DiscountNote, long? UnitPriceVnd = null,
        string? Region = null, int? EstimatedMinutes = null, string? LabWork = null);

    private static SheetRow ReadRow(JsonElement json)
    {
        SheetRow row;
        try { row = json.Deserialize<SheetRow>(Json) ?? throw Error("MSG04"); }
        catch (JsonException) { throw Error("MSG04"); }
        if (row.LabWork?.Length > 1000 || row.EstimatedMinutes is < 1 or > 480) throw Error("MSG04");
        if (row.ServiceId < 1 || row.DentistId < 1 || row.CostVnd < 0 || row.CostVnd > 9_000_000_000_000_000L || row.Quantity <= 0
            || row.Quantity > 999999 || decimal.Round(row.Quantity, 3) != row.Quantity
            || row.UnitPriceVnd is < 0 or > 1_000_000_000_000L || row.Note?.Length > 1000 || row.Region?.Length > 80
            || (row.Regions?.Length ?? 0) > 32 || row.Regions?.Any(x => !int.TryParse(x, out var n)
                || !TreatmentSheetRules.IsFdi(n)) == true
            || string.Join(", ", row.Regions ?? []).Length > 80) throw Error("MSG04");
        if (row.UnitPriceVnd == null && row.CostVnd / row.Quantity != decimal.Truncate(row.CostVnd / row.Quantity))
            throw Error("MSG04");
        return row;
    }

    private async Task<T> SheetCommandAsync<T>(int actorId, Guid dossierId, Guid key, string command,
        object payload, Func<TreatmentDossier, Task<T>> mutate, CancellationToken ct)
    {
        if (key == Guid.Empty) throw Error("MSG04");
        await using var gate = await ClinicalRequestLock.AcquireAsync(db, key, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({Features.Scheduling.WorkingScheduleService.TransactionLock})", ct);
        var dossier = await DossierAsync(actorId, dossierId, ct);
        var fingerprint = Hash(payload);
        var previous = await db.TreatmentProgressionReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (previous != null)
        {
            if (previous.ActorId != actorId || previous.Command != command || previous.Fingerprint != fingerprint)
                throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<T>(previous.ResultJson, Json) ?? throw Error("MSG144", 503);
        }
        var result = await mutate(dossier);
        await db.SaveChangesAsync(ct);
        db.TreatmentProgressionReceipts.Add(new TreatmentProgressionReceipt { RequestKey = key,
            ActorId = actorId, Command = command, Fingerprint = fingerprint, ResultJson = JsonSerializer.Serialize(result, Json) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<TreatmentSheetDraft> SheetDraftAsync(int actorId, Guid dossierId, Guid id,
        string kind, int draftVersion, int version, TreatmentDossier dossier, CancellationToken ct)
    {
        if (dossier.Version != version) throw Error("MSG111", 409);
        var draft = await db.TreatmentSheetDrafts.SingleOrDefaultAsync(x => x.Id == id && x.DossierId == dossierId, ct)
            ?? throw Error("MSG113", 404);
        if (draft.ActorId != actorId) throw Error("MSG06", 403);
        if (draft.Version != draftVersion || draft.Kind != kind)
            throw Error("MSG111", 409);
        if (dossier.State != "Open") throw Error("MSG230", 409);
        return draft;
    }

    private async Task<TreatmentItem> AddSheetItemAsync(int actorId, TreatmentDossier dossier,
        SheetRow row, Guid itemId, SheetDecision? decision, CancellationToken ct)
    {
        var clinicianEntry = decision == null;
        decision ??= new SheetDecision("Proposed", null, null);
        if (decision.Decision is not ("Proposed" or "Agreed" or "Deferred" or "Refused")
            || decision.Counseling?.Length > 4000 || decision.DecisionMaker?.Length > 160
            || decision.RepresentativeBasis?.Length > 1000
            || decision.Decision != "Proposed" && (Clean(decision.Counseling) == null || Clean(decision.DecisionMaker) == null))
            throw Error("MSG240", 409);
        // A new source may be added to an agreed version, but cannot silently commit other pending edits.
        if (dossier.PlanState == "Draft") throw Error("MSG111", 409);
        var service = await db.Set<SchedulingService>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == row.ServiceId && x.BranchId == dossier.BranchId && x.Active, ct) ?? throw Error("MSG04");
        var actorEmployee = await DentistAsync(actorId, dossier.BranchId, ct);
        if (row.DentistId != actorEmployee || dossier.DentistId != actorEmployee)
            throw Error("MSG06", 403);
        if (!TreatmentSheetRules.ValidQuantity(row.Quantity, service.Unit)) throw Error("MSG04");
        if (row.AssistantId.HasValue && !await db.Set<SchedulingMember>().AnyAsync(x => x.BranchId == dossier.BranchId
            && x.EmployeeId == row.AssistantId && x.Active && !x.CanDentist, ct)) throw Error("MSG04");
        if (decision.ConsentDocumentId.HasValue && !await db.Set<ClinicalDocument>().AnyAsync(x =>
            x.Id == decision.ConsentDocumentId && x.PatientId == dossier.PatientId && x.DossierId == dossier.Id
            && x.PlanId == dossier.PlanId && x.FileState == "Ready", ct)) throw Error("MSG240", 409);
        var unitPrice = row.UnitPriceVnd ?? checked((long)(row.CostVnd / row.Quantity));
        var gross = TreatmentPlanRules.Gross(row.Quantity, unitPrice);
        if (gross != row.CostVnd) throw Error("MSG04");
        var discountType = row.DiscountType == "VND" ? "Fixed" : row.DiscountType;
        if (row.DiscountValue > 0 && (discountType is not ("Fixed" or "Percent") || Clean(row.DiscountNote) == null))
            throw Error("MSG252");
        if (row.DiscountValue < 0) throw Error("MSG252");
        if (row.DiscountValue > 0)
        {
            try { _ = TreatmentPlanRules.Discount(gross, discountType!, row.DiscountValue); }
            catch (ArgumentOutOfRangeException) { throw Error("MSG252"); }
        }
        dossier.PlanVersion++;
        var item = new TreatmentItem { Id = itemId, DossierId = dossier.Id, ServiceId = service.Id,
            ServiceName = service.Name, Unit = service.Unit, Region = Clean(row.Region) ?? Clean(string.Join(", ", row.Regions ?? [])),
            Note = Clean(row.Note), ResponsibleDentistId = row.DentistId, AssistantId = row.AssistantId,
            Quantity = row.Quantity, AgreedUnitPriceVnd = unitPrice, ReferenceUnitPriceVnd = service.ReferenceUnitPriceVnd,
            ReferencePriceVersion = service.PriceVersion, EstimatedStart = row.Date, EstimatedMinutes = row.EstimatedMinutes,
            EntrySource = clinicianEntry ? "ClinicianEntry" : null,
            EntryRecordedBy = clinicianEntry ? actorId : null, EntryRecordedAt = clinicianEntry ? clock.GetUtcNow() : null,
            PlannedOrder = dossier.Items.Select(x => x.PlannedOrder).DefaultIfEmpty(0).Max() + 1,
            Decision = decision.Decision, Counseling = Clean(decision.Counseling), DecisionMaker = Clean(decision.DecisionMaker),
            RepresentativeBasis = Clean(decision.RepresentativeBasis), DecidedAt = decision.Decision == "Proposed" ? null : clock.GetUtcNow(),
            DecidedBy = decision.Decision == "Proposed" ? null : actorId, DecisionPlanVersion = dossier.PlanVersion,
            ConsentDocumentId = decision.ConsentDocumentId, ConsentPlanVersion = decision.ConsentDocumentId.HasValue ? dossier.PlanVersion : null };
        if (row.DiscountValue > 0)
        {
            if (actorEmployee != dossier.DentistId) throw Error("MSG06", 403);
            item.DiscountType = discountType; item.DiscountValue = row.DiscountValue; item.DiscountReason = Clean(row.DiscountNote);
            item.DiscountStatus = "Approved"; item.DiscountDecidedBy = actorId; item.DiscountDecidedAt = clock.GetUtcNow();
            item.DiscountBasisHash = DiscountBasis(item, null);
        }
        dossier.Items.Add(item);
        db.TreatmentItems.Add(item);
        dossier.PlanState = "Committed";
        dossier.Goals ??= clinicianEntry ? "Kế hoạch do bác sĩ ghi nhận" : "Kế hoạch điều trị theo thỏa thuận từng hạng mục";
        dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
        await CarryForwardUnchangedProgressionAsync(dossier, actorId, ct);
        Event(dossier, actorId, "PlanCommitted", new { dossier.PlanId, dossier.PlanVersion,
            dossier.Goals, dossier.PaymentTermsJson, Items = dossier.Items.OrderBy(x => x.PlannedOrder).Select(ItemDto).ToArray() });
        return item;
    }

    private async Task CarryForwardUnchangedProgressionAsync(TreatmentDossier dossier, int actorId, CancellationToken ct)
    {
        var previousVersion = dossier.PlanVersion - 1;
        var steps = await db.TreatmentSteps.Where(x => x.DossierId == dossier.Id && x.PlanVersion == previousVersion).ToListAsync(ct);
        var instructions = await db.FollowUpInstructions.Where(x => x.DossierId == dossier.Id && x.PlanVersion == previousVersion
            && (x.Status == "NeedsBooking" || x.Status == "Booked")).ToListAsync(ct);
        foreach (var step in steps) { step.PlanVersion = dossier.PlanVersion; step.Version++; }
        foreach (var instruction in instructions)
        {
            instruction.PlanVersion = dossier.PlanVersion; instruction.Version++; instruction.UpdatedAt = clock.GetUtcNow();
            db.TreatmentProgressionEvents.Add(new TreatmentProgressionEvent { AggregateId = instruction.Id, ActorId = actorId,
                Kind = "followup.plan-version-carried", StableKey = $"followup-plan:{instruction.Id}:{dossier.PlanVersion}",
                OccurredAt = clock.GetUtcNow(), PayloadJson = JsonSerializer.Serialize(new { instruction.Id,
                    previousVersion, dossier.PlanVersion, instruction.Version, instruction.ActiveAppointmentId }, Json) });
        }
        if (steps.Count + instructions.Count > 0)
            Event(dossier, actorId, "PlanReferencesCarriedForward", new { previousVersion, dossier.PlanVersion,
                Steps = steps.Select(x => x.Id), Instructions = instructions.Select(x => x.Id) });
    }

    public Task<TreatmentChange> ConfirmSheetPlanAsync(int actorId, Guid dossierId, Guid rowId,
        ConfirmSheetPlanRequest request, CancellationToken ct) => SheetCommandAsync(actorId, dossierId,
        request.RequestKey, "SheetPlanConfirm", new { dossierId, rowId, request }, async dossier =>
        {
            var draft = await SheetDraftAsync(actorId, dossierId, rowId, "Plan", request.ExpectedDraftVersion, request.ExpectedVersion, dossier, ct);
            var item = await AddSheetItemAsync(actorId, dossier, ReadRow(JsonDocument.Parse(draft.PayloadJson).RootElement), rowId, request.Decision, ct);
            if (request.Estimate is { } estimate)
            {
                if (!TreatmentPlanRules.ValidDuration(estimate.MinMinutes, estimate.SelectedMinutes, estimate.MaxMinutes, null, false)
                    || estimate.Sessions is < 1 or > 1000 || !TreatmentPlanRules.ValidReturnWindow(estimate.EarliestOn, estimate.TargetOn, estimate.LatestOn)) throw Error("MSG04");
                item.EstimatedMinMinutes = estimate.MinMinutes; item.EstimatedMinutes = estimate.SelectedMinutes;
                item.EstimatedMaxMinutes = estimate.MaxMinutes; item.EstimatedSessions = estimate.Sessions;
                item.ReturnEarliestOn = estimate.EarliestOn; item.ReturnTargetOn = estimate.TargetOn; item.ReturnLatestOn = estimate.LatestOn;
            }
            if (request.QualifyingItemId.HasValue)
            {
                var qualifying = dossier.Items.SingleOrDefault(x => x.Id == request.QualifyingItemId && x.Id != item.Id && x.Decision == "Agreed")
                    ?? throw Error("MSG252");
                if (item.DiscountStatus != "Approved") throw Error("MSG252");
                item.DiscountQualifyingItemId = qualifying.Id; item.DiscountBasisHash = DiscountBasis(item, qualifying);
            }
            // Replace this transaction's provisional snapshot with the complete immutable item contract.
            var snapshot = db.ChangeTracker.Entries<TreatmentEvent>().Single(x => x.Entity.DossierId == dossierId && x.Entity.Kind == "PlanCommitted" && x.State == EntityState.Added).Entity;
            snapshot.PayloadJson = JsonSerializer.Serialize(new { dossier.PlanId, dossier.PlanVersion, dossier.Goals,
                dossier.PaymentTermsJson, Items = dossier.Items.OrderBy(x => x.PlannedOrder).Select(ItemDto).ToArray() }, Json);
            db.TreatmentSheetDrafts.Remove(draft);
            await db.SaveChangesAsync(ct);
            return new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get(
                request.Decision?.Decision is "Deferred" or "Refused" ? "MSG249" : "MSG222", "kế hoạch điều trị", value: dossier.Code));
        }, ct);

    public Task<ConfirmSheetActualResult> ConfirmSheetActualAsync(int actorId, Guid dossierId, Guid rowId,
        ConfirmSheetActualRequest request, CancellationToken ct) => SheetCommandAsync(actorId, dossierId,
        request.RequestKey, "SheetActualConfirm", new { dossierId, rowId, request }, async dossier =>
        {
            var draft = await SheetDraftAsync(actorId, dossierId, rowId, "Actual", request.ExpectedDraftVersion, request.ExpectedVersion, dossier, ct);
            var row = ReadRow(JsonDocument.Parse(draft.PayloadJson).RootElement);
            var employee = await DentistAsync(actorId, dossier.BranchId, ct);
            if (!request.VisitId.HasValue)
            {
                TreatmentItem directItem;
                if (row.SourceItemId.HasValue)
                    directItem = dossier.Items.SingleOrDefault(x => x.Id == row.SourceItemId && !x.SheetDeleted) ?? throw Error("MSG113", 404);
                else
                {
                    directItem = await AddSheetItemAsync(actorId, dossier, row, rowId, null, ct);
                    directItem.HiddenFromSheet = true;
                    var snapshot = db.ChangeTracker.Entries<TreatmentEvent>().Single(x => x.Entity.DossierId == dossierId && x.Entity.Kind == "PlanCommitted" && x.State == EntityState.Added).Entity;
                    snapshot.PayloadJson = JsonSerializer.Serialize(new { dossier.PlanId, dossier.PlanVersion, dossier.Goals,
                        dossier.PaymentTermsJson, Items = dossier.Items.OrderBy(x => x.PlannedOrder).Select(ItemDto).ToArray() }, Json);
                }
                if (row.SourceItemId.HasValue && (row.ServiceId != directItem.ServiceId || row.Quantity != directItem.Quantity
                    || row.CostVnd != Gross(directItem) || !TreatmentSheetRules.RegionWithinScope(directItem.Region, Clean(string.Join(", ", row.Regions ?? []))))) throw Error("MSG04");
                var recorded = await RecordDossierActivityAsync(actorId, dossier, directItem, rowId,
                    row.Date, row.Note, false, request.RequestKey, ct, row.LabWork, row);
                db.TreatmentSheetDrafts.Remove(draft);
                dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
                Event(dossier, actorId, "DossierActualRecorded", new { ActivityId = recorded.Id, ItemId = directItem.Id });
                await db.SaveChangesAsync(ct);
                return new ConfirmSheetActualResult(await DetailAsync(dossier, ct), recorded);
            }
            var visit = await db.Visits.SingleOrDefaultAsync(x => x.Id == request.VisitId && x.PatientId == dossier.PatientId
                && x.BranchId == dossier.BranchId, ct) ?? throw Error("MSG113", 404);
            if (visit.Version != request.ExpectedVisitVersion) throw Error("MSG111", 409);
            if (visit.Status != "InProgress" || visit.DentistId != employee || row.DentistId != employee) throw Error("MSG230", 409);
            if (row.Date.HasValue && row.Date != DateOnly.FromDateTime(visit.ArrivalAt.ToOffset(TimeSpan.FromHours(7)).DateTime)) throw Error("MSG04");
            if (row.AssistantId.HasValue && !await db.Set<SchedulingMember>().AnyAsync(x => x.BranchId == dossier.BranchId
                && x.EmployeeId == row.AssistantId && x.Active && !x.CanDentist, ct)) throw Error("MSG04");
            if (Clean(request.Outcome) is not { Length: <= 4000 }) throw Error("MSG04");
            TreatmentItem item;
            if (row.SourceItemId.HasValue)
                item = dossier.Items.SingleOrDefault(x => x.Id == row.SourceItemId) ?? throw Error("MSG113", 404);
            else
            {
                if (request.DirectDecision?.Decision != "Agreed") throw Error("MSG240", 409);
                item = await AddSheetItemAsync(actorId, dossier, row, rowId, request.DirectDecision, ct);
            }
            if (item.Decision != "Agreed" || item.ServiceId != row.ServiceId || item.Progress is "Completed" or "Stopped" or "Cancelled") throw Error("MSG240", 409);
            var performedRegion = Clean(row.Region) ?? Clean(string.Join(", ", row.Regions ?? []));
            if (row.CostVnd != TreatmentPlanRules.Gross(row.Quantity, item.AgreedUnitPriceVnd)
                || !TreatmentSheetRules.RegionWithinScope(item.Region, performedRegion))
                throw Error("MSG240", 409);
            if (await db.TreatmentSteps.AnyAsync(x => x.DossierId == dossierId && x.ItemId == item.Id, ct) && !request.StepId.HasValue)
                throw Error("MSG230", 409);
            var activity = new ClinicalActivity { Id = rowId, PatientId = dossier.PatientId, BranchId = dossier.BranchId,
                DossierId = dossierId, ItemId = item.Id, PlanVersion = dossier.PlanVersion, StepId = request.StepId,
                VisitId = visit.Id, ServiceId = item.ServiceId, Unit = item.Unit, Quantity = row.Quantity,
                Region = performedRegion, PerformerId = employee, AssistantId = row.AssistantId ?? item.AssistantId, RecordedBy = actorId,
                PerformedAt = row.Date.HasValue ? new DateTimeOffset(row.Date.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime() : clock.GetUtcNow(),
                Outcome = request.Outcome.Trim(), ChargingBasis = request.ChargingBasis,
                ChargingBasisEvidence = Clean(request.ChargingBasisEvidence), PriceSnapshotVnd = item.AgreedUnitPriceVnd,
                CreatedAt = clock.GetUtcNow(), FeeStatus = "NotRequested" };
            db.ClinicalActivities.Add(activity);
            await db.SaveChangesAsync(ct);
            var posted = await clinicalWork.PostWithinTransactionAsync(actorId, activity.Id,
                new PostActivityRequest(activity.Version, visit.Version, request.ExpectedStepVersion,
                    request.CompleteItem ? "Completed" : "Partial", request.RequestKey), ct);
            if (request.CompleteItem)
            {
                if (item.Unit != "gói" && await db.ClinicalActivities.Where(x => x.ItemId == item.Id && x.State == "Posted").SumAsync(x => x.Quantity, ct) < item.Quantity)
                    throw Error("MSG203", 409);
                if (await db.TreatmentSteps.AnyAsync(x => x.DossierId == dossierId && x.ItemId == item.Id && x.Outcome != "Completed", ct))
                    throw Error("MSG230", 409);
                item.Progress = "Completed";
            }
            else item.Progress = "InProgress";
            dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
            Event(dossier, actorId, "ActualConfirmed", new { ActivityId = activity.Id, activity.VisitId, ItemId = item.Id, request.CompleteItem });
            db.TreatmentSheetDrafts.Remove(draft);
            await db.SaveChangesAsync(ct);
            return new ConfirmSheetActualResult(await DetailAsync(dossier, ct), posted);
        }, ct);

    public async Task<TreatmentSheetContext> SheetContextAsync(int actorId, Guid dossierId, CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        var employee = await DentistAsync(actorId, dossier.BranchId, ct);
        var services = await db.Set<SchedulingService>().AsNoTracking().Where(x => x.BranchId == dossier.BranchId && x.Active)
            .OrderBy(x => x.Name).Select(x => new SheetService(x.Id, x.BranchId, x.Name, x.Unit, x.ReferenceUnitPriceVnd, x.PriceVersion)).ToListAsync(ct);
        var members = await (from member in db.Set<SchedulingMember>().AsNoTracking()
            join staff in db.Employees on member.EmployeeId equals staff.Id
            where member.BranchId == dossier.BranchId && member.Active
            select new SheetMember(staff.Id, staff.FullName, member.CanDentist)).ToListAsync(ct);
        var visits = await db.Visits.AsNoTracking().Where(x => x.PatientId == dossier.PatientId && x.BranchId == dossier.BranchId
            && x.Status == "InProgress" && x.DentistId == employee).OrderByDescending(x => x.ArrivalAt)
            .Select(x => new SheetVisit(x.Id, x.Version, x.ArrivalAt, x.DentistId)).ToListAsync(ct);
        var steps = await db.TreatmentSteps.AsNoTracking().Where(x => x.DossierId == dossierId)
            .Select(x => new SheetStep(x.Id, x.ItemId, x.Name, x.Version, x.Outcome, x.HoldReason != null)).ToListAsync(ct);
        var documents = await db.Set<ClinicalDocument>().AsNoTracking().Where(x => x.PatientId == dossier.PatientId
            && x.DossierId == dossierId && x.PlanId == dossier.PlanId && x.FileState == "Ready")
            .Select(x => new SheetConsentDocument(x.Id, x.FileName, x.Version)).ToListAsync(ct);
        return new(services, members, visits, dossier.State == "Open" ? ["Draft", "Decide", "Perform", "Close"] : ["Reopen"], steps, documents);
    }

    public async Task<TreatmentActivityPage> ActivitiesAsync(int actorId, Guid dossierId, Guid? visitId,
        DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        if (page < 1 || page > int.MaxValue / 100 || pageSize is not (20 or 50 or 100) || from > to) throw Error("MSG04");
        var query = db.ClinicalActivities.AsNoTracking().Where(x => x.DossierId == dossierId
            && x.PatientId == dossier.PatientId && x.BranchId == dossier.BranchId && x.State == "Posted");
        if (visitId.HasValue) query = query.Where(x => x.VisitId == visitId);
        if (from.HasValue) { var start = new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime(); query = query.Where(x => x.PerformedAt >= start); }
        if (to.HasValue) { var end = new DateTimeOffset(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime(); query = query.Where(x => x.PerformedAt < end); }
        var overrides = await SheetActivityOverridesAsync(dossierId, ct);
        var deletedIds = overrides.Where(x => x.Value.SheetDeleted).Select(x => x.Key).ToArray();
        query = query.Where(x => !deletedIds.Contains(x.Id));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.PerformedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var names = await ActivitySourceNamesAsync(rows, dossier, ct);
        return new(rows.Select(x => overrides.GetValueOrDefault(x.Id) ?? new ActivityDto(x.Id, x.VisitId, x.PatientId, x.BranchId, x.DossierId, x.ItemId,
            x.StepId, x.PlanVersion, x.ServiceId, x.Region, x.Unit, x.Quantity, x.PerformedAt, x.ElapsedMinutes,
            x.PerformerId, x.Outcome, x.NextSteps, x.ChargingBasis, x.ChargingBasisEvidence, x.PriceSnapshotVnd,
            x.ReviewId, x.State, x.Version, x.CreatedAt, x.PostedAt, x.FeeStatus, x.DiscountSnapshotVnd, x.AssistantId, x.RecordedBy, x.SourceKind, names.GetValueOrDefault(x.Id), x.LabWorkSnapshot)).ToArray(), total, page, pageSize);
    }

    // Resolve legacy display metadata from immutable versions without rewriting posted actuals.
    private async Task<Dictionary<Guid, string>> ActivitySourceNamesAsync(List<ClinicalActivity> rows, TreatmentDossier dossier, CancellationToken ct)
    {
        var names = rows.Where(x => x.ServiceNameSnapshot != null).ToDictionary(x => x.Id, x => x.ServiceNameSnapshot!);
        if (rows.All(x => names.ContainsKey(x.Id))) return names;
        var events = await db.TreatmentEvents.AsNoTracking().Where(x => x.DossierId == dossier.Id &&
            (x.Kind == "PlanCommitted" || x.Kind == "PlanRevised" || x.Kind == "SheetItemEdited")).OrderBy(x => x.OccurredAt).ToListAsync(ct);
        foreach (var row in rows.Where(x => !names.ContainsKey(x.Id)))
        {
            string? priorName = null;
            foreach (var entry in events)
            {
                try
                {
                    using var document = JsonDocument.Parse(entry.PayloadJson); var payload = document.RootElement;
                    if (payload.TryGetProperty("planVersion", out var version) && version.GetInt32() == row.PlanVersion
                        && payload.TryGetProperty("items", out var items))
                    {
                        foreach (var item in items.EnumerateArray())
                            if (item.TryGetProperty("id", out var id) && id.GetGuid() == row.ItemId
                                && item.TryGetProperty("serviceName", out var name)) names[row.Id] = name.GetString() ?? "Dịch vụ đã lưu";
                    }
                    if (priorName == null && payload.TryGetProperty("before", out var before) && before.TryGetProperty("id", out var beforeId)
                        && beforeId.GetGuid() == row.ItemId && before.TryGetProperty("serviceName", out var beforeName)) priorName = beforeName.GetString();
                }
                catch (JsonException) { /* Older optional metadata must not hide the clinical row. */ }
            }
            if (!names.ContainsKey(row.Id)) names[row.Id] = priorName ?? dossier.Items.FirstOrDefault(i => i.Id == row.ItemId && i.ServiceId == row.ServiceId)?.ServiceName ?? "Dịch vụ đã lưu";
        }
        return names;
    }

    public Task<TreatmentChange> ScheduleNoteAsync(int actorId, Guid dossierId, Guid itemId,
        ScheduleNoteRequest request, CancellationToken ct) => SheetCommandAsync(actorId, dossierId, request.RequestKey,
        "SheetScheduleNote", new { dossierId, itemId, request }, async dossier =>
        {
            if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
            if (dossier.State != "Open" || request.Note?.Length > 1000) throw Error("MSG04");
            var item = dossier.Items.SingleOrDefault(x => x.Id == itemId) ?? throw Error("MSG113", 404);
            var before = new { item.EstimatedStart, item.Note };
            item.EstimatedStart = request.PlannedOn; item.Note = Clean(request.Note); dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
            Event(dossier, actorId, "ItemScheduleNoteChanged", new { itemId, before, request.PlannedOn, request.Note });
            await db.SaveChangesAsync(ct);
            return new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "ghi chú kế hoạch", value: dossier.Code));
        }, ct);

    public Task<TreatmentChange> DecideSheetItemAsync(int actorId, Guid dossierId, Guid itemId,
        DecideSheetItemRequest request, CancellationToken ct) => SheetCommandAsync(actorId, dossierId, request.RequestKey,
        "SheetItemDecision", new { dossierId, itemId, request }, async dossier =>
        {
            if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
            var item = dossier.Items.SingleOrDefault(x => x.Id == itemId) ?? throw Error("MSG113", 404);
            var decision = request.Decision;
            if (dossier.State != "Open" || dossier.PlanState != "Committed" || item.Decision == "Agreed"
                || item.Progress != "NotStarted") throw Error("MSG240", 409);
            if (decision.Decision is not ("Proposed" or "Agreed" or "Deferred" or "Refused")
                || decision.Decision != "Proposed" && (Clean(decision.Counseling) is not { Length: <= 4000 }
                    || Clean(decision.DecisionMaker) is not { Length: <= 160 })
                || decision.RepresentativeBasis?.Length > 1000) throw Error("MSG240");
            if (decision.ConsentDocumentId.HasValue && !await db.Set<ClinicalDocument>().AnyAsync(x =>
                x.Id == decision.ConsentDocumentId && x.PatientId == dossier.PatientId && x.DossierId == dossier.Id
                && x.PlanId == dossier.PlanId && x.FileState == "Ready", ct)) throw Error("MSG240");
            dossier.PlanVersion++; dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
            await CarryForwardUnchangedProgressionAsync(dossier, actorId, ct);
            item.Decision = decision.Decision; item.Counseling = Clean(decision.Counseling); item.DecisionMaker = Clean(decision.DecisionMaker);
            item.RepresentativeBasis = Clean(decision.RepresentativeBasis); item.DecidedBy = actorId; item.DecidedAt = clock.GetUtcNow();
            item.DecisionPlanVersion = dossier.PlanVersion; item.ConsentDocumentId = decision.ConsentDocumentId;
            item.ConsentPlanVersion = decision.ConsentDocumentId.HasValue ? dossier.PlanVersion : null;
            Event(dossier, actorId, "PlanCommitted", new { dossier.PlanId, dossier.PlanVersion, dossier.Goals,
                dossier.PaymentTermsJson, Items = dossier.Items.OrderBy(x => x.PlannedOrder).Select(ItemDto).ToArray() });
            await db.SaveChangesAsync(ct);
            return new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get(
                decision.Decision is "Deferred" or "Refused" ? "MSG249" : "MSG222", "quyết định điều trị", value: dossier.Code));
        }, ct);

    public async Task<DossierTotals> SummaryAsync(int actorId, int patientId, int? branchId, CancellationToken ct)
    {
        // Reuse list authorization, then aggregate the entire authorized filter, not its first page.
        await ListAsync(actorId, patientId, branchId, 1, 20, ct);
        var employee = await db.Employees.AsNoTracking().SingleAsync(x => x.AccountId == actorId, ct);
        var branches = db.Set<SchedulingMember>().Where(x => x.EmployeeId == employee.Id && x.Active && x.CanDentist).Select(x => x.BranchId);
        var dossiers = await db.TreatmentDossiers.AsNoTracking().Include(x => x.Items).Where(x => x.PatientId == patientId
            && x.State != "Deleted" && branches.Contains(x.BranchId) && (!branchId.HasValue || x.BranchId == branchId)).ToListAsync(ct);
        var gross = 0L; var reduction = 0L; var summaries = new List<TreatmentFinancialSummary>();
        foreach (var dossier in dossiers)
        {
            var preview = PricePreview(dossier); gross = checked(gross + preview.GrossVnd); reduction = checked(reduction + preview.ApprovedDiscountVnd);
            var finance = await financialPort.ReadAsync(patientId, dossier.BranchId, dossier.Id, ct);
            if (finance?.Available == true) summaries.Add(finance);
        }
        var available = dossiers.Count > 0 && summaries.Count == dossiers.Count;
        var ids = dossiers.Select(x => x.Id).ToArray();
        var pending = await db.ClinicalActivities.CountAsync(x => x.DossierId.HasValue && ids.Contains(x.DossierId.Value) && x.State == "Posted" && x.FeeStatus == "Pending", ct);
        return new(dossiers.Count, gross, reduction, gross - reduction, available,
            available && summaries.All(x => x.PostedChargeVnd.HasValue) ? summaries.Sum(x => x.PostedChargeVnd!.Value) : null,
            available && summaries.All(x => x.AllocatedVnd.HasValue) ? summaries.Sum(x => x.AllocatedVnd!.Value) : null,
            available && summaries.All(x => x.OutstandingVnd.HasValue) ? summaries.Sum(x => x.OutstandingVnd!.Value) : null,
            available && summaries.All(x => x.UnusedAdvanceVnd.HasValue) ? summaries.Sum(x => x.UnusedAdvanceVnd!.Value) : null, pending);
    }
}

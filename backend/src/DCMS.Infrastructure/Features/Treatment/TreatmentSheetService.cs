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
        string? Region = null, int? EstimatedMinutes = null, string? LabWork = null,
        DCMS.Application.Features.SystemSettings.MaterialUseLine[]? Materials = null);

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
        var draft = await db.LoadSheetDraftAsync(db.TreatmentSheetDrafts.Where(x => x.Id == id && x.DossierId == dossierId), ct)
            ?? throw Error("MSG113", 404);
        if (draft.ActorId != actorId) throw Error("MSG06", 403);
        if (draft.Version != draftVersion || draft.Kind != kind)
            throw Error("MSG111", 409);
        if (dossier.State != "Open") throw Error("MSG230", 409);
        return draft;
    }

    private async Task<TreatmentItem> AddSheetItemAsync(int actorId, TreatmentDossier dossier,
        SheetRow row, Guid itemId, CancellationToken ct, bool finalize = true, int? targetPlanVersion = null)
    {
        var service = await db.Set<SchedulingService>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == row.ServiceId && x.BranchId == dossier.BranchId && x.Active, ct) ?? throw Error("MSG04");
        var actorEmployee = await DentistAsync(actorId, dossier.BranchId, ct);
        if (row.DentistId != actorEmployee || dossier.DentistId != actorEmployee)
            throw Error("MSG06", 403);
        if (!TreatmentSheetRules.ValidQuantity(row.Quantity, service.Unit)) throw Error("MSG04");
        if (row.AssistantId.HasValue && !await db.SchedulingMembers.AnyAsync(x => x.BranchId == dossier.BranchId
            && x.EmployeeId == row.AssistantId && x.Active && !x.CanDentist, ct)) throw Error("MSG04");
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
        var item = new TreatmentItem { Id = itemId, DossierId = dossier.Id, ServiceId = service.Id,
            ServiceName = service.Name, Unit = service.Unit, Region = Clean(row.Region) ?? Clean(string.Join(", ", row.Regions ?? [])),
            Note = Clean(row.Note), ResponsibleDentistId = row.DentistId, AssistantId = row.AssistantId,
            Quantity = row.Quantity, AgreedUnitPriceVnd = unitPrice,
            EstimatedStart = row.Date, EstimatedMinutes = row.EstimatedMinutes,
            PlannedOrder = dossier.Items.Select(x => x.PlannedOrder).DefaultIfEmpty(0).Max() + 1 };
        if (row.DiscountValue > 0)
        {
            item.DiscountType = discountType; item.DiscountValue = row.DiscountValue; item.DiscountReason = Clean(row.DiscountNote);
        }
        dossier.Items.Add(item);
        db.TreatmentItems.Add(item);
        if (finalize)
        {
            var planVersion = targetPlanVersion ?? (await CurrentPlanVersionAsync(dossier.Id, false, ct)) + 1;
            FinalizeSheetItems(actorId, dossier, planVersion);
        }
        return item;
    }

    private void FinalizeSheetItems(int actorId, TreatmentDossier dossier, int planVersion)
    {
        dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
        Event(dossier, actorId, "PlanCommitted", new { planVersion,
            Items = dossier.Items.OrderBy(x => x.PlannedOrder).Select(ItemDto).ToArray() });
    }

    public Task<ImportItemPlanResult> ImportItemPlanAsync(int actorId, Guid dossierId, ImportItemPlanRequest request, CancellationToken ct) =>
        SheetCommandAsync(actorId, dossierId, request.RequestKey, "SheetImportItemPlan", new { dossierId, request }, async dossier =>
        {
            if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
            if (dossier.State != "Open") throw Error("MSG240", 409);
            var source = await db.TreatmentTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ItemPlanId && x.DeletedAt == null, ct) ?? throw Error("MSG113", 404);
            if (source.BranchId != dossier.BranchId) throw Error("MSG06", 403);
            if (source.Version != request.ExpectedItemPlanVersion) throw Error("MSG111", 409);
            var rows = await db.TreatmentTemplateItems.AsNoTracking().Where(x => x.TemplateId == source.Id && x.DeletedAt == null).OrderBy(x => x.Position).ThenBy(x => x.Id).ToListAsync(ct);
            if (rows.Count == 0) throw Error("MSG04");
            var created = new List<Guid>();
            var planVersion = (await CurrentPlanVersionAsync(dossier.Id, dossier.Items.Count > 0, ct)) + 1;
            foreach (var row in rows)
            {
                try {
                    var input = new SheetRow(row.Date, row.ServiceId, dossier.DentistId, JsonSerializer.Deserialize<string[]>(row.TeethJson), 1, row.CostVnd, row.Note, row.AssistantId,
                        null, "VND", 0, null, row.CostVnd, null, row.EstimatedMinutes);
                    // Reuse all sheet limits, not just the catalog's more permissive tooth limits.
                    input = ReadRow(JsonSerializer.SerializeToElement(input, Json));
                    var item = await AddSheetItemAsync(actorId, dossier, input, Guid.NewGuid(), ct, false, planVersion);
                    item.ServiceName = row.ServiceName;
                    created.Add(item.Id);
                } catch (BusinessException e) when (e.Status == 400) {
                    throw new BusinessException(400, new("ITEM_PLAN_IMPORT_INVALID", $"Hạng mục {row.Position} ({row.ServiceName}) không còn hợp lệ. Vui lòng sửa kế hoạch nguồn.") { Field = $"items.{row.Id}" });
                }
            }
            FinalizeSheetItems(actorId, dossier, planVersion);
            Event(dossier, actorId, "ItemPlanImported", new { source.Id, source.Version, snapshot = rows, mapping = rows.Select((x, i) => new { sourceItemId = x.Id, itemId = created[i] }).ToArray() });
            await db.SaveChangesAsync(ct);
            return new ImportItemPlanResult(await DetailAsync(dossier, ct), created.ToArray(), source.Id, source.Version);
        }, ct);

    public Task<TreatmentChange> ConfirmSheetPlanAsync(int actorId, Guid dossierId, Guid rowId,
        ConfirmSheetPlanRequest request, CancellationToken ct) => SheetCommandAsync(actorId, dossierId,
        request.RequestKey, "SheetPlanConfirm", new { dossierId, rowId, request }, async dossier =>
        {
            var draft = await SheetDraftAsync(actorId, dossierId, rowId, "Plan", request.ExpectedDraftVersion, request.ExpectedVersion, dossier, ct);
            await AddSheetItemAsync(actorId, dossier, ReadRow(JsonDocument.Parse(draft.PayloadJson).RootElement), rowId, ct);
            await db.RemoveTreatmentDataAsync(draft);
            await db.SaveChangesAsync(ct);
            return new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "kế hoạch điều trị", value: dossier.Code));
        }, ct);

    public Task<ConfirmSheetActualResult> ConfirmSheetActualAsync(int actorId, Guid dossierId, Guid rowId,
        ConfirmSheetActualRequest request, CancellationToken ct) => SheetCommandAsync(actorId, dossierId,
        request.RequestKey, "SheetActualConfirm", new { dossierId, rowId, request }, async dossier =>
        {
            var draft = await SheetDraftAsync(actorId, dossierId, rowId, "Actual", request.ExpectedDraftVersion, request.ExpectedVersion, dossier, ct);
            var row = ReadRow(JsonDocument.Parse(draft.PayloadJson).RootElement);
            TreatmentItem? directItem = null;
            if (row.SourceItemId.HasValue)
            {
                directItem = dossier.Items.SingleOrDefault(x => x.Id == row.SourceItemId) ?? throw Error("MSG113", 404);
                if (row.ServiceId != directItem.ServiceId || row.Quantity != directItem.Quantity
                    || row.CostVnd != Gross(directItem) || !TreatmentSheetRules.RegionWithinScope(directItem.Region, Clean(string.Join(", ", row.Regions ?? []))))
                    throw Error("MSG04");
            }
            var recorded = await RecordDossierActivityAsync(actorId, dossier, directItem, rowId,
                row.Date, row.Note, false, request.RequestKey, ct, row.LabWork, row);
            await db.RemoveTreatmentDataAsync(draft);
            dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
            Event(dossier, actorId, "DossierActualRecorded", new { ActivityId = recorded.Id, ItemId = directItem?.Id });
            await db.SaveChangesAsync(ct);
            return new ConfirmSheetActualResult(await DetailAsync(dossier, ct), recorded);
        }, ct);

    public async Task<TreatmentSheetContext> SheetContextAsync(int actorId, Guid dossierId, CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        var employee = await DentistAsync(actorId, dossier.BranchId, ct);
        var permissions = (await access.ResolveAsync(actorId, ct)).Permissions;
        var services = await db.Set<SchedulingService>().AsNoTracking().Where(x => x.BranchId == dossier.BranchId && x.Active)
            .OrderBy(x => x.Name).Select(x => new SheetService(x.Id, x.BranchId, x.Name, x.Unit, x.ReferenceUnitPriceVnd, x.PriceVersion)).ToListAsync(ct);
        var members = await (from member in db.SchedulingMembers.AsNoTracking()
            join staff in db.Employees on member.EmployeeId equals staff.Id
            where member.BranchId == dossier.BranchId && member.Active
            select new SheetMember(staff.Id, staff.FullName, member.CanDentist)).ToListAsync(ct);
        var visits = await db.Visits.AsNoTracking().Where(x => x.PatientId == dossier.PatientId && x.BranchId == dossier.BranchId
            && x.Status == "InProgress" && x.DentistId == employee)
            .OrderByDescending(x => x.ArrivalAt)
            .Select(x => new SheetVisit(x.Id, x.Version, x.ArrivalAt, x.DentistId)).ToListAsync(ct);
        var actions = new List<string>();
        if (dossier.State == "Open") {
            if (permissions.Any(x => x is "treatment.items.create" or "treatment.items.update" or "treatment.actuals.create" or "treatment.actuals.update" or "treatment.dossiers.update" or "treatment.plan.manage")) actions.Add("Draft");
            if (permissions.Contains("treatment.items.update")) actions.Add("Decide");
            if (permissions.Contains("treatment.actuals.create")) actions.Add("Perform");
            if (permissions.Contains("treatment.dossiers.update")) actions.Add("Close");
        } else if (dossier.State != "Cancelled" && permissions.Contains("treatment.dossiers.update")) actions.Add("Reopen");
        return new(services, members, visits, actions.ToArray());
    }

    public async Task<TreatmentActivityPage> ActivitiesAsync(int actorId, Guid dossierId, Guid? visitId,
        DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        if (page < 1 || page > int.MaxValue / 100 || pageSize is not (20 or 50 or 100) || from > to) throw Error("MSG04");
        var query = db.ClinicalActivities.AsNoTracking().Where(x => x.DossierId == dossierId);
        if (from.HasValue) { var start = new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime(); query = query.Where(x => x.PerformedAt >= start); }
        if (to.HasValue) { var end = new DateTimeOffset(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime(); query = query.Where(x => x.PerformedAt < end); }
        var overrides = await SheetActivityOverridesAsync(dossierId, ct);
        var deletedIds = overrides.Where(x => x.Value.SheetDeleted).Select(x => x.Key).ToArray();
        query = query.Where(x => !deletedIds.Contains(x.Id));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.PerformedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var names = await ActivitySourceNamesAsync(rows, dossier, ct);
        return new(rows.Select(x => overrides.GetValueOrDefault(x.Id) ?? ActivityDto.From(x) with { ServiceNameSnapshot = names.GetValueOrDefault(x.Id) }).ToArray(), total, page, pageSize);
    }

    // Resolve legacy display metadata from immutable versions without rewriting posted actuals.
    private async Task<Dictionary<Guid, string>> ActivitySourceNamesAsync(List<ClinicalActivity> rows, TreatmentDossier dossier, CancellationToken ct)
    {
        var names = rows.Where(x => x.ServiceNameSnapshot != null).ToDictionary(x => x.Id, x => x.ServiceNameSnapshot!);
        if (rows.All(x => names.ContainsKey(x.Id))) return names;
        var events = await db.TreatmentEvents.AsNoTracking()
            .Where(e => e.DossierId == dossier.Id && (e.Kind == "PlanCommitted" || e.Kind == "PlanRevised" || e.Kind == "SheetItemEdited"))
            .OrderBy(e => e.OccurredAt).ToListAsync(ct);
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

    public async Task<DossierTotals> SummaryAsync(int actorId, int patientId, int? branchId, CancellationToken ct)
    {
        // Reuse list authorization, then aggregate the entire authorized filter, not its first page.
        await ListAsync(actorId, patientId, branchId, 1, 20, ct);
        var employee = await db.Employees.AsNoTracking().SingleAsync(x => x.AccountId == actorId, ct);
        var branches = db.SchedulingMembers.Where(x => x.EmployeeId == employee.Id && x.Active && x.CanDentist).Select(x => x.BranchId);
        var dossiers = await db.TreatmentDossiers.AsNoTracking().Include(x => x.Items).Where(x => x.PatientId == patientId
            && x.State != "Deleted" && branches.Contains(x.BranchId) && (!branchId.HasValue || x.BranchId == branchId)).ToListAsync(ct);
        var gross = 0L; var reduction = 0L; var summaries = new List<TreatmentFinancialSummary>();
        foreach (var dossier in dossiers)
        {
            var preview = PricePreview(dossier, 0); gross = checked(gross + preview.GrossVnd); reduction = checked(reduction + preview.ApprovedDiscountVnd);
            var finance = await financialPort.ReadAsync(patientId, dossier.BranchId, dossier.Id, ct);
            if (finance?.Available == true) summaries.Add(finance);
        }
        var available = dossiers.Count > 0 && summaries.Count == dossiers.Count;
        var ids = dossiers.Select(x => x.Id).ToArray();
        var pending = await db.ClinicalActivities.CountAsync(x => ids.Contains(x.DossierId) && x.FeeStatus == "Pending", ct);
        return new(dossiers.Count, gross, reduction, gross - reduction, available,
            available && summaries.All(x => x.PostedChargeVnd.HasValue) ? summaries.Sum(x => x.PostedChargeVnd!.Value) : null,
            available && summaries.All(x => x.AllocatedVnd.HasValue) ? summaries.Sum(x => x.AllocatedVnd!.Value) : null,
            available && summaries.All(x => x.OutstandingVnd.HasValue) ? summaries.Sum(x => x.OutstandingVnd!.Value) : null,
            available && summaries.All(x => x.UnusedAdvanceVnd.HasValue) ? summaries.Sum(x => x.UnusedAdvanceVnd!.Value) : null, pending);
    }
}

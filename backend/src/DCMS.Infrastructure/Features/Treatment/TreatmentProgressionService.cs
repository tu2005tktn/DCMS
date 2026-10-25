using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed class TreatmentProgressionService(DcmsDbContext db, UserManager<ApplicationUser> users,
    ITreatmentActualEvidencePort actual, ITreatmentLabReadinessPort lab,
    ITreatmentVisitActivitySourcePort visitActivity, ITreatmentDentistCapabilityPort capabilities,
    TimeProvider clock)
    : ITreatmentProgressionService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static BusinessException Error(string code, int status = 400) => new(status, SystemMessages.Get(code));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Fingerprint(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static T[] Parse<T>(string json) => JsonSerializer.Deserialize<T[]>(json, Json) ?? [];
    private static string Serialize(object value) => JsonSerializer.Serialize(value, Json);

    private async Task DentistAsync(int actorId, int branchId, CancellationToken ct)
    {
        var account = await users.FindByIdAsync(actorId.ToString());
        if (account == null || !account.IsActive || account.AccountType != "Staff" ||
            !AccountRoles.Effective(await users.GetRolesAsync(account)).Contains(AccountRoles.Dentist))
            throw Error("MSG06", 403);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        if (employee == null || !await db.Set<SchedulingMember>().AsNoTracking().AnyAsync(x =>
                x.BranchId == branchId && x.EmployeeId == employee.Id && x.Active && x.CanDentist, ct))
            throw Error("MSG06", 403);
    }

    private async Task<TreatmentDossier> DossierAsync(int actorId, Guid id, CancellationToken ct)
    {
        var dossier = await db.TreatmentDossiers.Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);
        await DentistAsync(actorId, dossier.BranchId, ct);
        return dossier;
    }

    private async Task<T> CommandAsync<T>(int actorId, Guid key, string command, object payload,
        Func<Task<T>> mutate, CancellationToken ct)
    {
        if (key == Guid.Empty) throw Error("MSG04");
        // S22 can post an activity and confirm its step within its owning transaction.
        var ownsTransaction = db.Database.CurrentTransaction == null;
        await using var tx = ownsTransaction
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        // The shared lock also serializes Appointment's follow-up transitions with step commands.
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({DCMS.Infrastructure.Features.Scheduling.WorkingScheduleService.TransactionLock})", ct);
        var hash = Fingerprint(payload);
        var previous = await db.TreatmentProgressionReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (previous != null)
        {
            if (previous.ActorId != actorId || previous.Command != command || previous.Fingerprint != hash)
                throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<T>(previous.ResultJson, Json) ?? throw Error("MSG144", 503);
        }
        var result = await mutate();
        await db.SaveChangesAsync(ct);
        db.TreatmentProgressionReceipts.Add(new TreatmentProgressionReceipt
        {
            RequestKey = key, ActorId = actorId, Command = command, Fingerprint = hash,
            ResultJson = Serialize(result!)
        });
        await db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return result;
    }

    private void Event(Guid aggregateId, int actorId, string kind, object payload, string? stableKey = null) =>
        db.TreatmentProgressionEvents.Add(new TreatmentProgressionEvent
        {
            AggregateId = aggregateId, ActorId = actorId, Kind = kind,
            PayloadJson = Serialize(payload), StableKey = stableKey, OccurredAt = clock.GetUtcNow()
        });

    private static void ValidateSteps(StepDefinition[]? steps, bool requireItems)
    {
        if (steps is not { Length: >= 1 and <= 100 }) throw Error("MSG229");
        var ids = steps.Select(x => x.LogicalStepId).ToHashSet();
        if (ids.Count != steps.Length || ids.Contains(Guid.Empty)) throw Error("MSG229");
        foreach (var step in steps)
        {
            var hasOffsets = step.EarliestOffsetDays.HasValue || step.TargetOffsetDays.HasValue || step.LatestOffsetDays.HasValue;
            if (Clean(step.Name) is not { Length: <= 160 } || (requireItems && !step.ItemId.HasValue)
                || step.ItemId == Guid.Empty || Clean(step.Region)?.Length > 80
                || step.EstimatedMinutes is < 1 or > 480 || step.ReservedMinutes is < 15 or > 480
                || step.ReservedMinutes % 15 != 0 || step.ReservedMinutes < step.EstimatedMinutes
                || step.EstimatedMinMinutes.HasValue != step.EstimatedMaxMinutes.HasValue
                || (step.EstimatedMinMinutes.HasValue &&
                    (step.EstimatedMinMinutes < 1 || step.EstimatedMinMinutes > step.EstimatedMinutes
                    || step.EstimatedMaxMinutes < step.EstimatedMinutes || step.EstimatedMaxMinutes > 480))
                || Clean(step.Capability)?.Length > 160 || Clean(step.LabRequirement)?.Length > 160
                || step.ChargingCoverage is not ("IncludedInItem" or "FinancialReview")
                || step.PredecessorIds == null || step.PredecessorIds.Distinct().Count() != step.PredecessorIds.Length
                || step.PredecessorIds.Any(id => !ids.Contains(id) || id == step.LogicalStepId)
                || (hasOffsets && !(step.EarliestOffsetDays is >= 0 && step.TargetOffsetDays >= step.EarliestOffsetDays
                    && step.LatestOffsetDays >= step.TargetOffsetDays))) throw Error("MSG229");
        }
        var map = steps.ToDictionary(x => x.LogicalStepId);
        var visiting = new HashSet<Guid>();
        var visited = new HashSet<Guid>();
        bool Visit(Guid id)
        {
            if (visiting.Contains(id)) return false;
            if (visited.Contains(id)) return true;
            visiting.Add(id);
            foreach (var parent in map[id].PredecessorIds) if (!Visit(parent)) return false;
            visiting.Remove(id); visited.Add(id); return true;
        }
        if (ids.Any(id => !Visit(id))) throw Error("MSG229");
    }

    private static TreatmentTemplateDto TemplateDto(TreatmentTemplate model) => new(model.Id,
        model.BranchId, model.Name, model.Version, model.Versions.OrderBy(x => x.Number)
            .Select(x => new TreatmentTemplateVersionDto(x.Number, x.State, Parse<StepDefinition>(x.StepsJson),
                x.CreatedAt, x.ActivatedAt, x.RetiredAt)).ToArray());

    public async Task<IReadOnlyList<TemplateListItem>> ListTemplatesAsync(int actorId, int branchId, CancellationToken ct)
    {
        await DentistAsync(actorId, branchId, ct);
        return await db.TreatmentTemplates.AsNoTracking().Where(x => x.BranchId == branchId)
            .OrderBy(x => x.Name)
            .Select(x => new TemplateListItem(x.Id, x.BranchId, x.Name, x.Version,
                x.Versions.Where(v => v.State == "Active").Select(v => (int?)v.Number).FirstOrDefault(),
                x.CreatedAt)).ToListAsync(ct);
    }

    public async Task<TreatmentTemplateDto> GetTemplateAsync(int actorId, Guid id, CancellationToken ct)
    {
        var model = await db.TreatmentTemplates.Include(x => x.Versions).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);
        await DentistAsync(actorId, model.BranchId, ct);
        return TemplateDto(model);
    }

    public async Task<TreatmentTemplateDto> CreateTemplateAsync(int actorId, CreateTemplateRequest request, CancellationToken ct)
    {
        await DentistAsync(actorId, request.BranchId, ct);
        var name = Clean(request.Name);
        if (name is not { Length: <= 160 }) throw Error("MSG04");
        ValidateSteps(request.Steps, false);
        return await CommandAsync(actorId, request.RequestKey, "CreateTemplate", request, async () =>
        {
            var now = clock.GetUtcNow();
            var model = new TreatmentTemplate { BranchId = request.BranchId, Name = name,
                CreatedBy = actorId, CreatedAt = now };
            model.Versions.Add(new TreatmentTemplateVersion { TemplateId = model.Id, Number = 1,
                StepsJson = Serialize(request.Steps), CreatedBy = actorId, CreatedAt = now });
            db.TreatmentTemplates.Add(model);
            Event(model.Id, actorId, "template.created", new { version = 1 });
            await db.SaveChangesAsync(ct);
            return TemplateDto(model);
        }, ct);
    }

    public async Task<TreatmentTemplateDto> NewVersionAsync(int actorId, Guid id,
        NewTemplateVersionRequest request, CancellationToken ct)
    {
        ValidateSteps(request.Steps, false);
        return await CommandAsync(actorId, request.RequestKey, "NewTemplateVersion", new { id, request }, async () =>
        {
            var model = await db.TreatmentTemplates.Include(x => x.Versions).SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw Error("MSG113", 404);
            await DentistAsync(actorId, model.BranchId, ct);
            if (model.Version != request.ExpectedVersion) throw Error("MSG111", 409);
            if (model.Versions.Any(x => x.State == "Draft")) throw Error("MSG236", 409);
            var number = model.Versions.Max(x => x.Number) + 1;
            model.Version++;
            var draft = new TreatmentTemplateVersion { TemplateId = id, Number = number,
                StepsJson = Serialize(request.Steps), CreatedBy = actorId, CreatedAt = clock.GetUtcNow() };
            model.Versions.Add(draft);
            db.TreatmentTemplateVersions.Add(draft);
            Event(id, actorId, "template.version.created", new { number });
            await db.SaveChangesAsync(ct);
            return TemplateDto(model);
        }, ct);
    }

    public async Task<TreatmentTemplateDto> UpdateDraftAsync(int actorId, Guid id, int number,
        UpdateTemplateDraftRequest request, CancellationToken ct)
    {
        ValidateSteps(request.Steps, false);
        return await CommandAsync(actorId, request.RequestKey, "UpdateTemplateDraft", new { id, number, request }, async () =>
        {
            var model = await db.TreatmentTemplates.Include(x => x.Versions).SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw Error("MSG113", 404);
            await DentistAsync(actorId, model.BranchId, ct);
            if (model.Version != request.ExpectedVersion) throw Error("MSG111", 409);
            var version = model.Versions.SingleOrDefault(x => x.Number == number && x.State == "Draft")
                ?? throw Error("MSG236", 409);
            version.StepsJson = Serialize(request.Steps);
            model.Version++;
            Event(id, actorId, "template.draft.updated", new { number, request.Steps });
            await db.SaveChangesAsync(ct);
            return TemplateDto(model);
        }, ct);
    }

    public async Task<TreatmentTemplateDto> TemplateStateAsync(int actorId, Guid id, int number, string state,
        TemplateStateRequest request, CancellationToken ct)
    {
        if (state is not ("Active" or "Retired")) throw Error("MSG04");
        return await CommandAsync(actorId, request.RequestKey, "TemplateState", new { id, number, state, request }, async () =>
        {
            var model = await db.TreatmentTemplates.Include(x => x.Versions).SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw Error("MSG113", 404);
            await DentistAsync(actorId, model.BranchId, ct);
            if (model.Version != request.ExpectedVersion) throw Error("MSG111", 409);
            var version = model.Versions.SingleOrDefault(x => x.Number == number) ?? throw Error("MSG113", 404);
            if (state == "Active" && version.State != "Draft" || state == "Retired" && version.State != "Active")
                throw Error("MSG236", 409);
            var now = clock.GetUtcNow();
            if (state == "Active")
            {
                foreach (var active in model.Versions.Where(x => x.State == "Active"))
                    { active.State = "Retired"; active.RetiredAt = now; }
                version.ActivatedAt = now;
            }
            else version.RetiredAt = now;
            version.State = state;
            model.Version++;
            Event(id, actorId, state == "Active" ? "template.activated" : "template.retired", new { number });
            await db.SaveChangesAsync(ct);
            return TemplateDto(model);
        }, ct);
    }

    private async Task<StepDto[]> StepDtosAsync(TreatmentDossier dossier, CancellationToken ct)
    {
        var rows = await db.TreatmentSteps.Where(x => x.DossierId == dossier.Id)
            .OrderBy(x => x.Name).ToListAsync(ct);
        var byId = rows.ToDictionary(x => x.Id);
        var result = new List<StepDto>(rows.Count);
        foreach (var row in rows)
        {
            var blockers = new List<string>();
            if (row.HoldReason != null && row.HoldEffectiveAt <= clock.GetUtcNow())
                blockers.Add("Đang tạm dừng: " + row.HoldReason);
            foreach (var predecessorId in Parse<Guid>(row.PredecessorsJson))
                if (!byId.TryGetValue(predecessorId, out var predecessor) || predecessor.Outcome != "Completed")
                    blockers.Add("Bước tiền nhiệm chưa hoàn tất");
            if (row.LabRequirement != null)
            {
                var readiness = await lab.CheckAsync(dossier.PatientId, row.Id,
                    row.LabRequirement, row.PlanVersion, ct);
                if (!readiness.Available) blockers.Add("Chưa kết nối nguồn Labo");
                else if (!readiness.IsAccepted) blockers.Add("Chưa có Labo Accepted phù hợp");
            }
            if (row.Capability != null && !await capabilities.HasCapabilityAsync(dossier.BranchId,
                dossier.DentistId, row.Capability, ct))
                blockers.Add("Chưa xác minh năng lực bác sĩ phù hợp");
            if (dossier.Items.SingleOrDefault(x => x.Id == row.ItemId)?.Decision != "Agreed")
                blockers.Add("Hạng mục chưa được đồng ý");
            result.Add(new StepDto(row.Id, row.ItemId, row.Name, row.Region, row.PlanVersion,
                row.EstimatedMinutes, row.ReservedMinutes, Parse<Guid>(row.PredecessorsJson), row.Capability,
                row.LabRequirement, row.EarliestOffsetDays, row.TargetOffsetDays, row.LatestOffsetDays,
                row.ChargingCoverage, row.Outcome, blockers.Count == 0 ? "Ready" : "Blocked",
                blockers.Distinct().ToArray(), row.HoldReason, row.ReviewCondition, row.Version,
                row.EstimatedMinMinutes, row.EstimatedMaxMinutes,
                row.ShortReservationAcknowledgedBy, row.ShortReservationAcknowledgedAt));
        }
        return result.ToArray();
    }

    public async Task<IReadOnlyList<StepDto>> StepsAsync(int actorId, Guid dossierId, CancellationToken ct) =>
        await StepDtosAsync(await DossierAsync(actorId, dossierId, ct), ct);

    public async Task<StepChange> ApplyTemplateAsync(int actorId, Guid dossierId,
        ApplyTemplateRequest request, CancellationToken ct)
    {
        ValidateSteps(request.Steps, true);
        if (request.Steps.Any(x => x.EstimatedMaxMinutes > x.ReservedMinutes) &&
            !request.AcknowledgeShortReservation) throw Error("MSG229");
        return await CommandAsync(actorId, request.RequestKey, "ApplyTemplate", new { dossierId, request }, async () =>
        {
            var dossier = await DossierAsync(actorId, dossierId, ct);
            if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
            if (dossier.State != "Open" || dossier.PlanState == "Committed") throw Error("MSG240", 409);
            var template = await db.TreatmentTemplateVersions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.TemplateId == request.TemplateId && x.Number == request.TemplateVersion && x.State == "Active", ct)
                ?? throw Error("MSG236", 409);
            var source = Parse<StepDefinition>(template.StepsJson);
            var sourceIds = source.Select(x => x.LogicalStepId).Order().ToArray();
            if (!sourceIds.SequenceEqual(request.Steps.Select(x => x.LogicalStepId).Order())) throw Error("MSG236", 409);
            var owner = await db.TreatmentTemplates.AsNoTracking().SingleAsync(x => x.Id == request.TemplateId, ct);
            if (owner.BranchId != dossier.BranchId) throw Error("MSG06", 403);
            var ids = request.Steps.Select(x => x.ItemId!.Value).Distinct().ToArray();
            if (ids.Any(id => dossier.Items.All(x => x.Id != id || x.Decision != "Agreed"))) throw Error("MSG240", 409);
            if (await db.TreatmentSteps.AnyAsync(x => x.DossierId == dossierId, ct)) throw Error("MSG233", 409);
            // Template IDs identify the source graph. Patient steps get their own stable IDs.
            var patientIds = request.Steps.ToDictionary(x => x.LogicalStepId, _ => Guid.NewGuid());
            foreach (var definition in request.Steps)
                db.TreatmentSteps.Add(new TreatmentStep
                {
                    Id = patientIds[definition.LogicalStepId], DossierId = dossierId, PlanId = dossier.PlanId,
                    PlanVersion = dossier.PlanVersion + 1, ItemId = definition.ItemId!.Value,
                    Name = Clean(definition.Name)!, Region = Clean(definition.Region),
                    EstimatedMinutes = definition.EstimatedMinutes, ReservedMinutes = definition.ReservedMinutes,
                    EstimatedMinMinutes = definition.EstimatedMinMinutes,
                    EstimatedMaxMinutes = definition.EstimatedMaxMinutes,
                    ShortReservationAcknowledgedBy = definition.EstimatedMaxMinutes > definition.ReservedMinutes ? actorId : null,
                    ShortReservationAcknowledgedAt = definition.EstimatedMaxMinutes > definition.ReservedMinutes
                        ? clock.GetUtcNow() : null,
                    PredecessorsJson = Serialize(definition.PredecessorIds.Select(x => patientIds[x]).ToArray()),
                    Capability = Clean(definition.Capability),
                    LabRequirement = Clean(definition.LabRequirement),
                    EarliestOffsetDays = definition.EarliestOffsetDays, TargetOffsetDays = definition.TargetOffsetDays,
                    LatestOffsetDays = definition.LatestOffsetDays, ChargingCoverage = definition.ChargingCoverage
                });
            dossier.SourceTemplateId = request.TemplateId;
            dossier.SourceTemplateVersion = request.TemplateVersion;
            dossier.Version++;
            dossier.UpdatedAt = clock.GetUtcNow();
            Event(dossierId, actorId, "plan.template.applied", new { request.TemplateId, request.TemplateVersion,
                snapshot = source, patientSteps = request.Steps, patientIds });
            await db.SaveChangesAsync(ct);
            return new StepChange(await StepDtosAsync(dossier, ct), [], SystemMessages.Get("MSG222", "mẫu điều trị", value: owner.Name));
        }, ct);
    }

    public async Task<StepChange> AddPatientStepAsync(int actorId, Guid dossierId,
        AddPatientStepRequest request, CancellationToken ct)
    {
        if (Clean(request.Reason) is not { Length: <= 1000 }) throw Error("MSG04");
        var definition = new StepDefinition(Guid.NewGuid(), request.ItemId, request.Name, request.Region,
            request.EstimatedMinutes, request.ReservedMinutes, [], request.Capability,
            request.LabRequirement, request.EarliestOffsetDays, request.TargetOffsetDays,
            request.LatestOffsetDays, request.ChargingCoverage, request.EstimatedMinMinutes,
            request.EstimatedMaxMinutes);
        ValidateSteps([definition], true);
        if (request.EstimatedMaxMinutes > request.ReservedMinutes && !request.AcknowledgeShortReservation)
            throw Error("MSG229");
        if (request.PredecessorIds == null || request.PredecessorIds.Length != request.PredecessorIds.Distinct().Count()
            || request.PredecessorIds.Contains(Guid.Empty)) throw Error("MSG229");
        return await CommandAsync(actorId, request.RequestKey, "AddPatientStep",
            new { dossierId, request }, async () =>
        {
            var dossier = await DossierAsync(actorId, dossierId, ct);
            if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
            if (dossier.State != "Open" || dossier.PlanState != "Committed"
                || dossier.Items.All(x => x.Id != request.ItemId || x.Decision != "Agreed"))
                throw Error("MSG240", 409);
            var existing = await db.TreatmentSteps.Where(x => x.DossierId == dossierId &&
                x.PlanVersion == dossier.PlanVersion).ToListAsync(ct);
            if (request.PredecessorIds.Any(id => existing.All(x => x.Id != id))) throw Error("MSG229");
            var now = clock.GetUtcNow();
            var step = new TreatmentStep
            {
                DossierId = dossierId, PlanId = dossier.PlanId, PlanVersion = dossier.PlanVersion,
                ItemId = request.ItemId, Name = Clean(request.Name)!, Region = Clean(request.Region),
                EstimatedMinutes = request.EstimatedMinutes, ReservedMinutes = request.ReservedMinutes,
                EstimatedMinMinutes = request.EstimatedMinMinutes,
                EstimatedMaxMinutes = request.EstimatedMaxMinutes,
                ShortReservationAcknowledgedBy = request.EstimatedMaxMinutes > request.ReservedMinutes ? actorId : null,
                ShortReservationAcknowledgedAt = request.EstimatedMaxMinutes > request.ReservedMinutes ? now : null,
                PredecessorsJson = Serialize(request.PredecessorIds), Capability = Clean(request.Capability),
                LabRequirement = Clean(request.LabRequirement),
                EarliestOffsetDays = request.EarliestOffsetDays,
                TargetOffsetDays = request.TargetOffsetDays, LatestOffsetDays = request.LatestOffsetDays,
                ChargingCoverage = request.ChargingCoverage
            };
            db.TreatmentSteps.Add(step);
            dossier.Version++; dossier.UpdatedAt = now;
            Event(dossierId, actorId, "step.added", new { step.Id, step.ItemId, request.Reason,
                step.PlanVersion, step.EstimatedMinMinutes, step.EstimatedMaxMinutes,
                step.EstimatedMinutes, step.ReservedMinutes, request.PredecessorIds });
            await db.SaveChangesAsync(ct);
            return new StepChange(await StepDtosAsync(dossier, ct), [],
                SystemMessages.Get("MSG222", "bước", value: step.Name));
        }, ct);
    }

    private async Task<(TreatmentDossier Dossier, TreatmentStep Step)> ActiveStepAsync(int actorId,
        Guid dossierId, Guid stepId, int expectedVersion, int expectedStepVersion, CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        if (dossier.Version != expectedVersion) throw Error("MSG111", 409);
        if (dossier.State != "Open" || dossier.PlanState != "Committed") throw Error("MSG240", 409);
        var step = await db.TreatmentSteps.SingleOrDefaultAsync(x => x.Id == stepId && x.DossierId == dossierId, ct)
            ?? throw Error("MSG113", 404);
        if (step.Version != expectedStepVersion || step.PlanVersion != dossier.PlanVersion) throw Error("MSG111", 409);
        if (dossier.Items.SingleOrDefault(x => x.Id == step.ItemId)?.Decision != "Agreed") throw Error("MSG240", 409);
        return (dossier, step);
    }

    public async Task<StepChange> ProgressAsync(int actorId, Guid dossierId, Guid stepId,
        StepProgressRequest request, CancellationToken ct)
    {
        if (request.Outcome is not ("Partial" or "Repeat" or "Deferred" or "AddVisit") ||
            Clean(request.Reason) is not { Length: <= 1000 })
            throw Error("MSG04");
        return await CommandAsync(actorId, request.RequestKey, "StepProgress", new { dossierId, stepId, request }, async () =>
        {
            var (dossier, step) = await ActiveStepAsync(actorId, dossierId, stepId,
                request.ExpectedVersion, request.ExpectedStepVersion, ct);
            if (step.Outcome == "Completed") throw Error("MSG230", 409);
            // Deferring or requesting another visit is an assessment, not a reversal of work already recorded.
            if (request.Outcome is "Partial" or "Repeat") step.Outcome = "Partial";
            step.Version++; dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
            Event(dossierId, actorId, "step.progress", new { stepId, request.Outcome, request.Reason });
            await db.SaveChangesAsync(ct);
            return new StepChange(await StepDtosAsync(dossier, ct), [], SystemMessages.Get("MSG222", "tiến độ", value: step.Name));
        }, ct);
    }

    public Task<StepChange> HoldAsync(int actorId, Guid dossierId, Guid stepId,
        HoldStepRequest request, CancellationToken ct) => HoldChangeAsync(actorId, dossierId, stepId, request, false, ct);
    public Task<StepChange> ReleaseHoldAsync(int actorId, Guid dossierId, Guid stepId,
        HoldStepRequest request, CancellationToken ct) => HoldChangeAsync(actorId, dossierId, stepId, request, true, ct);

    private async Task<StepChange> HoldChangeAsync(int actorId, Guid dossierId, Guid stepId,
        HoldStepRequest request, bool release, CancellationToken ct)
    {
        if (Clean(request.Reason) is not { Length: <= 1000 } ||
            Clean(request.ReviewCondition) is not { Length: <= 1000 } || request.EffectiveAt == default)
            throw Error("MSG04");
        return await CommandAsync(actorId, request.RequestKey, release ? "ReleaseHold" : "Hold",
            new { dossierId, stepId, request }, async () =>
        {
            var (dossier, step) = await ActiveStepAsync(actorId, dossierId, stepId,
                request.ExpectedVersion, request.ExpectedStepVersion, ct);
            if (step.Outcome == "Completed" || release && step.HoldReason == null || !release && step.HoldReason != null)
                throw Error("MSG230", 409);
            step.HoldReason = release ? null : Clean(request.Reason);
            step.HoldEffectiveAt = release ? null : request.EffectiveAt.ToUniversalTime();
            step.ReviewCondition = release ? null : Clean(request.ReviewCondition);
            step.Version++; dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
            Event(dossierId, actorId, release ? "step.hold.released" : "step.held",
                new { stepId, request.Reason, request.EffectiveAt, request.ReviewCondition });
            var booked = await db.FollowUpInstructions.AsNoTracking().AnyAsync(x => x.StepId == stepId
                && x.Status == "Booked", ct);
            await db.SaveChangesAsync(ct);
            return new StepChange(await StepDtosAsync(dossier, ct), [],
                SystemMessages.Get(booked ? "MSG234" : "MSG241"));
        }, ct);
    }

    public async Task<StepChange> CompleteStepAsync(int actorId, Guid dossierId, Guid stepId,
        CompleteStepRequest request, CancellationToken ct)
    {
        if (request.ActualEvidenceIds is not { Length: >= 1 and <= 100 } ||
            request.ActualEvidenceIds.Any(x => x == Guid.Empty)) throw Error("MSG230", 409);
        return await CommandAsync(actorId, request.RequestKey, "CompleteStep", new { dossierId, stepId, request }, async () =>
        {
            var (dossier, step) = await ActiveStepAsync(actorId, dossierId, stepId,
                request.ExpectedVersion, request.ExpectedStepVersion, ct);
            var readiness = (await StepDtosAsync(dossier, ct)).Single(x => x.Id == stepId);
            if (readiness.Readiness != "Ready" || step.Outcome == "Completed" ||
                !await actual.HasDentistConfirmedActualAsync(dossier.PatientId, dossierId, stepId,
                    request.ActualEvidenceIds, ct)) throw Error("MSG230", 409);
            var now = clock.GetUtcNow();
            var completion = Guid.NewGuid();
            step.Outcome = "Completed"; step.CompletionEventId = completion; step.CompletedAt = now; step.Version++;
            dossier.Version++; dossier.UpdatedAt = now;
            var sameItem = await db.TreatmentSteps.Where(x => x.DossierId == dossierId && x.ItemId == step.ItemId).ToListAsync(ct);
            if (sameItem.All(x => x.Outcome == "Completed"))
                dossier.Items.Single(x => x.Id == step.ItemId).Progress = "Completed";
            var fulfilled = await db.FollowUpInstructions.Where(x => x.StepId == stepId &&
                (x.Status == "NeedsBooking" || x.Status == "Booked")).ToListAsync(ct);
            foreach (var instruction in fulfilled)
            {
                instruction.Status = "Completed"; instruction.Version++; instruction.UpdatedAt = now;
                Event(instruction.Id, actorId, "followup.completed", new { instruction.Id, stepId, completion });
            }
            Event(dossierId, actorId, "step.completed", new { stepId, completion,
                request.ActualEvidenceIds }, $"step-complete:{stepId}:{completion}");
            await db.SaveChangesAsync(ct);
            var rows = await db.TreatmentSteps.Where(x => x.DossierId == dossierId).ToListAsync(ct);
            var generated = new List<Guid>();
            foreach (var successor in rows.Where(x => x.Outcome != "Completed"))
            {
                var parents = Parse<Guid>(successor.PredecessorsJson);
                if (!parents.Contains(stepId) || parents.Any(id => rows.All(x => x.Id != id || x.Outcome != "Completed"))) continue;
                if (!successor.EarliestOffsetDays.HasValue || !successor.TargetOffsetDays.HasValue
                    || !successor.LatestOffsetDays.HasValue) continue;
                if (await db.FollowUpInstructions.AnyAsync(x => x.StepId == successor.Id &&
                    x.Status != "Cancelled" && x.Status != "Superseded", ct)) continue;
                var anchor = rows.Where(x => parents.Contains(x.Id)).Max(x => x.CompletedAt)!.Value
                    .ToOffset(TimeSpan.FromHours(7));
                var day = DateOnly.FromDateTime(anchor.DateTime);
                var instruction = new FollowUpInstruction
                {
                    PatientId = dossier.PatientId, BranchId = dossier.BranchId, DossierId = dossierId,
                    ItemId = successor.ItemId, StepId = successor.Id, PlanVersion = dossier.PlanVersion,
                    SourceCompletionEventId = completion, Reason = $"Tiếp tục bước {successor.Name}",
                    EarliestOn = day.AddDays(successor.EarliestOffsetDays.Value),
                    TargetOn = day.AddDays(successor.TargetOffsetDays.Value),
                    LatestOn = day.AddDays(successor.LatestOffsetDays.Value),
                    DurationMinutes = successor.ReservedMinutes, DentistId = dossier.DentistId,
                    Capability = successor.Capability, CreatedBy = actorId, CreatedAt = now, UpdatedAt = now
                };
                db.FollowUpInstructions.Add(instruction); generated.Add(instruction.Id);
                Event(instruction.Id, actorId, "followup.created", new { instructionId = instruction.Id, successorId = successor.Id },
                    $"followup:{successor.Id}:{completion}");
            }
            await db.SaveChangesAsync(ct);
            return new StepChange(await StepDtosAsync(dossier, ct), generated.ToArray(),
                SystemMessages.Get(generated.Count > 0 ? "MSG231" : "MSG222", "bước", value: step.Name));
        }, ct);
    }

    private FollowUpDto FollowUpDto(FollowUpInstruction x)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        var actions = x.Status switch
        {
            "NeedsBooking" => new[] { "reviseWindow", "cancel", "supersede", "findSlots" },
            "Booked" => new[] { "reviseWindow", "complete", "cancel", "supersede" },
            _ => []
        };
        return new FollowUpDto(x.Id, x.PatientId, x.BranchId,
            new FollowUpSource(x.DossierId, x.ItemId, x.StepId, x.SourceVisitId, x.SourceActivityId),
            x.Purpose, x.Reason, x.Guidance, x.EarliestOn, x.TargetOn, x.LatestOn,
            x.DurationMinutes, x.DentistId, x.Capability, x.Status,
            x.Status is "NeedsBooking" or "Booked" && today > x.LatestOn,
            x.ActiveAppointmentId, x.Version, actions);
    }

    private static void ValidateWindow(DateOnly earliest, DateOnly target, DateOnly latest, int duration)
    {
        if (earliest == default || target == default || latest == default || earliest > target || target > latest
            || duration is < 15 or > 480 || duration % 15 != 0) throw Error("MSG229");
    }

    public async Task<IReadOnlyList<FollowUpDto>> ListFollowUpsAsync(int actorId, int patientId, CancellationToken ct)
    {
        var account = await users.FindByIdAsync(actorId.ToString());
        if (account == null || account.AccountType != "Staff" || !account.IsActive ||
            !AccountRoles.Effective(await users.GetRolesAsync(account)).Contains(AccountRoles.Dentist))
            throw Error("MSG06", 403);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        if (employee == null) throw Error("MSG06", 403);
        var branches = db.Set<SchedulingMember>().AsNoTracking().Where(x => x.EmployeeId == employee.Id
            && x.Active && x.CanDentist).Select(x => x.BranchId);
        var rows = await db.FollowUpInstructions.AsNoTracking().Where(x => x.PatientId == patientId
            && branches.Contains(x.BranchId)).OrderBy(x => x.TargetOn).ThenBy(x => x.Id).ToListAsync(ct);
        return rows.Select(FollowUpDto).ToArray();
    }

    public async Task<FollowUpDto> GetFollowUpAsync(int actorId, Guid id, CancellationToken ct)
    {
        var row = await db.FollowUpInstructions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw Error("MSG113", 404);
        await DentistAsync(actorId, row.BranchId, ct);
        return FollowUpDto(row);
    }

    public async Task<FollowUpChange> CreateFollowUpAsync(int actorId, CreateFollowUpRequest request,
        CancellationToken ct)
    {
        ValidateWindow(request.EarliestOn, request.TargetOn, request.LatestOn, request.DurationMinutes);
        if (request.Source == null || Clean(request.Reason) is not { Length: <= 2000 } ||
            Clean(request.Guidance)?.Length > 4000 || Clean(request.Capability)?.Length > 160 ||
            request.Purpose is not ("TreatmentContinuation" or "PostoperativeReview" or "WarrantyReview"
                or "PeriodicRecall" or "NewProblem")) throw Error("MSG04");
        var hasDossierSource = request.Source.DossierId.HasValue && request.Source.ItemId.HasValue
            && !request.Source.VisitId.HasValue
            && !request.Source.ActivityId.HasValue;
        var hasVisitSource = !request.Source.DossierId.HasValue && !request.Source.ItemId.HasValue
            && !request.Source.StepId.HasValue && request.Source.VisitId.HasValue
            && request.Source.ActivityId.HasValue;
        if (!hasDossierSource && !hasVisitSource) throw Error("MSG04");
        return await CommandAsync(actorId, request.RequestKey, "CreateFollowUp", request, async () =>
        {
            await DentistAsync(actorId, request.BranchId, ct);
            if (!await db.Patients.AsNoTracking().AnyAsync(x => x.Id == request.PatientId && !x.IsArchived, ct))
                throw Error("MSG113", 404);
            var source = request.Source;
            if (source.DossierId.HasValue)
            {
                var dossier = await DossierAsync(actorId, source.DossierId.Value, ct);
                if (dossier.PatientId != request.PatientId || dossier.BranchId != request.BranchId ||
                    dossier.State != "Open" || dossier.PlanState != "Committed" || source.ItemId is not Guid itemId ||
                    dossier.Items.All(x => x.Id != itemId || x.Decision != "Agreed")) throw Error("MSG06", 403);
                if (source.StepId.HasValue)
                {
                    var step = await db.TreatmentSteps.AsNoTracking().SingleOrDefaultAsync(x =>
                        x.Id == source.StepId && x.DossierId == dossier.Id && x.ItemId == itemId, ct)
                        ?? throw Error("MSG113", 404);
                    if (step.PlanVersion != dossier.PlanVersion ||
                        await db.FollowUpInstructions.AnyAsync(x => x.StepId == step.Id &&
                            (x.Status == "NeedsBooking" || x.Status == "Booked" ||
                             x.ActiveAppointmentId.HasValue && db.Appointments.Any(a =>
                                 a.Id == x.ActiveAppointmentId &&
                                 (a.Status == "Confirmed" || a.Status == "CheckedIn" || a.Status == "InProgress"))), ct))
                        throw Error("MSG233", 409);
                }
                else if (await db.FollowUpInstructions.AnyAsync(x => x.DossierId == dossier.Id &&
                    x.ItemId == itemId && x.StepId == null &&
                    (x.Status == "NeedsBooking" || x.Status == "Booked"), ct))
                    throw Error("MSG233", 409);
            }
            else
            {
                var verified = await visitActivity.VerifyAsync(request.PatientId, request.BranchId,
                    source.VisitId!.Value, source.ActivityId!.Value, ct);
                if (!verified.Available) throw Error("MSG144", 503);
                if (!verified.Matches) throw Error("MSG06", 403);
            }
            if (request.DentistId.HasValue && !await db.Set<SchedulingMember>().AsNoTracking().AnyAsync(x =>
                    x.BranchId == request.BranchId && x.EmployeeId == request.DentistId && x.Active && x.CanDentist, ct))
                throw Error("MSG06", 403);
            var now = clock.GetUtcNow();
            var row = new FollowUpInstruction
            {
                PatientId = request.PatientId, BranchId = request.BranchId, DossierId = source.DossierId,
                ItemId = source.ItemId, StepId = source.StepId, SourceVisitId = source.VisitId,
                SourceActivityId = source.ActivityId, Purpose = request.Purpose,
                PlanVersion = source.DossierId.HasValue
                    ? await db.TreatmentDossiers.Where(x => x.Id == source.DossierId).Select(x => (int?)x.PlanVersion).SingleAsync(ct)
                    : null,
                Reason = Clean(request.Reason)!, Guidance = Clean(request.Guidance),
                EarliestOn = request.EarliestOn, TargetOn = request.TargetOn, LatestOn = request.LatestOn,
                DurationMinutes = request.DurationMinutes, DentistId = request.DentistId,
                Capability = Clean(request.Capability), CreatedBy = actorId, CreatedAt = now, UpdatedAt = now
            };
            db.FollowUpInstructions.Add(row);
            Event(row.Id, actorId, "followup.created", new { row.Id, row.PatientId, row.EarliestOn,
                row.TargetOn, row.LatestOn });
            await db.SaveChangesAsync(ct);
            return new FollowUpChange(FollowUpDto(row), SystemMessages.Get("MSG231"));
        }, ct);
    }

    public async Task<FollowUpChange> ReviseFollowUpAsync(int actorId, Guid id,
        ReviseFollowUpRequest request, CancellationToken ct)
    {
        if (Clean(request.Reason) is not { Length: <= 1000 }) throw Error("MSG04");
        return await CommandAsync(actorId, request.RequestKey, "ReviseFollowUp", new { id, request }, async () =>
        {
            var row = await db.FollowUpInstructions.SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw Error("MSG113", 404);
            await DentistAsync(actorId, row.BranchId, ct);
            if (row.Version != request.ExpectedVersion) throw Error("MSG111", 409);
            if (row.Status is "Cancelled" or "Superseded" or "Completed") throw Error("MSG230", 409);
            ValidateWindow(request.EarliestOn, request.TargetOn, request.LatestOn, row.DurationMinutes);
            if (row.ActiveAppointmentId.HasValue) throw Error("MSG233", 409);
            var old = new { row.EarliestOn, row.TargetOn, row.LatestOn };
            row.EarliestOn = request.EarliestOn; row.TargetOn = request.TargetOn;
            row.LatestOn = request.LatestOn; row.Version++; row.UpdatedAt = clock.GetUtcNow();
            Event(id, actorId, "followup.window.revised", new { old, request.EarliestOn,
                request.TargetOn, request.LatestOn, request.Reason });
            await db.SaveChangesAsync(ct);
            return new FollowUpChange(FollowUpDto(row), SystemMessages.Get("MSG231"));
        }, ct);
    }

    public async Task<FollowUpChange> ChangeFollowUpAsync(int actorId, Guid id, string action,
        FollowUpActionRequest request, CancellationToken ct)
    {
        if (action is not ("complete" or "cancel" or "supersede") ||
            Clean(request.Reason) is not { Length: <= 1000 }) throw Error("MSG04");
        return await CommandAsync(actorId, request.RequestKey, "FollowUpAction", new { id, action, request }, async () =>
        {
            var row = await db.FollowUpInstructions.SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw Error("MSG113", 404);
            await DentistAsync(actorId, row.BranchId, ct);
            if (row.Version != request.ExpectedVersion) throw Error("MSG111", 409);
            if (row.Status is "Cancelled" or "Superseded" or "Completed") throw Error("MSG230", 409);
            if (action == "complete")
            {
                if (!await actual.HasFollowUpFulfillmentAsync(row.PatientId, row.Id, ct))
                    throw Error("MSG230", 409);
                row.Status = "Completed";
            }
            else
            {
                if (row.ActiveAppointmentId.HasValue) throw Error("MSG233", 409);
                row.Status = action == "cancel" ? "Cancelled" : "Superseded";
            }
            row.Version++; row.UpdatedAt = clock.GetUtcNow();
            Event(id, actorId, "followup." + action, new { request.Reason });
            await db.SaveChangesAsync(ct);
            return new FollowUpChange(FollowUpDto(row), SystemMessages.Get("MSG231"));
        }, ct);
    }
}

public sealed class UnavailableTreatmentActualEvidencePort : ITreatmentActualEvidencePort
{
    public Task<bool> HasDentistConfirmedActualAsync(int patientId, Guid dossierId, Guid stepId,
        IReadOnlyList<Guid> evidenceIds, CancellationToken ct) => Task.FromResult(false);
    public Task<bool> HasFollowUpFulfillmentAsync(int patientId, Guid instructionId, CancellationToken ct) =>
        Task.FromResult(false);
}

public sealed class UnavailableTreatmentLabReadinessPort : ITreatmentLabReadinessPort
{
    public Task<LabReadinessResult> CheckAsync(int patientId, Guid stepId, string specification,
        int planVersion, CancellationToken ct) => Task.FromResult(new LabReadinessResult(false, false, null));
}

public sealed class UnavailableTreatmentVisitActivitySourcePort : ITreatmentVisitActivitySourcePort
{
    public Task<VisitActivitySourceResult> VerifyAsync(int patientId, int branchId, Guid visitId,
        Guid activityId, CancellationToken ct) => Task.FromResult(new VisitActivitySourceResult(false, false));
}

public sealed class UnavailableTreatmentDentistCapabilityPort : ITreatmentDentistCapabilityPort
{
    public Task<bool> HasCapabilityAsync(int branchId, int dentistId, string capability,
        CancellationToken ct) => Task.FromResult(false);
}

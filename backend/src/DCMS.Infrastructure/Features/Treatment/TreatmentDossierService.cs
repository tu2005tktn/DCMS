using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed class TreatmentDossierService(DcmsDbContext db, UserManager<ApplicationUser> users,
    TimeProvider clock) : ITreatmentDossierService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static BusinessException Error(string code, int status = 400) =>
        new(status, SystemMessages.Get(code));
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<int> DentistAsync(int actorId, int branchId, CancellationToken ct)
    {
        var account = await users.FindByIdAsync(actorId.ToString());
        if (account == null || !account.IsActive || account.AccountType != "Staff") throw Error("MSG06", 403);
        var roles = AccountRoles.Effective(await users.GetRolesAsync(account));
        if (!roles.Contains(AccountRoles.Dentist)) throw Error("MSG06", 403);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        if (employee == null || !await db.Set<SchedulingMember>().AsNoTracking().AnyAsync(x =>
                x.BranchId == branchId && x.EmployeeId == employee.Id && x.Active && x.CanDentist, ct))
            throw Error("MSG06", 403);
        return employee.Id;
    }

    private async Task<TreatmentDossier> DossierAsync(int actorId, Guid id, CancellationToken ct)
    {
        var dossier = await db.TreatmentDossiers.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw Error("MSG113", 404);
        await DentistAsync(actorId, dossier.BranchId, ct);
        return dossier;
    }

    private async Task<TreatmentChange?> ReplayAsync(int actorId, Guid key, string command, object request, CancellationToken ct)
    {
        if (key == Guid.Empty) throw Error("MSG04");
        var receipt = await db.TreatmentReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (receipt == null) return null;
        if (receipt.ActorId != actorId || receipt.Command != command || receipt.Fingerprint != Hash(request))
            throw Error("MSG111", 409);
        return JsonSerializer.Deserialize<TreatmentChange>(receipt.ResultJson, Json) ?? throw Error("MSG144", 503);
    }

    private void StoreReceipt(int actorId, Guid key, string command, object request, TreatmentChange result)
    {
        db.TreatmentReceipts.Add(new TreatmentReceipt
        {
            RequestKey = key, ActorId = actorId, Command = command, Fingerprint = Hash(request),
            DossierId = result.Dossier.Id, ResultJson = JsonSerializer.Serialize(result, Json)
        });
    }

    private void Event(TreatmentDossier dossier, int actorId, string kind, object payload)
    {
        db.TreatmentEvents.Add(new TreatmentEvent
        {
            DossierId = dossier.Id, ActorId = actorId, Kind = kind, DossierVersion = dossier.Version,
            OccurredAt = clock.GetUtcNow(), PayloadJson = JsonSerializer.Serialize(payload, Json)
        });
    }

    private async Task<TreatmentDossierDto> DetailAsync(TreatmentDossier dossier, CancellationToken ct)
    {
        var events = await db.TreatmentEvents.AsNoTracking().Where(x => x.DossierId == dossier.Id)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToListAsync(ct);
        var terms = JsonSerializer.Deserialize<PaymentTermDto[]>(dossier.PaymentTermsJson, Json) ?? [];
        return new(dossier.Id, dossier.Code, dossier.PatientId, dossier.BranchId, dossier.DentistId,
            dossier.StartedOn, dossier.Diagnosis, dossier.Note, dossier.State, dossier.ClosureOutcome,
            dossier.ClosureReason, dossier.Version, dossier.PlanId, dossier.PlanVersion, dossier.PlanState,
            dossier.Goals, dossier.Items.OrderBy(x => x.PlannedOrder).Select(ItemDto).ToArray(), terms,
            events.Select(x => new TreatmentEventDto(x.Id, x.Kind, x.DossierVersion, x.ActorId,
                x.OccurredAt, x.PayloadJson)).ToArray(), false, dossier.CreatedAt, dossier.UpdatedAt);
    }

    private static TreatmentItemDto ItemDto(TreatmentItem x) => new(x.Id, x.SourceDiagnosisId, x.ServiceId,
        x.ServiceName, x.Region, x.Unit, x.Quantity, x.PlannedOrder, x.EstimatedMinutes, x.EstimatedStart,
        x.EstimatedEnd, x.AgreedUnitPriceVnd, x.Decision, x.Progress, x.Counseling, x.DecisionMaker,
        x.RepresentativeBasis, x.DecidedAt, x.ConsentDocumentId, x.ConsentPlanVersion,
        x.WarrantyPolicy, x.WarrantyPolicyVersion, x.WarrantyScope, x.WarrantyConfirmedAt, x.WarrantyEndsOn,
        x.Material, x.DecisionPlanVersion);

    public async Task<DossierPage> ListAsync(int actorId, int patientId, int? branchId, int page, int pageSize,
        CancellationToken ct)
    {
        if (page < 1 || pageSize is not (20 or 50 or 100) || page > int.MaxValue / pageSize) throw Error("MSG04");
        if (!await db.Patients.AsNoTracking().AnyAsync(x => x.Id == patientId, ct)) throw Error("MSG113", 404);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        if (employee == null) throw Error("MSG06", 403);
        if (branchId.HasValue) await DentistAsync(actorId, branchId.Value, ct);
        else
        {
            var account = await users.FindByIdAsync(actorId.ToString());
            if (account == null || account.AccountType != "Staff" || !account.IsActive ||
                !AccountRoles.Effective(await users.GetRolesAsync(account)).Contains(AccountRoles.Dentist))
                throw Error("MSG06", 403);
        }
        var allowed = db.Set<SchedulingMember>().AsNoTracking().Where(x =>
            x.EmployeeId == employee.Id && x.Active && x.CanDentist).Select(x => x.BranchId);
        var query = db.TreatmentDossiers.AsNoTracking().Where(x => x.PatientId == patientId && allowed.Contains(x.BranchId));
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId.Value);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(x => new DossierListItem(x.Id, x.Code, x.PatientId, x.BranchId,
            x.DentistId, x.Diagnosis, x.State, x.ClosureOutcome, x.Version, x.PlanVersion, x.UpdatedAt))
            .ToArray(), total, page, pageSize);
    }

    public async Task<TreatmentDossierDto> GetAsync(int actorId, Guid dossierId, CancellationToken ct) =>
        await DetailAsync(await DossierAsync(actorId, dossierId, ct), ct);

    public async Task<TreatmentChange> CreateAsync(int actorId, int patientId, CreateDossierRequest request,
        CancellationToken ct)
    {
        await DentistAsync(actorId, request.BranchId, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "Create", new { patientId, request }, ct);
        if (replay != null) return replay;
        var diagnosis = Clean(request.Diagnosis);
        if (diagnosis == null || diagnosis.Length > 4000 || Clean(request.Note)?.Length > 4000) throw Error("MSG04");
        if (!await db.Patients.AsNoTracking().AnyAsync(x => x.Id == patientId && !x.IsArchived, ct))
            throw Error("MSG113", 404);
        if (!await db.Set<SchedulingMember>().AsNoTracking().AnyAsync(x => x.BranchId == request.BranchId
                && x.EmployeeId == request.DentistId && x.Active && x.CanDentist, ct)) throw Error("MSG06", 403);
        var now = clock.GetUtcNow();
        var dossier = new TreatmentDossier
        {
            Code = "HS" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            PatientId = patientId, BranchId = request.BranchId, DentistId = request.DentistId,
            Diagnosis = diagnosis, Note = Clean(request.Note),
            StartedOn = request.StartedOn ?? DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime),
            CreatedAt = now, UpdatedAt = now
        };
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        db.TreatmentDossiers.Add(dossier);
        Event(dossier, actorId, "DossierCreated", new { dossier.Code, dossier.Diagnosis });
        await db.SaveChangesAsync(ct);
        var result = new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "hồ sơ điều trị", value: dossier.Code));
        StoreReceipt(actorId, request.RequestKey, "Create", new { patientId, request }, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<TreatmentChange> SaveDraftAsync(int actorId, Guid dossierId, SavePlanDraftRequest request,
        CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "Draft", new { dossierId, request }, ct);
        if (replay != null) return replay;
        if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
        if (dossier.State != "Open" || dossier.PlanState == "Committed") throw Error("MSG240", 409);
        var goals = Clean(request.Goals);
        if (goals == null || goals.Length > 4000 || request.Items is not { Length: >= 1 and <= 100 }
            || request.PaymentTerms == null) throw Error("MSG04");
        if (request.Items.Any(x => x.Id == Guid.Empty) || request.Items.Where(x => x.Id.HasValue).Select(x => x.Id).Distinct().Count()
            != request.Items.Count(x => x.Id.HasValue) || request.Items.Select(x => x.PlannedOrder).Distinct().Count() != request.Items.Length)
            throw Error("MSG04");
        var terms = request.PaymentTerms.Select(x => new PaymentTermDto(Clean(x.Purpose) ?? "", x.AmountVnd,
            x.DueOn, Clean(x.Evidence))).ToArray();
        if (terms.Any(x => x.Purpose.Length is < 1 or > 160 || x.AmountVnd < 0
            || x.DueOn == default || x.Evidence?.Length > 1000)) throw Error("MSG04");
        var serviceIds = request.Items.Select(x => x.ServiceId).Distinct().ToArray();
        var services = await db.Set<SchedulingService>().AsNoTracking().Where(x => x.BranchId == dossier.BranchId
            && x.Active && serviceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (services.Count != serviceIds.Length) throw Error("MSG04");
        var linkedItems = await db.TreatmentSteps.AsNoTracking().Where(x => x.DossierId == dossierId)
            .Select(x => x.ItemId).Distinct().ToArrayAsync(ct);
        if (linkedItems.Any(id => request.Items.All(x => x.Id != id || x.Decision != "Agreed")))
            throw Error("MSG233", 409);
        var existing = dossier.Items.ToDictionary(x => x.Id);
        var incoming = new HashSet<Guid>();
        foreach (var input in request.Items)
        {
            if (input.Quantity <= 0 || input.Quantity > 999999999 || decimal.Round(input.Quantity, 3) != input.Quantity
                || input.AgreedUnitPriceVnd < 0
                || input.PlannedOrder < 1 || input.EstimatedMinutes is < 1 or > 480
                || (input.EstimatedStart.HasValue && input.EstimatedEnd < input.EstimatedStart)
                || Clean(input.Unit) is not { Length: > 0 and <= 40 }
                || Clean(input.Region)?.Length > 80 || Clean(input.Counseling)?.Length > 4000
                || Clean(input.DecisionMaker)?.Length > 160 || Clean(input.RepresentativeBasis)?.Length > 1000
                || input.Decision is not ("Proposed" or "Agreed" or "Refused" or "Deferred")) throw Error("MSG04");
            if (input.SourceDiagnosisId.HasValue) throw Error("MSG239"); // DCMS-93/94 adapter is not available yet.
            var item = input.Id.HasValue
                ? existing.GetValueOrDefault(input.Id.Value) ?? throw Error("MSG113", 404)
                : new TreatmentItem { DossierId = dossier.Id };
            incoming.Add(item.Id);
            if (!input.Id.HasValue)
            {
                dossier.Items.Add(item);
                db.TreatmentItems.Add(item);
            }
            item.ServiceId = input.ServiceId;
            item.ServiceName = services[input.ServiceId].Name;
            item.Region = Clean(input.Region);
            item.Unit = input.Unit.Trim();
            item.Quantity = input.Quantity;
            item.PlannedOrder = input.PlannedOrder;
            item.EstimatedMinutes = input.EstimatedMinutes;
            item.EstimatedStart = input.EstimatedStart;
            item.EstimatedEnd = input.EstimatedEnd;
            item.AgreedUnitPriceVnd = input.AgreedUnitPriceVnd;
            item.Decision = input.Decision;
            item.Counseling = Clean(input.Counseling);
            item.DecisionMaker = Clean(input.DecisionMaker);
            item.RepresentativeBasis = Clean(input.RepresentativeBasis);
            item.ConsentDocumentId = input.ConsentDocumentId;
            item.DecidedAt = input.Decision == "Proposed" ? null : clock.GetUtcNow();
            item.DecidedBy = input.Decision == "Proposed" ? null : actorId;
        }
        foreach (var removed in dossier.Items.Where(x => !incoming.Contains(x.Id)).ToArray())
        {
            dossier.Items.Remove(removed);
            db.TreatmentItems.Remove(removed);
        }
        dossier.Goals = goals;
        dossier.PaymentTermsJson = JsonSerializer.Serialize(terms, Json);
        dossier.PlanState = "Draft";
        dossier.Version++;
        dossier.UpdatedAt = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        Event(dossier, actorId, "PlanDraftSaved", new { dossier.PlanId, dossier.Version,
            dossier.Goals, dossier.PaymentTermsJson,
            Items = dossier.Items.OrderBy(x => x.PlannedOrder).Select(ItemDto).ToArray() });
        await db.SaveChangesAsync(ct);
        var result = new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "nháp kế hoạch", value: dossier.Code));
        StoreReceipt(actorId, request.RequestKey, "Draft", new { dossierId, request }, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<TreatmentChange> CommitAsync(int actorId, Guid dossierId, CommitPlanRequest request, CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "Commit", new { dossierId, request }, ct);
        if (replay != null) return replay;
        if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
        if (dossier.State != "Open" || dossier.PlanState != "Draft" || dossier.Items.Count == 0) throw Error("MSG04");
        if (dossier.Items.Any(x => x.Decision != "Proposed" && (x.Counseling == null || x.DecisionMaker == null)))
            throw Error("MSG240");
        // S20 is not present yet. Do not claim an unverified document as consent evidence.
        if (dossier.Items.Any(x => x.ConsentDocumentId.HasValue)) throw Error("MSG240");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        dossier.PlanVersion++;
        foreach (var item in dossier.Items.Where(x => x.Decision != "Proposed"))
            item.DecisionPlanVersion = dossier.PlanVersion;
        dossier.PlanState = "Committed";
        dossier.Version++;
        dossier.UpdatedAt = clock.GetUtcNow();
        Event(dossier, actorId, "PlanCommitted", new { dossier.PlanId, dossier.PlanVersion,
            dossier.Goals, dossier.PaymentTermsJson, Items = dossier.Items.OrderBy(x => x.PlannedOrder).Select(ItemDto).ToArray() });
        await db.SaveChangesAsync(ct);
        var result = new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "kế hoạch điều trị", value: dossier.Code));
        StoreReceipt(actorId, request.RequestKey, "Commit", new { dossierId, request }, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<IReadOnlyList<TreatmentHistoryItem>> HistoryAsync(int actorId, Guid dossierId, CancellationToken ct)
    {
        await DossierAsync(actorId, dossierId, ct);
        var versions = await db.TreatmentEvents.AsNoTracking().Where(x => x.DossierId == dossierId &&
                (x.Kind == "PlanCommitted" || x.Kind == "PlanRevised"))
            .OrderByDescending(x => x.OccurredAt).ToListAsync(ct);
        return versions.Select(x =>
        {
            using var payload = JsonDocument.Parse(x.PayloadJson);
            var planVersion = payload.RootElement.GetProperty("planVersion").GetInt32();
            return new TreatmentHistoryItem(planVersion, x.OccurredAt, x.ActorId, x.PayloadJson);
        }).ToArray();
    }

    public async Task<TreatmentChange> CloseAsync(int actorId, Guid dossierId, CloseDossierRequest request, CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "Close", new { dossierId, request }, ct);
        if (replay != null) return replay;
        if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
        if (dossier.State != "Open" || request.Outcome is not ("CompletedAsAgreed" or "CompletedRevisedScope" or "StoppedIncomplete"))
            throw Error("MSG04");
        var reason = Clean(request.Reason);
        if (reason?.Length > 1000 || (request.Outcome != "CompletedAsAgreed" && reason == null)) throw Error("MSG04");
        if (request.Outcome == "CompletedRevisedScope" &&
            !await db.TreatmentEvents.AnyAsync(x => x.DossierId == dossierId && x.Kind == "PlanRevised", ct))
            throw Error("MSG233", 409);
        if (request.Outcome == "CompletedAsAgreed" && dossier.PlanState != "Committed") throw Error("MSG243", 409);
        var unresolved = dossier.Items.Where(x => x.Decision == "Agreed" && x.Progress != "Completed").ToArray();
        if (request.Dispositions == null || request.Dispositions.Select(x => x.ItemId).Distinct().Count()
            != request.Dispositions.Length || request.Dispositions.Any(x => !unresolved.Any(item => item.Id == x.ItemId)))
            throw Error("MSG243", 409);
        var dispositions = request.Dispositions.ToDictionary(x => x.ItemId);
        if (unresolved.Any(x => !dispositions.TryGetValue(x.Id, out var d) ||
            d.Outcome is not ("Stopped" or "Skipped" or "Cancelled") || Clean(d.Reason) is not { Length: > 0 and <= 1000 }))
            throw Error("MSG243", 409);
        if (request.Outcome == "CompletedAsAgreed" && unresolved.Length > 0) throw Error("MSG243", 409);
        if (await db.FollowUpInstructions.AsNoTracking().AnyAsync(x => x.DossierId == dossierId &&
            (x.Status == "NeedsBooking" || x.Status == "Booked"), ct)) throw Error("MSG233", 409);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        foreach (var item in unresolved) item.Progress = dispositions[item.Id].Outcome;
        var openSteps = await db.TreatmentSteps.Where(x => x.DossierId == dossierId && x.Outcome != "Completed").ToListAsync(ct);
        foreach (var step in openSteps.Where(x => dispositions.ContainsKey(x.ItemId)))
        {
            step.Outcome = dispositions[step.ItemId].Outcome;
            step.Version++;
        }
        dossier.State = request.Outcome == "StoppedIncomplete" ? "Stopped" : "Completed";
        dossier.ClosureOutcome = request.Outcome;
        dossier.ClosureReason = reason;
        dossier.ClosedAt = clock.GetUtcNow();
        dossier.ClosedBy = actorId;
        dossier.Version++;
        dossier.UpdatedAt = clock.GetUtcNow();
        Event(dossier, actorId, "DossierClosed", new { request.Outcome, reason, request.Dispositions });
        await db.SaveChangesAsync(ct);
        var result = new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "kết thúc hồ sơ", value: dossier.Code));
        StoreReceipt(actorId, request.RequestKey, "Close", new { dossierId, request }, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<TreatmentChange> ReopenAsync(int actorId, Guid dossierId, ReopenDossierRequest request, CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "Reopen", new { dossierId, request }, ct);
        if (replay != null) return replay;
        if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
        var reason = Clean(request.Reason);
        if (dossier.State == "Open" || reason is not { Length: > 0 and <= 1000 }) throw Error("MSG122");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        dossier.State = "Open";
        dossier.ClosureOutcome = null;
        dossier.ClosureReason = null;
        dossier.ClosedAt = null;
        dossier.ClosedBy = null;
        dossier.Version++;
        dossier.UpdatedAt = clock.GetUtcNow();
        Event(dossier, actorId, "DossierReopened", new { reason });
        await db.SaveChangesAsync(ct);
        var result = new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "mở lại hồ sơ", value: dossier.Code));
        StoreReceipt(actorId, request.RequestKey, "Reopen", new { dossierId, request }, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<TreatmentChange> ConfirmWarrantyAsync(int actorId, Guid dossierId, Guid itemId,
        ConfirmWarrantyRequest request, CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "Warranty", new { dossierId, itemId, request }, ct);
        if (replay != null) return replay;
        if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
        var item = dossier.Items.SingleOrDefault(x => x.Id == itemId) ?? throw Error("MSG113", 404);
        var policy = Clean(request.Policy);
        var version = Clean(request.PolicyVersion);
        var scope = Clean(request.Scope);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        if (dossier.PlanState != "Committed" || item.Decision != "Agreed" || item.Progress != "Completed"
            || request.EndsOn is { } endsOn && endsOn < today
            || policy is not { Length: > 0 and <= 160 }
            || version is not { Length: > 0 and <= 40 } || scope is not { Length: > 0 and <= 1000 }
            || item.WarrantyConfirmedAt.HasValue) throw Error("MSG244");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        item.WarrantyPolicy = policy;
        item.WarrantyPolicyVersion = version;
        item.WarrantyScope = scope;
        item.WarrantyConfirmedAt = clock.GetUtcNow();
        item.WarrantyConfirmedBy = actorId;
        item.WarrantyEndsOn = request.EndsOn;
        dossier.Version++;
        dossier.UpdatedAt = clock.GetUtcNow();
        Event(dossier, actorId, "WarrantyConfirmed", new { itemId, policy, version, scope, request.EndsOn });
        await db.SaveChangesAsync(ct);
        var result = new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "bảo hành điều trị", value: dossier.Code));
        StoreReceipt(actorId, request.RequestKey, "Warranty", new { dossierId, itemId, request }, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<IReadOnlyList<OwnTreatmentSummaryItem>> OwnSummaryAsync(int accountId, CancellationToken ct)
    {
        var patientIds = db.PatientAccountLinks.AsNoTracking().Where(x => x.AccountId == accountId
            && x.Status == PatientLinkStatus.Verified).Select(x => x.PatientId);
        var dossiers = await db.TreatmentDossiers.AsNoTracking().Include(x => x.Items)
            .Where(x => patientIds.Contains(x.PatientId) && x.PlanState == "Committed")
            .OrderByDescending(x => x.StartedOn).ToListAsync(ct);
        return dossiers.Select(x => new OwnTreatmentSummaryItem(x.Id, x.Code, x.State, x.StartedOn,
            x.Diagnosis, x.PlanVersion, x.Items.Where(item => item.Decision == "Agreed")
                .OrderBy(item => item.PlannedOrder).Select(ItemDto).ToArray())).ToArray();
    }
}

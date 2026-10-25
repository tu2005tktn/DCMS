using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed class UnavailableRevisionActualPort : ITreatmentRevisionActualPort
{
    public Task<RevisionSourceState> CheckAsync(int patientId, Guid dossierId, Guid itemId, CancellationToken ct) =>
        Task.FromResult(new RevisionSourceState(false, null, "Nguồn điều trị thực tế DCMS-99 chưa được kết nối."));
}

public sealed class UnavailableRevisionFinancePort : ITreatmentRevisionFinancePort
{
    public Task<RevisionSourceState> CheckAsync(int patientId, int branchId, Guid dossierId, Guid itemId,
        RevisionProposal proposed, CancellationToken ct) =>
        Task.FromResult(new RevisionSourceState(false, null, "Nguồn tài chính DCMS-101/108 chưa được kết nối."));
}

public sealed class TreatmentRevisionService(DcmsDbContext db, UserManager<ApplicationUser> users,
    ITreatmentDossierService dossiers, ITreatmentRevisionActualPort actual,
    ITreatmentRevisionFinancePort finance, TimeProvider clock,
    IServiceScopeFactory scopes) : ITreatmentRevisionService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static BusinessException Error(string code, int status = 400) =>
        new(status, SystemMessages.Get(code));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static bool Retryable(DbUpdateException error) =>
        error.InnerException is PostgresException { SqlState: "40001" or "23505" };
    private static bool Active(Appointment x) => x.Status is "Confirmed" or "CheckedIn" or "InProgress";
    private static void RequireOpen(TreatmentDossier dossier)
    {
        if (dossier.State != "Open" || dossier.PlanState != "Committed")
            throw Error("MSG111", 409);
    }

    private async Task LockRequestAsync(Guid key, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_lock(hashtext({key.ToString()}))", ct);
        }
        catch
        {
            await db.Database.CloseConnectionAsync();
            throw;
        }
    }

    private async Task UnlockRequestAsync(Guid key)
    {
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_unlock(hashtext({key.ToString()}))", CancellationToken.None);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    private async Task<TreatmentDossier> ScopedAsync(int actorId, Guid dossierId, Guid itemId,
        CancellationToken ct)
    {
        var dossier = await db.TreatmentDossiers.Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == dossierId, ct) ?? throw Error("MSG113", 404);
        var account = await users.FindByIdAsync(actorId.ToString());
        if (account == null || !account.IsActive || account.AccountType != "Staff" ||
            !AccountRoles.Effective(await users.GetRolesAsync(account)).Contains(AccountRoles.Dentist))
            throw Error("MSG06", 403);
        var employeeId = await db.Employees.Where(x => x.AccountId == actorId).Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(ct);
        if (!employeeId.HasValue || !await db.Set<SchedulingMember>().AnyAsync(x =>
                x.BranchId == dossier.BranchId && x.EmployeeId == employeeId && x.Active && x.CanDentist, ct))
            throw Error("MSG06", 403);
        if (!await db.Patients.AnyAsync(x => x.Id == dossier.PatientId && !x.IsArchived, ct) ||
            dossier.Items.All(x => x.Id != itemId)) throw Error("MSG113", 404);
        return dossier;
    }

    private async Task<T?> ReplayAsync<T>(int actorId, Guid key, string command, object input,
        CancellationToken ct) where T : class
    {
        if (key == Guid.Empty) throw Error("MSG04");
        var receipt = await db.TreatmentReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (receipt == null) return null;
        if (receipt.ActorId != actorId || receipt.Command != command || receipt.Fingerprint != Hash(input))
            throw Error("MSG111", 409);
        return JsonSerializer.Deserialize<T>(receipt.ResultJson, Json) ?? throw Error("MSG144", 503);
    }

    private async Task<T?> ReplayFreshAsync<T>(int actorId, Guid key, string command,
        object input, CancellationToken ct) where T : class
    {
        using var scope = scopes.CreateScope();
        var fresh = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var receipt = await fresh.TreatmentReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (receipt == null) return null;
        if (receipt.ActorId != actorId || receipt.Command != command ||
            receipt.Fingerprint != Hash(input)) throw Error("MSG111", 409);
        return JsonSerializer.Deserialize<T>(receipt.ResultJson, Json) ?? throw Error("MSG144", 503);
    }

    private void Receipt<T>(int actorId, Guid key, string command, object input, Guid dossierId, T result)
    {
        db.TreatmentReceipts.Add(new TreatmentReceipt
        {
            RequestKey = key, ActorId = actorId, Command = command,
            Fingerprint = Hash(input), DossierId = dossierId,
            ResultJson = JsonSerializer.Serialize(result, Json)
        });
    }

    private async Task<(List<TreatmentStep> Steps, List<FollowUpInstruction> Instructions,
        List<Appointment> Bookings)> ImpactsAsync(Guid dossierId, Guid itemId, CancellationToken ct)
    {
        var steps = await db.TreatmentSteps.Where(x => x.DossierId == dossierId && x.ItemId == itemId)
            .OrderBy(x => x.Id).ToListAsync(ct);
        var instructions = await db.FollowUpInstructions.Where(x => x.DossierId == dossierId &&
            x.ItemId == itemId && (x.Status == "NeedsBooking" || x.Status == "Booked"))
            .OrderBy(x => x.Id).ToListAsync(ct);
        var instructionIds = instructions.Select(x => x.Id).ToArray();
        var bookings = await db.Appointments.Where(x => x.FollowUpId.HasValue &&
            instructionIds.Contains(x.FollowUpId.Value) &&
            (x.Status == "Confirmed" || x.Status == "CheckedIn" || x.Status == "InProgress"))
            .OrderBy(x => x.Id).ToListAsync(ct);
        return (steps, instructions, bookings);
    }

    private static RevisionStepImpact Step(TreatmentStep x) => new(x.Id, x.Version, x.PlanVersion,
        x.Name, x.Outcome, x.HoldReason != null);
    private static RevisionInstructionImpact Instruction(FollowUpInstruction x) =>
        new(x.Id, x.Version, x.Status, x.ActiveAppointmentId);
    private static RevisionBookingImpact Booking(Appointment x) =>
        new(x.Id, x.Code, x.Version, x.Status, x.StartAt);

    public async Task<RevisionContextDto> ContextAsync(int actorId, Guid dossierId, Guid itemId,
        CancellationToken ct)
    {
        var dossier = await ScopedAsync(actorId, dossierId, itemId, ct);
        RequireOpen(dossier);
        var impacts = await ImpactsAsync(dossierId, itemId, ct);
        var detail = await dossiers.GetAsync(actorId, dossierId, ct);
        var services = await db.Set<SchedulingService>().AsNoTracking()
            .Where(x => x.BranchId == dossier.BranchId && x.Active)
            .OrderBy(x => x.Name).Select(x => new RevisionServiceOption(x.Id, x.Name))
            .ToArrayAsync(ct);
        return new(detail, detail.Items.Single(x => x.Id == itemId),
            impacts.Steps.Select(Step).ToArray(), impacts.Instructions.Select(Instruction).ToArray(),
            impacts.Bookings.Select(Booking).ToArray(), services);
    }

    private static RevisionDraftDto ToDto(TreatmentRevisionDraft row)
    {
        var payload = JsonSerializer.Deserialize<SaveRevisionDraftRequest>(row.PayloadJson, Json)
            ?? throw Error("MSG144", 503);
        return new(row.DossierId, row.ItemId, row.ActorId, row.BasePlanVersion,
            row.Version, payload.Proposed, payload.Reason, payload.Counseling,
            payload.Decision, payload.DecisionMaker, payload.RepresentativeBasis,
            payload.FutureDispositions, row.UpdatedAt);
    }

    private async Task<TreatmentRevisionDraft> DraftRowAsync(int actorId, Guid dossierId,
        Guid itemId, CancellationToken ct) =>
        await db.TreatmentRevisionDrafts.SingleOrDefaultAsync(x => x.DossierId == dossierId &&
            x.ItemId == itemId && x.ActorId == actorId, ct) ?? throw Error("MSG113", 404);

    public async Task<RevisionDraftDto> DraftAsync(int actorId, Guid dossierId, Guid itemId,
        CancellationToken ct)
    {
        RequireOpen(await ScopedAsync(actorId, dossierId, itemId, ct));
        return ToDto(await DraftRowAsync(actorId, dossierId, itemId, ct));
    }

    private static void Validate(SaveRevisionDraftRequest request)
    {
        var p = request.Proposed;
        if (request.BasePlanVersion < 1 || request.ExpectedDraftVersion < 0 || p == null ||
            p.ServiceId < 1 || Clean(p.Material)?.Length > 160 || Clean(p.Region)?.Length > 80 ||
            Clean(p.Unit) is not { Length: <= 40 } || p.Quantity <= 0 || p.Quantity > 999999999 ||
            decimal.Round(p.Quantity, 3) != p.Quantity || p.AgreedUnitPriceVnd < 0 ||
            p.EstimatedMinutes is < 1 or > 480 || Clean(request.Reason)?.Length > 1000 ||
            !TreatmentPlanRules.ValidDuration(p.EstimatedMinMinutes, p.EstimatedMinutes,
                p.EstimatedMaxMinutes, p.ReservedMinutes, p.AcknowledgeShortReservation) ||
            p.EstimatedSessions is < 1 or > 100 ||
            !TreatmentPlanRules.ValidReturnWindow(p.ReturnEarliestOn, p.ReturnTargetOn,
                p.ReturnLatestOn) ||
            Clean(request.Counseling)?.Length > 4000 ||
            Clean(request.DecisionMaker)?.Length > 160 ||
            Clean(request.RepresentativeBasis)?.Length > 1000 ||
            request.Decision is not ("Proposed" or "Agreed" or "Refused" or "Deferred") ||
            request.FutureDispositions == null || request.FutureDispositions.Any(x =>
                x.StepId == Guid.Empty || x.Outcome is not ("Stopped" or "Cancelled" or "Skipped") ||
                Clean(x.Reason)?.Length > 1000) ||
            request.FutureDispositions.Select(x => x.StepId).Distinct().Count() !=
            request.FutureDispositions.Length) throw Error("MSG04");
    }

    public async Task<RevisionDraftDto> SaveDraftAsync(int actorId, Guid dossierId, Guid itemId,
        SaveRevisionDraftRequest request, CancellationToken ct)
    {
        var dossier = await ScopedAsync(actorId, dossierId, itemId, ct);
        var input = new { dossierId, itemId, request };
        var replay = await ReplayAsync<RevisionDraftDto>(actorId, request.RequestKey, "RevisionDraft", input, ct);
        if (replay != null) return replay;
        Validate(request);
        if (dossier.State != "Open" || dossier.PlanState != "Committed" ||
            dossier.PlanVersion != request.BasePlanVersion) throw Error("MSG111", 409);
        if (!await db.Set<SchedulingService>().AnyAsync(x => x.Id == request.Proposed.ServiceId &&
            x.BranchId == dossier.BranchId && x.Active, ct)) throw Error("MSG04");
        await LockRequestAsync(request.RequestKey, ct);
        try
        {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var inTransactionReplay = await ReplayAsync<RevisionDraftDto>(actorId, request.RequestKey,
            "RevisionDraft", input, ct);
        if (inTransactionReplay != null) return inTransactionReplay;
        var row = await db.TreatmentRevisionDrafts.SingleOrDefaultAsync(x => x.DossierId == dossierId &&
            x.ItemId == itemId && x.ActorId == actorId, ct);
        if (row == null)
        {
            if (request.ExpectedDraftVersion != 0) throw Error("MSG111", 409);
            row = new TreatmentRevisionDraft { DossierId = dossierId, ItemId = itemId,
                ActorId = actorId, BasePlanVersion = request.BasePlanVersion };
            db.TreatmentRevisionDrafts.Add(row);
        }
        else
        {
            if (row.Version != request.ExpectedDraftVersion) throw Error("MSG111", 409);
            row.Version++;
        }
        row.PayloadJson = JsonSerializer.Serialize(request, Json);
        row.UpdatedAt = clock.GetUtcNow();
        var result = ToDto(row);
        Receipt(actorId, request.RequestKey, "RevisionDraft", input, dossierId, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            var completed = await ReplayFreshAsync<RevisionDraftDto>(actorId, request.RequestKey,
                "RevisionDraft", input, ct);
            return completed ?? throw Error("MSG111", 409);
        }
        catch (PostgresException ex) when (ex.SqlState is "40001" or "23505")
        {
            var completed = await ReplayFreshAsync<RevisionDraftDto>(actorId, request.RequestKey,
                "RevisionDraft", input, ct);
            return completed ?? throw Error("MSG111", 409);
        }
        catch (DbUpdateException ex) when (Retryable(ex))
        {
            var completed = await ReplayFreshAsync<RevisionDraftDto>(actorId, request.RequestKey,
                "RevisionDraft", input, ct);
            return completed ?? throw Error("MSG111", 409);
        }
        catch (BusinessException ex) when (ex.Response.Code == "MSG111")
        {
            var completed = await ReplayFreshAsync<RevisionDraftDto>(actorId, request.RequestKey,
                "RevisionDraft", input, ct);
            if (completed != null) return completed;
            throw;
        }
        finally { await UnlockRequestAsync(request.RequestKey); }
    }

    public async Task DeleteDraftAsync(int actorId, Guid dossierId, Guid itemId, CancellationToken ct)
    {
        RequireOpen(await ScopedAsync(actorId, dossierId, itemId, ct));
        var row = await db.TreatmentRevisionDrafts.SingleOrDefaultAsync(x => x.DossierId == dossierId &&
            x.ItemId == itemId && x.ActorId == actorId, ct);
        if (row != null) { db.TreatmentRevisionDrafts.Remove(row); await db.SaveChangesAsync(ct); }
    }

    private async Task<RevisionPreviewDto> BuildPreviewAsync(TreatmentDossier dossier,
        TreatmentItem item, TreatmentRevisionDraft row, CancellationToken ct)
    {
        var draft = ToDto(row);
        var p = draft.Proposed;
        var impacts = await ImpactsAsync(dossier.Id, item.Id, ct);
        var needsActual = item.Progress != "NotStarted" || impacts.Steps.Any(x =>
            x.Outcome is "Partial" or "Completed");
        var needsFinance = p.AgreedUnitPriceVnd != item.AgreedUnitPriceVnd ||
            p.Quantity != item.Quantity || p.ServiceId != item.ServiceId ||
            Clean(p.Material) != Clean(item.Material);
        var actualState = needsActual
            ? await actual.CheckAsync(dossier.PatientId, dossier.Id, item.Id, ct)
            : new RevisionSourceState(true, "not-required", null);
        var financeState = needsFinance
            ? await finance.CheckAsync(dossier.PatientId, dossier.BranchId, dossier.Id, item.Id, p, ct)
            : new RevisionSourceState(true, "not-required", null);
        var blockers = new List<string>();
        if (Clean(draft.Reason) is null) blockers.Add("MSG04");
        if (dossier.Version < 1 || dossier.PlanVersion != row.BasePlanVersion)
            blockers.Add("MSG111");
        if (impacts.Bookings.Any(Active) || impacts.Instructions.Any(x => x.Status == "Booked"))
            blockers.Add("MSG233");
        var openSteps = impacts.Steps.Where(x => x.Outcome is not ("Completed" or "Stopped" or "Cancelled" or "Skipped"))
            .Select(x => x.Id).Order().ToArray();
        if (!openSteps.SequenceEqual(draft.FutureDispositions.Select(x => x.StepId).Order()))
            blockers.Add("MSG233");
        if (draft.FutureDispositions.Any(x => Clean(x.Reason) == null)) blockers.Add("MSG233");
        if (needsActual && (!actualState.Available || !actualState.CommitSafe ||
            string.IsNullOrWhiteSpace(actualState.VersionToken)))
            blockers.Add("MSG144");
        if (needsFinance && (!financeState.Available || !financeState.CommitSafe ||
            string.IsNullOrWhiteSpace(financeState.VersionToken) || financeState.Summary == null))
            blockers.Add("MSG245");
        if (item.DiscountStatus == "Approved" && (p.ServiceId != item.ServiceId ||
            p.Quantity != item.Quantity || p.AgreedUnitPriceVnd != item.AgreedUnitPriceVnd))
            blockers.Add("MSG245");
        if (item.Decision == "Agreed" && (Clean(draft.Counseling) == null ||
            Clean(draft.DecisionMaker) == null || draft.Decision is not ("Agreed" or "Refused" or "Deferred")))
            blockers.Add("MSG240");
        if (p.ServiceId == item.ServiceId && Clean(p.Material) == Clean(item.Material) &&
            Clean(p.Region) == Clean(item.Region) && p.Quantity == item.Quantity &&
            p.AgreedUnitPriceVnd == item.AgreedUnitPriceVnd && p.EstimatedMinutes == item.EstimatedMinutes &&
            p.EstimatedMinMinutes == item.EstimatedMinMinutes &&
            p.EstimatedMaxMinutes == item.EstimatedMaxMinutes && p.ReservedMinutes == item.ReservedMinutes &&
            p.EstimatedSessions == item.EstimatedSessions && p.ReturnEarliestOn == item.ReturnEarliestOn &&
            p.ReturnTargetOn == item.ReturnTargetOn && p.ReturnLatestOn == item.ReturnLatestOn &&
            p.Unit == item.Unit && draft.Decision == item.Decision) blockers.Add("MSG04");
        return new(dossier.Id, item.Id, row.Version, dossier.Version, dossier.PlanVersion,
            (await dossiers.GetAsync(row.ActorId, dossier.Id, ct)).Items.Single(x => x.Id == item.Id), p,
            impacts.Steps.Select(Step).ToArray(), impacts.Instructions.Select(Instruction).ToArray(),
            impacts.Bookings.Select(Booking).ToArray(), actualState, financeState,
            p.Quantity * p.AgreedUnitPriceVnd - item.Quantity * item.AgreedUnitPriceVnd,
            blockers.Count == 0, blockers.Distinct().ToArray());
    }

    public async Task<RevisionPreviewDto> PreviewAsync(int actorId, Guid dossierId,
        PreviewRevisionRequest request, CancellationToken ct)
    {
        var dossier = await ScopedAsync(actorId, dossierId, request.ItemId, ct);
        RequireOpen(dossier);
        var row = await DraftRowAsync(actorId, dossierId, request.ItemId, ct);
        if (row.Version != request.DraftVersion) throw Error("MSG111", 409);
        return await BuildPreviewAsync(dossier, dossier.Items.Single(x => x.Id == request.ItemId), row, ct);
    }

    private static bool Match<T>(IReadOnlyList<T> current, ExpectedRevisionVersion[] expected,
        Func<T, Guid> id, Func<T, int> version)
    {
        if (expected == null || current.Count != expected.Length ||
            expected.Select(x => x.Id).Distinct().Count() != expected.Length) return false;
        var map = expected.ToDictionary(x => x.Id, x => x.Version);
        return current.All(x => map.TryGetValue(id(x), out var v) && v == version(x));
    }

    public async Task<RevisionChange> CommitAsync(int actorId, Guid dossierId,
        CommitRevisionRequest request, CancellationToken ct)
    {
        var input = new { dossierId, request };
        await ScopedAsync(actorId, dossierId, request.ItemId, ct);
        var replay = await ReplayAsync<RevisionChange>(actorId, request.RequestKey, "RevisionCommit", input, ct);
        if (replay != null) return replay;
        await LockRequestAsync(request.RequestKey, ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var inTransactionReplay = await ReplayAsync<RevisionChange>(actorId,
                request.RequestKey, "RevisionCommit", input, ct);
            if (inTransactionReplay != null) return inTransactionReplay;
            var dossier = await ScopedAsync(actorId, dossierId, request.ItemId, ct);
            if (dossier.State != "Open" || dossier.PlanState != "Committed" ||
                dossier.Version != request.ExpectedDossierVersion ||
                dossier.PlanVersion != request.ExpectedPlanVersion) throw Error("MSG111", 409);
            var row = await DraftRowAsync(actorId, dossierId, request.ItemId, ct);
            if (row.Version != request.DraftVersion || row.BasePlanVersion != dossier.PlanVersion)
                throw Error("MSG111", 409);
            var item = dossier.Items.Single(x => x.Id == request.ItemId);
            var preview = await BuildPreviewAsync(dossier, item, row, ct);
            if (!preview.CanSave) throw Error(preview.Blockers[0], 409);
            if (!Match(preview.Steps, request.Steps, x => x.Id, x => x.Version) ||
                !Match(preview.Instructions, request.Instructions, x => x.Id, x => x.Version) ||
                !Match(preview.Bookings, request.Bookings, x => x.Id, x => x.Version) ||
                request.ActualVersionToken != preview.Actual.VersionToken ||
                request.FinanceVersionToken != preview.Finance.VersionToken) throw Error("MSG111", 409);
            var draft = ToDto(row);
            var service = await db.Set<SchedulingService>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == draft.Proposed.ServiceId &&
                    x.BranchId == dossier.BranchId && x.Active, ct) ?? throw Error("MSG04");
            var before = JsonSerializer.Serialize(preview.Current, Json);
            var impacts = await ImpactsAsync(dossierId, item.Id, ct);
            var dispositions = draft.FutureDispositions.ToDictionary(x => x.StepId);
            foreach (var step in impacts.Steps.Where(x => dispositions.ContainsKey(x.Id)))
            {
                var disposition = dispositions[step.Id];
                step.Outcome = disposition.Outcome;
                step.Version++;
            }
            foreach (var instruction in impacts.Instructions.Where(x => x.Status == "NeedsBooking"))
            {
                instruction.Status = "Superseded";
                instruction.Version++;
                instruction.UpdatedAt = clock.GetUtcNow();
            }
            var serviceChanged = item.ServiceId != service.Id;
            item.ServiceId = service.Id;
            item.ServiceName = service.Name;
            item.Material = Clean(draft.Proposed.Material);
            item.Region = Clean(draft.Proposed.Region);
            item.Unit = draft.Proposed.Unit.Trim();
            item.Quantity = draft.Proposed.Quantity;
            item.EstimatedMinutes = draft.Proposed.EstimatedMinutes;
            item.EstimatedMinMinutes = draft.Proposed.EstimatedMinMinutes;
            item.EstimatedMaxMinutes = draft.Proposed.EstimatedMaxMinutes;
            item.ReservedMinutes = draft.Proposed.ReservedMinutes;
            item.EstimatedSessions = draft.Proposed.EstimatedSessions;
            item.ReturnEarliestOn = draft.Proposed.ReturnEarliestOn;
            item.ReturnTargetOn = draft.Proposed.ReturnTargetOn;
            item.ReturnLatestOn = draft.Proposed.ReturnLatestOn;
            if (item.EstimatedMaxMinutes.HasValue && item.ReservedMinutes < item.EstimatedMaxMinutes)
            {
                item.ShortReservationAcknowledgedBy = actorId;
                item.ShortReservationAcknowledgedAt = clock.GetUtcNow();
            }
            else
            {
                item.ShortReservationAcknowledgedBy = null;
                item.ShortReservationAcknowledgedAt = null;
            }
            if (serviceChanged)
            {
                item.ReferenceUnitPriceVnd = service.ReferenceUnitPriceVnd;
                item.ReferencePriceVersion = service.PriceVersion;
            }
            item.AgreedUnitPriceVnd = draft.Proposed.AgreedUnitPriceVnd;
            item.Decision = draft.Decision;
            item.Counseling = Clean(draft.Counseling);
            item.DecisionMaker = Clean(draft.DecisionMaker);
            item.RepresentativeBasis = Clean(draft.RepresentativeBasis);
            item.DecidedBy = actorId;
            item.DecidedAt = clock.GetUtcNow();
            item.DecisionPlanVersion = dossier.PlanVersion + 1;
            item.ConsentDocumentId = null;
            item.ConsentPlanVersion = null;
            dossier.PlanVersion++;
            dossier.Version++;
            dossier.UpdatedAt = clock.GetUtcNow();
            var detailBeforeSave = await dossiers.GetAsync(actorId, dossierId, ct);
            db.TreatmentEvents.Add(new TreatmentEvent
            {
                DossierId = dossierId, ActorId = actorId, DossierVersion = dossier.Version,
                Kind = "PlanRevised", OccurredAt = dossier.UpdatedAt,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    dossier.PlanId, dossier.PlanVersion, SelectedItemId = item.Id, draft.Reason,
                    Previous = JsonSerializer.Deserialize<JsonElement>(before),
                    Current = detailBeforeSave.Items.Single(x => x.Id == item.Id),
                    draft.FutureDispositions, dossier.Goals, dossier.PaymentTermsJson,
                    Items = detailBeforeSave.Items
                }, Json)
            });
            db.TreatmentRevisionDrafts.Remove(row);
            await db.SaveChangesAsync(ct);
            var detail = await dossiers.GetAsync(actorId, dossierId, ct);
            var result = new RevisionChange(detail, item.Id, dossier.PlanVersion,
                SystemMessages.Get("MSG222", "phiên bản kế hoạch", value: dossier.Code));
            Receipt(actorId, request.RequestKey, "RevisionCommit", input, dossierId, result);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            var completed = await ReplayFreshAsync<RevisionChange>(actorId, request.RequestKey,
                "RevisionCommit", input, ct);
            return completed ?? throw Error("MSG111", 409);
        }
        catch (PostgresException ex) when (ex.SqlState is "40001" or "23505")
        {
            var completed = await ReplayFreshAsync<RevisionChange>(actorId, request.RequestKey,
                "RevisionCommit", input, ct);
            return completed ?? throw Error("MSG111", 409);
        }
        catch (DbUpdateException ex) when (Retryable(ex))
        {
            var completed = await ReplayFreshAsync<RevisionChange>(actorId, request.RequestKey,
                "RevisionCommit", input, ct);
            return completed ?? throw Error("MSG111", 409);
        }
        catch (BusinessException ex) when (ex.Response.Code == "MSG111")
        {
            var completed = await ReplayFreshAsync<RevisionChange>(actorId, request.RequestKey,
                "RevisionCommit", input, ct);
            if (completed != null) return completed;
            throw;
        }
        finally { await UnlockRequestAsync(request.RequestKey); }
    }
}

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

public sealed class UnavailableClinicalVisitPort : IClinicalVisitPort
{
    public Task<ClinicalVisitState> GetAsync(Guid visitId, CancellationToken ct) =>
        Task.FromResult(new ClinicalVisitState(false, false, 0, 0, 0));
}

public sealed class UnavailableClinicalConsentPort : IClinicalConsentPort
{
    public Task<bool> ValidAsync(int patientId, Guid? itemId, Guid? stepId, int? planVersion,
        int serviceId, CancellationToken ct) => Task.FromResult(false);
}

public sealed class UnavailableClinicalChargePort : IClinicalChargePort
{
    public Task<bool> PostInTransactionAsync(int patientId, int branchId, Guid activityId,
        decimal quantity, decimal priceSnapshotVnd, CancellationToken ct) => Task.FromResult(false);
}

public sealed class ClinicalActualEvidencePort(DcmsDbContext db) : ITreatmentActualEvidencePort
{
    public async Task<bool> HasDentistConfirmedActualAsync(int patientId, Guid dossierId, Guid stepId,
        IReadOnlyList<Guid> evidenceIds, CancellationToken ct)
    {
        if (evidenceIds.Count == 0 || evidenceIds.Contains(Guid.Empty)) return false;
        var rows = await db.ClinicalActivities.AsNoTracking().Where(x => evidenceIds.Contains(x.Id))
            .ToListAsync(ct);
        return rows.Count == evidenceIds.Distinct().Count() && rows.All(x => x.State == "Posted" &&
            x.PatientId == patientId && x.DossierId == dossierId && x.StepId == stepId);
    }

    public async Task<bool> HasFollowUpFulfillmentAsync(int patientId, Guid instructionId,
        CancellationToken ct)
    {
        var instruction = await db.FollowUpInstructions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == instructionId && x.PatientId == patientId, ct);
        return instruction?.StepId is { } stepId && await db.ClinicalActivities.AsNoTracking()
            .AnyAsync(x => x.PatientId == patientId && x.StepId == stepId && x.State == "Posted", ct);
    }
}

public sealed class ClinicalWorkService(DcmsDbContext db, UserManager<ApplicationUser> users,
    IClinicalVisitPort visits, IClinicalConsentPort consent, IClinicalChargePort charges,
    ITreatmentProgressionService progression, TimeProvider clock) : IClinicalWorkService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static BusinessException Error(string code, int status = 400) =>
        new(status, SystemMessages.Get(code));
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<int> DentistAsync(int actorId, int branchId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user == null || !user.IsActive || user.AccountType != "Staff" ||
            !AccountRoles.Effective(await users.GetRolesAsync(user)).Contains(AccountRoles.Dentist))
            throw Error("MSG06", 403);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        if (employee == null || !await db.Set<SchedulingMember>().AsNoTracking().AnyAsync(x =>
            x.BranchId == branchId && x.EmployeeId == employee.Id && x.Active && x.CanDentist, ct))
            throw Error("MSG06", 403);
        return employee.Id;
    }

    private async Task<ClinicalVisitState> VisitAsync(int actorId, Guid id, int expectedVersion,
        CancellationToken ct)
    {
        if (id == Guid.Empty) throw Error("MSG04");
        var visit = await visits.GetAsync(id, ct);
        if (!visit.Available) throw Error("MSG144", 503);
        await DentistAsync(actorId, visit.BranchId, ct);
        if (await db.Visits.AnyAsync(x => x.Id == id, ct))
            await new DCMS.Infrastructure.Features.Examinations.ClinicalAccess(db, users, clock).VisitAsync(actorId, id, true, ct);
        if (!visit.InProgress) throw Error("MSG230", 409);
        if (expectedVersion > 0 && visit.Version != expectedVersion) throw Error("MSG111", 409);
        return visit;
    }

    private async Task<Guid?> ReplayAsync(int actorId, Guid key, string command, object request,
        CancellationToken ct)
    {
        if (key == Guid.Empty) throw Error("MSG04");
        var receipt = await db.ClinicalCommandReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (receipt == null) return null;
        if (receipt.ActorId != actorId || receipt.Command != command || receipt.Fingerprint != Hash(request))
            throw Error("MSG111", 409);
        return receipt.ResultId;
    }

    private void Receipt(int actorId, Guid key, string command, object request, Guid id) =>
        db.ClinicalCommandReceipts.Add(new ClinicalCommandReceipt
        {
            RequestKey = key, ActorId = actorId, Command = command,
            Fingerprint = Hash(request), ResultId = id
        });

    private static ActivityDto Map(ClinicalActivity x) => new(x.Id, x.VisitId, x.PatientId,
        x.BranchId, x.DossierId, x.ItemId, x.StepId, x.PlanVersion, x.ServiceId, x.Region,
        x.Unit, x.Quantity, x.PerformedAt, x.ElapsedMinutes, x.PerformerId, x.Outcome,
        x.NextSteps, x.ChargingBasis, x.ChargingBasisEvidence, x.PriceSnapshotVnd,
        x.ReviewId, x.State, x.Version,
        x.CreatedAt, x.PostedAt);
    private static ReviewDto Map(ClinicalReview x) => new(x.Id, x.VisitId, x.PatientId,
        x.BranchId, x.SourceActivityId, x.SourceItemId, x.Complaint, x.Findings, x.Coverage,
        x.Outcome, x.CoveredComponent, x.Exclusions, x.Policy, x.PolicyVersion, x.Basis,
        x.NextAction, x.DentistAccountId, x.RecordedAt, x.Version);
    private static CareNoteDto Map(ClinicalCareNote x) => new(x.Id, x.VisitId, x.PatientId,
        x.BranchId, x.SourceActivityId, x.ReviewId, x.Content, x.Response, x.ActorId, x.RecordedAt);

    private static void Validate(ActivityInput x)
    {
        if (x.ServiceId <= 0 || Clean(x.Unit) is not { Length: <= 40 } || x.Quantity <= 0 ||
            decimal.Round(x.Quantity, 3) != x.Quantity || x.PerformedAt == default ||
            x.ElapsedMinutes is < 1 or > 1440 || Clean(x.Outcome) is not { Length: <= 4000 } ||
            Clean(x.NextSteps)?.Length > 2000 || Clean(x.Region)?.Length > 80 ||
            x.PriceSnapshotVnd < 0 || decimal.Truncate(x.PriceSnapshotVnd) != x.PriceSnapshotVnd ||
            x.ChargingBasis is not ("Free" or "Included" or "Separate") ||
            Clean(x.ChargingBasisEvidence)?.Length > 1000 ||
            (x.ChargingBasis == "Free" && Clean(x.ChargingBasisEvidence) == null) ||
            (x.ChargingBasis == "Included" && !x.ItemId.HasValue) ||
            (x.DossierId.HasValue != x.ItemId.HasValue) ||
            (x.StepId.HasValue && !x.ItemId.HasValue)) throw Error("MSG04");
    }

    public async Task<IReadOnlyList<ClinicalServiceOption>> ServicesAsync(int actorId, int branchId,
        CancellationToken ct)
    {
        await DentistAsync(actorId, branchId, ct);
        return await db.Set<SchedulingService>().AsNoTracking().Where(x => x.BranchId == branchId && x.Active)
            .OrderBy(x => x.Name).Select(x => new ClinicalServiceOption(x.Id, x.Name)).ToListAsync(ct);
    }

    private async Task ValidateSourceAsync(ClinicalVisitState visit, ActivityInput x,
        CancellationToken ct)
    {
        if (x.ChargingBasis == "Free" && !x.ReviewId.HasValue) throw Error("MSG244", 409);
        if (!await db.Set<SchedulingService>().AsNoTracking().AnyAsync(s =>
            s.Id == x.ServiceId && s.BranchId == visit.BranchId && s.Active, ct))
            throw Error("MSG04");
        if (x.ReviewId.HasValue)
        {
            var review = await db.ClinicalReviews.AsNoTracking().SingleOrDefaultAsync(r =>
                r.Id == x.ReviewId && r.PatientId == visit.PatientId && r.BranchId == visit.BranchId, ct);
            if (review == null) throw Error("MSG113", 404);
            if (x.ChargingBasis == "Free" &&
                (review.Coverage != "Covered" || Clean(review.Basis) == null)) throw Error("MSG244", 409);
        }
        if (x.DossierId.HasValue)
        {
            var dossier = await db.TreatmentDossiers.Include(d => d.Items)
                .SingleOrDefaultAsync(d => d.Id == x.DossierId, ct);
            if (dossier == null || dossier.PatientId != visit.PatientId ||
                dossier.BranchId != visit.BranchId) throw Error("MSG113", 404);
            if (dossier.State != "Open" || dossier.PlanState != "Committed" ||
                dossier.PlanVersion != x.PlanVersion) throw Error("MSG111", 409);
            var item = dossier.Items.SingleOrDefault(i => i.Id == x.ItemId);
            if (item == null || item.Decision != "Agreed" || item.ServiceId != x.ServiceId)
                throw Error("MSG230", 409);
            if (item.Unit == x.Unit && item.Unit != "gói")
            {
                var performed = await db.ClinicalActivities.AsNoTracking().Where(a =>
                    a.ItemId == item.Id && a.State == "Posted" && a.Unit == x.Unit)
                    .SumAsync(a => a.Quantity, ct);
                if (performed + x.Quantity > item.Quantity) throw Error("MSG230", 409);
            }
            if (x.StepId.HasValue)
            {
                var step = await db.TreatmentSteps.SingleOrDefaultAsync(s => s.Id == x.StepId &&
                    s.DossierId == dossier.Id && s.ItemId == item.Id, ct);
                if (step == null) throw Error("MSG113", 404);
                if (step.HoldReason != null || step.Outcome is "Completed" or "Stopped" or "Cancelled")
                    throw Error("MSG230", 409);
                var steps = await db.TreatmentSteps.AsNoTracking().Where(s => s.DossierId == dossier.Id)
                    .ToListAsync(ct);
                var parents = JsonSerializer.Deserialize<Guid[]>(step.PredecessorsJson, Json) ?? [];
                if (parents.Any(id => steps.All(s => s.Id != id || s.Outcome != "Completed")))
                    throw Error("MSG230", 409);
                if (step.LabRequirement != null) throw Error("MSG230", 409);
            }
        }
        if (!await consent.ValidAsync(visit.PatientId, x.ItemId, x.StepId, x.PlanVersion,
            x.ServiceId, ct)) throw Error("MSG240", 409);
    }

    public async Task<TreatmentContextDto> ContextAsync(int actorId, Guid visitId, CancellationToken ct)
    {
        var visit = await VisitAsync(actorId, visitId, 0, ct);
        var activities = await db.ClinicalActivities.AsNoTracking().Where(x =>
                x.PatientId == visit.PatientId && x.BranchId == visit.BranchId && x.State == "Posted")
            .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        return new(visit, [], activities.Select(Map).ToArray());
    }

    public async Task<ActivityDto> CreateDraftAsync(int actorId, CreateActivityDraftRequest request,
        CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty) throw Error("MSG04");
        await using var gate = await ClinicalRequestLock.AcquireAsync(db, request.RequestKey, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "ActivityDraft", request, ct);
        if (replay.HasValue) return Map(await db.ClinicalActivities.AsNoTracking().SingleAsync(x => x.Id == replay, ct));
        Validate(request.Activity);
        var visit = await VisitAsync(actorId, request.Activity.VisitId, request.ExpectedVisitVersion, ct);
        var performer = await DentistAsync(actorId, visit.BranchId, ct);
        var a = request.Activity;
        var row = new ClinicalActivity
        {
            VisitId = a.VisitId, PatientId = visit.PatientId, BranchId = visit.BranchId,
            DossierId = a.DossierId, ItemId = a.ItemId, StepId = a.StepId,
            PlanVersion = a.PlanVersion, ServiceId = a.ServiceId, Region = Clean(a.Region),
            Unit = a.Unit.Trim(), Quantity = a.Quantity, PerformedAt = a.PerformedAt,
            ElapsedMinutes = a.ElapsedMinutes, PerformerId = performer, Outcome = a.Outcome.Trim(),
            NextSteps = Clean(a.NextSteps), ChargingBasis = a.ChargingBasis,
            ChargingBasisEvidence = Clean(a.ChargingBasisEvidence),
            PriceSnapshotVnd = a.PriceSnapshotVnd, ReviewId = a.ReviewId,
            CreatedAt = clock.GetUtcNow()
        };
        db.ClinicalActivities.Add(row);
        Receipt(actorId, request.RequestKey, "ActivityDraft", request, row.Id);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<ActivityDto> UpdateDraftAsync(int actorId, Guid id,
        UpdateActivityDraftRequest request, CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty) throw Error("MSG04");
        await using var gate = await ClinicalRequestLock.AcquireAsync(db, request.RequestKey, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "ActivityDraftUpdate", new { id, request }, ct);
        if (replay.HasValue) return Map(await db.ClinicalActivities.AsNoTracking().SingleAsync(x => x.Id == replay, ct));
        Validate(request.Activity);
        var row = await db.ClinicalActivities.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw Error("MSG113", 404);
        var visit = await VisitAsync(actorId, row.VisitId, request.ExpectedVisitVersion, ct);
        if (row.PatientId != visit.PatientId || row.State != "Draft" ||
            row.Version != request.ExpectedVersion || request.Activity.VisitId != row.VisitId)
            throw Error("MSG111", 409);
        var a = request.Activity;
        row.DossierId = a.DossierId; row.ItemId = a.ItemId; row.StepId = a.StepId;
        row.PlanVersion = a.PlanVersion; row.ServiceId = a.ServiceId; row.Region = Clean(a.Region);
        row.Unit = a.Unit.Trim(); row.Quantity = a.Quantity; row.PerformedAt = a.PerformedAt;
        row.ElapsedMinutes = a.ElapsedMinutes; row.Outcome = a.Outcome.Trim();
        row.NextSteps = Clean(a.NextSteps); row.ChargingBasis = a.ChargingBasis;
        row.ChargingBasisEvidence = Clean(a.ChargingBasisEvidence);
        row.PriceSnapshotVnd = a.PriceSnapshotVnd; row.ReviewId = a.ReviewId; row.Version++;
        Receipt(actorId, request.RequestKey, "ActivityDraftUpdate", new { id, request }, id);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<ActivityDto> PostAsync(int actorId, Guid id, PostActivityRequest request,
        CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty) throw Error("MSG04");
        await using var gate = await ClinicalRequestLock.AcquireAsync(db, request.RequestKey, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "ActivityPost", new { id, request }, ct);
        if (replay.HasValue) return Map(await db.ClinicalActivities.AsNoTracking().SingleAsync(x => x.Id == replay, ct));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({DCMS.Infrastructure.Features.Scheduling.WorkingScheduleService.TransactionLock})", ct);
        var row = await db.ClinicalActivities.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw Error("MSG113", 404);
        var visit = await VisitAsync(actorId, row.VisitId, request.ExpectedVisitVersion, ct);
        if (row.State != "Draft" || row.Version != request.ExpectedVersion ||
            row.PatientId != visit.PatientId || row.BranchId != visit.BranchId)
            throw Error("MSG111", 409);
        await ValidateSourceAsync(visit, new ActivityInput(row.VisitId, row.DossierId, row.ItemId,
            row.StepId, row.PlanVersion, row.ServiceId, row.Region, row.Unit, row.Quantity,
            row.PerformedAt, row.ElapsedMinutes, row.Outcome, row.NextSteps, row.ChargingBasis,
            row.ChargingBasisEvidence,
            row.PriceSnapshotVnd, row.ReviewId), ct);
        if (row.ChargingBasis == "Separate" &&
            !await charges.PostInTransactionAsync(row.PatientId, row.BranchId, row.Id,
                row.Quantity, row.PriceSnapshotVnd, ct))
            throw Error("MSG144", 503);
        row.State = "Posted"; row.PostedAt = clock.GetUtcNow(); row.Version++;
        await db.SaveChangesAsync(ct);
        if (row.StepId.HasValue)
        {
            if (request.StepOutcome is not ("Partial" or "Completed") ||
                !request.ExpectedStepVersion.HasValue) throw Error("MSG04");
            var dossier = await db.TreatmentDossiers.SingleAsync(x => x.Id == row.DossierId, ct);
            if (request.StepOutcome == "Completed")
                await progression.CompleteStepAsync(actorId, dossier.Id, row.StepId.Value,
                    new CompleteStepRequest(dossier.Version, request.ExpectedStepVersion.Value,
                        [row.Id], Guid.NewGuid()), ct);
            else
                await progression.ProgressAsync(actorId, dossier.Id, row.StepId.Value,
                    new StepProgressRequest(dossier.Version, request.ExpectedStepVersion.Value,
                        "Partial", "Đã ghi hoạt động thực tế", Guid.NewGuid()), ct);
        }
        db.TreatmentProgressionEvents.Add(new TreatmentProgressionEvent
        {
            AggregateId = row.Id, Kind = "clinical.activity.posted",
            StableKey = $"clinical-activity-posted:{row.Id}", ActorId = actorId,
            OccurredAt = row.PostedAt.Value,
            PayloadJson = JsonSerializer.Serialize(new
            {
                activityId = row.Id, activityVersion = row.Version, row.VisitId,
                row.PatientId, row.BranchId, row.DossierId, row.ItemId, row.StepId,
                row.PlanVersion, row.ChargingBasis, row.Quantity, row.Unit
            }, Json)
        });
        Receipt(actorId, request.RequestKey, "ActivityPost", new { id, request }, id);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(row);
    }

    public async Task<ReviewDto> CreateReviewAsync(int actorId, CreateReviewRequest request,
        CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty) throw Error("MSG04");
        await using var gate = await ClinicalRequestLock.AcquireAsync(db, request.RequestKey, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "ReviewCreate", request, ct);
        if (replay.HasValue) return Map(await db.ClinicalReviews.AsNoTracking().SingleAsync(x => x.Id == replay, ct));
        var visit = await VisitAsync(actorId, request.VisitId, request.ExpectedVisitVersion, ct);
        if (Clean(request.Complaint) is not { Length: <= 4000 } ||
            Clean(request.Findings) is not { Length: <= 4000 } ||
            request.Coverage is not ("PendingAssessment" or "Covered" or "NotCovered") ||
            request.Outcome is not ("AdviceOnly" or "FurtherAssessment" or "CorrectiveCareNeeded" or "NewProblem") ||
            request.SourceActivityId.HasValue == request.SourceItemId.HasValue) throw Error("MSG04");
        if (request.Coverage != "PendingAssessment" &&
            (Clean(request.Basis) is not { Length: <= 1000 } ||
             Clean(request.CoveredComponent)?.Length > 500 || Clean(request.Exclusions)?.Length > 1000))
            throw Error("MSG244", 409);
        if (request.SourceActivityId.HasValue)
        {
            var source = await db.ClinicalActivities.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == request.SourceActivityId && x.State == "Posted", ct);
            if (source == null || source.PatientId != visit.PatientId ||
                source.BranchId != visit.BranchId) throw Error("MSG113", 404);
            if (source.Version != request.ExpectedSourceVersion) throw Error("MSG111", 409);
        }
        else
        {
            var source = await db.TreatmentItems.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == request.SourceItemId, ct);
            if (source == null || source.Progress != "Completed" ||
                !await db.TreatmentDossiers.AnyAsync(x => x.Id == source.DossierId &&
                    x.PatientId == visit.PatientId && x.BranchId == visit.BranchId, ct))
                throw Error("MSG113", 404);
            var sourceVersion = source.DecisionPlanVersion ?? await db.TreatmentDossiers.AsNoTracking()
                .Where(x => x.Id == source.DossierId).Select(x => x.PlanVersion).SingleAsync(ct);
            if (request.ExpectedSourceVersion != sourceVersion) throw Error("MSG111", 409);
        }
        var row = new ClinicalReview
        {
            VisitId = request.VisitId, PatientId = visit.PatientId, BranchId = visit.BranchId,
            SourceActivityId = request.SourceActivityId, SourceItemId = request.SourceItemId,
            Complaint = request.Complaint.Trim(), Findings = request.Findings.Trim(),
            Coverage = request.Coverage, Outcome = request.Outcome,
            CoveredComponent = Clean(request.CoveredComponent), Exclusions = Clean(request.Exclusions),
            Policy = Clean(request.Policy), PolicyVersion = Clean(request.PolicyVersion),
            Basis = Clean(request.Basis), NextAction = Clean(request.NextAction),
            DentistAccountId = actorId, RecordedAt = clock.GetUtcNow()
        };
        db.ClinicalReviews.Add(row);
        Receipt(actorId, request.RequestKey, "ReviewCreate", request, row.Id);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<IReadOnlyList<ReviewDto>> ReviewsAsync(int actorId, Guid visitId, CancellationToken ct)
    {
        await VisitAsync(actorId, visitId, 0, ct);
        return (await db.ClinicalReviews.AsNoTracking().Where(x => x.VisitId == visitId)
            .OrderByDescending(x => x.RecordedAt).ToListAsync(ct)).Select(Map).ToArray();
    }

    public async Task<ReviewDto> ReviewAsync(int actorId, Guid id, CancellationToken ct)
    {
        var row = await db.ClinicalReviews.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw Error("MSG113", 404);
        await DentistAsync(actorId, row.BranchId, ct);
        return Map(row);
    }

    public async Task<CareNoteDto> AddCareNoteAsync(int actorId, CreateCareNoteRequest request,
        CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty) throw Error("MSG04");
        await using var gate = await ClinicalRequestLock.AcquireAsync(db, request.RequestKey, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "CareNoteCreate", request, ct);
        if (replay.HasValue) return Map(await db.ClinicalCareNotes.AsNoTracking().SingleAsync(x => x.Id == replay, ct));
        var visit = await VisitAsync(actorId, request.VisitId, request.ExpectedVisitVersion, ct);
        if (Clean(request.Content) is not { Length: <= 4000 } || Clean(request.Response)?.Length > 4000)
            throw Error("MSG04");
        if (request.SourceActivityId.HasValue && !await db.ClinicalActivities.AsNoTracking().AnyAsync(x =>
            x.Id == request.SourceActivityId && x.PatientId == visit.PatientId && x.State == "Posted", ct))
            throw Error("MSG113", 404);
        if (request.ReviewId.HasValue && !await db.ClinicalReviews.AsNoTracking().AnyAsync(x =>
            x.Id == request.ReviewId && x.PatientId == visit.PatientId, ct)) throw Error("MSG113", 404);
        var row = new ClinicalCareNote
        {
            VisitId = request.VisitId, PatientId = visit.PatientId, BranchId = visit.BranchId,
            SourceActivityId = request.SourceActivityId, ReviewId = request.ReviewId,
            Content = request.Content.Trim(), Response = Clean(request.Response), ActorId = actorId,
            RecordedAt = clock.GetUtcNow()
        };
        db.ClinicalCareNotes.Add(row);
        Receipt(actorId, request.RequestKey, "CareNoteCreate", request, row.Id);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }
}

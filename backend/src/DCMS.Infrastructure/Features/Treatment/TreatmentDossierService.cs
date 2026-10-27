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

public sealed partial class TreatmentDossierService(DcmsDbContext db, UserManager<ApplicationUser> users,
    TimeProvider clock, ITreatmentFinancialSummaryPort financialPort, IClinicalWorkService clinicalWork) : ITreatmentDossierService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static BusinessException Error(string code, int status = 400) =>
        new(status, SystemMessages.Get(code));
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static long Gross(TreatmentItem item) => TreatmentPlanRules.Gross(item.Quantity, item.AgreedUnitPriceVnd);

    private static long DiscountAmount(TreatmentItem item)
    {
        if (item.DiscountStatus != "Approved" || item.DiscountValue is null) return 0;
        var gross = Gross(item);
        return TreatmentPlanRules.Discount(gross, item.DiscountType!, item.DiscountValue.Value);
    }

    private static string DiscountBasis(TreatmentItem item, TreatmentItem? qualifying) => Hash(new
    {
        item.ServiceId, item.Quantity, item.AgreedUnitPriceVnd, item.DiscountType,
        item.DiscountValue, item.DiscountQualifyingItemId,
        QualifyingServiceId = qualifying?.ServiceId, QualifyingDecision = qualifying?.Decision,
        QualifyingQuantity = qualifying?.Quantity
    });

    private static PlanPricePreviewDto PricePreview(TreatmentDossier dossier)
    {
        var lines = dossier.Items.Where(x => !x.SheetDeleted && !x.HiddenFromSheet).OrderBy(x => x.PlannedOrder).Select(x =>
        {
            var gross = Gross(x);
            var discount = DiscountAmount(x);
            return new PlanPricePreviewItem(x.Id, gross, discount, gross - discount,
                x.ReferenceUnitPriceVnd, x.Decision, x.Decision == "Agreed" || x.EntrySource == "ClinicianEntry");
        }).ToArray();
        return new(dossier.Id, dossier.Version, dossier.PlanVersion, lines,
            checked(lines.Where(x => x.IncludedInTotal).Sum(x => x.GrossVnd)),
            checked(lines.Where(x => x.IncludedInTotal).Sum(x => x.ApprovedDiscountVnd)),
            checked(lines.Where(x => x.IncludedInTotal).Sum(x => x.NetVnd)));
    }

    private async Task CommercialAsync(int actorId, int branchId, CancellationToken ct)
    {
        var account = await users.FindByIdAsync(actorId.ToString());
        if (account is not { IsActive: true, AccountType: "Staff" }) throw Error("MSG06", 403);
        var roles = AccountRoles.Effective(await users.GetRolesAsync(account));
        if (!roles.Contains(AccountRoles.Admin) && !roles.Contains(AccountRoles.ClinicOwner)
            && !roles.Contains(AccountRoles.Dentist))
            throw Error("MSG06", 403);
        if (roles.Contains(AccountRoles.Admin)) return;
        var isOwner = roles.Contains(AccountRoles.ClinicOwner);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        if (employee == null || !await db.Set<SchedulingMember>().AsNoTracking().AnyAsync(x =>
            x.BranchId == branchId && x.EmployeeId == employee.Id && x.Active &&
            (isOwner || x.CanDentist), ct))
            throw Error("MSG06", 403);
    }

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
        if (dossier.State == "Deleted") throw Error("MSG113", 404);
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
        var actorIds = events.Select(x => x.ActorId).Distinct().ToArray();
        var actorNames = await db.Users.AsNoTracking().Where(x => actorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var terms = JsonSerializer.Deserialize<PaymentTermDto[]>(dossier.PaymentTermsJson, Json) ?? [];
        var finance = await financialPort.ReadAsync(dossier.PatientId, dossier.BranchId, dossier.Id, ct);
        var pendingFeeCount = await db.ClinicalActivities.CountAsync(x => x.DossierId == dossier.Id && x.State == "Posted" && x.FeeStatus == "Pending", ct);
        var planned = PricePreview(dossier).NetBeforeTaxVnd;
        finance = finance is { Available: true }
            ? finance with { PlannedVnd = planned, PendingFeeCount = pendingFeeCount }
            : new TreatmentFinancialSummary(false, planned, null, null, null, null, null, PendingFeeCount: pendingFeeCount);
        return new(dossier.Id, dossier.Code, dossier.PatientId, dossier.BranchId, dossier.DentistId,
            dossier.StartedOn, dossier.Diagnosis, dossier.Note, dossier.State, dossier.ClosureOutcome,
            dossier.ClosureReason, dossier.Version, dossier.PlanId, dossier.PlanVersion, dossier.PlanState,
            dossier.Goals, dossier.Items.OrderBy(x => x.PlannedOrder).Select(ItemDto).ToArray(), terms,
            events.Select(x => new TreatmentEventDto(x.Id, x.Kind, x.DossierVersion, x.ActorId,
                x.OccurredAt, x.PayloadJson, actorNames.GetValueOrDefault(x.ActorId))).ToArray(), finance.Available, dossier.CreatedAt, dossier.UpdatedAt,
            finance, dossier.DepositExceptionReason, dossier.DepositExceptionBy,
            dossier.DepositExceptionAt, dossier.State == "Completed", dossier.SourceExaminationId, dossier.SourceExaminationVersion);
    }

    private static TreatmentItemDto ItemDto(TreatmentItem x) => new(x.Id, x.SourceDiagnosisId, x.ServiceId,
        x.ServiceName, x.Region, x.Unit, x.Quantity, x.PlannedOrder, x.EstimatedMinutes, x.EstimatedStart,
        x.EstimatedEnd, x.AgreedUnitPriceVnd, x.Decision, x.Progress, x.Counseling, x.DecisionMaker,
        x.RepresentativeBasis, x.DecidedAt, x.ConsentDocumentId, x.ConsentPlanVersion,
        x.WarrantyPolicy, x.WarrantyPolicyVersion, x.WarrantyScope, x.WarrantyConfirmedAt, x.WarrantyEndsOn,
        x.Material, x.DecisionPlanVersion, x.EstimatedMinMinutes, x.EstimatedMaxMinutes,
        x.ReservedMinutes, x.EstimatedSessions, x.ReturnEarliestOn, x.ReturnTargetOn,
        x.ReturnLatestOn, x.ShortReservationAcknowledgedBy, x.ShortReservationAcknowledgedAt,
        x.ReferenceUnitPriceVnd, x.ReferencePriceVersion,
        x.DiscountType == null || x.DiscountValue == null || x.DiscountReason == null || x.DiscountStatus == null
            ? null : new TreatmentDiscountDto(x.DiscountType, x.DiscountValue.Value, x.DiscountReason,
                x.DiscountQualifyingItemId, x.DiscountStatus, x.DiscountDecidedBy,
                x.DiscountDecidedAt, DiscountAmount(x)),
        x.WarrantyComponent, x.WarrantyExclusions, x.WarrantyActivatedOn, x.WarrantyExplanation,
        x.ExpectedServiceLifeMonths, x.Note, x.ResponsibleDentistId, x.AssistantId, x.EntrySource, x.DisplayCompleted, x.SheetDeleted, x.HiddenFromSheet);

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
        var query = db.TreatmentDossiers.AsNoTracking().Where(x => x.PatientId == patientId
            && x.State != "Deleted" && allowed.Contains(x.BranchId));
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId.Value);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(x => new DossierListItem(x.Id, x.Code, x.PatientId, x.BranchId,
            x.DentistId, x.Diagnosis, x.State, x.ClosureOutcome, x.Version, x.PlanVersion, x.UpdatedAt,
            x.State == "Completed"))
            .ToArray(), total, page, pageSize);
    }

    public async Task<TreatmentDossierDto> GetAsync(int actorId, Guid dossierId, CancellationToken ct) =>
        await DetailAsync(await DossierAsync(actorId, dossierId, ct), ct);

    public async Task<TreatmentChange> SetDisplayStatusAsync(int actorId, Guid dossierId,
        SetDossierDisplayStatusRequest request, CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
        // Compatibility endpoint: a display flag cannot close or reopen a clinical course.
        if (request.Completed != (dossier.State == "Completed")) throw Error("MSG243", 409);
        return new(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "trạng thái hồ sơ", value: dossier.Code));
    }

    public async Task<TreatmentChange> UpdateNoteAsync(int actorId, Guid dossierId,
        UpdateDossierNoteRequest request, CancellationToken ct)
    {
        var note = Clean(request.Note);
        if (note?.Length > 4000) throw Error("MSG04");
        var fingerprint = new { dossierId, request };
        var replay = await ReplayAsync(actorId, request.RequestKey, "DossierNote", fingerprint, ct);
        if (replay != null) return replay;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var dossier = await DossierAsync(actorId, dossierId, ct);
        if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
        if (dossier.Note != note)
        {
            var before = dossier.Note;
            dossier.Note = note;
            dossier.Version++;
            dossier.UpdatedAt = clock.GetUtcNow();
            Event(dossier, actorId, "DossierNoteUpdated", new { Before = before, After = note });
            await db.SaveChangesAsync(ct);
        }
        var result = new TreatmentChange(await DetailAsync(dossier, ct),
            SystemMessages.Get("MSG222", "ghi chú phiếu điều trị", value: dossier.Code));
        StoreReceipt(actorId, request.RequestKey, "DossierNote", fingerprint, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private static SheetDraftDto SheetDraftDto(TreatmentSheetDraft row) => new(row.Id, row.Kind,
        row.VisitId, JsonSerializer.Deserialize<JsonElement>(row.PayloadJson), row.Version, row.UpdatedAt);

    public async Task<IReadOnlyList<SheetDraftDto>> SheetDraftsAsync(int actorId, Guid dossierId,
        CancellationToken ct)
    {
        await DossierAsync(actorId, dossierId, ct);
        var rows = await db.TreatmentSheetDrafts.AsNoTracking().Where(x => x.DossierId == dossierId
            && x.ActorId == actorId)
            .OrderBy(x => x.UpdatedAt).ToListAsync(ct);
        return rows.Select(SheetDraftDto).ToArray();
    }

    public async Task<SheetDraftDto> SaveSheetDraftAsync(int actorId, Guid dossierId, Guid rowId,
        SaveSheetDraftRequest request, CancellationToken ct)
    {
        if (rowId == Guid.Empty || request.RequestKey == Guid.Empty || request.Kind is not ("Plan" or "Actual")
            || request.Row.ValueKind != JsonValueKind.Object) throw Error("MSG04");
        var row = request.Row;
        var typed = ReadRow(row);
        if (request.Kind == "Plan" && request.VisitId.HasValue) throw Error("MSG04");
        await using var gate = await ClinicalRequestLock.AcquireAsync(db, request.RequestKey, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({DCMS.Infrastructure.Features.Scheduling.WorkingScheduleService.TransactionLock})", ct);
        var dossier = await DossierAsync(actorId, dossierId, ct);
        if (await db.ClinicalActivities.AnyAsync(x => x.Id == rowId, ct)
            || request.Kind == "Plan" && await db.TreatmentItems.AnyAsync(x => x.Id == rowId, ct)) throw Error("MSG111", 409);
        if (dossier.State != "Open") throw Error("MSG240", 409);
        var serviceId = typed.ServiceId;
        var dentistId = typed.DentistId;
        if (!await db.Set<SchedulingService>().AsNoTracking().AnyAsync(x => x.Id == serviceId
            && x.BranchId == dossier.BranchId && x.Active, ct)
            || !await db.Set<SchedulingMember>().AsNoTracking().AnyAsync(x => x.EmployeeId == dentistId
                && x.BranchId == dossier.BranchId && x.Active && x.CanDentist, ct)) throw Error("MSG04");
        if (request.VisitId.HasValue && !await db.Visits.AsNoTracking().AnyAsync(x =>
            x.Id == request.VisitId.Value && x.PatientId == dossier.PatientId
            && x.BranchId == dossier.BranchId, ct)) throw Error("MSG113", 404);
        var fingerprint = Hash(new { dossierId, rowId, request });
        var existing = await db.TreatmentSheetDrafts.SingleOrDefaultAsync(x => x.Id == rowId, ct);
        if (existing is not null && existing.LastRequestKey == request.RequestKey)
        {
            if (existing.LastFingerprint != fingerprint) throw Error("MSG111", 409);
            return SheetDraftDto(existing);
        }
        if (existing is null)
        {
            if (request.ExpectedVersion != 0) throw Error("MSG111", 409);
            existing = new TreatmentSheetDraft { Id = rowId, DossierId = dossierId,
                ActorId = actorId, Kind = request.Kind, VisitId = request.VisitId };
            db.TreatmentSheetDrafts.Add(existing);
        }
        else
        {
            if (existing.DossierId != dossierId || existing.ActorId != actorId
                || existing.Kind != request.Kind || existing.Version != request.ExpectedVersion)
                throw Error("MSG111", 409);
            existing.Version++;
            existing.VisitId = request.VisitId;
        }
        existing.PayloadJson = row.GetRawText();
        existing.LastRequestKey = request.RequestKey;
        existing.LastFingerprint = fingerprint;
        existing.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return SheetDraftDto(existing);
    }

    public async Task DeleteSheetDraftAsync(int actorId, Guid dossierId, Guid rowId,
        int expectedVersion, CancellationToken ct)
    {
        await DossierAsync(actorId, dossierId, ct);
        var row = await db.TreatmentSheetDrafts.SingleOrDefaultAsync(x => x.Id == rowId
            && x.DossierId == dossierId, ct) ?? throw Error("MSG113", 404);
        if (row.ActorId != actorId) throw Error("MSG06", 403);
        if (row.Version != expectedVersion) throw Error("MSG111", 409);
        db.TreatmentSheetDrafts.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    public async Task<TreatmentChange> DeleteAsync(int actorId, Guid dossierId, DeleteDossierRequest request,
        CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty) throw Error("MSG04");
        var fingerprint = new { dossierId, request };
        var replay = await ReplayAsync(actorId, request.RequestKey, "Delete", fingerprint, ct);
        if (replay != null) return replay;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var dossier = await DossierAsync(actorId, dossierId, ct);
        if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
        if (dossier.State != "Open" || dossier.PlanState != "None" || dossier.PlanVersion != 0
            || dossier.Items.Count != 0 || dossier.Goals != null || dossier.PaymentTermsJson != "[]"
            || dossier.DepositExceptionReason != null
            || await db.TreatmentEvents.AnyAsync(x => x.DossierId == dossierId && x.Kind != "DossierCreated", ct)
            || await db.TreatmentSteps.AnyAsync(x => x.DossierId == dossierId, ct)
            || await db.FollowUpInstructions.AnyAsync(x => x.DossierId == dossierId, ct)
            || await db.ClinicalActivities.AnyAsync(x => x.DossierId == dossierId, ct)
            || await db.TreatmentRevisionDrafts.AnyAsync(x => x.DossierId == dossierId, ct)
            || await db.TreatmentSheetDrafts.AnyAsync(x => x.DossierId == dossierId, ct)
            || await db.Set<DCMS.Domain.Features.Examinations.ClinicalDocument>()
                .AnyAsync(x => x.DossierId == dossierId, ct)) throw Error("MSG233", 409);
        dossier.State = "Deleted";
        dossier.Version++;
        dossier.UpdatedAt = clock.GetUtcNow();
        Event(dossier, actorId, "DossierDeleted", new { dossier.Code });
        await db.SaveChangesAsync(ct);
        var result = new TreatmentChange(await DetailAsync(dossier, ct),
            SystemMessages.Get("MSG222", "xóa phiếu điều trị", value: dossier.Code));
        StoreReceipt(actorId, request.RequestKey, "Delete", fingerprint, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

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
        if (request.SourceVisitId.HasValue)
        {
            var source = await db.Visits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.SourceVisitId
                && x.PatientId == patientId && x.BranchId == request.BranchId, ct) ?? throw Error("MSG113", 404);
            if (source.Version != request.ExpectedSourceVisitVersion) throw Error("MSG111", 409);
            var examination = await db.Set<DCMS.Domain.Features.Examinations.VisitExamination>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.VisitId == source.Id && x.Confirmed, ct) ?? throw Error("MSG240", 409);
            dossier.SourceExaminationId = source.Id; dossier.SourceExaminationVersion = source.Version;
            Event(dossier, actorId, "ExaminationSourceCaptured", new { SourceVisitId = source.Id,
                SourceVisitVersion = source.Version, Content = JsonSerializer.Deserialize<JsonElement>(examination.ContentJson) });
        }
        else if (request.ExpectedSourceVisitVersion.HasValue) throw Error("MSG04");
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
            x.DueOn, Clean(x.Evidence), Clean(x.Milestone), dossier.PlanVersion + 1)).ToArray();
        if (terms.Any(x => x.Purpose.Length is < 1 or > 160 || x.AmountVnd < 0
            || x.DueOn == default || x.Evidence?.Length > 1000 || x.Milestone?.Length > 160)) throw Error("MSG04");
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
                || decimal.Round(input.Quantity * input.AgreedUnitPriceVnd, 0,
                    MidpointRounding.AwayFromZero) > long.MaxValue
                || input.PlannedOrder < 1 || input.EstimatedMinutes is < 1 or > 480
                || !TreatmentPlanRules.ValidDuration(input.EstimatedMinMinutes, input.EstimatedMinutes,
                    input.EstimatedMaxMinutes, input.ReservedMinutes, input.AcknowledgeShortReservation)
                || input.EstimatedSessions is < 1 or > 100
                || !TreatmentPlanRules.ValidReturnWindow(input.ReturnEarliestOn,
                    input.ReturnTargetOn, input.ReturnLatestOn)
                || (input.Discount != null && (input.Discount.Type is not ("Fixed" or "Percent")
                    || input.Discount.Value <= 0 || input.Discount.Type == "Percent" && input.Discount.Value > 100
                    || Clean(input.Discount.Reason) is not { Length: > 0 and <= 1000 }))
                || (input.ClearDiscount && input.Discount != null)
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
            var serviceChanged = !input.Id.HasValue || item.ServiceId != input.ServiceId;
            var reservationChanged = item.EstimatedMaxMinutes != input.EstimatedMaxMinutes
                || item.ReservedMinutes != input.ReservedMinutes;
            item.ServiceId = input.ServiceId;
            if (serviceChanged)
            {
                item.ReferenceUnitPriceVnd = services[input.ServiceId].ReferenceUnitPriceVnd;
                item.ReferencePriceVersion = services[input.ServiceId].PriceVersion;
            }
            item.ServiceName = services[input.ServiceId].Name;
            item.Region = Clean(input.Region);
            item.Unit = input.Unit.Trim();
            item.Quantity = input.Quantity;
            item.PlannedOrder = input.PlannedOrder;
            item.EstimatedMinutes = input.EstimatedMinutes;
            item.EstimatedMinMinutes = input.EstimatedMinMinutes;
            item.EstimatedMaxMinutes = input.EstimatedMaxMinutes;
            item.ReservedMinutes = input.ReservedMinutes;
            item.EstimatedSessions = input.EstimatedSessions;
            item.ReturnEarliestOn = input.ReturnEarliestOn;
            item.ReturnTargetOn = input.ReturnTargetOn;
            item.ReturnLatestOn = input.ReturnLatestOn;
            if (input.EstimatedMaxMinutes.HasValue && input.ReservedMinutes < input.EstimatedMaxMinutes)
            {
                if (item.ShortReservationAcknowledgedBy == null || reservationChanged)
                {
                    item.ShortReservationAcknowledgedBy = actorId;
                    item.ShortReservationAcknowledgedAt = clock.GetUtcNow();
                }
            }
            else
            {
                item.ShortReservationAcknowledgedBy = null;
                item.ShortReservationAcknowledgedAt = null;
            }
            item.EstimatedStart = input.EstimatedStart;
            item.EstimatedEnd = input.EstimatedEnd;
            item.AgreedUnitPriceVnd = input.AgreedUnitPriceVnd;
            if (input.ClearDiscount)
            {
                item.DiscountType = null; item.DiscountValue = null; item.DiscountReason = null;
                item.DiscountQualifyingItemId = null; item.DiscountStatus = null;
                item.DiscountBasisHash = null; item.DiscountDecidedBy = null; item.DiscountDecidedAt = null;
            }
            else if (input.Discount != null && (item.DiscountType != input.Discount.Type
                || item.DiscountValue != input.Discount.Value
                || item.DiscountReason != Clean(input.Discount.Reason)
                || item.DiscountQualifyingItemId != input.Discount.QualifyingItemId))
            {
                item.DiscountType = input.Discount.Type; item.DiscountValue = input.Discount.Value;
                item.DiscountReason = Clean(input.Discount.Reason);
                item.DiscountQualifyingItemId = input.Discount.QualifyingItemId;
                item.DiscountStatus = "Proposed"; item.DiscountDecidedBy = null;
                item.DiscountDecidedAt = null; item.DiscountBasisHash = null;
            }
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
        foreach (var item in dossier.Items.Where(x => x.DiscountType != null))
        {
            var qualifying = item.DiscountQualifyingItemId.HasValue
                ? dossier.Items.SingleOrDefault(x => x.Id == item.DiscountQualifyingItemId.Value) : null;
            if (item.DiscountQualifyingItemId.HasValue && (qualifying == null || qualifying.Id == item.Id))
                throw Error("MSG04");
            if (item.Quantity * item.AgreedUnitPriceVnd > long.MaxValue) throw Error("MSG04");
            var gross = Gross(item);
            try { TreatmentPlanRules.Discount(gross, item.DiscountType!, item.DiscountValue!.Value); }
            catch (ArgumentOutOfRangeException) { throw Error("MSG04"); }
            if (item.DiscountStatus == "Approved" && item.DiscountBasisHash != DiscountBasis(item, qualifying))
            {
                item.DiscountStatus = "Proposed"; item.DiscountDecidedBy = null;
                item.DiscountDecidedAt = null; item.DiscountBasisHash = null;
            }
        }
        decimal totalGross = 0;
        foreach (var item in dossier.Items.Where(x => x.Decision == "Agreed"))
        {
            var gross = decimal.Round(item.Quantity * item.AgreedUnitPriceVnd, 0,
                MidpointRounding.AwayFromZero);
            if (gross > long.MaxValue - totalGross) throw Error("MSG04");
            totalGross += gross;
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
        if (dossier.Items.Any(x => x.DiscountStatus == "Proposed")) throw Error("MSG240");
        if (dossier.Items.Any(x => x.DiscountStatus == "Approved" && x.DiscountQualifyingItemId.HasValue
            && dossier.Items.All(q => q.Id != x.DiscountQualifyingItemId || q.Decision != "Agreed")))
            throw Error("MSG240");
        foreach (var item in dossier.Items.Where(x => x.ConsentDocumentId.HasValue))
            if (!await db.Set<DCMS.Domain.Features.Examinations.ClinicalDocument>().AnyAsync(x => x.Id == item.ConsentDocumentId
                && x.PatientId == dossier.PatientId && x.DossierId == dossier.Id && x.PlanId == dossier.PlanId && x.FileState == "Ready", ct)) throw Error("MSG240");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        dossier.PlanVersion++;
        foreach (var item in dossier.Items.Where(x => x.Decision != "Proposed"))
        {
            item.DecisionPlanVersion = dossier.PlanVersion;
            item.ConsentPlanVersion = item.ConsentDocumentId.HasValue ? dossier.PlanVersion : null;
        }
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
        var component = Clean(request.Component);
        var exclusions = Clean(request.Exclusions);
        var explanation = Clean(request.Explanation);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        if (dossier.PlanState != "Committed" || item.Decision != "Agreed" || item.Progress != "Completed"
            || request.EndsOn is { } endsOn && endsOn < today
            || policy is not { Length: > 0 and <= 160 }
            || version is not { Length: > 0 and <= 40 } || scope is not { Length: > 0 and <= 1000 }
            || component is not { Length: > 0 and <= 500 }
            || exclusions?.Length > 1000 || explanation is not { Length: > 0 and <= 2000 }
            || request.ActivationOn is null || request.ActivationOn > today
            || request.EndsOn < request.ActivationOn
            || request.ExpectedServiceLifeMonths is < 1 or > 1200
            || item.WarrantyConfirmedAt.HasValue) throw Error("MSG244");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        item.WarrantyPolicy = policy;
        item.WarrantyPolicyVersion = version;
        item.WarrantyScope = scope;
        item.WarrantyConfirmedAt = clock.GetUtcNow();
        item.WarrantyConfirmedBy = actorId;
        item.WarrantyEndsOn = request.EndsOn;
        item.WarrantyComponent = component;
        item.WarrantyExclusions = exclusions;
        item.WarrantyActivatedOn = request.ActivationOn;
        item.WarrantyExplanation = explanation;
        item.ExpectedServiceLifeMonths = request.ExpectedServiceLifeMonths;
        dossier.Version++;
        dossier.UpdatedAt = clock.GetUtcNow();
        Event(dossier, actorId, "WarrantyConfirmed", new { itemId, policy, version, scope,
            component, exclusions, explanation, request.ActivationOn, request.EndsOn,
            request.ExpectedServiceLifeMonths });
        await db.SaveChangesAsync(ct);
        var result = new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "bảo hành điều trị", value: dossier.Code));
        StoreReceipt(actorId, request.RequestKey, "Warranty", new { dossierId, itemId, request }, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<PlanPricePreviewDto> PricePreviewAsync(int actorId, Guid dossierId, CancellationToken ct) =>
        PricePreview(await DossierAsync(actorId, dossierId, ct));

    public async Task<CommercialPlanDto> CommercialPlanAsync(int actorId, Guid dossierId, CancellationToken ct)
    {
        var dossier = await db.TreatmentDossiers.AsNoTracking().Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == dossierId, ct) ?? throw Error("MSG113", 404);
        await CommercialAsync(actorId, dossier.BranchId, ct);
        return new(dossier.Id, dossier.Code, dossier.BranchId, dossier.Version, dossier.PlanState,
            dossier.Items.OrderBy(x => x.PlannedOrder).Select(ItemDto).ToArray(),
            dossier.DepositExceptionReason, PricePreview(dossier));
    }

    public async Task<IReadOnlyList<CommercialDossierListItem>> CommercialDossiersAsync(int actorId,
        CancellationToken ct)
    {
        var account = await users.FindByIdAsync(actorId.ToString());
        if (account is not { IsActive: true, AccountType: "Staff" }) throw Error("MSG06", 403);
        var roles = AccountRoles.Effective(await users.GetRolesAsync(account));
        if (!roles.Contains(AccountRoles.Admin) && !roles.Contains(AccountRoles.ClinicOwner)
            && !roles.Contains(AccountRoles.Dentist))
            throw Error("MSG06", 403);
        var query = db.TreatmentDossiers.AsNoTracking().Include(x => x.Items)
            .Where(x => x.State == "Open" && x.PlanState == "Draft");
        if (!roles.Contains(AccountRoles.Admin))
        {
            var isOwner = roles.Contains(AccountRoles.ClinicOwner);
            var employeeId = await db.Employees.AsNoTracking().Where(x => x.AccountId == actorId)
                .Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
            if (employeeId == null) throw Error("MSG06", 403);
            var branches = db.Set<SchedulingMember>().AsNoTracking().Where(x =>
                x.EmployeeId == employeeId.Value && x.Active &&
                (isOwner || x.CanDentist)).Select(x => x.BranchId);
            query = query.Where(x => branches.Contains(x.BranchId));
        }
        var dossiers = await query.OrderByDescending(x => x.UpdatedAt).Take(100).ToListAsync(ct);
        var result = new List<CommercialDossierListItem>();
        foreach (var dossier in dossiers)
        {
            result.Add(new(dossier.Id, dossier.Code, dossier.BranchId, dossier.Version,
                dossier.Items.Count(x => x.DiscountStatus == "Proposed")));
        }
        return result;
    }

    public async Task<TreatmentChange> DecideDiscountAsync(int actorId, Guid dossierId, Guid itemId,
        DecideDiscountRequest request, CancellationToken ct)
    {
        var dossier = await db.TreatmentDossiers.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == dossierId, ct)
            ?? throw Error("MSG113", 404);
        await CommercialAsync(actorId, dossier.BranchId, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "Discount", new { dossierId, itemId, request }, ct);
        if (replay != null) return replay;
        if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
        var item = dossier.Items.SingleOrDefault(x => x.Id == itemId) ?? throw Error("MSG113", 404);
        if (dossier.State != "Open" || dossier.PlanState != "Draft" || item.DiscountStatus != "Proposed"
            || request.Decision is not ("Approved" or "Rejected")) throw Error("MSG240", 409);
        var qualifying = item.DiscountQualifyingItemId.HasValue
            ? dossier.Items.SingleOrDefault(x => x.Id == item.DiscountQualifyingItemId.Value) : null;
        if (item.Decision != "Agreed" || item.DiscountQualifyingItemId.HasValue &&
            (qualifying == null || qualifying.Decision != "Agreed"))
            throw Error("MSG240", 409);
        if (item.DiscountValue is null || item.DiscountValue <= 0 ||
            item.DiscountType is not ("Fixed" or "Percent") ||
            item.DiscountType == "Percent" && item.DiscountValue > 100 ||
            (item.DiscountType == "Fixed" ? item.DiscountValue : Gross(item) * item.DiscountValue / 100m) > Gross(item))
            throw Error("MSG04");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        item.DiscountStatus = request.Decision;
        item.DiscountBasisHash = request.Decision == "Approved" ? DiscountBasis(item, qualifying) : null;
        item.DiscountDecidedBy = actorId;
        item.DiscountDecidedAt = clock.GetUtcNow();
        dossier.Version++;
        dossier.UpdatedAt = clock.GetUtcNow();
        Event(dossier, actorId, "DiscountDecision", new { itemId, request.Decision,
            item.DiscountType, item.DiscountValue, item.DiscountReason, item.DiscountQualifyingItemId });
        await db.SaveChangesAsync(ct);
        var result = new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "giảm trừ", value: dossier.Code));
        StoreReceipt(actorId, request.RequestKey, "Discount", new { dossierId, itemId, request }, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<TreatmentChange> SetDepositExceptionAsync(int actorId, Guid dossierId,
        DepositExceptionRequest request, CancellationToken ct)
    {
        var dossier = await db.TreatmentDossiers.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == dossierId, ct)
            ?? throw Error("MSG113", 404);
        await CommercialAsync(actorId, dossier.BranchId, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "Deposit", new { dossierId, request }, ct);
        if (replay != null) return replay;
        if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
        var reason = Clean(request.Reason);
        if (dossier.State != "Open" || dossier.PlanState == "Committed" || reason is not { Length: > 0 and <= 1000 })
            throw Error("MSG04");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        dossier.DepositExceptionReason = reason;
        dossier.DepositExceptionBy = actorId;
        dossier.DepositExceptionAt = clock.GetUtcNow();
        dossier.Version++;
        dossier.UpdatedAt = clock.GetUtcNow();
        Event(dossier, actorId, "DepositException", new { reason });
        await db.SaveChangesAsync(ct);
        var result = new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "ngoại lệ cọc", value: dossier.Code));
        StoreReceipt(actorId, request.RequestKey, "Deposit", new { dossierId, request }, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<IReadOnlyList<OwnTreatmentSummaryItem>> OwnSummaryAsync(int accountId, CancellationToken ct)
    {
        var patientIds = db.PatientAccountLinks.AsNoTracking().Where(x => x.AccountId == accountId
            && x.Status == PatientLinkStatus.Verified).Select(x => x.PatientId);
        var dossiers = await db.TreatmentDossiers.AsNoTracking().Include(x => x.Items)
            .Where(x => patientIds.Contains(x.PatientId) && x.PlanState == "Committed" && x.State != "Deleted")
            .OrderByDescending(x => x.StartedOn).ToListAsync(ct);
        return dossiers.Select(x => new OwnTreatmentSummaryItem(x.Id, x.Code, x.State, x.StartedOn,
            x.Diagnosis, x.PlanVersion, x.Items.Where(item => item.Decision == "Agreed")
                .OrderBy(item => item.PlannedOrder).Select(ItemDto).ToArray())).ToArray();
    }
}

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
    TimeProvider clock, ITreatmentFinancialSummaryPort financialPort, IClinicalWorkService clinicalWork, DCMS.Application.Features.Settings.ISettingsAccess access) : ITreatmentDossierService
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
        if (item.DiscountType is not ("Fixed" or "Percent") || item.DiscountValue is not > 0) return 0;
        var gross = Gross(item);
        return TreatmentPlanRules.Discount(gross, item.DiscountType, item.DiscountValue.Value);
    }

    private async Task<int> CurrentPlanVersionAsync(Guid dossierId, bool hasItems, CancellationToken ct)
    {
        var count = await db.TreatmentEvents.AsNoTracking().CountAsync(x => x.DossierId == dossierId && x.Kind == "PlanCommitted", ct);
        return count > 0 ? count : (hasItems ? 1 : 0);
    }

    private static PlanPricePreviewDto PricePreview(TreatmentDossier dossier, int planVersion)
    {
        var lines = dossier.Items.OrderBy(x => x.PlannedOrder).Select(x =>
        {
            var gross = Gross(x);
            var discount = DiscountAmount(x);
            return new PlanPricePreviewItem(x.Id, gross, discount, gross - discount);
        }).ToArray();
        return new(dossier.Id, dossier.Version, planVersion, lines,
            checked(lines.Sum(x => x.GrossVnd)),
            checked(lines.Sum(x => x.ApprovedDiscountVnd)),
            checked(lines.Sum(x => x.NetVnd)));
    }

    private async Task<int> DentistAsync(int actorId, int branchId, CancellationToken ct)
    {
        var scope = await access.ResolveAsync(actorId, ct);
        if (scope.Assigned && !scope.Owner && !scope.BranchIds.Contains(branchId)) throw Error("MSG05", 403);
        var account = await users.FindByIdAsync(actorId.ToString());
        if (account == null || !account.IsActive || account.AccountType != "Staff") throw Error("MSG05", 403);
        var roles = AccountRoles.Effective(await users.GetRolesAsync(account));
        // Staff in an access group act through treatment.* grants within assignment branches (checked above), not their role.
        if (!scope.GrantDriven && !roles.Contains(AccountRoles.Dentist)) throw Error("MSG05", 403);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        if (employee == null || (scope.Owner || scope.GrantDriven ? !scope.BranchIds.Contains(branchId) : !await db.SchedulingMembers.AsNoTracking().AnyAsync(x =>
                x.BranchId == branchId && x.EmployeeId == employee.Id && x.Active && x.CanDentist, ct)))
            throw Error("MSG05", 403);
        return employee.Id;
    }

    private async Task<TreatmentDossier> DossierAsync(int actorId, Guid id, CancellationToken ct)
    {
        var dossier = await db.TreatmentDossiers.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw Error("MSG42", 404);
        await DentistAsync(actorId, dossier.BranchId, ct);
        if (dossier.State == "Deleted") throw Error("MSG42", 404);
        return dossier;
    }

    private async Task<TreatmentChange?> ReplayAsync(int actorId, Guid key, string command, object request, CancellationToken ct)
    {
        if (key == Guid.Empty) throw Error("MSG04");
        var receipt = await db.TreatmentReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (receipt == null) return null;
        if (receipt.ActorId != actorId || receipt.Command != command || receipt.Fingerprint != Hash(request))
            throw Error("MSG41", 409);
        return JsonSerializer.Deserialize<TreatmentChange>(receipt.ResultJson, Json) ?? throw Error("MSG49", 503);
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
        var finance = await financialPort.ReadAsync(dossier.PatientId, dossier.BranchId, dossier.Id, ct);
        var pendingFeeCount = await db.ClinicalActivities.CountAsync(x => x.DossierId == dossier.Id && x.FeeStatus == "Pending", ct);
        var commitCount = events.Count(x => x.Kind == "PlanCommitted");
        var planVersion = commitCount > 0 ? commitCount : (dossier.Items.Count > 0 ? 1 : 0);
        var planned = PricePreview(dossier, planVersion).NetBeforeTaxVnd;
        finance = finance is { Available: true }
            ? finance with { PlannedVnd = planned, PendingFeeCount = pendingFeeCount }
            : new TreatmentFinancialSummary(false, planned, null, null, null, null, null, PendingFeeCount: pendingFeeCount);
        return new(dossier.Id, dossier.Code, dossier.PatientId, dossier.BranchId, dossier.DentistId,
            dossier.StartedOn, dossier.Diagnosis, dossier.Note, dossier.State, dossier.Version, planVersion,
            dossier.Items.OrderBy(x => x.PlannedOrder).Select(ItemDto).ToArray(),
            events.Select(x => new TreatmentEventDto(x.Id, x.Kind, x.DossierVersion, x.ActorId,
                x.OccurredAt, x.PayloadJson, actorNames.GetValueOrDefault(x.ActorId))).ToArray(), finance.Available, dossier.CreatedAt, dossier.UpdatedAt,
            finance, dossier.SourceExaminationVisitId);
    }

    private static TreatmentItemDto ItemDto(TreatmentItem x) => new(x.Id, x.ServiceId,
        x.ServiceName, x.Region, x.Unit, x.Quantity, x.PlannedOrder, x.EstimatedMinutes, x.EstimatedStart,
        x.AgreedUnitPriceVnd,
        x.DiscountType == null || x.DiscountValue is not > 0 || x.DiscountReason == null
            ? null : new TreatmentDiscountDto(x.DiscountType, x.DiscountValue.Value, x.DiscountReason, DiscountAmount(x)),
        x.Note, x.ResponsibleDentistId, x.AssistantId, x.DisplayCompleted);

    public async Task<DossierPage> ListAsync(int actorId, int patientId, int? branchId, int page, int pageSize,
        CancellationToken ct)
    {
        if (page < 1 || pageSize is not (20 or 50 or 100) || page > int.MaxValue / pageSize) throw Error("MSG04");
        if (!await db.Patients.AsNoTracking().AnyAsync(x => x.Id == patientId, ct)) throw Error("MSG42", 404);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        if (employee == null) throw Error("MSG05", 403);
        var scope = await access.ResolveAsync(actorId, ct);
        if (branchId.HasValue) await DentistAsync(actorId, branchId.Value, ct);
        else
        {
            var account = await users.FindByIdAsync(actorId.ToString());
            if (account == null || account.AccountType != "Staff" || !account.IsActive ||
                !scope.GrantDriven && !AccountRoles.Effective(await users.GetRolesAsync(account)).Contains(AccountRoles.Dentist))
                throw Error("MSG05", 403);
        }
        var allowed = db.SchedulingMembers.AsNoTracking().Where(x =>
            x.EmployeeId == employee.Id && x.Active && x.CanDentist).Select(x => x.BranchId);
        var query = db.TreatmentDossiers.AsNoTracking().Where(x => x.PatientId == patientId
            && x.State != "Deleted" && (scope.Owner || scope.GrantDriven ? scope.BranchIds.Contains(x.BranchId) : allowed.Contains(x.BranchId)));
        if (scope.Assigned && !scope.Owner) query = query.Where(x => scope.BranchIds.Contains(x.BranchId));
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId.Value);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(x => new DossierListItem(x.Id, x.Code, x.PatientId, x.BranchId,
            x.DentistId, x.Diagnosis, x.State, x.Version, x.UpdatedAt))
            .ToArray(), total, page, pageSize);
    }

    public async Task<TreatmentDossierDto> GetAsync(int actorId, Guid dossierId, CancellationToken ct) =>
        await DetailAsync(await DossierAsync(actorId, dossierId, ct), ct);

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
        if (dossier.Version != request.ExpectedVersion) throw Error("MSG41", 409);
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
            SystemMessages.Get("MSG53", "ghi chú phiếu điều trị", value: dossier.Code));
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
            || request.Kind == "Plan" && await db.TreatmentItems.AnyAsync(x => x.Id == rowId, ct)) throw Error("MSG41", 409);
        if (dossier.State != "Open") throw Error("MSG57", 409);
        var serviceId = typed.ServiceId;
        var dentistId = typed.DentistId;
        if (!await db.Set<SchedulingService>().AsNoTracking().AnyAsync(x => x.Id == serviceId
            && x.BranchId == dossier.BranchId && x.Active, ct)
            || !await db.SchedulingMembers.AsNoTracking().AnyAsync(x => x.EmployeeId == dentistId
                && x.BranchId == dossier.BranchId && x.Active && x.CanDentist, ct)) throw Error("MSG04");
        if (request.VisitId.HasValue && !await db.Visits.AsNoTracking().AnyAsync(x =>
            x.Id == request.VisitId.Value && x.PatientId == dossier.PatientId
            && x.BranchId == dossier.BranchId, ct)) throw Error("MSG42", 404);
        var fingerprint = Hash(new { dossierId, rowId, request });
        var existing = await db.LoadSheetDraftAsync(db.TreatmentSheetDrafts.Where(x => x.Id == rowId), ct);
        if (existing is not null && existing.LastRequestKey == request.RequestKey)
        {
            if (existing.LastFingerprint != fingerprint) throw Error("MSG41", 409);
            return SheetDraftDto(existing);
        }
        if (existing is null)
        {
            if (request.ExpectedVersion != 0) throw Error("MSG41", 409);
            existing = new TreatmentSheetDraft { Id = rowId, DossierId = dossierId,
                ActorId = actorId, Kind = request.Kind, VisitId = request.VisitId };
            await db.AddTreatmentDataAsync(existing);
        }
        else
        {
            if (existing.DossierId != dossierId || existing.ActorId != actorId
                || existing.Kind != request.Kind || existing.Version != request.ExpectedVersion)
                throw Error("MSG41", 409);
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
        var row = await db.LoadSheetDraftAsync(db.TreatmentSheetDrafts.Where(x => x.Id == rowId
            && x.DossierId == dossierId), ct) ?? throw Error("MSG42", 404);
        if (row.ActorId != actorId) throw Error("MSG05", 403);
        if (row.Version != expectedVersion) throw Error("MSG41", 409);
        await db.RemoveTreatmentDataAsync(row);
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
        if (dossier.Version != request.ExpectedVersion) throw Error("MSG41", 409);
        // A paid deposit is an accounting record even when the clinical sheet is otherwise empty.
        // Keep the sheet so its receipt remains visible in finance and can be reconciled.
        if (await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM "AUDIT_LOG"
            WHERE "AuditType" = 'FinanceLedger' AND "Target" = {dossierId.ToString()}
            LIMIT 1
            """).AnyAsync(ct)) throw Error("MSG55", 409);
        if (dossier.State != "Open" || dossier.Items.Count != 0
            || await db.TreatmentEvents.AnyAsync(x => x.DossierId == dossierId && x.Kind != "DossierCreated", ct)
            || await db.Appointments.AnyAsync(x => x.DossierId == dossierId, ct)
            || await db.ClinicalActivities.AnyAsync(x => x.DossierId == dossierId, ct)
            || await db.TreatmentSheetDrafts.AnyAsync(x => x.DossierId == dossierId, ct)
            || await db.Set<DCMS.Domain.Features.Examinations.ClinicalDocument>()
                .AnyAsync(x => x.DossierId == dossierId, ct)) throw Error("MSG55", 409);
        dossier.State = "Deleted";
        dossier.Version++;
        dossier.UpdatedAt = clock.GetUtcNow();
        Event(dossier, actorId, "DossierDeleted", new { dossier.Code });
        await db.SaveChangesAsync(ct);
        var result = new TreatmentChange(await DetailAsync(dossier, ct),
            SystemMessages.Get("MSG53", "xóa phiếu điều trị", value: dossier.Code));
        StoreReceipt(actorId, request.RequestKey, "Delete", fingerprint, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<DossierCreateContext> CreateContextAsync(int actorId, int patientId, CancellationToken ct)
    {
        var account = await users.FindByIdAsync(actorId.ToString());
        var scope = await access.ResolveAsync(actorId, ct);
        if (account == null || !account.IsActive || account.AccountType != "Staff" || !scope.GrantDriven && !AccountRoles.Effective(await users.GetRolesAsync(account)).Contains(AccountRoles.Dentist)) throw Error("MSG05",403);
        if (!await db.Patients.AnyAsync(x => x.Id == patientId && !x.IsArchived, ct)) throw Error("MSG42",404);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct) ?? throw Error("MSG05",403);
        var branches = await (from m in db.SchedulingMembers join b in db.Set<SchedulingBranch>() on m.BranchId equals b.Id
            where m.EmployeeId == employee.Id && m.Active && (m.CanDentist || scope.GrantDriven) && b.Active orderby b.Name select new DossierCreateBranch(b.Id,b.Name)).ToListAsync(ct);
        if (scope.Assigned && !scope.Owner) branches = branches.Where(x => scope.BranchIds.Contains(x.Id)).ToList();
        return new(employee.Id, branches);
    }
    public Task<TreatmentChange> CreateAsync(int actorId, int patientId, CreateDossierRequest request, CancellationToken ct) => CreateCoreAsync(actorId, patientId, request, ct, false);
    public async Task<TreatmentChange> QuickCreateAsync(int actorId, int patientId, QuickDossierRequest request, CancellationToken ct)
    {
        var context = await CreateContextAsync(actorId, patientId, ct);
        if (!context.Branches.Any(x => x.Id == request.BranchId)) throw Error("MSG05",403);
        return await CreateCoreAsync(actorId, patientId, new CreateDossierRequest(request.BranchId, context.DentistId, null, null, null, request.RequestKey, request.SourceVisitId, request.ExpectedSourceVisitVersion), ct, true);
    }
    private async Task<TreatmentChange> CreateCoreAsync(int actorId, int patientId, CreateDossierRequest request,
        CancellationToken ct, bool quick)
    {
        await DentistAsync(actorId, request.BranchId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({Features.Scheduling.WorkingScheduleService.TransactionLock})", ct);
        if (!await db.Patients.AsNoTracking().AnyAsync(x => x.Id == patientId && !x.IsArchived, ct)) throw Error("MSG42", 404);
        var command = quick ? "QuickCreate" : "Create";
        var replay = await ReplayAsync(actorId, request.RequestKey, command, new { patientId, request }, ct);
        if (replay != null) return replay;
        var diagnosis = Clean(request.Diagnosis);
        if ((!quick && diagnosis == null) || diagnosis?.Length > 4000 || Clean(request.Note)?.Length > 4000) throw Error("MSG04");
        if (!await db.Patients.AsNoTracking().AnyAsync(x => x.Id == patientId && !x.IsArchived, ct))
            throw Error("MSG42", 404);
        if (!await db.SchedulingMembers.AsNoTracking().AnyAsync(x => x.BranchId == request.BranchId
                && x.EmployeeId == request.DentistId && x.Active && x.CanDentist, ct)) throw Error("MSG05", 403);
        var now = clock.GetUtcNow();
        var dossier = new TreatmentDossier
        {
            Code = "HS" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            PatientId = patientId, BranchId = request.BranchId, DentistId = request.DentistId,
            Diagnosis = diagnosis ?? "", Note = Clean(request.Note),
            StartedOn = request.StartedOn ?? DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime),
            CreatedAt = now, UpdatedAt = now
        };

        db.TreatmentDossiers.Add(dossier);
        if (request.SourceVisitId.HasValue)
        {
            var source = await db.Visits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.SourceVisitId
                && x.PatientId == patientId && x.BranchId == request.BranchId, ct) ?? throw Error("MSG42", 404);
            if (source.Version != request.ExpectedSourceVisitVersion) throw Error("MSG41", 409);
            var examination = await db.Set<DCMS.Domain.Features.Examinations.VisitExamination>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.VisitId == source.Id, ct) is { } found && DCMS.Domain.Features.Visits.VisitStatuses.HasConfirmedExamination.Contains(source.Status) ? found : throw Error("MSG57", 409);
            dossier.SourceExaminationVisitId = source.Id;
            Event(dossier, actorId, "ExaminationSourceCaptured", new { SourceVisitId = source.Id,
                SourceVisitVersion = source.Version, Content = JsonSerializer.SerializeToElement(DCMS.Infrastructure.Features.Examinations.ExaminationService.WithDerivedDiagnoses(JsonSerializer.Deserialize<DCMS.Application.Features.Examinations.ExaminationContent>(examination.ContentJson, DCMS.Infrastructure.Features.Examinations.ExaminationService.Json)!), DCMS.Infrastructure.Features.Examinations.ExaminationService.Json) });
        }
        else if (request.ExpectedSourceVisitVersion.HasValue) throw Error("MSG04");
        Event(dossier, actorId, "DossierCreated", new { dossier.Code, dossier.Diagnosis });
        await db.SaveChangesAsync(ct);
        var result = new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG53", "hồ sơ điều trị", value: dossier.Code));
        StoreReceipt(actorId, request.RequestKey, command, new { patientId, request }, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<TreatmentChange> CloseAsync(int actorId, Guid dossierId, CloseDossierRequest request, CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "Close", new { dossierId, request }, ct);
        if (replay != null) return replay;
        if (dossier.Version != request.ExpectedVersion) throw Error("MSG41", 409);
        if (dossier.State != "Open" || request.Outcome is not ("CompletedAsAgreed" or "CompletedRevisedScope" or "StoppedIncomplete"))
            throw Error("MSG04");
        var reason = Clean(request.Reason);
        if (reason?.Length > 1000 || (request.Outcome != "CompletedAsAgreed" && reason == null)) throw Error("MSG04");
        if (request.Outcome == "CompletedRevisedScope" &&
            !await db.TreatmentEvents.AnyAsync(x => x.DossierId == dossierId && x.Kind == "PlanRevised", ct))
            throw Error("MSG55", 409);
        if (request.Outcome == "CompletedAsAgreed" && !dossier.HasCommittedPlan()) throw Error("MSG58", 409);
        // An upcoming follow-up appointment of this dossier must be handled before closing it.
        if (await db.Appointments.AsNoTracking().AnyAsync(x => x.DossierId == dossierId &&
            (x.Status == "Confirmed" || x.Status == "CheckedIn"), ct)) throw Error("MSG55", 409);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        dossier.State = request.Outcome == "StoppedIncomplete" ? "Stopped" : "Completed";
        dossier.Version++;
        dossier.UpdatedAt = clock.GetUtcNow();
        Event(dossier, actorId, "DossierClosed", new { request.Outcome, reason });
        await db.SaveChangesAsync(ct);
        var result = new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG53", "kết thúc hồ sơ", value: dossier.Code));
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
        if (dossier.Version != request.ExpectedVersion) throw Error("MSG41", 409);
        var reason = Clean(request.Reason);
        if (dossier.State is "Open" or "Cancelled" || reason is not { Length: > 0 and <= 1000 }) throw Error("MSG45");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        dossier.State = "Open";
        dossier.Version++;
        dossier.UpdatedAt = clock.GetUtcNow();
        Event(dossier, actorId, "DossierReopened", new { reason });
        await db.SaveChangesAsync(ct);
        var result = new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG53", "mở lại hồ sơ", value: dossier.Code));
        StoreReceipt(actorId, request.RequestKey, "Reopen", new { dossierId, request }, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<IReadOnlyList<OwnTreatmentSummaryItem>> OwnSummaryAsync(int accountId, CancellationToken ct)
    {
        var patientIds = db.PatientAccountLinks.AsNoTracking().Where(x => x.AccountId == accountId
            && x.Status == PatientLinkStatus.Verified).Select(x => x.PatientId);
        var dossiers = await db.TreatmentDossiers.AsNoTracking().Include(x => x.Items)
            .Where(x => patientIds.Contains(x.PatientId) && x.State != "Deleted" && x.Items.Any())
            .OrderByDescending(x => x.StartedOn).ToListAsync(ct);
        var dossierIds = dossiers.Select(x => x.Id).ToArray();
        var planCommits = dossierIds.Length == 0 ? [] : await db.TreatmentEvents.AsNoTracking()
            .Where(x => dossierIds.Contains(x.DossierId) && x.Kind == "PlanCommitted")
            .GroupBy(x => x.DossierId).Select(g => new { DossierId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DossierId, x => x.Count, ct);
        var verifiedPatientIds = await patientIds.ToArrayAsync(ct);
        var activities = await db.ClinicalActivities.AsNoTracking()
            .Where(x => dossierIds.Contains(x.DossierId))
            .OrderByDescending(x => x.PerformedAt).ToArrayAsync(ct);
        // Legacy finance rows are archived before the compact-schema migration drops their tables.
        var archived = dossierIds.Length == 0 ? [] : await db.Database.SqlQuery<string>($"""
            SELECT jsonb_build_object('source', a."Target", 'row', item.row)::text AS "Value"
            FROM "AUDIT_LOG" a CROSS JOIN LATERAL jsonb_array_elements(a."PayloadJson"->'rows') AS item(row)
            WHERE a."AuditType" = 'FinanceLegacyArchive'
              AND a."Target" IN ('TREATMENT_DOSSIER_PAYMENT', 'PAYMENT_RECEIPT')
              AND (item.row->>'PatientId')::integer = ANY({verifiedPatientIds})
              AND (item.row->>'DossierId')::uuid = ANY({dossierIds})
            """).ToArrayAsync(ct);
        var payments = new List<(Guid DossierId, int PatientId, OwnTreatmentPaymentItem Item)>();
        foreach (var snapshot in archived)
        {
            using var document = JsonDocument.Parse(snapshot);
            var root = document.RootElement;
            var source = root.GetProperty("source").GetString();
            var row = root.GetProperty("row");
                var dossierValue = row.GetProperty("DossierId");
                if (dossierValue.ValueKind == JsonValueKind.Null) continue;
                var dossierId = dossierValue.GetGuid();
                var patientId = row.GetProperty("PatientId").GetInt32();
                if (!dossierIds.Contains(dossierId) || !verifiedPatientIds.Contains(patientId)) continue;
                if (source == "PAYMENT_RECEIPT" && row.GetProperty("State").GetString() != "Posted") continue;
                var date = source == "PAYMENT_RECEIPT"
                    ? DateOnly.FromDateTime((row.TryGetProperty("ConfirmedAt", out var confirmed) && confirmed.ValueKind != JsonValueKind.Null
                        ? confirmed : row.GetProperty("CreatedAt")).GetDateTimeOffset().UtcDateTime)
                    : DateOnly.Parse(row.GetProperty("PaidOn").GetString()!);
                payments.Add((dossierId, patientId, new OwnTreatmentPaymentItem(row.GetProperty("Id").GetGuid(), date,
                    row.GetProperty("Method").GetString() ?? "", row.GetProperty(source == "PAYMENT_RECEIPT" ? "AmountVnd" : "TotalVnd").GetInt64(),
                    source == "PAYMENT_RECEIPT" ? "PaymentReceipt" : "DossierPayment")));
        }
        return dossiers.Select(x => new OwnTreatmentSummaryItem(x.Id, x.Code, x.State, x.StartedOn,
            x.Diagnosis, Math.Max(1, planCommits.GetValueOrDefault(x.Id)), x.Items
                .OrderBy(item => item.PlannedOrder).Select(ItemDto).ToArray(),
            activities.Where(activity => activity.DossierId == x.Id).Select(activity =>
                new PerformedTreatmentItem(activity.Id, activity.PerformedAt,
                    activity.ServiceNameSnapshot ?? x.Items.FirstOrDefault(item => item.Id == activity.ItemId)?.ServiceName ?? "Dịch vụ điều trị",
                    activity.Region, activity.Quantity, activity.Unit,
                    string.IsNullOrWhiteSpace(activity.Outcome) ? null : activity.Outcome)).ToArray(),
            payments.Where(payment => payment.DossierId == x.Id).Select(payment => payment.Item)
                .OrderByDescending(payment => payment.PaidOn).ToArray())).ToArray();
    }
}

using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed class CareTaskService(DcmsDbContext db, UserManager<ApplicationUser> users,
    TimeProvider clock, DCMS.Application.Features.Settings.ISettingsAccess access) : ICareTaskService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static BusinessException Error(string code, int status = 400) =>
        new(status, SystemMessages.Get(code));
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task<bool> AuthorizedAsync(int actorId, int branchId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user == null || !user.IsActive || user.AccountType != "Staff") throw Error("MSG06", 403);
        var scope = await access.ResolveAsync(actorId, ct);
        if (scope.Assigned && !scope.Owner && !scope.BranchIds.Contains(branchId)) throw Error("MSG06", 403);
        var roles = AccountRoles.Effective(await users.GetRolesAsync(user));
        if (!roles.Contains(AccountRoles.Dentist) && !roles.Contains(AccountRoles.ClinicAssistant))
            throw Error("MSG06", 403);
        var employeeId = await db.Employees.AsNoTracking().Where(x => x.AccountId == actorId)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
        if (!employeeId.HasValue || !await db.SchedulingMembers.AsNoTracking().AnyAsync(x =>
            x.BranchId == branchId && x.EmployeeId == employeeId && x.Active, ct))
            throw Error("MSG06", 403);
        return roles.Contains(AccountRoles.Dentist);
    }

    private async Task<CareTask> TaskAsync(int actorId, Guid id, CancellationToken ct)
    {
        var row = await db.CareTasks.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw Error("MSG113", 404);
        await AuthorizedAsync(actorId, row.BranchId, ct);
        return row;
    }

    private async Task RequireOwnerAsync(int? ownerId, int branchId, CancellationToken ct)
    {
        if (ownerId.HasValue) await AuthorizedAsync(ownerId.Value, branchId, ct);
    }

    private static CareTaskDto Map(CareTask x) => new(x.Id, x.PatientId, x.BranchId,
        x.InstructionId, x.AppointmentId, x.ReviewId, x.Type, x.Content, x.OwnerAccountId,
        x.DueAt, x.Status, x.Version, x.Status is "Done" or "Cancelled" ? [] :
            ["update", "recordContact", "escalate"]);
    private static ContactAttemptDto Map(CareContactAttempt x) => new(x.Id, x.TaskId,
        x.Channel, x.Result, x.NextAction, x.RebookedAppointmentId, x.ActorId, x.OccurredAt);
    private static EscalationDto Map(CareEscalation x) => new(x.Id, x.TaskId,
        x.TargetClinicianAccountId, x.Reason, x.Resolution, x.Status, x.CreatedBy,
        x.CreatedAt, x.Version, x.AcknowledgedAt, x.ResolvedAt);

    private async Task<Guid?> ReplayAsync(int actorId, Guid key, string command, object payload,
        CancellationToken ct)
    {
        if (key == Guid.Empty) throw Error("MSG04");
        var receipt = await db.ClinicalCommandReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (receipt == null) return null;
        if (receipt.ActorId != actorId || receipt.Command != command || receipt.Fingerprint != Hash(payload))
            throw Error("MSG111", 409);
        return receipt.ResultId;
    }
    private void Receipt(int actorId, Guid key, string command, object payload, Guid resultId) =>
        db.ClinicalCommandReceipts.Add(new ClinicalCommandReceipt
        {
            RequestKey = key, ActorId = actorId, Command = command,
            Fingerprint = Hash(payload), ResultId = resultId
        });

    public async Task<IReadOnlyList<CareTaskDto>> ListAsync(int actorId, int branchId,
        DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        await AuthorizedAsync(actorId, branchId, ct);
        if (from.HasValue && to.HasValue && from > to) throw Error("MSG04");
        var query = db.CareTasks.AsNoTracking().Where(x => x.BranchId == branchId);
        if (from.HasValue) query = query.Where(x => x.DueAt >= new DateTimeOffset(
            from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.DueAt < new DateTimeOffset(
            to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime());
        return (await query.OrderBy(x => x.DueAt).ThenBy(x => x.Id).Take(500).ToListAsync(ct))
            .Select(Map).ToArray();
    }

    public async Task<CareTaskDetail> GetAsync(int actorId, Guid id, CancellationToken ct)
    {
        var row = await db.CareTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw Error("MSG113", 404);
        await ReadableAsync(actorId, row.BranchId, ct);
        var contacts = await db.CareContactAttempts.AsNoTracking().Where(x => x.TaskId == id)
            .OrderBy(x => x.OccurredAt).ToListAsync(ct);
        var escalations = await db.CareEscalations.AsNoTracking().Where(x => x.TaskId == id)
            .OrderBy(x => x.CreatedAt).ToListAsync(ct);
        return new(Map(row), contacts.Select(Map).ToArray(), escalations.Select(Map).ToArray());
    }

    public async Task<CareTaskDto> CreateAsync(int actorId, CreateCareTaskRequest request,
        CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty) throw Error("MSG04");
        await using var gate = await ClinicalRequestLock.AcquireAsync(db, request.RequestKey, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "CareTaskCreate", request, ct);
        if (replay.HasValue) return Map(await db.CareTasks.AsNoTracking().SingleAsync(x => x.Id == replay, ct));
        await AuthorizedAsync(actorId, request.BranchId, ct);
        await RequireOwnerAsync(request.OwnerAccountId, request.BranchId, ct);
        if (Clean(request.Content) is not { Length: <= 4000 } ||
            Clean(request.Type) is not { Length: <= 40 } || request.DueAt == default ||
            !await db.Patients.AsNoTracking().AnyAsync(x => x.Id == request.PatientId && !x.IsArchived, ct))
            throw Error("MSG04");
        if (request.InstructionId.HasValue && !await db.FollowUpInstructions.AsNoTracking()
            .AnyAsync(x => x.Id == request.InstructionId && x.PatientId == request.PatientId &&
                x.BranchId == request.BranchId, ct)) throw Error("MSG113", 404);
        if (request.AppointmentId.HasValue && !await db.Appointments.AsNoTracking()
            .AnyAsync(x => x.Id == request.AppointmentId && x.BranchId == request.BranchId &&
                x.PatientId == request.PatientId, ct)) throw Error("MSG113", 404);
        if (request.ReviewId.HasValue && !await db.ClinicalReviews.AsNoTracking()
            .AnyAsync(x => x.Id == request.ReviewId && x.PatientId == request.PatientId &&
                x.BranchId == request.BranchId, ct)) throw Error("MSG113", 404);
        var now = clock.GetUtcNow();
        var row = new CareTask
        {
            PatientId = request.PatientId, BranchId = request.BranchId,
            InstructionId = request.InstructionId, AppointmentId = request.AppointmentId,
            ReviewId = request.ReviewId, Type = request.Type.Trim(),
            Content = request.Content.Trim(), OwnerAccountId = request.OwnerAccountId,
            // Npgsql only writes UTC offsets to timestamptz; the Web sends clinic time (+07:00).
            DueAt = request.DueAt.ToUniversalTime(), CreatedBy = actorId, CreatedAt = now, UpdatedAt = now
        };
        db.CareTasks.Add(row);
        Receipt(actorId, request.RequestKey, "CareTaskCreate", request, row.Id);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<CareTaskDto> UpdateAsync(int actorId, Guid id, UpdateCareTaskRequest request,
        CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty) throw Error("MSG04");
        await using var gate = await ClinicalRequestLock.AcquireAsync(db, request.RequestKey, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "CareTaskUpdate", new { id, request }, ct);
        if (replay.HasValue) return Map(await db.CareTasks.AsNoTracking().SingleAsync(x => x.Id == replay, ct));
        var row = await TaskAsync(actorId, id, ct);
        await RequireOwnerAsync(request.OwnerAccountId, row.BranchId, ct);
        if (row.Version != request.ExpectedVersion || row.Status is "Done" or "Cancelled")
            throw Error("MSG111", 409);
        if (request.Status is not ("Open" or "InProgress" or "Done" or "Cancelled") ||
            Clean(request.Content) is not { Length: <= 4000 } || request.DueAt == default)
            throw Error("MSG04");
        row.Status = request.Status; row.Content = request.Content.Trim();
        row.OwnerAccountId = request.OwnerAccountId; row.DueAt = request.DueAt.ToUniversalTime();
        row.Version++; row.UpdatedAt = clock.GetUtcNow();
        Receipt(actorId, request.RequestKey, "CareTaskUpdate", new { id, request }, id);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<ContactAttemptDto> ContactAsync(int actorId, Guid id,
        ContactAttemptRequest request, CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty) throw Error("MSG04");
        await using var gate = await ClinicalRequestLock.AcquireAsync(db, request.RequestKey, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "CareContact", new { id, request }, ct);
        if (replay.HasValue) return Map(await db.CareContactAttempts.AsNoTracking()
            .SingleAsync(x => x.Id == replay, ct));
        var task = await TaskAsync(actorId, id, ct);
        if (task.Version != request.ExpectedTaskVersion || task.Status is "Done" or "Cancelled")
            throw Error("MSG111", 409);
        if (request.Channel is not ("Phone" or "InPerson" or "Other") ||
            request.Result is not ("Reached" or "NoAnswer" or "Rebooked" or "Declined" or "Other") ||
            Clean(request.NextAction)?.Length > 1000) throw Error("MSG04");
        if (request.Result == "Rebooked" && (!request.RebookedAppointmentId.HasValue ||
            !await db.Appointments.AsNoTracking().AnyAsync(x => x.Id == request.RebookedAppointmentId &&
                x.BranchId == task.BranchId && x.PatientId == task.PatientId &&
                x.Status == "Confirmed", ct))) throw Error("MSG230", 409);
        var row = new CareContactAttempt
        {
            TaskId = id, Channel = request.Channel, Result = request.Result,
            NextAction = Clean(request.NextAction), RebookedAppointmentId = request.RebookedAppointmentId,
            ActorId = actorId, OccurredAt = clock.GetUtcNow()
        };
        task.Version++; task.UpdatedAt = row.OccurredAt;
        db.CareContactAttempts.Add(row);
        Receipt(actorId, request.RequestKey, "CareContact", new { id, request }, row.Id);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<EscalationDto> EscalateAsync(int actorId, Guid id,
        EscalateCareRequest request, CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty) throw Error("MSG04");
        await using var gate = await ClinicalRequestLock.AcquireAsync(db, request.RequestKey, ct);
        var replay = await ReplayAsync(actorId, request.RequestKey, "CareEscalate", new { id, request }, ct);
        if (replay.HasValue) return Map(await db.CareEscalations.AsNoTracking().SingleAsync(x => x.Id == replay, ct));
        var task = await TaskAsync(actorId, id, ct);
        if (task.Version != request.ExpectedTaskVersion || task.Status is "Done" or "Cancelled")
            throw Error("MSG111", 409);
        if (Clean(request.Reason) is not { Length: <= 1000 } ||
            !await AuthorizedAsync(request.TargetClinicianAccountId, task.BranchId, ct))
            throw Error("MSG06", 403);
        var row = new CareEscalation
        {
            TaskId = id, TargetClinicianAccountId = request.TargetClinicianAccountId,
            Reason = request.Reason.Trim(), CreatedBy = actorId, CreatedAt = clock.GetUtcNow()
        };
        task.Version++; task.UpdatedAt = row.CreatedAt;
        db.CareEscalations.Add(row);
        Receipt(actorId, request.RequestKey, "CareEscalate", new { id, request }, row.Id);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<EscalationDto> ChangeEscalationAsync(int actorId, Guid id, bool resolve,
        ResolveEscalationRequest request, CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty) throw Error("MSG04");
        await using var gate = await ClinicalRequestLock.AcquireAsync(db, request.RequestKey, ct);
        var command = resolve ? "CareResolve" : "CareAcknowledge";
        var replay = await ReplayAsync(actorId, request.RequestKey, command, new { id, request }, ct);
        if (replay.HasValue) return Map(await db.CareEscalations.AsNoTracking().SingleAsync(x => x.Id == replay, ct));
        var row = await db.CareEscalations.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw Error("MSG113", 404);
        var task = await db.CareTasks.AsNoTracking().SingleAsync(x => x.Id == row.TaskId, ct);
        if (!await AuthorizedAsync(actorId, task.BranchId, ct) || actorId != row.TargetClinicianAccountId)
            throw Error("MSG06", 403);
        if (row.Version != request.ExpectedVersion ||
            (resolve ? row.Status != "Acknowledged" : row.Status != "Pending"))
            throw Error("MSG111", 409);
        if (resolve && Clean(request.Resolution) is not { Length: <= 4000 }) throw Error("MSG04");
        row.Status = resolve ? "Resolved" : "Acknowledged";
        row.Version++;
        if (resolve) { row.Resolution = request.Resolution!.Trim(); row.ResolvedAt = clock.GetUtcNow(); }
        else row.AcknowledgedAt = clock.GetUtcNow();
        Receipt(actorId, request.RequestKey, command, new { id, request }, row.Id);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    // S60 staff Web (docs/specs/S60-CUSTOMER-CARE-WEB.vi.md). Types created by ingestion are listed for filtering only.
    private static readonly Dictionary<string, string> SystemTypes = new()
    {
        ["FollowUp"] = "Theo dõi tái khám", ["FollowUpDue"] = "Chỉ định đến hạn đặt lịch",
        ["PrerequisiteBlocked"] = "Bị chặn bởi bước trước", ["LabBlocked"] = "Chờ labo",
        ["MissedAppointment"] = "Vắng hẹn"
    };
    private static readonly Dictionary<string, string> StatusLabels = new()
    {
        ["Open"] = "Mở", ["InProgress"] = "Đang xử lý", ["Done"] = "Đã xong", ["Cancelled"] = "Đã hủy"
    };
    private static readonly Dictionary<string, string> ContactLabels = new()
    {
        ["Reached"] = "Đã liên lạc được", ["NoAnswer"] = "Không nghe máy", ["Rebooked"] = "Đã đặt lại lịch",
        ["Declined"] = "Khách từ chối", ["Other"] = "Khác"
    };
    private const int ExportLimit = 5000;
    private static readonly TimeSpan ClinicOffset = TimeSpan.FromHours(7);
    private static string Like(string value) =>
        "%" + value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

    private async Task<string[]?> StaffRolesAsync(int accountId, int branchId, CancellationToken ct)
    {
        try { await AuthorizedAsync(accountId, branchId, ct); }
        catch (BusinessException) { return null; }
        var user = await users.FindByIdAsync(accountId.ToString());
        return AccountRoles.Effective(await users.GetRolesAsync(user!))
            .Where(x => x is AccountRoles.Dentist or AccountRoles.ClinicAssistant).Order().ToArray();
    }

    // Read scope (docs/specs/S60-CUSTOMER-CARE-WEB.vi.md §2): the owner reads every active branch without being a
    // scheduling member (S58-BRANCHES-AND-CHAIRS §4); other staff read the branches they may also write.
    private async Task<(bool Owner, Dictionary<int, string> Branches)> ReadScopeAsync(int actorId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user == null || !user.IsActive || user.AccountType != "Staff") throw Error("MSG06", 403);
        var owner = (await access.ResolveAsync(actorId, ct)).Owner;
        var branches = new Dictionary<int, string>();
        foreach (var branch in await db.Set<SchedulingBranch>().AsNoTracking().Where(x => x.Active).OrderBy(x => x.Id).ToListAsync(ct))
            if (owner || await StaffRolesAsync(actorId, branch.Id, ct) != null) branches[branch.Id] = branch.Name;
        return (owner, branches);
    }

    private async Task ReadableAsync(int actorId, int branchId, CancellationToken ct)
    {
        if (!(await ReadScopeAsync(actorId, ct)).Branches.ContainsKey(branchId)) throw Error("MSG06", 403);
    }

    private async Task<Dictionary<string, string>> TypeNamesAsync(CancellationToken ct)
    {
        var names = new Dictionary<string, string>(SystemTypes);
        foreach (var x in await db.ReferenceEntries.AsNoTracking()
                     .Where(x => x.Kind == DCMS.Application.Features.SystemSettings.ReferenceKinds.CareTypes && x.Code != null)
                     .Select(x => new { x.Code, x.Name }).ToListAsync(ct))
            names[x.Code!] = x.Name;
        return names;
    }

    public async Task<CareTaskOptions> OptionsAsync(int actorId, CancellationToken ct)
    {
        var scope = await ReadScopeAsync(actorId, ct);
        var branches = new List<CareBranchOption>();
        foreach (var branch in scope.Branches.Select(x => new { Id = x.Key, Name = x.Value }))
        {
            var canCreate = await StaffRolesAsync(actorId, branch.Id, ct) != null;
            var members = await (from m in db.SchedulingMembers.AsNoTracking()
                                 join e in db.Employees.AsNoTracking() on m.EmployeeId equals e.Id
                                 where m.BranchId == branch.Id && m.Active
                                 orderby e.FullName
                                 select new { e.AccountId, e.FullName }).ToListAsync(ct);
            var staff = new List<CareStaffOption>();
            foreach (var member in members)
                if (await StaffRolesAsync(member.AccountId, branch.Id, ct) is { } roles)
                    staff.Add(new(member.AccountId, member.FullName, roles));
            branches.Add(new(branch.Id, branch.Name, canCreate, staff));
        }
        var types = (await db.ReferenceEntries.AsNoTracking()
                .Where(x => x.Kind == DCMS.Application.Features.SystemSettings.ReferenceKinds.CareTypes && x.IsActive && x.Code != null)
                .OrderBy(x => x.NormalizedName).Select(x => new { x.Code, x.Name }).ToListAsync(ct))
            .Select(x => new CareTypeOption(x.Code!, x.Name, false))
            .Concat(SystemTypes.Select(x => new CareTypeOption(x.Key, x.Value, true))).ToArray();
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(ClinicOffset).DateTime);
        return new(today, actorId, scope.Owner, branches, types);
    }

    public async Task<IReadOnlyList<CarePatientOption>> PatientOptionsAsync(int actorId, int branchId,
        string? search, CancellationToken ct)
    {
        await AuthorizedAsync(actorId, branchId, ct);
        if (Clean(search) is not { Length: <= 100 } term) throw Error("MSG04");
        var pattern = Like(term);
        var rows = await (from p in db.Patients.AsNoTracking()
                          where !p.IsArchived && (EF.Functions.ILike(p.PatientCode, pattern) ||
                              EF.Functions.ILike(p.FullName, pattern) ||
                              p.Phone != null && EF.Functions.ILike(p.Phone, pattern))
                          let last = db.Visits.Where(v => v.PatientId == p.Id && v.BranchId == branchId &&
                                  (v.Status == VisitStatuses.InProgress || v.Status == VisitStatuses.Completed))
                              .Max(v => (DateTimeOffset?)v.ArrivalAt)
                          where last != null
                          orderby last descending, p.Id
                          select new { p.Id, p.PatientCode, p.FullName, p.Phone, Last = last!.Value })
            .Take(20).ToListAsync(ct);
        return rows.Select(x => new CarePatientOption(x.Id, x.PatientCode, x.FullName, x.Phone, x.Last)).ToArray();
    }

    private async Task<(IQueryable<CareTask> Query, Dictionary<int, string> Branches)> FilterAsync(int actorId,
        CareWorklistQuery q, CancellationToken ct)
    {
        var scope = await ReadScopeAsync(actorId, ct);
        if (q.BranchId.HasValue && !scope.Branches.ContainsKey(q.BranchId.Value)) throw Error("MSG06", 403);
        var branchIds = q.BranchId.HasValue ? new[] { q.BranchId.Value } : scope.Branches.Keys.ToArray();
        if (q.From.HasValue && q.To.HasValue && (q.From > q.To || q.To.Value.DayNumber - q.From.Value.DayNumber > 366) ||
            q.Status is not (null or "Open" or "InProgress" or "Done" or "Cancelled" or "Pending") ||
            Clean(q.Type)?.Length > 40 || Clean(q.Search)?.Length > 100) throw Error("MSG04");
        var query = db.CareTasks.AsNoTracking().Where(x => branchIds.Contains(x.BranchId));
        if (q.From.HasValue)
        {
            var from = new DateTimeOffset(q.From.Value.ToDateTime(TimeOnly.MinValue), ClinicOffset).ToUniversalTime();
            query = query.Where(x => x.DueAt >= from);
        }
        if (q.To.HasValue)
        {
            var to = new DateTimeOffset(q.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), ClinicOffset).ToUniversalTime();
            query = query.Where(x => x.DueAt < to);
        }
        if (q.OwnerAccountId.HasValue) query = query.Where(x => x.OwnerAccountId == q.OwnerAccountId);
        if (Clean(q.Type) is { } type) query = query.Where(x => x.Type == type);
        if (q.Status == "Pending") query = query.Where(x => x.Status == "Open" || x.Status == "InProgress");
        else if (q.Status != null) query = query.Where(x => x.Status == q.Status);
        if (Clean(q.Search) is { } search)
        {
            var pattern = Like(search);
            query = query.Where(x => db.Patients.Any(p => p.Id == x.PatientId && (EF.Functions.ILike(p.PatientCode, pattern) ||
                EF.Functions.ILike(p.FullName, pattern) || p.Phone != null && EF.Functions.ILike(p.Phone, pattern))));
        }
        return (query.OrderBy(x => x.DueAt).ThenBy(x => x.Id), scope.Branches);
    }

    private async Task<IReadOnlyList<CareWorklistItem>> ItemsAsync(IReadOnlyList<CareTask> rows,
        Dictionary<int, string> branches, CancellationToken ct)
    {
        var patientIds = rows.Select(x => x.PatientId).Distinct().ToArray();
        var patients = await db.Patients.AsNoTracking().Where(x => patientIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => new { x.PatientCode, x.FullName, x.Phone }, ct);
        var ownerIds = rows.Where(x => x.OwnerAccountId.HasValue).Select(x => x.OwnerAccountId!.Value).Distinct().ToArray();
        var owners = await db.Employees.AsNoTracking().Where(x => ownerIds.Contains(x.AccountId))
            .ToDictionaryAsync(x => x.AccountId, x => x.FullName, ct);
        var taskIds = rows.Select(x => x.Id).ToArray();
        var contacts = (await db.CareContactAttempts.AsNoTracking().Where(x => taskIds.Contains(x.TaskId))
                .Select(x => new { x.TaskId, x.Result, x.OccurredAt }).ToListAsync(ct))
            .GroupBy(x => x.TaskId).ToDictionary(x => x.Key, x => x.MaxBy(c => c.OccurredAt)!);
        var types = await TypeNamesAsync(ct);
        return rows.Select(x =>
        {
            var patient = patients.GetValueOrDefault(x.PatientId);
            var contact = contacts.GetValueOrDefault(x.Id);
            return new CareWorklistItem(Map(x), branches.GetValueOrDefault(x.BranchId, ""), patient?.PatientCode ?? "", patient?.FullName ?? "", patient?.Phone,
                types.GetValueOrDefault(x.Type, x.Type),
                x.OwnerAccountId is { } owner ? owners.GetValueOrDefault(owner) : null,
                contact?.Result, contact?.OccurredAt);
        }).ToArray();
    }

    public async Task<CareWorklistPage> WorklistAsync(int actorId, CareWorklistQuery query, CancellationToken ct)
    {
        if (query.Page < 1 || query.PageSize is not (20 or 50 or 100)) throw Error("MSG04");
        var (rows, branches) = await FilterAsync(actorId, query, ct);
        var total = await rows.CountAsync(ct);
        var page = await rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(await ItemsAsync(page, branches, ct), total, query.Page, query.PageSize);
    }

    public async Task<byte[]> ExportAsync(int actorId, CareWorklistQuery query, CancellationToken ct)
    {
        var (filtered, branches) = await FilterAsync(actorId, query, ct);
        var rows = await filtered.Take(ExportLimit + 1).ToListAsync(ct);
        if (rows.Count > ExportLimit) throw new BusinessException(400, SystemMessages.Get("MSG04",
            field: $"Bộ lọc (tối đa {ExportLimit} dòng mỗi lần xuất)"));
        string Time(DateTimeOffset value) => value.ToOffset(ClinicOffset).ToString("dd/MM/yyyy HH:mm");
        var header = new string?[] { "Thời gian", "Chi nhánh", "Mã KH", "Khách hàng", "SĐT", "Loại chăm sóc", "Nội dung",
            "Người phụ trách", "Trạng thái", "Liên hệ gần nhất" };
        var items = await ItemsAsync(rows, branches, ct);
        return DCMS.Infrastructure.Features.SystemSettings.Xlsx.Write("Cham soc khach hang", [header, .. items.Select(x => new[]
        {
            Time(x.Task.DueAt), x.BranchName, x.PatientCode, x.PatientName, x.Phone, x.TypeName, x.Task.Content, x.OwnerName,
            StatusLabels.GetValueOrDefault(x.Task.Status, x.Task.Status),
            x.LastContactResult == null ? null :
                $"{ContactLabels.GetValueOrDefault(x.LastContactResult, x.LastContactResult)} · {Time(x.LastContactAt!.Value)}"
        })]);
    }
}

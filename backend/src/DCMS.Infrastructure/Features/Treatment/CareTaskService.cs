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

public sealed class CareTaskService(DcmsDbContext db, UserManager<ApplicationUser> users,
    TimeProvider clock) : ICareTaskService
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
        var roles = AccountRoles.Effective(await users.GetRolesAsync(user));
        if (!roles.Contains(AccountRoles.Dentist) && !roles.Contains(AccountRoles.ClinicAssistant))
            throw Error("MSG06", 403);
        var employeeId = await db.Employees.AsNoTracking().Where(x => x.AccountId == actorId)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
        if (!employeeId.HasValue || !await db.Set<SchedulingMember>().AsNoTracking().AnyAsync(x =>
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
        var row = await TaskAsync(actorId, id, ct);
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
            DueAt = request.DueAt, CreatedBy = actorId, CreatedAt = now, UpdatedAt = now
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
        row.OwnerAccountId = request.OwnerAccountId; row.DueAt = request.DueAt;
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
}

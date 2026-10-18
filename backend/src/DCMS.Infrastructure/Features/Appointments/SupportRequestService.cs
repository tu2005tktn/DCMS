using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.SupportRequests;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Notifications;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DCMS.Infrastructure.Features.Appointments;

public sealed class SupportRequestService(DcmsDbContext db, UserManager<ApplicationUser> users,
    IAppointmentChangeService changes, IRealtimeNotifier realtime,
    ILogger<SupportRequestService> logger, TimeProvider clock) : ISupportRequestService
{
    private static BusinessException Error(string code, int status = 409) => new(status, SystemMessages.Get(code));
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<SupportRequestPage> ListAsync(int actorId, string? statuses, int? branchId,
        int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        var branches = await StaffBranchesAsync(actorId, ct);
        if (branchId.HasValue && !branches.Contains(branchId.Value)) throw Error("MSG06", 403);
        var selected = branchId.HasValue ? [branchId.Value] : branches;
        var allowed = new HashSet<string>((statuses ?? "Pending").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
        var query = from request in db.AppointmentSupportRequests.AsNoTracking()
                    join appointment in db.Appointments.AsNoTracking() on request.AppointmentId equals appointment.Id
                    where selected.Contains(appointment.BranchId) && allowed.Contains(request.Status)
                    select new { request, appointment };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.request.Status == "Pending" ? 0 : 1)
            .ThenByDescending(x => x.request.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = rows.Select(x => ToDto(x.request, x.appointment, true,
            x.request.AppointmentActionRequestKey.HasValue)).ToArray();
        return new SupportRequestPage(items, total, page, pageSize);
    }

    public async Task<SupportRequestDetail> DetailAsync(int actorId, string id, CancellationToken ct)
    {
        var (request, appointment) = await AuthorizedAsync(actorId, id, ct);
        return await ToDetailAsync(request, appointment, ct);
    }

    public async Task<SupportRequestDetail> VerifyRequesterAsync(int actorId, string id,
        VerifyRequesterCommand command, CancellationToken ct)
    {
        if (command.RequestKey == Guid.Empty || command.ExpectedVersion <= 0) throw Error("MSG04", 400);
        var notes = Clean(command.Notes);
        if (notes == null) throw new BusinessException(400, SystemMessages.Get("MSG02", "Xác minh người yêu cầu"));
        if (notes.Length > 500) throw new BusinessException(400, SystemMessages.Get("MSG03", "Xác minh người yêu cầu", 500));
        var fingerprint = Hash(new { action = "verify", id, command.ExpectedVersion, notes });
        await AuthorizedAsync(actorId, id, ct);
        var replay = await ReplayAsync<SupportRequestDetail>(actorId, command.RequestKey, fingerprint, ct);
        if (replay != null) return replay;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (request, appointment) = await AuthorizedAsync(actorId, id, ct);
        RequirePending(request, command.ExpectedVersion);
        request.RequesterVerifiedAt = clock.GetUtcNow(); request.RequesterVerifiedBy = actorId;
        request.RequesterVerification = notes; request.Version++; request.UpdatedAt = clock.GetUtcNow();
        db.AppointmentSupportHistories.Add(History(request, appointment, actorId, "RequesterVerified", notes));
        await db.SaveChangesAsync(ct);
        var result = await ToDetailAsync(request, appointment, ct);
        await SaveReceiptAsync(actorId, command.RequestKey, fingerprint, request.Id, result, ct);
        await tx.CommitAsync(ct);
        await PublishSupportUpdatedAsync(request, appointment, "RequesterVerified");
        return result;
    }

    public async Task<StaffSupportChangeResult> ChangeAppointmentAsync(int actorId, string id,
        StaffSupportChangeCommand command, CancellationToken ct)
    {
        if (command.RequestKey == Guid.Empty || command.ExpectedVersion <= 0) throw Error("MSG04", 400);
        var fingerprint = Hash(new { action = "appointment-change", id, command.ExpectedVersion,
            command.Reason, command.Notes, NewStartAt = command.NewStartAt?.ToUniversalTime() });
        var (request, appointment) = await AuthorizedAsync(actorId, id, ct);
        var replay = await ReplayAsync<StaffSupportChangeResult>(actorId, command.RequestKey, fingerprint, ct);
        if (replay != null) return replay;
        RequirePending(request, command.ExpectedVersion);
        if (request.RequesterVerifiedAt == null || string.IsNullOrWhiteSpace(request.RequesterVerification))
            throw new BusinessException(400, SystemMessages.Get("MSG02", "Xác minh người yêu cầu"));
        if (request.Kind is not ("Change" or "Cancel")) throw Error("MSG04", 400);

        AppointmentChangeResult changed;
        if (request.Kind == "Cancel")
        {
            changed = await changes.CancelAsync(actorId, appointment.Code,
                new CancelAppointmentRequest(appointment.Version, command.RequestKey,
                    Clean(command.Notes) ?? command.Reason, request.RequesterVerification), ct);
        }
        else
        {
            var start = command.NewStartAt ?? request.ProposedStartAt ?? throw Error("MSG04", 400);
            var availability = await changes.AvailabilityAsync(actorId, appointment.Code,
                DateOnly.FromDateTime(start.ToOffset(TimeSpan.FromHours(7)).DateTime), appointment.DentistId, ct);
            var slot = availability.Slots.FirstOrDefault(x => x.Start == start.ToUniversalTime())
                ?? throw Error("MSG34", 409);
            changed = await changes.RescheduleAsync(actorId, appointment.Code,
                new RescheduleAppointmentRequest(appointment.Version, command.RequestKey, slot.Start,
                    slot.ChairId, slot.ScheduleVersion, slot.AssistantScheduleVersion,
                    Clean(command.Notes) ?? command.Reason, request.RequesterVerification), ct);
        }

        // Booking action is committed by IAppointmentChangeService before this marker is written.
        await using var markerTx = await db.Database.BeginTransactionAsync(ct);
        request = await db.AppointmentSupportRequests.SingleAsync(x => x.Id == request.Id, ct);
        RequirePending(request, command.ExpectedVersion);
        request.AppointmentActionRequestKey = command.RequestKey;
        request.Version++; request.UpdatedAt = clock.GetUtcNow();
        db.AppointmentSupportHistories.Add(History(request, appointment, actorId,
            request.Kind == "Cancel" ? "Cancelled" : "Rescheduled", Clean(command.Notes)));
        await db.SaveChangesAsync(ct);
        var current = await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == appointment.Id, ct);
        var result = new StaffSupportChangeResult(true, await AppointmentSummaryAsync(current, ct),
            changed.Notification.Code, Request: await ToDetailAsync(request, current, ct));
        db.SupportRequestReceipts.Add(new SupportRequestReceipt { ActorAccountId = actorId,
            RequestKey = command.RequestKey, Fingerprint = fingerprint, SupportRequestId = request.Id,
            ResultJson = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync(ct);
        await markerTx.CommitAsync(ct);
        await PublishSupportUpdatedAsync(request, current,
            request.Kind == "Cancel" ? "AppointmentCancelled" : "AppointmentRescheduled");
        return result;
    }

    public async Task<ResolveSupportResult> ResolveAsync(int actorId, string id,
        ResolveSupportCommand command, CancellationToken ct)
    {
        if (command.RequestKey == Guid.Empty || command.ExpectedVersion <= 0
            || command.Decision is not ("Resolve" or "Reject")) throw Error("MSG04", 400);
        var reason = Clean(command.DecisionReason);
        if (command.Decision == "Reject" && reason == null)
            throw new BusinessException(400, SystemMessages.Get("MSG02", "Lý do từ chối"));
        if (reason?.Length > 500 || Clean(command.Notes)?.Length > 500) throw Error("MSG04", 400);
        var fingerprint = Hash(new { action = command.Decision, id, command.ExpectedVersion, reason, command.Notes });
        await AuthorizedAsync(actorId, id, ct);
        var replay = await ReplayAsync<ResolveSupportResult>(actorId, command.RequestKey, fingerprint, ct);
        if (replay != null) return replay;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (request, appointment) = await AuthorizedAsync(actorId, id, ct);
        RequirePending(request, command.ExpectedVersion);
        if (command.Decision == "Resolve" && request.Kind is "Change" or "Cancel")
        {
            if (!request.AppointmentActionRequestKey.HasValue || !await db.AppointmentChanges.AsNoTracking().AnyAsync(x =>
                    x.AppointmentId == appointment.Id && x.RequestKey == request.AppointmentActionRequestKey
                    && x.Action == (request.Kind == "Cancel" ? "CANCEL" : "RESCHEDULE"), ct))
                throw Error("MSG111");
        }
        if (command.Decision == "Resolve" && request.Kind is "ConfirmToAttend" or "WithdrawAttend")
        {
            appointment.ConfirmedToAttend = request.Kind == "ConfirmToAttend" ? "Yes" : "No";
            appointment.Version++;
            db.AppointmentOutboxEvents.Add(new AppointmentOutboxEvent
            {
                AggregateId = appointment.Id, EventType = "appointment.attendance", OccurredAt = clock.GetUtcNow(),
                PayloadJson = JsonSerializer.Serialize(new { appointmentId = appointment.Id, appointment.Version,
                    appointment.ConfirmedToAttend })
            });
        }
        request.Status = command.Decision == "Reject" ? "Rejected" : "Resolved";
        request.DecisionReason = reason ?? Clean(command.Notes) ?? "Đã xử lý yêu cầu.";
        request.PublicNote = Clean(command.Notes); request.DecidedAt = clock.GetUtcNow(); request.DecidedBy = actorId;
        request.Version++; request.UpdatedAt = clock.GetUtcNow();
        db.AppointmentSupportHistories.Add(History(request, appointment, actorId,
            command.Decision == "Reject" ? "RequestRejected" : "RequestResolved", request.PublicNote));
        db.AppointmentOutboxEvents.Add(new AppointmentOutboxEvent
        {
            AggregateId = appointment.Id, EventType = command.Decision == "Reject" ? "support.rejected" : "support.resolved",
            OccurredAt = clock.GetUtcNow(), PayloadJson = JsonSerializer.Serialize(new { requestId = request.Id })
        });
        await db.SaveChangesAsync(ct);
        var detail = await ToDetailAsync(request, appointment, ct);
        var result = new ResolveSupportResult(true, detail, command.Decision == "Reject" ? "MSG38" : "MSG37");
        db.SupportRequestReceipts.Add(new SupportRequestReceipt { ActorAccountId = actorId,
            RequestKey = command.RequestKey, Fingerprint = fingerprint, SupportRequestId = request.Id,
            ResultJson = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await PublishSupportUpdatedAsync(request, appointment,
            command.Decision == "Reject" ? "Rejected" : "Resolved");
        return result;
    }

    public async Task<AttendanceConfirmationResult> SetAttendanceAsync(int actorId, Guid appointmentId,
        AttendanceConfirmationCommand command, CancellationToken ct)
    {
        if (command.RequestKey == Guid.Empty || command.ExpectedVersion <= 0) throw Error("MSG04", 400);
        var fingerprint = Hash(new { appointmentId, command.ExpectedVersion, command.Confirmed });
        var appointment = await db.Appointments.SingleOrDefaultAsync(x => x.Id == appointmentId, ct) ?? throw Error("MSG102", 404);
        await RequireBranchAsync(actorId, appointment.BranchId, ct);
        var receipt = await db.AppointmentReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == command.RequestKey, ct);
        if (receipt != null)
        {
            if (receipt.ActorScope != $"staff:{actorId}" || receipt.Fingerprint != fingerprint) throw Error("MSG111");
            return JsonSerializer.Deserialize<AttendanceConfirmationResult>(receipt.ResultJson)!;
        }
        if (appointment.Version != command.ExpectedVersion) throw Error("MSG111");
        appointment.ConfirmedToAttend = command.Confirmed ? "Yes" : "No";
        appointment.Version++;
        var result = new AttendanceConfirmationResult(appointment.Id, appointment.Version,
            appointment.Status, appointment.ConfirmedToAttend);
        db.AppointmentReceipts.Add(new AppointmentReceipt { RequestKey = command.RequestKey,
            ActorScope = $"staff:{actorId}", Fingerprint = fingerprint, AppointmentId = appointment.Id,
            ResultJson = JsonSerializer.Serialize(result) });
        db.AppointmentOutboxEvents.Add(new AppointmentOutboxEvent { AggregateId = appointment.Id,
            EventType = "appointment.attendance", OccurredAt = clock.GetUtcNow(),
            PayloadJson = JsonSerializer.Serialize(new { appointmentId, appointment.Version, appointment.ConfirmedToAttend }) });
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<SupportCustomerResult> CreateCustomerRequestAsync(int actorId, string code,
        CustomerSupportRequestCommand command, CancellationToken ct)
    {
        if (command.RequestKey == Guid.Empty) throw Error("MSG04", 400);
        var reason = Clean(command.Reason);
        if (reason == null) throw new BusinessException(400, SystemMessages.Get("MSG02", "Lý do"));
        if (reason.Length > 500) throw new BusinessException(400, SystemMessages.Get("MSG03", "Lý do", 500));
        var kind = command.Kind.Equals("Cancel", StringComparison.OrdinalIgnoreCase) ? "Cancel" : "Change";

        var fingerprint = Hash(new { action = "customer-support-request", code, kind, reason,
            NewStartAt = command.NewStartAt?.ToUniversalTime(), command.DentistId, command.ChairId });
        var replay = await ReplayAsync<SupportCustomerResult>(actorId, command.RequestKey, fingerprint, ct);
        if (replay != null) return replay;

        var appointment = await AuthorizeCustomerAppointmentAsync(actorId, code, ct);
        if (appointment.Status != "Confirmed" || appointment.StartAt <= clock.GetUtcNow())
            throw Error("MSG31", 409);

        if (await db.AppointmentSupportRequests.AnyAsync(x => x.AppointmentId == appointment.Id && x.Status == "Pending", ct))
            throw Error("MSG111", 409);

        var request = new AppointmentSupportRequest
        {
            Id = Guid.NewGuid(),
            Code = "SR-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            AppointmentId = appointment.Id,
            Kind = kind,
            Status = "Pending",
            Version = 1,
            RequestKey = command.RequestKey,
            RequesterAccountId = actorId,
            RequesterName = appointment.AttendeeName,
            RequesterPhone = appointment.BookerPhone,
            Reason = reason,
            ProposedStartAt = command.NewStartAt?.ToUniversalTime(),
            ProposedReason = reason,
            CreatedAt = clock.GetUtcNow(),
            UpdatedAt = clock.GetUtcNow()
        };

        db.AppointmentSupportRequests.Add(request);
        db.AppointmentSupportHistories.Add(new AppointmentSupportHistory
        {
            RequestId = request.Id,
            AppointmentId = appointment.Id,
            ActorAccountId = actorId,
            ActorKind = "SELF",
            Action = "RequestSubmitted",
            Notes = reason,
            OccurredAt = clock.GetUtcNow()
        });
        db.AppointmentOutboxEvents.Add(new AppointmentOutboxEvent
        {
            AggregateId = appointment.Id,
            EventType = "appointment.support_requested",
            OccurredAt = clock.GetUtcNow(),
            PayloadJson = JsonSerializer.Serialize(new { requestId = request.Id, appointmentId = appointment.Id, code = request.Code })
        });
        await db.SaveChangesAsync(ct);

        var dto = new SupportCustomerRequestDto(request.Code, appointment.Code,
            request.Kind == "Change" ? "RESCHEDULE" : request.Kind == "Cancel" ? "CANCEL" : request.Kind,
            request.Reason ?? "", request.Status, request.CreatedAt, request.DecidedAt, request.PublicNote,
            request.Version, request.ProposedStartAt);
        var result = new SupportCustomerResult(dto, SystemMessages.Get("MSG33"));
        await SaveReceiptAsync(actorId, command.RequestKey, fingerprint, request.Id, result, ct);
        await PublishSupportUpdatedAsync(request, appointment, "Created");
        return result;
    }

    public async Task<SupportCustomerRequestDto> WithdrawCustomerRequestAsync(int actorId, string id,
        WithdrawSupportCommand command, CancellationToken ct)
    {
        if (command.ExpectedVersion <= 0) throw Error("MSG04", 400);
        var request = await db.AppointmentSupportRequests.SingleOrDefaultAsync(x => x.Code == id, ct)
            ?? throw Error("MSG102", 404);
        var appointment = await db.Appointments.SingleAsync(x => x.Id == request.AppointmentId, ct);
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user == null || !user.IsActive) throw Error("MSG07", 401);
        if (request.RequesterAccountId != actorId && appointment.BookerAccountId != actorId)
        {
            var linked = await db.Set<PatientAccountLink>().AsNoTracking()
                .AnyAsync(x => x.AccountId == actorId && x.PatientId == appointment.PatientId && x.Status == PatientLinkStatus.Verified, ct);
            if (!linked) throw Error("MSG102", 404);
        }

        if (request.Status != "Pending" || request.Version != command.ExpectedVersion)
            throw Error("MSG111", 409);

        request.Status = "Withdrawn";
        request.DecidedAt = clock.GetUtcNow();
        request.Version++;
        request.UpdatedAt = clock.GetUtcNow();

        db.AppointmentSupportHistories.Add(new AppointmentSupportHistory
        {
            RequestId = request.Id,
            AppointmentId = appointment.Id,
            ActorAccountId = actorId,
            ActorKind = "SELF",
            Action = "RequestWithdrawn",
            Notes = "Người dùng rút yêu cầu.",
            OccurredAt = clock.GetUtcNow()
        });
        db.AppointmentOutboxEvents.Add(new AppointmentOutboxEvent
        {
            AggregateId = appointment.Id,
            EventType = "appointment.support_withdrawn",
            OccurredAt = clock.GetUtcNow(),
            PayloadJson = JsonSerializer.Serialize(new { requestId = request.Id, appointmentId = appointment.Id, code = request.Code })
        });
        await db.SaveChangesAsync(ct);

        await PublishSupportUpdatedAsync(request, appointment, "Withdrawn");

        return new SupportCustomerRequestDto(request.Code, appointment.Code,
            request.Kind == "Change" ? "RESCHEDULE" : request.Kind == "Cancel" ? "CANCEL" : request.Kind,
            request.Reason ?? "", request.Status, request.CreatedAt, request.DecidedAt, request.PublicNote,
            request.Version, request.ProposedStartAt);
    }

    private async Task<Appointment> AuthorizeCustomerAppointmentAsync(int actorId, string code, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user == null || !user.IsActive || (user.LockoutEnabled && user.LockoutEnd > clock.GetUtcNow()))
            throw Error("MSG07", 401);
        if (user.AccountType != "Public" && user.AccountType != "Staff") throw Error("MSG06", 403);
        var appointment = await db.Appointments.SingleOrDefaultAsync(x => x.Code == code, ct)
            ?? throw Error("MSG102", 404);
        var linkedPatientId = await db.Set<PatientAccountLink>().AsNoTracking()
            .Where(x => x.AccountId == actorId && x.Status == PatientLinkStatus.Verified)
            .Select(x => (int?)x.PatientId).SingleOrDefaultAsync(ct);
        if (appointment.BookerAccountId != actorId && (linkedPatientId == null || appointment.PatientId != linkedPatientId))
            throw Error("MSG102", 404);
        return appointment;
    }

    private async Task<(AppointmentSupportRequest Request, Appointment Appointment)> AuthorizedAsync(int actorId,
        string id, CancellationToken ct)
    {
        var request = await db.AppointmentSupportRequests.SingleOrDefaultAsync(x => x.Code == id, ct)
            ?? throw Error("MSG102", 404);
        var appointment = await db.Appointments.SingleAsync(x => x.Id == request.AppointmentId, ct);
        await RequireBranchAsync(actorId, appointment.BranchId, ct);
        return (request, appointment);
    }

    private async Task<int[]> StaffBranchesAsync(int actorId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user == null || !user.IsActive || user.AccountType != "Staff"
            || !await users.IsInRoleAsync(user, AccountRoles.ClinicAssistant)) throw Error("MSG06", 403);
        return await (from member in db.Set<SchedulingMember>().AsNoTracking()
            join employee in db.Employees.AsNoTracking() on member.EmployeeId equals employee.Id
            join branch in db.Set<SchedulingBranch>().AsNoTracking() on member.BranchId equals branch.Id
            where employee.AccountId == actorId && member.Active && branch.Active
            select member.BranchId).Distinct().ToArrayAsync(ct);
    }

    private async Task RequireBranchAsync(int actorId, int branchId, CancellationToken ct)
    {
        if (!(await StaffBranchesAsync(actorId, ct)).Contains(branchId)) throw Error("MSG06", 403);
    }

    private static void RequirePending(AppointmentSupportRequest request, int expectedVersion)
    {
        if (request.Status != "Pending" || request.Version != expectedVersion) throw Error("MSG111");
    }

    private AppointmentSupportHistory History(AppointmentSupportRequest request, Appointment appointment,
        int actorId, string action, string? notes) => new()
    {
        RequestId = request.Id, AppointmentId = appointment.Id, ActorAccountId = actorId,
        ActorKind = "Staff", Action = action, Notes = notes, OccurredAt = clock.GetUtcNow()
    };

    private static SupportRequestDto ToDto(AppointmentSupportRequest request, Appointment appointment, bool allowed,
        bool appointmentActionCommitted) => new(
        request.Code, appointment.Id, appointment.Code, request.Kind, request.Status, request.Version,
        request.RequestKey, request.Reason,
        request.ProposedStartAt.HasValue || request.ProposedReason != null
            ? new SupportProposedChange(request.ProposedStartAt, null, null, request.ProposedReason ?? "PatientRequest") : null,
        new SupportRequester(request.RequesterAccountId, request.RequesterName, request.RequesterPhone,
            !request.RequesterAccountId.HasValue), new StaffPermission(allowed, allowed, allowed, allowed),
        appointmentActionCommitted, request.CreatedAt, request.UpdatedAt);

    private async Task<SupportRequestDetail> ToDetailAsync(AppointmentSupportRequest request,
        Appointment appointment, CancellationToken ct)
    {
        var appointmentActionCommitted = await HasCommittedActionAsync(request, appointment, ct);
        var dto = ToDto(request, appointment, true, appointmentActionCommitted);
        var histories = new List<SupportHistoryEntry>
        {
            new(appointment.CreatedAt, "System", null, "Created", null,
                new { startAt = appointment.StartAt, status = appointment.Status }, null)
        };
        var appointmentHistory = await db.AppointmentChanges.AsNoTracking().Where(x => x.AppointmentId == appointment.Id)
            .OrderBy(x => x.OccurredAt).ToListAsync(ct);
        histories.AddRange(appointmentHistory.Select(x => new SupportHistoryEntry(x.OccurredAt,
            x.ActorKind == "STAFF" ? "Staff" : "Booker", x.ActorAccountId,
            x.Action == "CANCEL" ? "Cancelled" : "Rescheduled",
            new { startAt = x.OldStart }, new { startAt = x.NewStart, reason = x.Reason }, x.RequesterVerification)));
        var supportHistory = await db.AppointmentSupportHistories.AsNoTracking().Where(x => x.RequestId == request.Id)
            .OrderBy(x => x.OccurredAt).ToListAsync(ct);
        histories.AddRange(supportHistory.Select(x => new SupportHistoryEntry(x.OccurredAt, x.ActorKind,
            x.ActorAccountId, x.Action, null, null, x.Notes)));
        var decider = request.DecidedBy.HasValue
            ? await db.Users.AsNoTracking().Where(x => x.Id == request.DecidedBy).Select(x => x.FullName).SingleOrDefaultAsync(ct) : null;
        return new SupportRequestDetail(dto.Id, dto.AppointmentId, dto.AppointmentCode, dto.Kind, dto.Status,
            dto.ExpectedVersion, dto.RequestKey, dto.Reason, dto.ProposedChange, dto.Requester, dto.Permission,
            dto.AppointmentActionCommitted, dto.CreatedAt, dto.UpdatedAt, await AppointmentSummaryAsync(appointment, ct),
            histories.OrderBy(x => x.At).ToArray(), request.DecisionReason, request.DecidedAt, decider,
            request.RequesterVerifiedAt, request.RequesterVerification);
    }

    private async Task<bool> HasCommittedActionAsync(AppointmentSupportRequest request, Appointment appointment,
        CancellationToken ct)
    {
        if (request.Kind is not ("Change" or "Cancel") || !request.AppointmentActionRequestKey.HasValue) return false;
        var action = request.Kind == "Cancel" ? "CANCEL" : "RESCHEDULE";
        return await db.AppointmentChanges.AsNoTracking().AnyAsync(x => x.AppointmentId == appointment.Id
            && x.RequestKey == request.AppointmentActionRequestKey && x.Action == action, ct);
    }

    private async Task<SupportAppointmentSummary> AppointmentSummaryAsync(Appointment appointment, CancellationToken ct)
    {
        var branchName = await db.Set<SchedulingBranch>().AsNoTracking().Where(x => x.Id == appointment.BranchId)
            .Select(x => x.Name).SingleOrDefaultAsync(ct) ?? "";
        var dentistName = await (from employee in db.Employees.AsNoTracking()
            join user in db.Users.AsNoTracking() on employee.AccountId equals user.Id
            where employee.Id == appointment.DentistId select user.FullName).SingleOrDefaultAsync(ct) ?? "";
        var serviceName = appointment.ServiceId.HasValue
            ? await db.Set<SchedulingService>().AsNoTracking().Where(x => x.Id == appointment.ServiceId)
                .Select(x => x.Name).SingleOrDefaultAsync(ct) ?? "" : "";
        return new SupportAppointmentSummary(appointment.Id, appointment.Code, appointment.Version, appointment.Status,
            appointment.StartAt, appointment.EndAt, appointment.BranchId, branchName, appointment.ChairId,
            appointment.DentistId, dentistName, appointment.ServiceId, serviceName,
            (int)(appointment.EndAt - appointment.StartAt).TotalMinutes,
            new SupportAppointmentPerson(appointment.PatientId, appointment.AttendeeName,
                appointment.AttendeeBirthDate, appointment.AttendeeRelation),
            new SupportBooker(appointment.BookerAccountId, appointment.BookerName, appointment.BookerPhone,
                !appointment.BookerAccountId.HasValue), appointment.ConfirmedToAttend);
    }

    private async Task<T?> ReplayAsync<T>(int actorId, Guid key, string fingerprint, CancellationToken ct) where T : class
    {
        var receipt = await db.SupportRequestReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorAccountId == actorId && x.RequestKey == key, ct);
        if (receipt == null) return null;
        if (receipt.Fingerprint != fingerprint) throw Error("MSG111");
        return JsonSerializer.Deserialize<T>(receipt.ResultJson)!;
    }

    private async Task SaveReceiptAsync<T>(int actorId, Guid key, string fingerprint, Guid requestId,
        T result, CancellationToken ct)
    {
        db.SupportRequestReceipts.Add(new SupportRequestReceipt { ActorAccountId = actorId,
            RequestKey = key, Fingerprint = fingerprint, SupportRequestId = requestId,
            ResultJson = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync(ct);
    }

    private async Task PublishSupportUpdatedAsync(AppointmentSupportRequest request, Appointment appointment,
        string action)
    {
        try
        {
            await realtime.SendToStaffBranchAsync(appointment.BranchId, "supportRequestUpdated", new
            {
                requestId = request.Code,
                appointmentId = appointment.Id,
                branchId = appointment.BranchId,
                status = request.Status,
                version = request.Version,
                action,
                updatedAt = request.UpdatedAt
            }, CancellationToken.None);
        }
        catch (Exception error)
        {
            // Realtime is an invalidation hint. A push failure must never roll back or falsify
            // the already committed support/appointment command; clients can still reload via API.
            logger.LogWarning(error, "Realtime support update failed for request {RequestId}.", request.Code);
        }
    }
}

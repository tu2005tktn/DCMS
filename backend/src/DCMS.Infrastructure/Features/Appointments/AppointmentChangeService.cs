using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Scheduling;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Appointments;

public sealed class AppointmentChangeService(DcmsDbContext db, IAvailabilityService availability,
    IAppointmentQueryService queries, UserManager<ApplicationUser> users,
    TimeProvider clock, ISettingsAccess access) : IAppointmentChangeService
{
    private static BusinessException Error(string code, int status = 409) => new(status, SystemMessages.Get(code));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<AvailabilityResult> AvailabilityAsync(int actorId, string code, DateOnly date, int? dentistId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 40 || date.Year is < 2000 or > 2100) throw Error("MSG04", 400);
        var (appointment, staff) = await AuthorizeAsync(actorId, code, ct, clinicInitiated: true);
        var now = clock.GetUtcNow();
        var missed = staff && (await MissedAppointments.RebookableAsync(db, [appointment], now, ct)).Contains(appointment.Id);
        if (!missed) RequireAvailabilityEligible(appointment, now);
        var selectedDentistId = dentistId ?? appointment.DentistId;
        if (selectedDentistId <= 0) throw Error("MSG04", 400);
        var result = await availability.QueryForAppointmentAsync(new(appointment.BranchId, selectedDentistId,
            appointment.ServiceId, date, (int)(appointment.EndAt - appointment.StartAt).TotalMinutes),
            appointment.Id, appointment.PatientId, ct, requireLeadTime: !missed);
        return result with { Slots = result.Slots.Where(x => selectedDentistId != appointment.DentistId || x.Start != appointment.StartAt).ToArray() };
    }

    private static void RequireAvailabilityEligible(Appointment appointment, DateTimeOffset now)
    {
        if (appointment.Status != "Confirmed" || appointment.StartAt <= now) throw Error("MSG26");
    }

    private static void RequireEligible(Appointment appointment, bool staff, DateTimeOffset now)
    {
        if (staff ? appointment.Status != "Confirmed" || appointment.StartAt <= now
            : !AppointmentRules.CanSelfService(appointment.Status, appointment.StartAt, now)) throw Error("MSG26");
    }

    private async Task<(Appointment Appointment, bool Staff)> AuthorizeAsync(int actorId, string code, CancellationToken ct,
        string permission = "appointments.read", bool clinicInitiated = false)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user == null || !user.IsActive || (user.LockoutEnabled && user.LockoutEnd > clock.GetUtcNow())) throw Error("MSG06", 401);
        var staff = user.AccountType == "Staff";
        if (!staff && user.AccountType != "Public") throw Error("MSG05", 403);
        if (staff && !clinicInitiated && !(await access.ResolveAsync(actorId, ct)).GrantDriven
            && !await users.IsInRoleAsync(user, AccountRoles.ClinicAssistant)) throw Error("MSG05", 403);
        var appointment = await db.Appointments.SingleOrDefaultAsync(x => x.Code == code, ct) ?? throw Error("MSG37", 404);
        if (staff)
        {
            var state = await access.RequireAsync(actorId, permission, ct);
            if (!state.BranchIds.Contains(appointment.BranchId)) throw Error("MSG37", 404);
            if (clinicInitiated) await queries.DetailAsync(actorId, appointment.Id, ct);
        }
        else if (appointment.BookerAccountId != actorId && !(appointment.PatientId.HasValue &&
            await db.PatientAccountLinks.AnyAsync(x => x.AccountId == actorId && x.PatientId == appointment.PatientId
                && x.Status == PatientLinkStatus.Verified, ct))) throw Error("MSG37", 404);
        return (appointment, staff);
    }

    public Task<AppointmentChangeResult> RescheduleAsync(int actorId, string code, RescheduleAppointmentRequest request, CancellationToken ct) =>
        ChangeAsync(actorId, code, request.ExpectedVersion, request.RequestKey, request.Reason, request.RequesterVerification, request, request.ClinicInitiated, ct);

    public Task<AppointmentChangeResult> CancelAsync(int actorId, string code, CancelAppointmentRequest request, CancellationToken ct) =>
        ChangeAsync(actorId, code, request.ExpectedVersion, request.RequestKey, request.Reason, request.RequesterVerification, null, request.ClinicInitiated, ct);

    public async Task<AppointmentChangeResult> MarkNoShowAsync(int actorId, string code,
        NoShowAppointmentRequest request, CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty || request.ExpectedVersion <= 0 ||
            string.IsNullOrWhiteSpace(code) || code.Length > 40) throw Error("MSG04", 400);
        var reason = RequiredText(request.Reason, "Lý do");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { action = "NO_SHOW", code, request.ExpectedVersion, reason }))));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})", ct);
        var (appointment, staff) = await AuthorizeAsync(actorId, code, ct, "appointments.no-show", clinicInitiated: true);
        if (!staff) throw Error("MSG05", 403);
        var receipt = await db.AppointmentChangeReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.ActorAccountId == actorId && x.RequestKey == request.RequestKey, ct);
        if (receipt != null)
        {
            if (receipt.Fingerprint != fingerprint) throw Error("MSG41");
            return JsonSerializer.Deserialize<AppointmentChangeResult>(receipt.ResultJson)!;
        }
        var now = clock.GetUtcNow();
        if (appointment.Version != request.ExpectedVersion) throw Error("MSG41");
        if (appointment.Status != "Confirmed" || appointment.StartAt > now || await db.Visits.AnyAsync(x => x.AppointmentId == appointment.Id, ct)) throw Error("MSG26");
        appointment.Status = "NoShow";
        appointment.Version++;
        db.AppointmentChanges.Add(new AppointmentChange
        {
            AppointmentId = appointment.Id, ActorAccountId = actorId, ActorKind = "STAFF",
            Action = "NO_SHOW", OldStart = appointment.StartAt, OldEnd = appointment.EndAt,
            OldChairId = appointment.ChairId, OldDentistId = appointment.DentistId,
            Reason = reason, RequestKey = request.RequestKey, OccurredAt = now
        });
        db.AppointmentOutboxEvents.Add(new AppointmentOutboxEvent
        {
            AggregateId = appointment.Id, EventType = "appointment.no_show", OccurredAt = now,
            PayloadJson = JsonSerializer.Serialize(new { appointmentId = appointment.Id,
                appointment.Code, appointment.Version, appointment.DossierId })
        });
        await db.SaveChangesAsync(ct);
        var result = new AppointmentChangeResult(await queries.ChangeDetailAsync(actorId, code, ct),
            SystemMessages.Get("MSG53", "trạng thái vắng mặt", value: code));
        db.AppointmentChangeReceipts.Add(new AppointmentChangeReceipt
        {
            ActorAccountId = actorId, RequestKey = request.RequestKey, AppointmentId = appointment.Id,
            Fingerprint = fingerprint, ResultJson = JsonSerializer.Serialize(result)
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<AppointmentChangeResult> UndoNoShowAsync(int actorId, string code,
        NoShowAppointmentRequest request, CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty || request.ExpectedVersion <= 0 ||
            string.IsNullOrWhiteSpace(code) || code.Length > 40) throw Error("MSG04", 400);
        var reason = RequiredText(request.Reason, "Lý do");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { action = "UNDO_NO_SHOW", code, request.ExpectedVersion, reason }))));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})", ct);
        var (appointment, staff) = await AuthorizeAsync(actorId, code, ct, "appointments.no-show", clinicInitiated: true);
        if (!staff) throw Error("MSG05", 403);
        var receipt = await db.AppointmentChangeReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.ActorAccountId == actorId && x.RequestKey == request.RequestKey, ct);
        if (receipt != null)
        {
            if (receipt.Fingerprint != fingerprint) throw Error("MSG41");
            return JsonSerializer.Deserialize<AppointmentChangeResult>(receipt.ResultJson)!;
        }
        var now = clock.GetUtcNow();
        if (appointment.Version != request.ExpectedVersion) throw Error("MSG41");
        if (appointment.Status != "NoShow") throw Error("MSG26");
        if (!(await MissedAppointments.UndoableAsync(db, [appointment], now, ct)).Contains(appointment.Id)) throw Error("MSG119");
        // The freed time may already belong to someone else (replacement walk-in, or a new booking).
        if (await db.Appointments.AsNoTracking().AnyAsync(x => x.Id != appointment.Id
            && (x.Status == "Confirmed" || x.Status == "CheckedIn" || x.Status == "InProgress" || x.Status == "Completed")
            && x.StartAt < appointment.EndAt && x.EndAt > appointment.StartAt
            && (x.ChairId == appointment.ChairId || x.DentistId == appointment.DentistId
                || appointment.PatientId != null && x.PatientId == appointment.PatientId), ct)) throw Error("MSG120");
        if (appointment.DossierId != null && await db.TreatmentDossiers.AsNoTracking()
            .AnyAsync(x => x.Id == appointment.DossierId && x.State == "Deleted", ct)) throw Error("MSG121");
        appointment.Status = "Confirmed";
        appointment.Version++;
        db.AppointmentChanges.Add(new AppointmentChange
        {
            AppointmentId = appointment.Id, ActorAccountId = actorId, ActorKind = "STAFF",
            Action = "UNDO_NO_SHOW", OldStart = appointment.StartAt, OldEnd = appointment.EndAt,
            OldChairId = appointment.ChairId, OldDentistId = appointment.DentistId,
            Reason = reason, RequestKey = request.RequestKey, OccurredAt = now
        });
        await CloseMissedTasksAsync(appointment.Id, "MissedAppointmentUndone", ct);
        db.AppointmentOutboxEvents.Add(new AppointmentOutboxEvent
        {
            AggregateId = appointment.Id, EventType = "appointment.no_show_undone", OccurredAt = now,
            PayloadJson = JsonSerializer.Serialize(new { appointmentId = appointment.Id,
                appointment.Code, appointment.Version, appointment.DossierId })
        });
        await db.SaveChangesAsync(ct);
        var result = new AppointmentChangeResult(await queries.ChangeDetailAsync(actorId, code, ct),
            SystemMessages.Get("MSG53", "hoàn tác khách không tới", value: code));
        db.AppointmentChangeReceipts.Add(new AppointmentChangeReceipt
        {
            ActorAccountId = actorId, RequestKey = request.RequestKey, AppointmentId = appointment.Id,
            Fingerprint = fingerprint, ResultJson = JsonSerializer.Serialize(result)
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    // The callback task of a no-show is kept for history but retyped, so a later real no-show gets a new one.
    private async Task CloseMissedTasksAsync(Guid appointmentId, string type, CancellationToken ct)
    {
        foreach (var task in await db.CareTasks.Where(x => x.AppointmentId == appointmentId && x.Type == "MissedAppointment").ToListAsync(ct))
        {
            task.Type = type;
            if (task.Status is "Open" or "InProgress") task.Status = "Cancelled";
            task.Version++;
        }
    }

    private async Task RequireRebookAsync(Appointment appointment, DateTimeOffset now, CancellationToken ct)
    {
        if (appointment.DossierId != null && await db.TreatmentDossiers.AsNoTracking()
            .AnyAsync(x => x.Id == appointment.DossierId && x.State == "Deleted", ct)) throw Error("MSG121");
        // The person may have booked again after missing this one; do not leave them with two open bookings.
        if (await OpenBookings.Of(db, now, appointment.PatientId, appointment.FamilyMemberId, appointment.AttendeeName,
                appointment.BookerAccountId, appointment.BookingInfo.BookerContact)
            .AnyAsync(x => x.Id != appointment.Id, ct)) throw Error("MSG118");
    }

    private static string RequiredText(string? value, string field)
    {
        var text = Clean(value);
        if (text == null) throw new BusinessException(400, SystemMessages.Get("MSG02", field));
        if (text.Length > 500) throw new BusinessException(400, SystemMessages.Get("MSG03", field, 500));
        return text;
    }

    private async Task<AppointmentChangeResult> ChangeAsync(int actorId, string code, int expectedVersion, Guid requestKey,
        string? rawReason, string? rawVerification, RescheduleAppointmentRequest? move, bool clinicInitiated, CancellationToken ct)
    {
        if (requestKey == Guid.Empty || expectedVersion <= 0 || string.IsNullOrWhiteSpace(code) || code.Length > 40
            || (move != null && (move.NewStart == default || move.ChairId <= 0 || move.ScheduleVersion <= 0
                || move.AssistantScheduleVersion is <= 0))) throw Error("MSG04", 400);
        var reason = RequiredText(rawReason, "Lý do");
        var verification = Clean(rawVerification);
        if (verification != null) verification = RequiredText(verification, "Xác minh người yêu cầu");
        var action = move == null ? "CANCEL" : "RESCHEDULE";
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            action, code, expectedVersion, reason, verification,
            NewStart = move?.NewStart.ToUniversalTime(), move?.ChairId, move?.ScheduleVersion,
            move?.AssistantScheduleVersion, move?.DentistId, move?.BookingSettingsVersion
        }) + (clinicInitiated ? "|clinic-initiated:true" : ""))));

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})", ct);
        var (appointment, staff) = await AuthorizeAsync(actorId, code, ct, move == null ? "appointments.cancel" : "appointments.reschedule", clinicInitiated);
        if (clinicInitiated && !staff) throw Error("MSG05", 403);
        if (staff) verification = clinicInitiated ? "Phòng khám chủ động điều chỉnh lịch hẹn." : RequiredText(verification, "Xác minh người yêu cầu");

        var receipt = await db.AppointmentChangeReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorAccountId == actorId && x.RequestKey == requestKey, ct);
        if (receipt != null)
        {
            if (receipt.Fingerprint != fingerprint) throw Error("MSG41");
            return JsonSerializer.Deserialize<AppointmentChangeResult>(receipt.ResultJson)!;
        }
        var now = clock.GetUtcNow();
        if (appointment.Version != expectedVersion) throw Error("MSG41");
        // Staff may move a missed booking (late arrival, or a no-show recorded today) to a new time without the usual limits.
        var missed = move != null && staff && clinicInitiated
            && (await MissedAppointments.RebookableAsync(db, [appointment], now, ct)).Contains(appointment.Id);
        if (missed) await RequireRebookAsync(appointment, now, ct);
        else RequireEligible(appointment, staff, now);
        var change = new AppointmentChange
        {
            AppointmentId = appointment.Id, ActorAccountId = actorId, ActorKind = staff ? "STAFF" : "SELF",
            Action = action, OldStart = appointment.StartAt, OldEnd = appointment.EndAt, OldChairId = appointment.ChairId,
            OldDentistId = appointment.DentistId,
            Reason = reason, RequesterVerification = verification, RequestKey = requestKey, OccurredAt = now
        };
        if (move != null)
        {
            var selectedDentistId = move.DentistId ?? appointment.DentistId;
            if (staff && clinicInitiated && !(await queries.OptionsAsync(actorId, ct)).Dentists.Any(x => x.BranchId == appointment.BranchId && x.Id == selectedDentistId))
                throw Error("MSG05", 403);
            if (selectedDentistId <= 0) throw Error("MSG29");
            if (missed ? move.NewStart <= now : !AppointmentRules.PublicLeadTimeValid(now, move.NewStart)) throw Error("MSG29");
            if (move.NewStart == appointment.StartAt && move.ChairId == appointment.ChairId
                && selectedDentistId == appointment.DentistId) throw Error("MSG29");
            AvailabilitySlot slot;
            try
            {
                slot = await availability.CheckInTransactionAsync(new ResourceCheck(appointment.BranchId,
                    selectedDentistId, appointment.ServiceId, move.NewStart.ToUniversalTime(),
                    (int)(appointment.EndAt - appointment.StartAt).TotalMinutes, move.ChairId, null,
                    appointment.PatientId, move.ScheduleVersion, move.AssistantScheduleVersion, appointment.Id, Public: false, BookingSettingsVersion: move.BookingSettingsVersion), ct);
            }
            catch (BusinessException error) when (error.Response.Code != "MSG41") { throw Error("MSG29"); }
            appointment.StartAt = slot.Start.ToUniversalTime();
            appointment.EndAt = slot.End.ToUniversalTime();
            appointment.ChairId = slot.ChairId;
            appointment.DentistId = slot.DentistId;
            change.NewStart = appointment.StartAt; change.NewEnd = appointment.EndAt;
            change.NewDentistId = appointment.DentistId; change.NewChairId = appointment.ChairId;
            if (missed)
            {
                appointment.Status = "Confirmed";
                await CloseMissedTasksAsync(appointment.Id, "MissedAppointmentRebooked", ct);
            }
        }
        else
        {
            appointment.Status = "Cancelled";
        }
        appointment.Version++;
        db.AppointmentChanges.Add(change);
        db.AppointmentOutboxEvents.Add(new AppointmentOutboxEvent
        {
            AggregateId = appointment.Id, EventType = move == null ? "appointment.cancelled" : "appointment.rescheduled", OccurredAt = now,
            PayloadJson = JsonSerializer.Serialize(new { appointmentId = appointment.Id, appointment.Code, appointment.Version,
                appointment.Status, appointment.BranchId, oldStart = change.OldStart, oldEnd = change.OldEnd,
                newStart = change.NewStart, newEnd = change.NewEnd, appointment.DentistId, appointment.ChairId })
        });
        await db.SaveChangesAsync(ct);
        var result = new AppointmentChangeResult(await queries.ChangeDetailAsync(actorId, code, ct),
            SystemMessages.Get(move == null ? "MSG31" : "MSG30", value: move == null ? code :
                appointment.StartAt.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture)));
        db.AppointmentChangeReceipts.Add(new AppointmentChangeReceipt { ActorAccountId = actorId, RequestKey = requestKey,
            AppointmentId = appointment.Id, Fingerprint = fingerprint, ResultJson = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }
}

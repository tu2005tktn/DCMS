using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Scheduling;
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
    IFollowUpAppointmentChangePort followUps, TimeProvider clock) : IAppointmentChangeService
{
    private static BusinessException Error(string code, int status = 409) => new(status, SystemMessages.Get(code));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<AvailabilityResult> AvailabilityAsync(int actorId, string code, DateOnly date, int? dentistId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 40 || date.Year is < 2000 or > 2100) throw Error("MSG04", 400);
        var (appointment, staff) = await AuthorizeAsync(actorId, code, ct);
        RequireAvailabilityEligible(appointment, clock.GetUtcNow());
        var selectedDentistId = dentistId ?? appointment.DentistId;
        if (selectedDentistId <= 0 || selectedDentistId == appointment.AssistantId) throw Error("MSG04", 400);
        var reservation = await db.Set<ResourceReservation>().AsNoTracking().SingleAsync(x => x.Id == appointment.ReservationId, ct);
        var result = await availability.QueryForReservationAsync(new(appointment.BranchId, selectedDentistId,
            appointment.ServiceId, date, (int)(appointment.EndAt - appointment.StartAt).TotalMinutes, appointment.AssistantId),
            reservation.Id, reservation.AttendeeKey, ct);
        return result with { Slots = result.Slots.Where(x => selectedDentistId != appointment.DentistId || x.Start != appointment.StartAt).ToArray() };
    }

    private static void RequireAvailabilityEligible(Appointment appointment, DateTimeOffset now)
    {
        if (appointment.Status != "Confirmed" || appointment.StartAt <= now) throw Error("MSG31");
    }

    private static void RequireEligible(Appointment appointment, bool staff, DateTimeOffset now)
    {
        if (staff ? appointment.Status != "Confirmed" || appointment.StartAt <= now
            : !AppointmentRules.CanSelfService(appointment.Status, appointment.StartAt, now)) throw Error("MSG31");
    }

    private async Task<(Appointment Appointment, bool Staff)> AuthorizeAsync(int actorId, string code, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user == null || !user.IsActive || (user.LockoutEnabled && user.LockoutEnd > clock.GetUtcNow())) throw Error("MSG07", 401);
        var staff = user.AccountType == "Staff";
        if (!staff && user.AccountType != "Public") throw Error("MSG06", 403);
        if (staff && !await users.IsInRoleAsync(user, AccountRoles.ClinicAssistant)) throw Error("MSG06", 403);
        var appointment = await db.Appointments.SingleOrDefaultAsync(x => x.Code == code, ct) ?? throw Error("MSG102", 404);
        if (staff)
        {
            if (!await (from member in db.Set<SchedulingMember>() join employee in db.Employees on member.EmployeeId equals employee.Id
                join branch in db.Set<SchedulingBranch>() on member.BranchId equals branch.Id
                where employee.AccountId == actorId && member.BranchId == appointment.BranchId && member.Active && branch.Active
                select member).AnyAsync(ct)) throw Error("MSG102", 404);
        }
        else if (appointment.BookerAccountId != actorId && !(appointment.PatientId.HasValue &&
            await db.PatientAccountLinks.AnyAsync(x => x.AccountId == actorId && x.PatientId == appointment.PatientId
                && x.Status == PatientLinkStatus.Verified, ct))) throw Error("MSG102", 404);
        return (appointment, staff);
    }

    public Task<AppointmentChangeResult> RescheduleAsync(int actorId, string code, RescheduleAppointmentRequest request, CancellationToken ct) =>
        ChangeAsync(actorId, code, request.ExpectedVersion, request.RequestKey, request.Reason, request.RequesterVerification, request, ct);

    public Task<AppointmentChangeResult> CancelAsync(int actorId, string code, CancelAppointmentRequest request, CancellationToken ct) =>
        ChangeAsync(actorId, code, request.ExpectedVersion, request.RequestKey, request.Reason, request.RequesterVerification, null, ct);

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
        var (appointment, staff) = await AuthorizeAsync(actorId, code, ct);
        if (!staff) throw Error("MSG06", 403);
        var receipt = await db.AppointmentChangeReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.ActorAccountId == actorId && x.RequestKey == request.RequestKey, ct);
        if (receipt != null)
        {
            if (receipt.Fingerprint != fingerprint) throw Error("MSG111");
            return JsonSerializer.Deserialize<AppointmentChangeResult>(receipt.ResultJson)!;
        }
        var now = clock.GetUtcNow();
        if (appointment.Version != request.ExpectedVersion) throw Error("MSG111");
        if (appointment.Status != "Confirmed" || appointment.StartAt > now) throw Error("MSG31");
        var reservation = await db.Set<ResourceReservation>().SingleAsync(x => x.Id == appointment.ReservationId, ct);
        if (reservation.Status != "Confirmed") throw Error("MSG111");
        if (appointment.FollowUpId.HasValue)
            await followUps.MarkNeedsBookingAsync(appointment.FollowUpId.Value, appointment.Id, ct);
        appointment.Status = reservation.Status = "NoShow";
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
                appointment.Code, appointment.Version, appointment.FollowUpId })
        });
        await db.SaveChangesAsync(ct);
        var result = new AppointmentChangeResult(await queries.ChangeDetailAsync(actorId, code, ct),
            SystemMessages.Get("MSG222", "trạng thái vắng mặt", value: code));
        db.AppointmentChangeReceipts.Add(new AppointmentChangeReceipt
        {
            ActorAccountId = actorId, RequestKey = request.RequestKey, AppointmentId = appointment.Id,
            Fingerprint = fingerprint, ResultJson = JsonSerializer.Serialize(result)
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private static string RequiredText(string? value, string field)
    {
        var text = Clean(value);
        if (text == null) throw new BusinessException(400, SystemMessages.Get("MSG02", field));
        if (text.Length > 500) throw new BusinessException(400, SystemMessages.Get("MSG03", field, 500));
        return text;
    }

    private async Task<AppointmentChangeResult> ChangeAsync(int actorId, string code, int expectedVersion, Guid requestKey,
        string? rawReason, string? rawVerification, RescheduleAppointmentRequest? move, CancellationToken ct)
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
        }))));

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})", ct);
        var (appointment, staff) = await AuthorizeAsync(actorId, code, ct);
        if (staff) verification = RequiredText(verification, "Xác minh người yêu cầu");

        var receipt = await db.AppointmentChangeReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorAccountId == actorId && x.RequestKey == requestKey, ct);
        if (receipt != null)
        {
            if (receipt.Fingerprint != fingerprint) throw Error("MSG111");
            return JsonSerializer.Deserialize<AppointmentChangeResult>(receipt.ResultJson)!;
        }
        var now = clock.GetUtcNow();
        if (appointment.Version != expectedVersion) throw Error("MSG111");
        RequireEligible(appointment, staff, now);
        var reservation = await db.Set<ResourceReservation>().SingleAsync(x => x.Id == appointment.ReservationId, ct);
        if (reservation.Status != "Confirmed" || reservation.Start != appointment.StartAt || reservation.End != appointment.EndAt)
            throw Error("MSG111");
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
            if (selectedDentistId <= 0 || selectedDentistId == appointment.AssistantId) throw Error("MSG34");
            if (!AppointmentRules.PublicLeadTimeValid(now, move.NewStart)) throw Error("MSG34");
            if (move.NewStart == appointment.StartAt && move.ChairId == appointment.ChairId
                && selectedDentistId == appointment.DentistId) throw Error("MSG34");
            if (appointment.FollowUpId.HasValue)
                await followUps.ValidateRescheduleAsync(appointment.FollowUpId.Value, appointment.Id,
                    selectedDentistId, move.NewStart, ct);
            AvailabilitySlot slot;
            try
            {
                slot = await availability.CheckInTransactionAsync(new ResourceCheck(appointment.BranchId,
                    selectedDentistId, appointment.ServiceId, move.NewStart.ToUniversalTime(),
                    (int)(appointment.EndAt - appointment.StartAt).TotalMinutes, move.ChairId, appointment.AssistantId,
                    reservation.AttendeeKey, move.ScheduleVersion, move.AssistantScheduleVersion, reservation.Id, Public: false, BookingSettingsVersion: move.BookingSettingsVersion), ct);
            }
            catch (BusinessException error) when (error.Response.Code != "MSG111") { throw Error("MSG34"); }
            appointment.StartAt = reservation.Start = slot.Start.ToUniversalTime();
            appointment.EndAt = reservation.End = slot.End.ToUniversalTime();
            appointment.ChairId = reservation.ChairId = slot.ChairId;
            appointment.DentistId = reservation.DentistId = slot.DentistId;
            change.NewStart = appointment.StartAt; change.NewEnd = appointment.EndAt;
            change.NewDentistId = appointment.DentistId; change.NewChairId = appointment.ChairId;
        }
        else
        {
            if (appointment.FollowUpId.HasValue)
                await followUps.MarkNeedsBookingAsync(appointment.FollowUpId.Value, appointment.Id, ct);
            appointment.Status = reservation.Status = "Cancelled";
            appointment.CancelledAt = now; appointment.CancelledBy = actorId; appointment.CancelReason = reason;
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
            SystemMessages.Get(move == null ? "MSG37" : "MSG35", value: move == null ? code :
                appointment.StartAt.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture)));
        db.AppointmentChangeReceipts.Add(new AppointmentChangeReceipt { ActorAccountId = actorId, RequestKey = requestKey,
            AppointmentId = appointment.Id, Fingerprint = fingerprint, ResultJson = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }
}

public sealed class UnavailableFollowUpAppointmentChangePort : IFollowUpAppointmentChangePort
{
    public Task ValidateRescheduleAsync(Guid followUpId, Guid appointmentId, int dentistId,
        DateTimeOffset startAt, CancellationToken ct) =>
        throw new BusinessException(503, SystemMessages.Get("MSG144"));
    public Task MarkNeedsBookingAsync(Guid followUpId, Guid appointmentId, CancellationToken ct) =>
        throw new BusinessException(503, SystemMessages.Get("MSG144"));
}

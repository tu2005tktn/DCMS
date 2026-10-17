using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

public sealed class AppointmentService(DcmsDbContext db, IAvailabilityService availability,
    UserManager<ApplicationUser> users, IFollowUpBookingPort followUps, TimeProvider clock) : IAppointmentService
{
    private static BusinessException Error(string code, int status = 400) => new(status, SystemMessages.Get(code));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool ValidPhone(string? value) => value != null && Regex.IsMatch(value, @"^0[0-9]{9}$", RegexOptions.CultureInvariant);
    private static bool ValidEmail(string? value) => value != null && value.Length <= 254
        && System.Net.Mail.MailAddress.TryCreate(value, out var address) && address.Address.Equals(value, StringComparison.OrdinalIgnoreCase);

    public async Task<CreateAppointmentOutcome> CreateAsync(int? accountId, CreateAppointmentRequest request, CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty || request.BranchId <= 0 || request.DentistId <= 0
            || request.ScheduleVersion <= 0 || request.StartAt == default) throw Error("MSG04");

        var account = accountId.HasValue ? await users.FindByIdAsync(accountId.Value.ToString()) : null;
        if (accountId.HasValue && (account == null || !account.IsActive)) throw Error("MSG07", 401);
        var staff = account?.AccountType == "Staff";
        if (staff)
        {
            if (!await users.IsInRoleAsync(account!, AccountRoles.ClinicAssistant)) throw Error("MSG06", 403);
            var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == accountId, ct);
            if (employee == null || !await db.Set<SchedulingMember>().AsNoTracking().AnyAsync(x =>
                    x.BranchId == request.BranchId && x.EmployeeId == employee.Id && x.Active, ct)) throw Error("MSG06", 403);
        }

        var relation = Clean(request.AttendeeRelation) ?? "Self";
        var kind = Clean(request.Kind) ?? "Standard";
        var source = staff ? Clean(request.Source) ?? "Phone" : "App";
        if (relation is not ("Self" or "Relative") || kind is not ("Standard" or "WalkIn" or "FollowUp")
            || (staff && source is not ("Phone" or "Zalo" or "Facebook" or "InPerson" or "App"))
            || (!staff && kind == "WalkIn") || (request.FollowUpId.HasValue != (kind == "FollowUp"))) throw Error("MSG04");

        var bookerName = Clean(request.Booker?.Name) ?? Clean(account?.FullName);
        var phone = Clean(request.Booker?.Phone) ?? Clean(account?.PhoneNumber);
        var email = Clean(request.Booker?.Email) ?? Clean(account?.Email);
        if (bookerName == null || bookerName.Length > 100 || (phone == null && email == null)
            || (phone != null && !ValidPhone(phone)) || (email != null && !ValidEmail(email))) throw Error("MSG04");
        email = email?.ToLowerInvariant();

        var attendeeName = Clean(request.Attendee?.Name);
        var attendeeBirthDate = request.Attendee?.BirthDate;
        if (attendeeName == null || attendeeName.Length > 100) throw Error("MSG23");
        if (request.Attendee?.BirthDate is { } birthDate
            && birthDate > DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime)) throw Error("MSG04");
        var patientId = request.Attendee?.PatientId;
        // A verified self link is explicit evidence of identity; a phone number is never used for this.
        if (!staff && relation == "Self" && accountId.HasValue && !patientId.HasValue)
            patientId = await db.PatientAccountLinks.AsNoTracking()
                .Where(x => x.AccountId == accountId && x.Status == PatientLinkStatus.Verified)
                .Select(x => x.PatientId).SingleOrDefaultAsync(ct);
        if (patientId.HasValue)
        {
            if (!staff && (relation != "Self" || !accountId.HasValue || !await db.PatientAccountLinks.AsNoTracking().AnyAsync(x =>
                    x.AccountId == accountId && x.PatientId == patientId && x.Status == PatientLinkStatus.Verified, ct))) throw Error("MSG06", 403);
            var patient = await db.Patients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == patientId.Value, ct);
            if (patient == null) throw Error("MSG06", 404);
            attendeeName = patient.FullName;
            attendeeBirthDate = patient.BirthDate;
        }
        if (request.FollowUpId.HasValue && (!patientId.HasValue || (!staff && relation != "Self"))) throw Error("MSG06", 403);

        var reason = Clean(request.Reason);
        var exceptionReason = Clean(request.ExceptionReason);
        if (reason?.Length > 1000 || exceptionReason?.Length > 500 || (staff && request.ServiceId == null && reason == null)
            || (!staff && (request.ServiceId == null || request.AssistantId.HasValue || exceptionReason != null))) throw Error("MSG04");
        if (!ScheduleRules.ValidDuration(request.DurationMinutes, !staff)) throw Error("MSG04");
        var start = request.StartAt.ToUniversalTime();
        var now = clock.GetUtcNow();

        var scope = accountId.HasValue ? $"account:{accountId.Value}" : "guest:" + Hash($"{phone}|{email}");
        var canonical = new
        {
            request.BranchId, BookerName = Clean(request.Booker?.Name), Phone = Clean(request.Booker?.Phone),
            Email = Clean(request.Booker?.Email)?.ToLowerInvariant(), PatientId = request.Attendee?.PatientId,
            AttendeeName = Clean(request.Attendee?.Name), BirthDate = request.Attendee?.BirthDate,
            Relation = relation, Kind = kind, Source = source,
            request.ServiceId, Reason = reason, request.DentistId, request.AssistantId,
            ChairId = staff ? request.ChairId : null, StartAt = start, request.DurationMinutes,
            request.FollowUpId, ExceptionReason = exceptionReason, request.ScheduleVersion, request.AssistantScheduleVersion
        };
        var fingerprint = Hash(JsonSerializer.Serialize(canonical));

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Share S09's reservation lock so duplicate requests and competing slots serialize.
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})", ct);
        var receipt = await db.AppointmentReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, ct);
        if (receipt != null)
        {
            if (receipt.ActorScope != scope || receipt.Fingerprint != fingerprint) throw Error("MSG111", 409);
            var previous = JsonSerializer.Deserialize<AppointmentResult>(receipt.ResultJson)!;
            return new(previous with { Notification = SystemMessages.Get("MSG30") }, true);
        }

        if (start <= now) throw Error("MSG25", 409);
        if (!staff && !AppointmentRules.PublicLeadTimeValid(now, start)) throw Error("MSG24");
        if (staff && start < now.AddMinutes(60) && exceptionReason == null) throw Error("MSG24");

        if (request.FollowUpId.HasValue)
            await followUps.ValidateAsync(request.FollowUpId.Value, patientId!.Value, request.DentistId, request.DurationMinutes, ct);

        var slot = await availability.CheckInTransactionAsync(new ResourceCheck(request.BranchId, request.DentistId,
            request.ServiceId, start, request.DurationMinutes, staff ? request.ChairId : null, request.AssistantId,
            patientId.HasValue ? $"patient:{patientId.Value}" : null, request.ScheduleVersion,
            request.AssistantScheduleVersion, Public: !staff), ct);

        var id = Guid.NewGuid();
        var code = "AP-" + id.ToString("N").ToUpperInvariant();
        var reservation = new ResourceReservation
        {
            Id = id, Code = code, BranchId = request.BranchId, DentistId = request.DentistId,
            AssistantId = request.AssistantId, ChairId = slot.ChairId, ServiceId = request.ServiceId,
            Start = slot.Start.ToUniversalTime(), End = slot.End.ToUniversalTime(),
            AttendeeKey = patientId.HasValue ? $"patient:{patientId.Value}" : null,
            AttendeeLabel = attendeeName, Status = "Confirmed"
        };
        var appointment = new Appointment
        {
            Id = id, ReservationId = id, Code = code, BranchId = request.BranchId, BookerAccountId = accountId,
            BookerName = bookerName, BookerPhone = phone, BookerEmail = email, PatientId = patientId,
            AttendeeName = attendeeName, AttendeeBirthDate = attendeeBirthDate,
            AttendeeRelation = relation, Kind = kind, Source = source, ServiceId = request.ServiceId,
            Reason = reason, DentistId = request.DentistId, AssistantId = request.AssistantId,
            ChairId = slot.ChairId, StartAt = reservation.Start, EndAt = reservation.End,
            FollowUpId = request.FollowUpId, ExceptionReason = exceptionReason, CreatedAt = now
        };
        db.Add(reservation);
        db.Add(appointment);
        db.Add(new AppointmentReceipt { ActorScope = scope, RequestKey = request.RequestKey,
            Fingerprint = fingerprint, AppointmentId = id,
            ResultJson = JsonSerializer.Serialize(Result(appointment, "MSG29")) });
        db.Add(new AppointmentOutboxEvent
        {
            AggregateId = id, OccurredAt = now,
            PayloadJson = JsonSerializer.Serialize(new { appointmentId = id, code, version = 1,
                status = "Confirmed", branchId = request.BranchId, startAt = reservation.Start, endAt = reservation.End })
        });
        await db.SaveChangesAsync(ct);
        if (request.FollowUpId.HasValue)
        {
            await followUps.MarkBookedAsync(request.FollowUpId.Value, id, ct);
            await db.SaveChangesAsync(ct);
        }
        var saved = await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        await tx.CommitAsync(ct);
        return new(Result(saved, "MSG29"), false);
    }

    private static AppointmentResult Result(Appointment appointment, string message) => new(
        appointment.Id, appointment.Code, appointment.Version, appointment.Status,
        appointment.StartAt, appointment.EndAt, SystemMessages.Get(message));
}

// A missing TV4 module must not silently create a booking that claims a follow-up was booked.
public sealed class UnavailableFollowUpBookingPort : IFollowUpBookingPort
{
    public Task ValidateAsync(Guid followUpId, int patientId, int dentistId, int durationMinutes, CancellationToken ct) =>
        throw new BusinessException(503, SystemMessages.Get("MSG144"));
    public Task MarkBookedAsync(Guid followUpId, Guid appointmentId, CancellationToken ct) =>
        throw new BusinessException(503, SystemMessages.Get("MSG144"));
}

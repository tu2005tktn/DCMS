using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

public sealed class AppointmentService(DcmsDbContext db, IAvailabilityService availability,
    UserManager<ApplicationUser> users, TimeProvider clock,
    ISettingsAccess access, IAppointmentQueryService queries) : IAppointmentService
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
            || request.ScheduleVersion <= 0 || request.StartAt == default || request.ReplacesAppointmentId == Guid.Empty) throw Error("MSG04");

        var account = accountId.HasValue ? await users.FindByIdAsync(accountId.Value.ToString()) : null;
        if (accountId.HasValue && (account == null || !account.IsActive)) throw Error("MSG06", 401);
        var staff = account?.AccountType == "Staff";
        if (request.ReplacesAppointmentId.HasValue && !staff) throw Error("MSG05", 403);
        if (staff)
        {
            // Counter booking is for assistants; a dentist may only book a treatment follow-up of their own plan.
            var treatmentFollowUp = request.DossierId.HasValue
                && AccountRoles.Effective(await users.GetRolesAsync(account!)).Contains(AccountRoles.Dentist);
            if (request.ReplacesAppointmentId.HasValue) await access.RequireAsync(accountId!.Value, "appointments.create", ct);
            // Staff in an access group book through appointments.create (enforced per endpoint), not their role.
            var grantDriven = (await access.ResolveAsync(accountId!.Value, ct)).GrantDriven;
            if (!request.ReplacesAppointmentId.HasValue && !treatmentFollowUp && !grantDriven && !await users.IsInRoleAsync(account!, AccountRoles.ClinicAssistant)) throw Error("MSG05", 403);
            var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == accountId, ct);
            if (employee == null || !await db.SchedulingMembers.AsNoTracking().AnyAsync(x =>
                    x.BranchId == request.BranchId && x.EmployeeId == employee.Id && x.Active, ct)) throw Error("MSG05", 403);
        }

        var relation = Clean(request.AttendeeRelation) ?? "Self";
        var kind = Clean(request.Kind) ?? "Standard";
        var source = staff ? Clean(request.Source) ?? "Phone" : "App";
        if (request.ReplacesAppointmentId.HasValue && (kind != "WalkIn" || source != "InPerson" || request.DossierId.HasValue)) throw Error("MSG04");
        if (request.FamilyMemberId.HasValue && (account?.AccountType != "Public" || relation != "Relative")) throw Error("MSG05", 403);
        if (relation is not ("Self" or "Relative") || kind is not ("Standard" or "WalkIn" or "FollowUp")
            || (staff && source is not ("Phone" or "Zalo" or "Facebook" or "InPerson" or "App"))
            || (!staff && kind == "WalkIn") || (request.DossierId.HasValue != (kind == "FollowUp"))) throw Error("MSG04");

        var bookerName = Clean(request.Booker?.Name) ?? Clean(account?.FullName);
        var phone = Clean(request.Booker?.Phone) ?? Clean(account?.PhoneNumber);
        var email = Clean(request.Booker?.Email) ?? Clean(account?.Email);
        if (bookerName == null || bookerName.Length > 100 || (phone == null && email == null)
            || (phone != null && !ValidPhone(phone)) || (email != null && !ValidEmail(email))) throw Error("MSG04");
        email = email?.ToLowerInvariant();

        var attendeeName = Clean(request.Attendee?.Name);
        var attendeeBirthDate = request.Attendee?.BirthDate;
        if (attendeeName == null || attendeeName.Length > 100) throw Error("MSG18");
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
                    x.AccountId == accountId && x.PatientId == patientId && x.Status == PatientLinkStatus.Verified, ct))) throw Error("MSG05", 403);
            var patient = await db.Patients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == patientId.Value, ct);
            if (patient == null) throw Error("MSG05", 404);
            attendeeName = patient.FullName;
            attendeeBirthDate = patient.BirthDate;
        }
        if (request.DossierId.HasValue && (!patientId.HasValue || (!staff && relation != "Self"))) throw Error("MSG05", 403);

        var reason = Clean(request.Reason);
        var exceptionReason = Clean(request.ExceptionReason);
        if (reason?.Length > 1000 || exceptionReason?.Length > 500 || (staff && request.ServiceId == null && reason == null)
            || (!staff && (request.ServiceId == null || exceptionReason != null))) throw Error("MSG04");
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
            request.ServiceId, Reason = reason, request.DentistId,
            ChairId = staff ? request.ChairId : null, StartAt = start, request.DurationMinutes,
            request.DossierId, ExceptionReason = exceptionReason, request.ScheduleVersion, request.AssistantScheduleVersion, request.BookingSettingsVersion
        };
        // Preserve fingerprints of older requests that omitted this optional field.
        var fingerprint = Hash(JsonSerializer.Serialize(canonical) + (request.PatientConsentConfirmed ? "|patient-consent:true" : "")
            + (request.FamilyMemberId.HasValue ? "|family:" + request.FamilyMemberId.Value.ToString("N") : "")
            + (request.ReplacesAppointmentId.HasValue ? "|replaces:" + request.ReplacesAppointmentId.Value.ToString("N") : ""));

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Share S09's reservation lock so duplicate requests and competing slots serialize.
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})", ct);
        AppointmentListItem? replacementSource = null;
        if (request.ReplacesAppointmentId.HasValue) {
            var permissions = await access.RequireAsync(accountId!.Value, "appointments.create", ct);
            replacementSource = (await queries.DetailAsync(accountId.Value, request.ReplacesAppointmentId.Value, ct)).Appointment;
            if (!permissions.BranchIds.Contains(replacementSource.BranchId)) throw Error("MSG05", 403);
        }
        DCMS.Domain.Features.FamilyMembers.FamilyMember? familyMember = null;
        if (request.FamilyMemberId.HasValue) {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(90121, {accountId!.Value})", ct);
            familyMember = await db.Set<DCMS.Domain.Features.FamilyMembers.FamilyMember>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.FamilyMemberId && x.OwnerAccountId == accountId, ct)
                ?? throw new BusinessException(404, new("MSG67", "Không tìm thấy người thân trong danh sách của bạn."));
        }
        var receipt = await db.AppointmentReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, ct);
        if (receipt != null)
        {
            if (receipt.ActorScope != scope || receipt.Fingerprint != fingerprint) throw Error("MSG41", 409);
            var previous = JsonSerializer.Deserialize<AppointmentResult>(receipt.ResultJson)!;
            return new(previous with { Notification = SystemMessages.Get("MSG25") }, true);
        }
        if (familyMember != null) {
            if (familyMember.DeletedAt.HasValue) throw new BusinessException(404, new("MSG67", "Người thân không còn khả dụng."));
            attendeeName = familyMember.FullName; attendeeBirthDate = familyMember.BirthDate;
        }

        now = clock.GetUtcNow();
        if (replacementSource != null && (replacementSource.Status != "NoShow" || replacementSource.StartAt > now || replacementSource.EndAt <= now
            || replacementSource.BranchId != request.BranchId || replacementSource.DentistId != request.DentistId
            || start.AddMinutes(request.DurationMinutes) > replacementSource.EndAt
            || await db.Visits.AnyAsync(x => x.AppointmentId == replacementSource.Id, ct)))
            throw new BusinessException(409, new SystemMessage("MSG66", "Khung giờ của lịch khách không tới không còn phù hợp. Vui lòng tải lại hoặc đặt một lịch khác."));

        if (kind != "FollowUp" && !await db.Set<SchedulingService>().AsNoTracking().AnyAsync(s =>
            s.Id == request.ServiceId && s.BranchId == request.BranchId && s.Active && s.IsInitialExamination, ct)) throw Error("MSG40");
        if (start <= now) throw Error("MSG20", 409);
        if (!staff && !AppointmentRules.PublicLeadTimeValid(now, start)) throw Error("MSG19");
        if (staff && start < now.AddMinutes(60) && exceptionReason == null) throw Error("MSG19");
        if (!staff && kind == "Standard")
        {
            // One open self-booked examination per person until it is checked in or cancelled. A person is the linked patient,
            // the saved family member, or otherwise the same attendee name under the same booker (account, or guest contact).
            var open = db.Appointments.AsNoTracking().Where(a => a.Status == "Confirmed" && a.EndAt > now && a.Kind == "Standard");
            var contact = phone ?? email;
            if (patientId.HasValue) open = open.Where(a => a.PatientId == patientId);
            else if (request.FamilyMemberId.HasValue) open = open.Where(a => a.FamilyMemberId == request.FamilyMemberId);
            else
            {
                var name = attendeeName.ToLower();
                open = open.Where(a => a.PatientId == null && a.FamilyMemberId == null && a.AttendeeName.ToLower() == name
                    && (accountId.HasValue ? a.BookerAccountId == accountId : a.BookerAccountId == null && a.BookingInfo.BookerContact == contact));
            }
            if (await open.AnyAsync(ct)) throw Error("MSG118", 409);
        }

        if (request.DossierId.HasValue)
        {
            // A follow-up is booked against the patient's open treatment dossier; the item is chosen when treatment is recorded.
            var dossier = await db.TreatmentDossiers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.DossierId.Value, ct)
                ?? throw Error("MSG42", 404);
            if (dossier.PatientId != patientId || dossier.BranchId != request.BranchId) throw Error("MSG05", 403);
            if (dossier.State != "Open") throw Error("MSG54", 409);
            if (!request.PatientConsentConfirmed)
                throw new BusinessException(400, new SystemMessage("MSG65",
                    "Vui lòng xác nhận đã có sự đồng ý của bệnh nhân trước khi đặt lịch.") { Field = "patientConsentConfirmed" });
        }

        var slot = await availability.CheckInTransactionAsync(new ResourceCheck(request.BranchId, request.DentistId,
            request.ServiceId, start, request.DurationMinutes, staff ? request.ChairId : null, null,
            patientId, request.ScheduleVersion,
            request.AssistantScheduleVersion, Public: !staff, BookingSettingsVersion: request.BookingSettingsVersion), ct);

        var id = Guid.NewGuid();
        var code = "AP-" + id.ToString("N").ToUpperInvariant();
        var startUtc = slot.Start.ToUniversalTime();
        var endUtc = slot.End.ToUniversalTime();
        var appointment = new Appointment
        {
            Id = id, Code = code, BranchId = request.BranchId, BookerAccountId = accountId,
            FamilyMemberId = request.FamilyMemberId, PatientId = patientId,
            AttendeeName = attendeeName, Kind = kind, Source = source, ServiceId = request.ServiceId,
            Reason = reason, DentistId = request.DentistId,
            ChairId = slot.ChairId, StartAt = startUtc, EndAt = endUtc,
            DossierId = request.DossierId, CreatedAt = now,
            BookingInfo = new AppointmentBookingInfo
            {
                BookerName = bookerName,
                BookerContact = phone ?? email,
                AttendeeBirthDate = attendeeBirthDate,
                AttendeeRelation = relation,
                ExceptionReason = exceptionReason
            }
        };
        db.Add(appointment);
        db.Add(new AppointmentReceipt { ActorScope = scope, RequestKey = request.RequestKey,
            Fingerprint = fingerprint, AppointmentId = id,
            ResultJson = JsonSerializer.Serialize(Result(appointment, "MSG24")) });
        var eventPayload = JsonSerializer.SerializeToNode(new { appointmentId = id, code, version = 1,
            status = "Confirmed", branchId = request.BranchId, startAt = startUtc, endAt = endUtc })!;
        if (request.ReplacesAppointmentId.HasValue) eventPayload["replacesAppointmentId"] = JsonSerializer.SerializeToNode(request.ReplacesAppointmentId.Value);
        db.Add(new AppointmentOutboxEvent
        {
            AggregateId = id, OccurredAt = now,
            PayloadJson = eventPayload.ToJsonString()
        });
        await db.SaveChangesAsync(ct);
        var saved = await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        await tx.CommitAsync(ct);
        return new(Result(saved, "MSG24"), false);
    }

    private static AppointmentResult Result(Appointment appointment, string message) => new(
        appointment.Id, appointment.Code, appointment.Version, appointment.Status,
        appointment.StartAt, appointment.EndAt, SystemMessages.Get(message));
}

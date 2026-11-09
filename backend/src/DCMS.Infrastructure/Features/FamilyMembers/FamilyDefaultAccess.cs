using DCMS.Domain.Features.FamilyMembers;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.PatientLinks;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.FamilyMembers;

public sealed partial class FamilyRecordService
{
    // Reception establishes the Patient identity. A customer-supplied name/code alone never establishes this link.
    private async Task EnsureDefaultAccessAsync(int actor, Guid memberId, CancellationToken ct)
    {
        await MemberAsync(actor, memberId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(90121, {actor})", ct);
        var member = await MemberAsync(actor, memberId, ct);
        var history = await db.Set<FamilyRecordAccess>().Where(x => x.FamilyMemberId == memberId && x.OwnerAccountId == actor).ToArrayAsync(ct);
        // Preserve explicit decisions, scopes, expiry and any termination, including after another request is submitted.
        if (history.Any(x => x.Status != "Pending")) return;
        var pending = history.SingleOrDefault();
        var links = await (from appointment in db.Appointments.AsNoTracking()
            join visit in db.Visits.AsNoTracking() on appointment.Id equals visit.AppointmentId
            join patient in db.Patients.AsNoTracking() on visit.PatientId equals patient.Id
            join recorder in db.Users.AsNoTracking() on visit.RecordedBy equals recorder.Id
            where appointment.BookerAccountId == actor && appointment.FamilyMemberId == memberId
                && appointment.PatientId == patient.Id && appointment.BranchId == visit.BranchId
                && visit.Status != VisitStatuses.Cancelled && !patient.IsArchived && recorder.AccountType == "Staff"
            orderby visit.RecordedAt descending, visit.Id
            select new { AppointmentId = appointment.Id, VisitId = visit.Id, visit.RecordedBy, visit.BranchId, patient.Id,
                PatientName = patient.FullName, PatientBirthDate = patient.BirthDate, patient.PatientCode,
                appointment.AttendeeName, appointment.AttendeeBirthDate }).ToArrayAsync(ct);
        var name = PatientLinkSupport.NormalizeName(member.FullName);
        var matching = links.Where(x => PatientLinkSupport.NormalizeName(x.PatientName) == name
            && PatientLinkSupport.NormalizeName(x.AttendeeName) == name
            && (!member.BirthDate.HasValue || member.BirthDate == x.PatientBirthDate && member.BirthDate == x.AttendeeBirthDate)).ToArray();
        if (matching.Select(x => x.Id).Distinct().Count() != 1) return;
        var source = matching[0];
        if (pending != null && (pending.DeclaredFullName != member.FullName || pending.DeclaredBirthDate != member.BirthDate
            || pending.DeclaredPatientCode != null && !pending.DeclaredPatientCode.Equals(source.PatientCode, StringComparison.OrdinalIgnoreCase))) return;
        var now = clock.GetUtcNow();
        var row = pending ?? new FamilyRecordAccess { FamilyMemberId = memberId, OwnerAccountId = actor,
            Code = "FA-" + Guid.NewGuid().ToString("N").ToUpperInvariant(), DeclaredFullName = member.FullName,
            DeclaredBirthDate = member.BirthDate, Relationship = member.Relationship, RequestedAt = now };
        if (pending == null) db.Add(row); else row.Version++;
        row.PatientId = source.Id; row.BranchId = source.BranchId; row.Status = "Verified";
        row.Scopes = [.. FamilyRecordScopes.All]; row.ExpiresAt = now.AddDays(365);
        row.DecidedAt = now; row.DecidedBy = source.RecordedBy; row.SharingBasis = "DefaultFamilyBooking";
        row.VerificationNote = $"Liên kết mặc định từ lịch đặt hộ {source.AppointmentId} và lượt tiếp nhận {source.VisitId}; không ghi nhận xác minh đồng ý riêng.";
        row.PublicNote = "Bạn được xem hồ sơ người thân mặc định từ lịch đặt hộ đã được phòng khám tiếp nhận.";
        Audit(row, actor, "DefaultGranted", $"Appointment:{source.AppointmentId}; Visit:{source.VisitId}; Patient:{source.Id}; Branch:{source.BranchId}");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}

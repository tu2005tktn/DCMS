using DCMS.Domain.Features.Appointments;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Appointments;

// Open examinations of one person: the linked patient, the saved family member, or otherwise the same attendee name
// under the same booker (account, or guest contact).
internal static class OpenBookings
{
    public static IQueryable<Appointment> Of(DcmsDbContext db, DateTimeOffset now, int? patientId, Guid? familyMemberId,
        string attendeeName, int? bookerAccountId, string? contact)
    {
        var open = db.Appointments.AsNoTracking().Where(a => a.Status == "Confirmed" && a.EndAt > now && a.Kind == "Standard");
        if (patientId.HasValue) return open.Where(a => a.PatientId == patientId);
        if (familyMemberId.HasValue) return open.Where(a => a.FamilyMemberId == familyMemberId);
        var name = attendeeName.ToLower();
        return open.Where(a => a.PatientId == null && a.FamilyMemberId == null && a.AttendeeName.ToLower() == name
            && (bookerAccountId.HasValue ? a.BookerAccountId == bookerAccountId : a.BookerAccountId == null && a.BookingInfo.BookerContact == contact));
    }
}

using DCMS.Domain.Features.Appointments;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Appointments;

// A mistaken no-show can be reverted only on the clinic day (UTC+7) it was recorded.
internal static class MissedAppointments
{
    public static async Task<HashSet<Guid>> UndoableAsync(DcmsDbContext db, IEnumerable<Appointment> rows,
        DateTimeOffset now, CancellationToken ct)
    {
        var ids = rows.Where(x => x.Status == "NoShow").Select(x => x.Id).ToArray();
        if (ids.Length == 0) return [];
        var offset = TimeSpan.FromHours(7);
        var since = new DateTimeOffset(now.ToOffset(offset).Date, offset).ToUniversalTime();
        return (await db.AppointmentChanges.AsNoTracking()
            .Where(x => ids.Contains(x.AppointmentId) && x.Action == "NO_SHOW" && x.OccurredAt >= since)
            .Select(x => x.AppointmentId).ToListAsync(ct)).ToHashSet();
    }

    // A missed booking the clinic may move to another time: a no-show recorded today,
    // or a booking of today whose start has passed while nobody has been checked in.
    public static async Task<HashSet<Guid>> RebookableAsync(DcmsDbContext db, IEnumerable<Appointment> rows, DateTimeOffset now, CancellationToken ct)
    {
        var list = rows.ToArray();
        var offset = TimeSpan.FromHours(7);
        var result = await UndoableAsync(db, list, now, ct);
        result.UnionWith(list.Where(x => x.Status == "Confirmed" && x.StartAt <= now && x.StartAt.ToOffset(offset).Date == now.ToOffset(offset).Date)
            .Select(x => x.Id));
        return result;
    }
}

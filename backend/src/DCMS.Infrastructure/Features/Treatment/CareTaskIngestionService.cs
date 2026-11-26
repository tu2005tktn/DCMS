using System.Data;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

// Turns missed bookings into callback tasks. It does not send messages.
public sealed class CareTaskIngestionService(DcmsDbContext db, TimeProvider clock)
{
    // A missed booking reaches customer care once the clinic day (UTC+7) it was recorded on is over:
    // until then the clinic can still undo it or move it to another time.
    public async Task<int> IngestAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({DCMS.Infrastructure.Features.Scheduling.WorkingScheduleService.TransactionLock})", ct);
        var now = clock.GetUtcNow();
        var offset = TimeSpan.FromHours(7);
        var today = new DateTimeOffset(now.ToOffset(offset).Date, offset).ToUniversalTime();
        var missed = await db.Appointments.AsNoTracking()
            .Where(a => a.Status == "NoShow" && a.StartAt < today
                && !db.AppointmentChanges.Any(c => c.AppointmentId == a.Id && c.Action == "NO_SHOW" && c.OccurredAt >= today)
                && !db.CareTasks.Any(t => t.AppointmentId == a.Id && t.Type == "MissedAppointment"))
            .OrderBy(a => a.StartAt).ThenBy(a => a.Id)
            .Select(a => new { a.Id, a.PatientId, a.BranchId })
            .ToListAsync(ct);
        db.CareTasks.AddRange(missed.Select(a => new CareTask
        {
            PatientId = a.PatientId, BranchId = a.BranchId,
            AppointmentId = a.Id,
            Type = "MissedAppointment",
            Content = "Liên hệ hỗ trợ sau khi bệnh nhân vắng mặt theo lịch hẹn.",
            DueAt = now
        }));
        if (missed.Count > 0) await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return missed.Count;
    }
}

using System.Data;
using System.Text.Json;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

// Consumes only the care-relevant, source-linked treatment events. It does not send messages.
public sealed class CareTaskIngestionService(DcmsDbContext db, TimeProvider clock)
{
    public async Task<int> IngestAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({DCMS.Infrastructure.Features.Scheduling.WorkingScheduleService.TransactionLock})", ct);
        var added = 0;
        for (var offset = 0; ; offset += 500)
        {
            var missedEvents = await db.AppointmentOutboxEvents.AsNoTracking()
                .Where(x => x.EventType == "appointment.no_show")
                .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id)
                .Skip(offset).Take(500).ToListAsync(ct);
            if (missedEvents.Count == 0) break;
            foreach (var source in missedEvents)
            {
            var stableKey = $"appointment-no-show:{source.AggregateId}";
            if (await db.CareTasks.AnyAsync(x => x.EpisodeKey == stableKey, ct)) continue;
            var appointment = await db.Appointments.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == source.AggregateId, ct);
            if (appointment is not { Status: "NoShow", PatientId: not null }) continue;
            db.CareTasks.Add(new CareTask
            {
                PatientId = appointment.PatientId.Value, BranchId = appointment.BranchId,
                AppointmentId = appointment.Id,
                EpisodeKey = stableKey, Type = "MissedAppointment",
                Content = "Liên hệ hỗ trợ sau khi bệnh nhân vắng mặt theo lịch hẹn.",
                DueAt = clock.GetUtcNow(), CreatedBy = 0,
                CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow()
            });
                added++;
            }
            if (missedEvents.Count < 500) break;
        }
        if (added > 0) await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return added;
    }
}

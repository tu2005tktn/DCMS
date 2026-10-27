using System.Data;
using System.Text.Json;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

// Consumes only the care-relevant, source-linked treatment events. It does not send messages.
public sealed class CareTaskIngestionService(DcmsDbContext db, ITreatmentLabReadinessPort lab,
    TimeProvider clock)
{
    public async Task<int> IngestAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({DCMS.Infrastructure.Features.Scheduling.WorkingScheduleService.TransactionLock})", ct);
        var added = 0;
        for (var offset = 0; ; offset += 500)
        {
            var events = await db.TreatmentProgressionEvents.AsNoTracking().Where(x =>
                x.StableKey != null && (x.Kind == "followup.due" ||
                    x.Kind == "step.prerequisite_blocked" || x.Kind == "step.lab_blocked"))
                .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id)
                .Skip(offset).Take(500).ToListAsync(ct);
            if (events.Count == 0) break;
            foreach (var source in events)
            {
            var stableKey = source.StableKey;
            if (stableKey == null || await db.CareTasks.AnyAsync(x => x.EpisodeKey == stableKey, ct)) continue;
            using var payload = JsonDocument.Parse(source.PayloadJson);
            var root = payload.RootElement;
            if (!root.TryGetProperty("patientId", out var patient)) continue;
            var patientId = patient.GetInt32();
            int branchId;
            Guid? instructionId = null;
            string type;
            string content;
            if (source.Kind == "followup.due")
            {
                var instruction = await db.FollowUpInstructions.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == source.AggregateId, ct);
                if (instruction == null || instruction.Status != "NeedsBooking" ||
                    instruction.PatientId != patientId ||
                    stableKey != $"followup-due:{instruction.Id}:{instruction.EarliestOn:yyyyMMdd}:{instruction.TargetOn:yyyyMMdd}:{instruction.LatestOn:yyyyMMdd}")
                    continue;
                branchId = instruction.BranchId; instructionId = instruction.Id;
                type = "FollowUpDue"; content = "Liên hệ hỗ trợ đặt lịch theo chỉ định đang đến hạn.";
            }
            else
            {
                var step = await db.TreatmentSteps.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == source.AggregateId, ct);
                if (step == null || step.Outcome is "Completed" or "Stopped" or "Cancelled" ||
                    step.PlanVersion != root.GetProperty("planVersion").GetInt32()) continue;
                var dossier = await db.TreatmentDossiers.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == step.DossierId, ct);
                if (dossier == null || dossier.PatientId != patientId || dossier.State != "Open") continue;
                branchId = dossier.BranchId;
                if (source.Kind == "step.prerequisite_blocked")
                {
                    var parents = JsonSerializer.Deserialize<Guid[]>(step.PredecessorsJson) ?? [];
                    var rows = await db.TreatmentSteps.AsNoTracking().Where(x => x.DossierId == dossier.Id)
                        .ToListAsync(ct);
                    if (parents.All(id => rows.Any(x => x.Id == id && x.Outcome == "Completed"))) continue;
                    type = "PrerequisiteBlocked";
                    content = "Bước điều trị còn điều kiện tiền nhiệm cần được bác sĩ xem lại.";
                }
                else
                {
                    if (step.LabRequirement == null) continue;
                    var state = await lab.CheckAsync(patientId, step.Id, step.LabRequirement,
                        step.PlanVersion, ct);
                    if (!state.Available || state.IsAccepted ||
                        !stableKey.EndsWith(':' + state.BlockerEpisodeKey, StringComparison.Ordinal))
                        continue;
                    type = "LabBlocked";
                    content = "Bước điều trị đang chờ bác sĩ kiểm tra nghiệm thu Labo.";
                }
            }
            db.CareTasks.Add(new CareTask
            {
                PatientId = patientId, BranchId = branchId, InstructionId = instructionId,
                EpisodeKey = stableKey, Type = type, Content = content,
                DueAt = clock.GetUtcNow(), CreatedBy = 0, CreatedAt = clock.GetUtcNow(),
                UpdatedAt = clock.GetUtcNow()
            });
                added++;
            }
            if (events.Count < 500) break;
        }
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
                InstructionId = appointment.FollowUpId, AppointmentId = appointment.Id,
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

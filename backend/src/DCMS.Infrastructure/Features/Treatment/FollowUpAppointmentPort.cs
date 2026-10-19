using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

// Appointment commands own the transaction and scheduling lock. This port never commits or calls an external service.
public sealed class FollowUpAppointmentPort(DcmsDbContext db, ITreatmentLabReadinessPort lab, TimeProvider clock)
    : IFollowUpBookingPort, IFollowUpAppointmentChangePort
{
    private static BusinessException Error(string code, int status = 409) => new(status, SystemMessages.Get(code));

    private async Task<FollowUpInstruction> InstructionAsync(Guid id, CancellationToken ct) =>
        await db.FollowUpInstructions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);

    private static bool InWindow(FollowUpInstruction instruction, DateTimeOffset startAt)
    {
        var local = DateOnly.FromDateTime(startAt.ToOffset(TimeSpan.FromHours(7)).DateTime);
        return local >= instruction.EarliestOn && local <= instruction.LatestOn;
    }

    private async Task CheckReadinessAsync(FollowUpInstruction instruction, DateTimeOffset startAt, CancellationToken ct)
    {
        if (!instruction.StepId.HasValue) return;
        var step = await db.TreatmentSteps.AsNoTracking().SingleOrDefaultAsync(x => x.Id == instruction.StepId, ct)
            ?? throw Error("MSG230");
        if (step.Outcome is "Completed" or "Stopped" or "Cancelled" ||
            step.HoldReason != null && step.HoldEffectiveAt <= startAt
            || step.PlanVersion != instruction.PlanVersion) throw Error("MSG230");
        var dossier = await db.TreatmentDossiers.Include(x => x.Items).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == step.DossierId, ct) ?? throw Error("MSG230");
        if (dossier.State != "Open" || dossier.PatientId != instruction.PatientId ||
            dossier.Items.SingleOrDefault(x => x.Id == step.ItemId)?.Decision != "Agreed") throw Error("MSG230");
        var predecessors = JsonSerializer.Deserialize<Guid[]>(step.PredecessorsJson) ?? [];
        var rows = await db.TreatmentSteps.AsNoTracking().Where(x => x.DossierId == step.DossierId).ToListAsync(ct);
        if (predecessors.Any(id => rows.All(x => x.Id != id || x.Outcome != "Completed"))) throw Error("MSG230");
        if (step.LabRequirement != null)
        {
            var labState = await lab.CheckAsync(instruction.PatientId, step.Id,
                step.LabRequirement, step.PlanVersion, ct);
            if (!labState.Available || !labState.IsAccepted) throw Error("MSG230");
        }
    }

    public async Task ValidateAsync(Guid followUpId, int patientId, int branchId, int dentistId, int durationMinutes,
        DateTimeOffset startAt, CancellationToken ct)
    {
        var row = await InstructionAsync(followUpId, ct);
        if (row.PatientId != patientId || row.BranchId != branchId) throw Error("MSG06", 403);
        if (row.Status != "NeedsBooking" || row.ActiveAppointmentId.HasValue) throw Error("MSG127");
        if (row.DentistId.HasValue && row.DentistId != dentistId || row.DurationMinutes != durationMinutes
            || !InWindow(row, startAt)) throw Error("MSG230");
        await CheckReadinessAsync(row, startAt, ct);
    }

    public async Task MarkBookedAsync(Guid followUpId, Guid appointmentId, CancellationToken ct)
    {
        var row = await InstructionAsync(followUpId, ct);
        if (row.Status == "Booked" && row.ActiveAppointmentId == appointmentId) return;
        if (row.Status != "NeedsBooking" || row.ActiveAppointmentId.HasValue) throw Error("MSG127");
        row.Status = "Booked"; row.ActiveAppointmentId = appointmentId; row.Version++;
        row.UpdatedAt = clock.GetUtcNow();
        db.TreatmentProgressionEvents.Add(new TreatmentProgressionEvent
        {
            AggregateId = followUpId, Kind = "followup.booked", ActorId = 0,
            OccurredAt = row.UpdatedAt, PayloadJson = JsonSerializer.Serialize(new { appointmentId })
        });
    }

    public async Task ValidateRescheduleAsync(Guid followUpId, Guid appointmentId, int dentistId,
        DateTimeOffset startAt, CancellationToken ct)
    {
        var row = await InstructionAsync(followUpId, ct);
        if (row.Status != "Booked" || row.ActiveAppointmentId != appointmentId ||
            row.DentistId.HasValue && row.DentistId != dentistId || !InWindow(row, startAt))
            throw Error("MSG230");
        await CheckReadinessAsync(row, startAt, ct);
    }

    public async Task MarkNeedsBookingAsync(Guid followUpId, Guid appointmentId, CancellationToken ct)
    {
        var row = await InstructionAsync(followUpId, ct);
        if (row.Status == "Completed" && row.ActiveAppointmentId == appointmentId) return;
        if (row.Status == "NeedsBooking" && row.ActiveAppointmentId == null) return;
        if (row.Status != "Booked" || row.ActiveAppointmentId != appointmentId) throw Error("MSG111");
        row.Status = "NeedsBooking"; row.ActiveAppointmentId = null; row.Version++;
        row.UpdatedAt = clock.GetUtcNow();
        db.TreatmentProgressionEvents.Add(new TreatmentProgressionEvent
        {
            AggregateId = followUpId, Kind = "followup.needs_booking", ActorId = 0,
            OccurredAt = row.UpdatedAt, PayloadJson = JsonSerializer.Serialize(new { appointmentId })
        });
    }
}

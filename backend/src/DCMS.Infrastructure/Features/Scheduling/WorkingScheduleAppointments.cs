using DCMS.Application.Features.Scheduling;
using DCMS.Domain.Features.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Scheduling;

public sealed partial class WorkingScheduleService
{
    public async Task<ScheduleAppointments> AppointmentsAsync(int actorId, int dentistId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await access.RequireAsync(actorId, "working-schedules.read", ct);
        if (dentistId <= 0 || from.Year is < 2000 or > 2100 || to.Year is < 2000 or > 2100 || from > to || to.DayNumber - from.DayNumber > 30)
            throw Error("MSG110");
        var options = await OptionsAsync(actorId, ct);
        var branches = options.Employees.Where(x => x.Id == dentistId && x.CanDentist && (x.CanView ?? x.CanEdit))
            .Select(x => x.BranchId).Distinct().ToArray();
        if (branches.Length == 0) throw Error("MSG06", 403);
        var lower = Local(from).ToUniversalTime();
        var upper = Local(to.AddDays(1)).ToUniversalTime();
        var rows = await (from appointment in db.Appointments.AsNoTracking()
            join branch in db.Set<SchedulingBranch>() on appointment.BranchId equals branch.Id
            join service in db.Set<SchedulingService>() on appointment.ServiceId equals (int?)service.Id into services
            from service in services.DefaultIfEmpty()
            where appointment.DentistId == dentistId && branches.Contains(appointment.BranchId)
                && appointment.StartAt < upper && appointment.EndAt > lower
            orderby appointment.StartAt, appointment.Id
            select new ScheduleAppointment(appointment.Id, appointment.Code, appointment.BranchId, branch.Name,
                appointment.StartAt, appointment.EndAt, appointment.AttendeeName, service == null ? "Khác" : service.Name,
                appointment.Status, appointment.FollowUpId.HasValue && db.FollowUpInstructions.Any(x => x.Id == appointment.FollowUpId
                    && x.DossierId.HasValue && x.ItemId.HasValue), appointment.PatientConsentConfirmedAt)).ToListAsync(ct);
        return new(rows);
    }
}

using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

// Next plan item to book for a patient. See docs/specs/TREATMENT-NEXT-VISIT-BOOKING.md.
public sealed partial class TreatmentDossierService
{
    private static int RoundVisitMinutes(int? minutes) => Math.Clamp((int)Math.Ceiling((minutes ?? 30) / 15d) * 15, 15, 480);

    public async Task<NextVisitCandidates> NextVisitCandidatesAsync(int actorId, int patientId, CancellationToken ct)
    {
        if (!await db.Patients.AsNoTracking().AnyAsync(x => x.Id == patientId, ct)) throw Error("MSG113", 404);
        var account = await users.FindByIdAsync(actorId.ToString());
        if (account is not { IsActive: true, AccountType: "Staff" }
            || !AccountRoles.Effective(await users.GetRolesAsync(account)).Contains(AccountRoles.Dentist)) throw Error("MSG06", 403);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct) ?? throw Error("MSG06", 403);
        var branches = await db.Set<SchedulingMember>().AsNoTracking().Where(x => x.EmployeeId == employee.Id && x.Active && x.CanDentist)
            .Select(x => x.BranchId).ToListAsync(ct);
        var dossiers = await db.TreatmentDossiers.AsNoTracking().Include(x => x.Items).Where(x => x.PatientId == patientId
            && x.State == "Open" && x.PlanState == "Committed" && branches.Contains(x.BranchId)).ToListAsync(ct);
        if (dossiers.Count == 0) return new([]);

        var dossierIds = dossiers.Select(x => x.Id).ToArray();
        var performed = new HashSet<Guid>();
        foreach (var dossier in dossiers)
        {
            var deleted = (await SheetActivityOverridesAsync(dossier.Id, ct)).Where(x => x.Value.SheetDeleted).Select(x => x.Key).ToHashSet();
            foreach (var itemId in await db.ClinicalActivities.AsNoTracking().Where(x => x.DossierId == dossier.Id && x.State == "Posted"
                && x.ItemId.HasValue && !deleted.Contains(x.Id)).Select(x => x.ItemId!.Value).Distinct().ToListAsync(ct))
                performed.Add(itemId);
        }

        var instructions = await db.FollowUpInstructions.AsNoTracking().Where(x => x.PatientId == patientId && x.DossierId.HasValue
            && dossierIds.Contains(x.DossierId.Value) && x.ItemId.HasValue && x.StepId == null
            && (x.Status == "NeedsBooking" || x.Status == "Booked")).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var appointmentIds = instructions.Where(x => x.ActiveAppointmentId.HasValue).Select(x => x.ActiveAppointmentId!.Value).ToArray();
        var appointments = await db.Appointments.AsNoTracking().Where(x => appointmentIds.Contains(x.Id)
            && (x.Status == "Confirmed" || x.Status == "CheckedIn" || x.Status == "InProgress")).ToDictionaryAsync(x => x.Id, ct);

        var rows = new List<(TreatmentDossier Dossier, TreatmentItem Item, int DentistId)>();
        foreach (var dossier in dossiers)
            foreach (var item in dossier.Items.Where(x => !x.SheetDeleted && !x.HiddenFromSheet && TreatmentItemRules.IsEffective(x)
                && !x.DisplayCompleted && x.Progress != "Completed" && !performed.Contains(x.Id)))
                rows.Add((dossier, item, item.ResponsibleDentistId ?? dossier.DentistId));
        var dentistIds = rows.Select(x => x.DentistId).Distinct().ToArray();
        var names = await db.Employees.AsNoTracking().Where(x => dentistIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

        var items = rows.OrderBy(x => x.Item.EstimatedStart == null).ThenBy(x => x.Item.EstimatedStart).ThenBy(x => x.Dossier.CreatedAt)
            .ThenBy(x => x.Item.PlannedOrder).Select(x =>
            {
                var instruction = instructions.FirstOrDefault(i => i.DossierId == x.Dossier.Id && i.ItemId == x.Item.Id);
                var appointment = instruction?.ActiveAppointmentId is { } id && appointments.TryGetValue(id, out var found) ? found : null;
                var status = instruction == null ? "Ready" : instruction.Status == "Booked" && appointment != null ? "Booked" : "NeedsBooking";
                return new NextVisitCandidate(x.Dossier.Id, x.Dossier.Code, x.Item.Id, x.Item.ServiceId, x.Item.ServiceName, x.Item.Region,
                    x.Item.EstimatedStart, RoundVisitMinutes(x.Item.EstimatedMinutes), x.DentistId, names.GetValueOrDefault(x.DentistId) ?? "—",
                    x.Dossier.BranchId, status, instruction?.Id, appointment == null ? null : new(appointment.Id, appointment.Code, appointment.StartAt),
                    instruction?.EarliestOn, instruction?.LatestOn);
            }).ToList();
        return new(items);
    }
}

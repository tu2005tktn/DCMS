using DCMS.Application.Common;
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
        var scope = await access.ResolveAsync(actorId, ct);
        if (scope.Assigned && !scope.Owner) branches = branches.Where(scope.BranchIds.Contains).ToList();
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

        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
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
                var window = TreatmentItemRules.BookingWindow(x.Item, today);
                return new NextVisitCandidate(x.Dossier.Id, x.Dossier.Code, x.Item.Id, x.Item.ServiceId, x.Item.ServiceName, x.Item.Region,
                    x.Item.EstimatedStart, RoundVisitMinutes(x.Item.EstimatedMinutes), x.DentistId, names.GetValueOrDefault(x.DentistId) ?? "—",
                    x.Dossier.BranchId, status, instruction?.Id, appointment == null ? null : new(appointment.Id, appointment.Code, appointment.StartAt),
                    instruction?.EarliestOn, instruction?.LatestOn, TreatmentItemRules.PlannedTarget(x.Item), window?.From, window?.To);
            }).ToList();
        return new(items);
    }

    private async Task<HashSet<Guid>> PerformedItemIdsAsync(Guid dossierId, CancellationToken ct)
    {
        var deleted = (await SheetActivityOverridesAsync(dossierId, ct)).Where(x => x.Value.SheetDeleted).Select(x => x.Key).ToHashSet();
        return (await db.ClinicalActivities.AsNoTracking().Where(x => x.DossierId == dossierId && x.State == "Posted"
            && x.ItemId.HasValue && !deleted.Contains(x.Id)).Select(x => x.ItemId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
    }

    // Proposes the planned-date changes that follow from booking `itemId` on `plannedOn`. See docs/specs/TREATMENT-NEXT-VISIT-BOOKING.md.
    public async Task<PlannedDatePreview> PlannedDatePreviewAsync(int actorId, Guid dossierId, Guid itemId, DateOnly plannedOn, CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        var item = dossier.Items.SingleOrDefault(x => x.Id == itemId) ?? throw Error("MSG113", 404);
        var changes = new List<PlannedDateChange>();
        var target = TreatmentItemRules.PlannedTarget(item);
        if (dossier.State != "Open" || target == plannedOn) return new(dossier.Version, changes);
        changes.Add(new(item.Id, item.ServiceName, item.Region, target, plannedOn, true));
        // Booking later than planned pushes the items behind it by the same number of days; booking earlier moves only this item.
        var shift = target.HasValue ? plannedOn.DayNumber - target.Value.DayNumber : 0;
        if (shift > 0)
        {
            var performed = await PerformedItemIdsAsync(dossier.Id, ct);
            var booked = (await db.FollowUpInstructions.AsNoTracking().Where(x => x.DossierId == dossier.Id && x.ItemId.HasValue
                && x.StepId == null && x.Status == "Booked" && x.ActiveAppointmentId.HasValue).Select(x => x.ItemId!.Value).ToListAsync(ct)).ToHashSet();
            foreach (var later in dossier.Items.Where(x => x.Id != item.Id && x.PlannedOrder > item.PlannedOrder && !x.SheetDeleted
                && !x.HiddenFromSheet && TreatmentItemRules.IsEffective(x) && !x.DisplayCompleted && x.Progress != "Completed"
                && !performed.Contains(x.Id) && !booked.Contains(x.Id)).OrderBy(x => x.PlannedOrder))
                if (TreatmentItemRules.PlannedTarget(later) is { } current)
                    changes.Add(new(later.Id, later.ServiceName, later.Region, current, current.AddDays(shift), false));
        }
        return new(dossier.Version, changes);
    }

    public Task<TreatmentChange> ApplyPlannedDatesAsync(int actorId, Guid dossierId, ApplyPlannedDatesRequest request, CancellationToken ct) =>
        SheetCommandAsync(actorId, dossierId, request.RequestKey, "PlannedDatesApply", new { dossierId, request }, async dossier =>
        {
            if (dossier.Version != request.ExpectedVersion) throw Error("MSG111", 409);
            if (dossier.State != "Open" || request.Changes is not { Count: > 0 and <= 50 }
                || request.Changes.Select(x => x.ItemId).Distinct().Count() != request.Changes.Count
                || request.Changes.Any(x => x.PlannedOn.Year < 2000)) throw Error("MSG04");
            var performed = await PerformedItemIdsAsync(dossier.Id, ct);
            var applied = new List<object>();
            foreach (var change in request.Changes)
            {
                var item = dossier.Items.SingleOrDefault(x => x.Id == change.ItemId) ?? throw Error("MSG113", 404);
                if (item.SheetDeleted || item.DisplayCompleted || item.Progress == "Completed" || performed.Contains(item.Id)) throw Error("MSG111", 409);
                var before = new { item.EstimatedStart, item.EstimatedEnd, item.ReturnEarliestOn, item.ReturnTargetOn, item.ReturnLatestOn };
                var delta = change.PlannedOn.DayNumber - (TreatmentItemRules.PlannedTarget(item)?.DayNumber ?? change.PlannedOn.DayNumber);
                item.EstimatedStart = change.PlannedOn;
                // The explicit return window moves with the target so the allowed range keeps its shape.
                if (item.ReturnEarliestOn is { } from && item.ReturnTargetOn is { } mid && item.ReturnLatestOn is { } to)
                {
                    item.ReturnEarliestOn = from.AddDays(delta); item.ReturnTargetOn = mid.AddDays(delta); item.ReturnLatestOn = to.AddDays(delta);
                }
                if (item.EstimatedEnd is { } end) item.EstimatedEnd = end.AddDays(delta) < change.PlannedOn ? change.PlannedOn : end.AddDays(delta);
                applied.Add(new { itemId = item.Id, before, after = new { item.EstimatedStart, item.EstimatedEnd, item.ReturnEarliestOn, item.ReturnTargetOn, item.ReturnLatestOn } });
            }
            dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
            Event(dossier, actorId, "ItemPlannedDatesChanged", new { changes = applied });
            await db.SaveChangesAsync(ct);
            return new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "ngày dự kiến", value: dossier.Code));
        }, ct);
}

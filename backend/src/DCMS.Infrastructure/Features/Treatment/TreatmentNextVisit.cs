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
        var scope = await access.ResolveAsync(actorId, ct);
        if (account is not { IsActive: true, AccountType: "Staff" }
            || !scope.GrantDriven && !AccountRoles.Effective(await users.GetRolesAsync(account)).Contains(AccountRoles.Dentist)) throw Error("MSG06", 403);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct) ?? throw Error("MSG06", 403);
        var branches = await db.SchedulingMembers.AsNoTracking().Where(x => x.EmployeeId == employee.Id && x.Active && (x.CanDentist || scope.GrantDriven))
            .Select(x => x.BranchId).ToListAsync(ct);
        if (scope.Assigned && !scope.Owner) branches = branches.Where(scope.BranchIds.Contains).ToList();
        var dossiers = await db.TreatmentDossiers.AsNoTracking().Include(x => x.Items).Where(x => x.PatientId == patientId
            && x.State == "Open" && x.Items.Any() && branches.Contains(x.BranchId)).ToListAsync(ct);
        if (dossiers.Count == 0) return new([]);

        var dossierIds = dossiers.Select(x => x.Id).ToArray();
        var performed = new HashSet<Guid>();
        foreach (var dossier in dossiers)
            performed.UnionWith(await PerformedItemIdsAsync(dossier.Id, ct));

        // A dossier is booked when it already has an upcoming follow-up appointment.
        var appointments = (await db.Appointments.AsNoTracking().Where(x => x.DossierId.HasValue && dossierIds.Contains(x.DossierId.Value)
            && (x.Status == "Confirmed" || x.Status == "CheckedIn" || x.Status == "InProgress")).OrderBy(x => x.StartAt).ToListAsync(ct))
            .GroupBy(x => x.DossierId!.Value).ToDictionary(x => x.Key, x => x.First());

        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        // One suggestion per dossier: its nearest pending plan item, which only prefills the booking.
        var rows = new List<(TreatmentDossier Dossier, TreatmentItem Item, int DentistId)>();
        foreach (var dossier in dossiers)
            if (dossier.Items.Where(x => TreatmentItemRules.IsPendingForBooking(x) && !performed.Contains(x.Id))
                    .OrderBy(x => TreatmentItemRules.PlannedTarget(x) == null).ThenBy(x => TreatmentItemRules.PlannedTarget(x))
                    .ThenBy(x => x.PlannedOrder).FirstOrDefault() is { } nearest)
                rows.Add((dossier, nearest, nearest.ResponsibleDentistId ?? dossier.DentistId));
        var dentistIds = rows.Select(x => x.DentistId).Distinct().ToArray();
        var names = await db.Employees.AsNoTracking().Where(x => dentistIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

        var items = rows.OrderBy(x => TreatmentItemRules.PlannedTarget(x.Item) == null).ThenBy(x => TreatmentItemRules.PlannedTarget(x.Item))
            .ThenBy(x => x.Dossier.CreatedAt).Select(x =>
            {
                var appointment = appointments.GetValueOrDefault(x.Dossier.Id);
                var window = TreatmentItemRules.BookingWindow(x.Item, today);
                return new NextVisitCandidate(x.Dossier.Id, x.Dossier.Code, x.Item.Id, x.Item.ServiceId, x.Item.ServiceName, x.Item.Region,
                    x.Item.EstimatedStart, RoundVisitMinutes(x.Item.EstimatedMinutes), x.DentistId, names.GetValueOrDefault(x.DentistId) ?? "—",
                    x.Dossier.BranchId, appointment == null ? "Ready" : "Booked",
                    appointment == null ? null : new(appointment.Id, appointment.Code, appointment.StartAt),
                    TreatmentItemRules.PlannedTarget(x.Item), window?.From, window?.To);
            }).ToList();
        return new(items);
    }

    private Task<HashSet<Guid>> PerformedItemIdsAsync(Guid dossierId, CancellationToken ct)
        => TreatmentBookingQueries.PerformedItemIdsAsync(db, dossierId, ct);

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
            foreach (var later in dossier.Items.Where(x => x.Id != item.Id && x.PlannedOrder > item.PlannedOrder
                && TreatmentItemRules.IsPendingForBooking(x) && !performed.Contains(x.Id)).OrderBy(x => x.PlannedOrder))
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
                if (item.DisplayCompleted || performed.Contains(item.Id)) throw Error("MSG111", 409);
                var before = item.EstimatedStart;
                item.EstimatedStart = change.PlannedOn;
                applied.Add(new { itemId = item.Id, before, after = item.EstimatedStart });
            }
            dossier.Version++; dossier.UpdatedAt = clock.GetUtcNow();
            Event(dossier, actorId, "ItemPlannedDatesChanged", new { changes = applied });
            await db.SaveChangesAsync(ct);
            return new TreatmentChange(await DetailAsync(dossier, ct), SystemMessages.Get("MSG222", "ngày dự kiến", value: dossier.Code));
        }, ct);
}

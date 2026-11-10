namespace DCMS.Domain.Features.Treatment;

public static class TreatmentItemRules
{
    // A visit may be booked at most this many days before or after the planned date of an item.
    public const int BookingToleranceDays = 4;

    // An item counts as agreed work either through an explicit decision or a clinician entry made directly on the sheet.
    public static bool IsEffective(TreatmentItem item) =>
        item.Decision == "Agreed" || item.EntrySource == "ClinicianEntry";

    public static bool IsPendingForBooking(TreatmentItem item) =>
        IsEffective(item) && !item.SheetDeleted && !item.HiddenFromSheet
        && !item.DisplayCompleted && item.Progress != "Completed";

    // The date the plan currently aims for: the explicit return target if the window is complete, else the planned date.
    public static DateOnly? PlannedTarget(TreatmentItem item) =>
        item.ReturnEarliestOn.HasValue && item.ReturnTargetOn.HasValue && item.ReturnLatestOn.HasValue ? item.ReturnTargetOn : item.EstimatedStart;

    // Planned window of an item, never wider than the tolerance either side of the target.
    public static (DateOnly From, DateOnly Target, DateOnly To)? PlanWindow(TreatmentItem item)
    {
        if (PlannedTarget(item) is not { } target) return null;
        var from = target.AddDays(-BookingToleranceDays);
        var to = target.AddDays(BookingToleranceDays);
        if (item.ReturnEarliestOn is { } earliest && item.ReturnLatestOn is { } latest && item.ReturnTargetOn.HasValue)
        {
            if (earliest > from) from = earliest;
            if (latest < to) to = latest;
        }
        return (from, target, to);
    }

    // Days a visit can actually be booked on: the planned window without the past. A plan that is overdue by more than
    // the tolerance counts as moved to today. See docs/specs/TREATMENT-NEXT-VISIT-BOOKING.md.
    public static (DateOnly From, DateOnly Target, DateOnly To)? BookingWindow(TreatmentItem item, DateOnly today)
    {
        if (PlanWindow(item) is not { } window) return null;
        if (window.To < today) return (today, today, today.AddDays(BookingToleranceDays));
        var from = window.From > today ? window.From : today;
        var target = window.Target < from ? from : window.Target > window.To ? window.To : window.Target;
        return (from, target, window.To);
    }
}

namespace DCMS.Domain.Features.Treatment;

public static class TreatmentItemRules
{
    // A visit may be booked at most this many days before or after the planned date of an item.
    public const int BookingToleranceDays = 4;

    public static bool IsPendingForBooking(TreatmentItem item) => !item.DisplayCompleted;

    public static DateOnly? PlannedTarget(TreatmentItem item) => item.EstimatedStart;

    public static (DateOnly From, DateOnly Target, DateOnly To)? PlanWindow(TreatmentItem item)
    {
        if (item.EstimatedStart is not { } target) return null;
        return (target.AddDays(-BookingToleranceDays), target, target.AddDays(BookingToleranceDays));
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

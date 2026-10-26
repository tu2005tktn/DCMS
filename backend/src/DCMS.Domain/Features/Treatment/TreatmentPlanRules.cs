namespace DCMS.Domain.Features.Treatment;

public static class TreatmentPlanRules
{
    public static bool ValidDuration(int? min, int? selected, int? max, int? reserved,
        bool acknowledged)
    {
        if (selected is < 1 or > 480 || min.HasValue != max.HasValue) return false;
        if (min.HasValue && (selected is null || min < 1 || min > selected || max < selected || max > 480))
            return false;
        if (reserved.HasValue && (selected is null || reserved is < 15 or > 480 ||
            reserved % 15 != 0 || reserved < selected)) return false;
        return !max.HasValue || !reserved.HasValue || reserved >= max || acknowledged;
    }

    public static bool ValidReturnWindow(DateOnly? earliest, DateOnly? target, DateOnly? latest) =>
        earliest.HasValue == target.HasValue && target.HasValue == latest.HasValue &&
        (!earliest.HasValue || earliest <= target && target <= latest);

    public static long Gross(decimal quantity, long unitPrice) => checked((long)decimal.Round(
        quantity * unitPrice, 0, MidpointRounding.AwayFromZero));

    public static long Discount(long gross, string type, decimal value)
    {
        if (gross < 0 || type is not ("Fixed" or "Percent") || value <= 0 ||
            type == "Fixed" && decimal.Truncate(value) != value ||
            type == "Percent" && value > 100) throw new ArgumentOutOfRangeException(nameof(value));
        var amount = decimal.Round(type == "Percent" ? gross * value / 100m : value,
            0, MidpointRounding.AwayFromZero);
        if (amount > gross) throw new ArgumentOutOfRangeException(nameof(value));
        return checked((long)amount);
    }
}

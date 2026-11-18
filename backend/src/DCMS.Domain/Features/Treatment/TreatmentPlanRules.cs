namespace DCMS.Domain.Features.Treatment;

public static class TreatmentPlanRules
{
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

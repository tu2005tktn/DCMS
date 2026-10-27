namespace DCMS.Domain.Features.Treatment;

public static class TreatmentSheetRules
{
    public static bool IsFdi(int tooth) => tooth / 10 is >= 1 and <= 4 && tooth % 10 is >= 1 and <= 8
        || tooth / 10 is >= 5 and <= 8 && tooth % 10 is >= 1 and <= 5;

    public static bool ValidQuantity(decimal quantity, string unit) => quantity > 0
        && decimal.Round(quantity, 3) == quantity
        && (unit is not ("răng" or "hàm" or "lần" or "gói") || quantity == decimal.Truncate(quantity));

    public static bool RegionWithinScope(string? agreed, string? actual)
    {
        if (string.Equals(agreed?.Trim(), actual?.Trim(), StringComparison.Ordinal)) return true;
        var planned = (agreed ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
        var performed = (actual ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
        return planned.Length > 0 && performed.Length > 0
            && planned.All(x => int.TryParse(x, out var tooth) && IsFdi(tooth))
            && performed.All(x => planned.Contains(x, StringComparer.Ordinal));
    }
}

using System.Globalization;
using System.Numerics;

namespace DCMS.Domain.Features.SystemSettings;

/// <summary>Exact base-unit arithmetic. Never rounds a material quantity or a medicine dose.</summary>
public static class UnitConversionRules
{
    private static (BigInteger Value, int Scale) Parse(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > 60)
            throw new ArgumentException("Nhập số thập phân dương.");
        var parts = text.Split('.');
        if (parts.Length > 2 || parts.Any(p => p.Length == 0 || p.Any(c => c is < '0' or > '9')))
            throw new ArgumentException("Nhập số thập phân dương, không dùng ký hiệu mũ.");
        var value = BigInteger.Parse(string.Concat(parts), CultureInfo.InvariantCulture);
        if (value <= 0) throw new ArgumentException("Giá trị phải lớn hơn 0.");
        return (value, parts.Length == 2 ? parts[1].Length : 0);
    }

    public static string ToBase(string quantity, string factor, bool medicine = false)
    {
        var q = Parse(quantity); var f = Parse(factor);
        var product = q.Value * f.Value; var scale = q.Scale + f.Scale;
        while (scale > 0 && product % 10 == 0) { product /= 10; scale--; }
        if (scale > (medicine ? 0 : 3))
            throw new ArgumentException(medicine ? "Thuốc phải quy ra đơn vị gốc nguyên." : "Số lượng gốc tối đa 3 chữ số thập phân.");
        var digits = product.ToString(CultureInfo.InvariantCulture).PadLeft(scale + 1, '0');
        return scale == 0 ? digits : digits.Insert(digits.Length - scale, ".");
    }

    public static void ValidateFactor(string factor) => Parse(factor);
}

namespace DCMS.Infrastructure.Features.Prescriptions;

// Prescription quantities are printed in digits and words (0..999 999); sufficient for the 1..10 000 quantity range.
public static class VietnameseNumberWords
{
    private static readonly string[] Digits = ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];

    public static string ToWords(int value)
    {
        if (value < 0 || value > 999_999) return value.ToString();
        if (value == 0) return "Không";
        var thousands = value / 1000;
        var rest = value % 1000;
        var parts = new List<string>();
        if (thousands > 0) parts.Add(Hundreds(thousands, false) + " nghìn");
        if (rest > 0) parts.Add(Hundreds(rest, thousands > 0));
        var text = string.Join(" ", parts);
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string Hundreds(int n, bool full)
    {
        var h = n / 100; var t = n / 10 % 10; var u = n % 10;
        var words = new List<string>();
        if (h > 0 || full) words.Add($"{Digits[h]} trăm");
        if (t == 0 && u > 0 && words.Count > 0) words.Add("lẻ");
        if (t == 1) words.Add("mười");
        else if (t > 1) words.Add($"{Digits[t]} mươi");
        if (u > 0)
            words.Add(u switch
            {
                1 when t > 1 => "mốt",
                5 when t > 0 => "lăm",
                4 when t > 1 => "tư",
                _ => Digits[u]
            });
        return string.Join(" ", words);
    }
}

using System.Net.Mail;
using System.Text.RegularExpressions;

namespace DCMS.Application.Common;

// [PROPOSED] Shared contact rules for S04 and future S02/S03; see docs/specs/S04-UC31.md.
public static partial class ContactRules
{
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public static string? NormalizeEmail(string? value) => Clean(value)?.ToUpperInvariant();
    public static string? NormalizePhone(string? value) => Clean(value) is { } phone
        ? PhoneSeparators().Replace(phone, "") : null;

    public static void Validate(string? email, string? phone, bool required)
    {
        email = Clean(email); phone = Clean(phone);
        if (required && email == null && phone == null)
            throw new BusinessException(400, SystemMessages.Get("MSG02", "Email hoặc số điện thoại"));
        if (email != null && (email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email))
            throw new BusinessException(400, SystemMessages.Get("MSG04", "Email"));
        if (phone != null && (!PhonePattern().IsMatch(phone) || !NormalizedPhonePattern().IsMatch(NormalizePhone(phone)!)))
            throw new BusinessException(400, SystemMessages.Get("MSG04", "Số điện thoại"));
    }

    [GeneratedRegex(@"[ ()-]")] private static partial Regex PhoneSeparators();
    [GeneratedRegex(@"^\+?[0-9 ()-]{7,24}$")] private static partial Regex PhonePattern();
    [GeneratedRegex(@"^\+?[0-9]{7,24}$")] private static partial Regex NormalizedPhonePattern();
}

using System.Text.RegularExpressions;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Authentication;

namespace DCMS.Infrastructure.Features.Authentication;

// Contact detection and normalisation shared by every recovery delivery adapter (development and SMTP).
internal static partial class RecoveryContactFormat
{
    public static RecoveryChannel DetectChannel(string? contact)
    {
        if (string.IsNullOrWhiteSpace(contact)) return RecoveryChannel.Unknown;
        var value = contact.Trim();
        if (EmailPattern().IsMatch(value)) return RecoveryChannel.Email;
        if (PhonePattern().IsMatch(value)) return RecoveryChannel.Phone;
        return RecoveryChannel.Unknown;
    }

    public static string Normalize(string? contact)
    {
        if (string.IsNullOrWhiteSpace(contact)) return "";
        var value = contact.Trim();
        return DetectChannel(value) == RecoveryChannel.Email ? value.ToLowerInvariant() : value;
    }

    [GeneratedRegex(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$")]
    private static partial Regex EmailPattern();
    [GeneratedRegex(@"^\+?[0-9 ()-]{7,24}$")]
    private static partial Regex PhonePattern();
}

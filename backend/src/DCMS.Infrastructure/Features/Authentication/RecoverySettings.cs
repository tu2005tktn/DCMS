using DCMS.Domain.Features.Authentication;

namespace DCMS.Infrastructure.Features.Authentication;

/// <summary>
/// Configurable UC-02 recovery policy (GBR-082). Values come from configuration
/// ("Recovery" section) with no hard-coded business numbers in code.
/// Defaults below are development placeholders marked [PROPOSED] and must be
/// confirmed with the clinic before deployment.
/// </summary>
public sealed class RecoverySettings
{
    /// [PROPOSED] How long a verification code / reset grant stays valid, in minutes.
    public int TokenLifetimeMinutes { get; set; } = 15;

    /// [PROPOSED] Minimum wait between two sends for the same account, in minutes.
    public int ResendCooldownMinutes { get; set; } = 2;

    /// [PROPOSED] Maximum number of resends inside one recovery session.
    public int MaxResends { get; set; } = 3;

    /// [PROPOSED] Maximum wrong-proof attempts before the request is blocked.
    public int MaxVerifyAttempts { get; set; } = 5;

    /// [PROPOSED] Maximum recovery requests per account per hour.
    public int RateLimitPerAccountPerHour { get; set; } = 5;

    /// [PROPOSED] Maximum recovery requests per source IP per hour.
    public int RateLimitPerIpPerHour { get; set; } = 20;

    /// [PROPOSED] Channel used for recovery delivery: Email, Phone or Link.
    public RecoveryChannel Channel { get; set; } = RecoveryChannel.Email;

    /// [PROPOSED] Length of the numeric verification code.
    public int CodeLength { get; set; } = 6;

    /// Base URL used to build the verification link for the Link channel.
    public string VerificationBaseUrl { get; set; } = "";

    public bool IsLinkChannel => Channel == RecoveryChannel.Link;

    public bool IsValid() => TokenLifetimeMinutes is >= 1 and <= 1440
        && ResendCooldownMinutes is >= 0 and <= 1440
        && MaxResends is >= 0 and <= 20
        && MaxVerifyAttempts is >= 1 and <= 20
        && RateLimitPerAccountPerHour is >= 1 and <= 1000
        && RateLimitPerIpPerHour is >= 1 and <= 10000
        && CodeLength is >= 4 and <= 12
        && Enum.IsDefined(Channel);

    public void Validate()
    {
        if (TokenLifetimeMinutes is < 1 or > 1440) throw new InvalidOperationException("Recovery:TokenLifetimeMinutes must be between 1 and 1440.");
        if (ResendCooldownMinutes is < 0 or > 1440) throw new InvalidOperationException("Recovery:ResendCooldownMinutes must be between 0 and 1440.");
        if (MaxResends is < 0 or > 20) throw new InvalidOperationException("Recovery:MaxResends must be between 0 and 20.");
        if (MaxVerifyAttempts is < 1 or > 20) throw new InvalidOperationException("Recovery:MaxVerifyAttempts must be between 1 and 20.");
        if (RateLimitPerAccountPerHour is < 1 or > 1000) throw new InvalidOperationException("Recovery:RateLimitPerAccountPerHour must be between 1 and 1000.");
        if (RateLimitPerIpPerHour is < 1 or > 10000) throw new InvalidOperationException("Recovery:RateLimitPerIpPerHour must be between 1 and 10000.");
        if (CodeLength is < 4 or > 12) throw new InvalidOperationException("Recovery:CodeLength must be between 4 and 12.");
        if (!Enum.IsDefined(Channel)) throw new InvalidOperationException("Recovery:Channel is not a supported channel.");
    }
}

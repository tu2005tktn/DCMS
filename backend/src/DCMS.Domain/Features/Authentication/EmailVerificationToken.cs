namespace DCMS.Domain.Features.Authentication;

public sealed class EmailVerificationToken
{
    public long Id { get; set; }
    public int AccountId { get; set; }
    public string NormalizedEmail { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset LastSentAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public int ResendCount { get; set; }
    public int VerifyAttempts { get; set; }
}

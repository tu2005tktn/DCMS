namespace DCMS.Domain.Features.Authentication;

public sealed class AuthSession
{
    public Guid Id { get; set; }
    public int AccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

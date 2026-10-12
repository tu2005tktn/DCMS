namespace DCMS.Domain.Features.Authentication;

public sealed class RecoveryToken
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string TokenHash { get; set; } = "";
    // Reset grant is a separate, single-use value handed to the client after proof verification.
    public string ResetGrantHash { get; set; } = "";
    public string ChannelType { get; set; } = "";
    public string NormalizedDestination { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public int VerifyAttempts { get; set; }
    public int ResendCount { get; set; }
    public DateTimeOffset? LastResentAt { get; set; }
}

public sealed class RecoveryAudit
{
    public long Id { get; set; }
    public int? ActorId { get; set; }
    public int TargetId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string ChannelType { get; set; } = "";
    public string Result { get; set; } = "";
}

public sealed class RecoveryOutbox
{
    public long Id { get; set; }
    public int AccountId { get; set; }
    public string ChannelType { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ProviderMessageId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
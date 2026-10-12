using DCMS.Domain.Features.Authentication;

namespace DCMS.Application.Features.Authentication;

// Results returned by the shared UC-02 recovery process.
// Self-service results are intentionally neutral: they never reveal whether an account exists.
public sealed record RecoveryRequestOutcome(bool Accepted, string MessageCode);

public sealed record RecoveryVerifyOutcome(bool Valid, string? ResetGrant);

public sealed record RecoveryResetOutcome(bool Succeeded, string MessageCode);

public sealed record RecoveryResendOutcome(bool Accepted, string MessageCode, int? RetryAfterSeconds);

// Rich outcome for the authorized Admin-assisted path (S04). Admins already have a legitimate
// need to know, so this path is not neutral - and it is guarded by the service itself (GBR-075).
public sealed record AdminRecoveryRequestOutcome(bool Accepted, string MessageCode, int AccountId,
    string ChannelType, string Destination, int? RetryAfterSeconds);

public sealed record RecoveryStatusSnapshot(bool HasActiveRequest, string ChannelType,
    int AttemptsUsed, int ResendsUsed, int? SecondsUntilExpiry, int? MaxAttempts, int? MaxResends,
    int? RetryAfterSeconds);

public interface IAccountRecoveryService
{
    // UC-02 self-service recovery (anonymous caller).
    Task<RecoveryRequestOutcome> RequestAsync(string identifier, string? ipAddress, CancellationToken ct);
    Task<RecoveryVerifyOutcome> VerifyAsync(string identifier, string proof, string? ipAddress, CancellationToken ct);
    Task<RecoveryResendOutcome> ResendAsync(string identifier, string? ipAddress, CancellationToken ct);
    Task<RecoveryResetOutcome> ResetAsync(string identifier, string resetGrant, string newPassword,
        string confirmPassword, CancellationToken ct);
    Task<RecoveryStatusSnapshot> GetStatusAsync(string identifier, CancellationToken ct);

    // UC-31 / S04 Admin-assisted recovery. The service re-checks the Admin permission server-side
    // on every call instead of trusting the caller (GBR-075).
    Task<AdminRecoveryRequestOutcome> RequestForAccountAsync(int actorId, int targetAccountId, CancellationToken ct);
    Task<RecoveryVerifyOutcome> VerifyForAccountAsync(int actorId, int targetAccountId, string proof, CancellationToken ct);
    Task<RecoveryResetOutcome> ResetForAccountAsync(int actorId, int targetAccountId, string newPassword,
        string confirmPassword, CancellationToken ct);
}

// Shared S02 contact adapter contract. Consumed here; never duplicated or re-implemented.
public interface IContactDeliveryAdapter
{
    RecoveryChannel DetectChannel(string contact);
    bool IsValid(string contact);
    string Normalize(string contact);
    Task<bool> SendRecoveryAsync(int accountId, RecoveryChannel channel, string destination,
        string proof, string resetLink, CancellationToken ct);
}

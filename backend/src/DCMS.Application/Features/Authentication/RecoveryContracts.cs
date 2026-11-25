using DCMS.Domain.Features.Authentication;

namespace DCMS.Application.Features.Authentication;

// Results returned by the shared UC-02 recovery process.
// Self-service results are intentionally neutral: they never reveal whether an account exists.
public sealed record RecoveryRequestOutcome(bool Accepted, string MessageCode);

public sealed record RecoveryVerifyOutcome(bool Valid, string? ResetGrant);

public sealed record RecoveryResetOutcome(bool Succeeded, string MessageCode);

public sealed record RecoveryResendOutcome(bool Accepted, string MessageCode, int? RetryAfterSeconds);

public interface IAccountRecoveryService
{
    // UC-02 self-service recovery (anonymous caller). Every response is neutral: the same text,
    // the same status and the same shape whether or not the account exists.
    Task<RecoveryRequestOutcome> RequestAsync(string identifier, string? ipAddress, CancellationToken ct,
        bool publicOnly = false);
    Task<RecoveryVerifyOutcome> VerifyAsync(string identifier, string proof, string? ipAddress, CancellationToken ct,
        bool publicOnly = false);
    Task<RecoveryResetOutcome> ResetAsync(string identifier, string resetGrant, string newPassword,
        string confirmPassword, CancellationToken ct, bool publicOnly = false);
    Task<RecoveryResendOutcome> ResendAsync(string identifier, string? ipAddress, CancellationToken ct,
        bool publicOnly = false);

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

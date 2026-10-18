namespace DCMS.Application.Features.Authentication;

public sealed record EmailVerificationStatus(string Email, bool Confirmed, int CodeLength);
public sealed record EmailVerificationRequestResult(string Message, int RetryAfterSeconds);
public sealed record EmailVerificationConfirmRequest(string Code);

public interface IEmailVerificationService
{
    Task<EmailVerificationStatus> GetStatusAsync(int accountId, CancellationToken ct);
    Task<EmailVerificationRequestResult> RequestAsync(int accountId, CancellationToken ct);
    Task<EmailVerificationStatus> ConfirmAsync(int accountId, string code, CancellationToken ct);
}

public interface IEmailVerificationDelivery
{
    Task<bool> SendAsync(string destination, string code, int lifetimeMinutes, CancellationToken ct);
}

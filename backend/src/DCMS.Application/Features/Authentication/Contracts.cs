using DCMS.Application.Common;

namespace DCMS.Application.Features.Authentication;

public sealed record SignInRequest(string UserName, string Password);
public sealed record CurrentUser(int Id, string UserName, string FullName, string AccountType,
    string[] Roles, string[] EffectiveRoles, string[] Permissions, bool HasVerifiedPatientLink = false);
public sealed record SignInResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUser User);
public sealed record CredentialPolicy(int RequiredLength, bool RequireDigit, bool RequireLowercase,
    bool RequireUppercase, bool RequireNonAlphanumeric, int SessionMinutes, int MaximumLength = 256);
public sealed record RegisterRequest(string? FullName, string? PhoneNumber, string? Email, string? Password, string? ConfirmPassword);
public sealed record RegisterResponse(int AccountId, string PhoneNumber, SystemMessage Notification);

public interface IRegistrationService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
}

public interface IAuthenticationService
{
    Task<SignInResponse> SignInAsync(SignInRequest request, CancellationToken ct);
    Task<CurrentUser?> ValidateSessionAsync(int accountId, Guid sessionId, CancellationToken ct);
    Task SignOutAsync(int accountId, Guid sessionId, CancellationToken ct);
}

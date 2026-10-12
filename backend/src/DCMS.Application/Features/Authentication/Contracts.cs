namespace DCMS.Application.Features.Authentication;

public sealed record SignInRequest(string UserName, string Password, string? DeviceLabel = null);
public sealed record CurrentUser(int Id, string UserName, string FullName, string AccountType,
    string[] Roles, string[] EffectiveRoles, string[] Permissions, bool HasVerifiedPatientLink = false);
public sealed record SignInResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUser User,
    string RefreshToken, Guid SessionId);
public sealed record RefreshRequest(string RefreshToken);
public sealed record RefreshResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken, Guid SessionId);
public sealed record SessionInfo(Guid SessionId, string DeviceLabel, DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt, DateTimeOffset ExpiresAt, bool CurrentSession);
public sealed record CredentialPolicy(int RequiredLength, bool RequireDigit, bool RequireLowercase,
    bool RequireUppercase, bool RequireNonAlphanumeric, int SessionMinutes);

public interface IAuthenticationService
{
    Task<SignInResponse> SignInAsync(SignInRequest request, CancellationToken ct);
    Task<CurrentUser?> ValidateSessionAsync(int accountId, Guid sessionId, CancellationToken ct);
    Task SignOutAsync(int accountId, Guid sessionId, CancellationToken ct);
    /// <summary>
    /// Đổi refresh token → access token mới. Rotation: refresh cũ bị thu hồi và thay bằng
    /// refresh mới. Nếu refresh cũ đã REVOKED/REPLACED (reuse detection) → toàn bộ chain
    /// bị thu hồi, trả 401.
    /// </summary>
    Task<RefreshResponse> RefreshAsync(RefreshRequest request, CancellationToken ct);
    /// <summary>Thu hồi tất cả session + refresh token còn active của account hiện tại.</summary>
    Task SignOutAllAsync(int accountId, Guid currentSessionId, string? reason, CancellationToken ct);
    /// <summary>Liệt kê session đang hoạt động (cho UI "đăng xuất mọi thiết bị" tương lai).</summary>
    Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(int accountId, Guid currentSessionId, CancellationToken ct);
}

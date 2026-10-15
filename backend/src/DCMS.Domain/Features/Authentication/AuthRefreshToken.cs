namespace DCMS.Domain.Features.Authentication;

/// <summary>
/// Refresh token rotation record. Mỗi token chỉ dùng được 1 lần; khi rotate, token cũ
/// được đánh dấu REPLACED trỏ sang token mới. Nếu REUSED, toàn bộ chain bị thu hồi.
/// </summary>
public sealed class AuthRefreshToken
{
    public long Id { get; set; }
    public int AccountId { get; set; }
    public Guid SessionId { get; set; }
    /// <summary>SHA-256 hash hex (64 chars) của refresh token plaintext. Không lưu plaintext.</summary>
    public string TokenHash { get; set; } = "";
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    /// <summary>Token mới thay thế token này (chain của rotation). Null nếu chưa rotate hoặc đã revoke.</summary>
    public long? ReplacedById { get; set; }
    public string Status { get; set; } = "ACTIVE"; // ACTIVE | REPLACED | REVOKED
}

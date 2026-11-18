namespace DCMS.Domain.Features.Authentication;

public sealed class AccountClaim
{
    public int Id { get; set; }
    public string? Type { get; set; }
    public string? Value { get; set; }
}

public sealed class AccountLogin
{
    public string Provider { get; set; } = "";
    public string Key { get; set; } = "";
    public string? DisplayName { get; set; }
}

public sealed class AccountIdentityToken
{
    public string Provider { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Value { get; set; }
}

public sealed class UserAuthData
{
    public string? SecurityStamp { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; }
    public int AccessFailedCount { get; set; }
    public List<AccountClaim> Claims { get; set; } = [];
    public List<AccountLogin> Logins { get; set; } = [];
    public List<AccountIdentityToken> IdentityTokens { get; set; } = [];
    public List<AuthSession> Sessions { get; set; } = [];
    public List<EmailVerificationToken> EmailVerifications { get; set; } = [];
    public List<RecoveryToken> Recoveries { get; set; } = [];
    public List<RecoveryOutbox> RecoveryDeliveries { get; set; } = [];
}

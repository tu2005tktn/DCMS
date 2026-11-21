using DCMS.Application.Common;
using DCMS.Domain.Features.Authentication;
using Microsoft.AspNetCore.Identity;

namespace DCMS.Infrastructure.Features.Accounts;

public sealed class ApplicationUser : IdentityUser<int>
{
    public int EmployeeId { get; set; }

    public string FullName { get; set; } = "";
    public string AccountType { get; set; } = "Staff";
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? NormalizedPhone => ContactRules.NormalizePhone(PhoneNumber);
    public string[] AssignedRoles { get; set; } = [];
    public UserAuthData AuthData { get; set; } = new();

    public override string? NormalizedUserName
    {
        get => UserName?.ToUpperInvariant();
        set { }
    }

    public override string? NormalizedEmail
    {
        get => Email?.ToUpperInvariant();
        set { }
    }

    public override string? SecurityStamp
    {
        get => AuthData.SecurityStamp;
        set => AuthData.SecurityStamp = value;
    }

    public override bool PhoneNumberConfirmed
    {
        get => AuthData.PhoneNumberConfirmed;
        set => AuthData.PhoneNumberConfirmed = value;
    }

    public override bool TwoFactorEnabled
    {
        get => AuthData.TwoFactorEnabled;
        set => AuthData.TwoFactorEnabled = value;
    }

    public override DateTimeOffset? LockoutEnd
    {
        get => AuthData.LockoutEnd;
        set => AuthData.LockoutEnd = value;
    }

    public override bool LockoutEnabled
    {
        get => AuthData.LockoutEnabled;
        set => AuthData.LockoutEnabled = value;
    }

    public override int AccessFailedCount
    {
        get => AuthData.AccessFailedCount;
        set => AuthData.AccessFailedCount = value;
    }

    public List<AccountClaim> Claims { get => AuthData.Claims; set => AuthData.Claims = value; }
    public List<AccountLogin> Logins { get => AuthData.Logins; set => AuthData.Logins = value; }
    public List<AccountIdentityToken> IdentityTokens { get => AuthData.IdentityTokens; set => AuthData.IdentityTokens = value; }
    public List<AuthSession> Sessions { get => AuthData.Sessions; set => AuthData.Sessions = value; }
    public List<EmailVerificationToken> EmailVerifications { get => AuthData.EmailVerifications; set => AuthData.EmailVerifications = value; }
    public List<RecoveryToken> Recoveries { get => AuthData.Recoveries; set => AuthData.Recoveries = value; }
    public List<RecoveryOutbox> RecoveryDeliveries { get => AuthData.RecoveryDeliveries; set => AuthData.RecoveryDeliveries = value; }
}

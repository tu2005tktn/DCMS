using Microsoft.AspNetCore.Identity;
using DCMS.Domain.Features.Authentication;

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
    public string? NormalizedPhone { get; private set; }
    public string[] AssignedRoles { get; set; } = [];
    public string? GoogleSubject { get; set; }
    public List<AccountClaim> Claims { get; set; } = [];
    public List<AccountLogin> Logins { get; set; } = [];
    public List<AccountIdentityToken> IdentityTokens { get; set; } = [];
    public List<AuthSession> Sessions { get; set; } = [];
    public List<EmailVerificationToken> EmailVerifications { get; set; } = [];
    public List<RecoveryToken> Recoveries { get; set; } = [];
    public List<RecoveryOutbox> RecoveryDeliveries { get; set; } = [];
}

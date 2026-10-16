using Microsoft.AspNetCore.Identity;

namespace DCMS.Infrastructure.Features.Accounts;

public sealed class ApplicationUser : IdentityUser<int>
{
    public string FullName { get; set; } = "";
    public string AccountType { get; set; } = "Staff";
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? NormalizedPhone { get; private set; }
}

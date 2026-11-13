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

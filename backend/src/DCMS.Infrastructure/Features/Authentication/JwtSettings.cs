namespace DCMS.Infrastructure.Features.Authentication;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "DCMS";
    public string Audience { get; set; } = "DCMS.Web";
    public string SigningKey { get; set; } = "";
    public int SessionMinutes { get; set; } = 30;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

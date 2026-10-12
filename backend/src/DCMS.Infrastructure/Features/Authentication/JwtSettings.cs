namespace DCMS.Infrastructure.Features.Authentication;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "DCMS";
    public string Audience { get; set; } = "DCMS.Web";
    public string SigningKey { get; set; } = "";
    public int SessionMinutes { get; set; } = 30;
}

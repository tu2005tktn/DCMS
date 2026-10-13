using DCMS.Application.Abstractions.Persistence;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using DCMS.Application.Features.Accounts;
using DCMS.Application.Features.Authentication;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Authentication;
using DCMS.Application.Features.PatientLinks;
using DCMS.Infrastructure.Features.PatientLinks;

namespace DCMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<DcmsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IDatabaseConnectionCheck, DatabaseConnectionCheck>();
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            configuration.GetSection("Identity:Password").Bind(options.Password);
            options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._";
        }).AddRoles<IdentityRole<int>>().AddEntityFrameworkStores<DcmsDbContext>();
        services.AddOptions<JwtSettings>().Bind(configuration.GetSection("Jwt"))
            .Validate(x => System.Text.Encoding.UTF8.GetByteCount(x.SigningKey) >= 32, "Jwt:SigningKey must contain at least 32 bytes.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer) && !string.IsNullOrWhiteSpace(x.Audience)
                && x.SessionMinutes is >= 1 and <= 1440
                && x.AccessTokenMinutes is >= 1 and <= 60
                && x.RefreshTokenDays is >= 1 and <= 365, "Invalid JWT configuration.").ValidateOnStart();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<DevelopmentAccountSeeder>();
        services.AddScoped<IPatientLinkService, PatientLinkService>();
        services.AddScoped<IPatientLinkReviewService, PatientLinkReviewService>();
        services.AddScoped<DevelopmentPatientLinkSeeder>();
        return services;
    }
}

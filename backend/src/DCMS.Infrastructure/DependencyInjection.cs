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
using Microsoft.Extensions.Hosting;
using DCMS.Infrastructure.Features.Appointments;
using DCMS.Application.Features.Appointments;

namespace DCMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<DcmsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IDatabaseConnectionCheck, DatabaseConnectionCheck>();
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            configuration.GetSection("Identity:Password").Bind(options.Password);
            options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._";
        }).AddRoles<IdentityRole<int>>().AddEntityFrameworkStores<DcmsDbContext>()
            // Password reset issues a security token, so the default token providers must be
            // registered; AddIdentityCore alone does not add them.
            .AddDefaultTokenProviders();
        services.AddOptions<JwtSettings>().Bind(configuration.GetSection("Jwt"))
            .Validate(x => System.Text.Encoding.UTF8.GetByteCount(x.SigningKey) >= 32, "Jwt:SigningKey must contain at least 32 bytes.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer) && !string.IsNullOrWhiteSpace(x.Audience)
                && x.SessionMinutes is >= 1 and <= 1440, "Invalid JWT configuration.").ValidateOnStart();
        services.AddOptions<RecoverySettings>().Bind(configuration.GetSection("Recovery"))
            .Validate(x => x.IsValid(), "Invalid Recovery configuration (see RecoverySettings.Validate).").ValidateOnStart();
        services.AddMemoryCache(options => options.SizeLimit = 8192);
        services.AddScoped<IRecoveryOutbox, RecoveryOutboxWriter>();
        services.AddScoped<IAccountRecoveryService, AccountRecoveryService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<DevelopmentAccountSeeder>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        if (environment.IsDevelopment())
        {
            // The development adapter is never registered outside Development. The concrete type is
            // registered as well so the Development-only recovery proof endpoint can resolve it.
            services.AddScoped<DevelopmentContactDeliveryAdapter>();
            services.AddScoped<IContactDeliveryAdapter>(sp => sp.GetRequiredService<DevelopmentContactDeliveryAdapter>());
        }
            services.AddScoped<DCMS.Application.Features.Appointments.Interfaces.IAppointmentService, DCMS.Application.Features.Appointments.AppointmentService>();`n    services.AddKeyedScoped<DCMS.Application.Features.Appointments.IAppointmentService, DCMS.Infrastructure.Features.Appointments.AppointmentService>(\"Infrastructure\AppointmentService\">);`n        services.AddScoped<DCMS.Application.Features.Appointments.Interfaces.IAppointmentService, DCMS.Application.Features.Appointments.AppointmentService>();`n    services.AddKeyedScoped<DCMS.Application.Features.Appointments.IAppointmentService, DCMS.Infrastructure.Features.Appointments.AppointmentService>(\"Infrastructure\AppointmentService\">);`n    return services;
    }
}



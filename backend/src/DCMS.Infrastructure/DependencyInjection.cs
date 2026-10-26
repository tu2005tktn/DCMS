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
using DCMS.Application.Features.Patients;
using DCMS.Infrastructure.Features.Patients;
using DCMS.Application.Features.Appointments;
using DCMS.Infrastructure.Features.Appointments;
using Microsoft.Extensions.Hosting;
using DCMS.Infrastructure.Features.Notifications;
using DCMS.Application.Features.SupportRequests;

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
                && x.SessionMinutes is >= 1 and <= 1440
                && x.AccessTokenMinutes is >= 1 and <= 60
                && x.RefreshTokenDays is >= 1 and <= 365, "Invalid JWT configuration.").ValidateOnStart();
        services.AddOptions<RecoverySettings>().Bind(configuration.GetSection("Recovery"))
            .Validate(x => x.IsValid(), "Invalid Recovery configuration (see RecoverySettings.Validate).").ValidateOnStart();
        services.AddMemoryCache(options => options.SizeLimit = 8192);
        services.AddScoped<IRecoveryOutbox, RecoveryOutboxWriter>();
        services.AddScoped<IAccountRecoveryService, AccountRecoveryService>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddHttpClient();
        services.AddSingleton<IGoogleTokenVerifier, GoogleTokenVerifier>();
        services.AddScoped<IGoogleAuthenticationService, GoogleAuthenticationService>();
        services.AddScoped<DevelopmentAccountSeeder>();
        services.AddScoped<IPatientLinkService, PatientLinkService>();
        services.AddScoped<IPatientLinkReviewService, PatientLinkReviewService>();
        services.AddScoped<DevelopmentPatientLinkSeeder>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<Features.Scheduling.WorkingScheduleService>();
        services.AddScoped<DCMS.Application.Features.Scheduling.IBookingSettingsService, Features.Scheduling.BookingSettingsService>();
        services.AddScoped<DCMS.Application.Features.Scheduling.IWorkingScheduleService>(sp => sp.GetRequiredService<Features.Scheduling.WorkingScheduleService>());
        services.AddScoped<DCMS.Application.Features.Scheduling.IAvailabilityService>(sp => sp.GetRequiredService<Features.Scheduling.WorkingScheduleService>());
        services.AddScoped<Features.Scheduling.DevelopmentSchedulingSeeder>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<DCMS.Application.Features.Reception.IReceptionService, Features.Reception.ReceptionService>();
        services.AddScoped<DCMS.Application.Features.Reception.IReceptionSourceReader, Features.Reception.UnavailableReceptionSourceReader>();
        services.AddScoped<IAppointmentQueryService, AppointmentQueryService>();
        services.AddScoped<IAppointmentChangeService, AppointmentChangeService>();
        services.AddScoped<ISupportRequestService, SupportRequestService>();
        services.AddScoped<IFollowUpAppointmentChangePort, UnavailableFollowUpAppointmentChangePort>();
        services.AddScoped<DevelopmentAppointmentSeeder>();
        services.AddScoped<IFollowUpBookingPort, UnavailableFollowUpBookingPort>();
        services.AddScoped<IAppointmentNotificationDispatcher, AppointmentNotificationDispatcher>();
        services.AddScoped<IExternalNotificationAdapter, UnavailableExternalNotificationAdapter>();
        // S03 recovery delivery: a configured SMTP server sends real email in any environment; otherwise
        // only Development gets the logged-only adapter, so no environment can claim a message was sent.
        var smtp = configuration.GetSection("Smtp").Get<SmtpSettings>() ?? new SmtpSettings();
        services.AddOptions<SmtpSettings>().Bind(configuration.GetSection("Smtp"));
        if (environment.IsDevelopment())
        {
            // The concrete type stays registered in Development so the proof endpoint can resolve it;
            // with SMTP configured it simply has no proof to return.
            services.AddScoped<DevelopmentContactDeliveryAdapter>();
            if (!smtp.IsConfigured)
                services.AddScoped<IContactDeliveryAdapter>(sp => sp.GetRequiredService<DevelopmentContactDeliveryAdapter>());
        }
        if (smtp.IsConfigured)
        {
            services.AddScoped<IMailSender, SmtpMailSender>();
            services.AddScoped<SmtpContactDeliveryAdapter>();
            services.AddScoped<IContactDeliveryAdapter>(sp => sp.GetRequiredService<SmtpContactDeliveryAdapter>());
            services.AddScoped<IEmailVerificationDelivery>(sp => sp.GetRequiredService<SmtpContactDeliveryAdapter>());
        }
        else services.AddScoped<IEmailVerificationDelivery, UnavailableEmailVerificationDelivery>();
        return services;
    }
}

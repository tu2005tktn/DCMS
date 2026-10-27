using DCMS.Api.Features.Health;
using DCMS.Api.Features.Reception;
using DCMS.Api.Features.Scheduling;
using DCMS.Api.Features.Appointments;
using DCMS.Api.Features.Notifications;
using DCMS.Api.Features.Realtime;
using DCMS.Infrastructure;
using System.Security.Claims;
using System.Text;
using DCMS.Api.Common;
using DCMS.Api.Features.Accounts;
using DCMS.Api.Features.Authentication;
using DCMS.Api.Features.PatientLinks;
using DCMS.Api.Features.Patients;
using DCMS.Api.Features.Treatment;
using DCMS.Infrastructure.Features.Notifications;
using DCMS.Infrastructure.Features.PatientLinks;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Notifications;
using DCMS.Domain.Features.Accounts;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddOpenApi();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "DCMS",
        ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"] ?? "DCMS.Web",
        ValidateLifetime = true, ValidateIssuerSigningKey = true, ClockSkew = TimeSpan.Zero,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"] ?? "")),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], NameClaimType = "sub", RoleClaimType = ClaimTypes.Role
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (!int.TryParse(context.Principal?.FindFirstValue("sub"), out var accountId)
                || !Guid.TryParse(context.Principal?.FindFirstValue("sid"), out var sessionId))
            { context.Fail("Invalid session"); return; }
            var service = context.HttpContext.RequestServices.GetRequiredService<IAuthenticationService>();
            var current = await service.ValidateSessionAsync(accountId, sessionId, context.HttpContext.RequestAborted);
            if (current == null) { context.Fail("Unavailable account or session"); return; }
            // Trust current database roles, never a stale JWT role claim.
            var identity = new ClaimsIdentity([
                new Claim("sub", accountId.ToString()), new Claim("sid", sessionId.ToString()),
                .. current.EffectiveRoles.Select(role => new Claim(ClaimTypes.Role, role))
            ], "CurrentSession", "sub", ClaimTypes.Role);
            context.Principal = new ClaimsPrincipal(identity);
            context.HttpContext.Items["CurrentUser"] = current;
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = 401;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(SystemMessages.Get("MSG07"));
        },
        OnForbidden = context =>
        {
            context.Response.StatusCode = 403;
            return context.Response.WriteAsJsonAsync(SystemMessages.Get("MSG06"));
        },
        // S26+ — SignalR browser client không gửi Authorization header tuỳ ý được; cho phép
        // negotiate kèm ?access_token=... (chỉ khi path là /hubs/*).
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("ManageAccounts", policy => policy.RequireRole(AccountRoles.Admin))
    // Guest is effective for every public account (Patient inherits it) and never for staff.
    .AddPolicy(PatientLinkEndpoints.PublicAccountPolicy, policy => policy.RequireRole(AccountRoles.Guest))
    // Admin, Dentist and Clinic Owner do not inherit link verification.
    .AddPolicy(PatientLinkEndpoints.VerifyPolicy, policy => policy.RequireRole(AccountRoles.ClinicAssistant))
    // S05/S06: Admin has no patient access (GBR-026); Clinic Owner reads through effective Dentist.
    .AddPolicy(PatientEndpoints.ReadPolicy, policy => policy.RequireRole(AccountRoles.ClinicAssistant, AccountRoles.Dentist))
    .AddPolicy(PatientEndpoints.ManagePolicy, policy => policy.RequireRole(AccountRoles.ClinicAssistant))
    .AddPolicy(TreatmentEndpoints.DentistPolicy, policy => policy.RequireRole(AccountRoles.Dentist));

var webOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("WebDevelopment", policy =>
    policy.WithOrigins(webOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// S26+ — SignalR + notification. Đăng ký trước Build() để hub có thể inject.
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddScoped<INotificationStore, EfNotificationStore>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddHostedService<AppointmentNotificationWorker>();

var app = builder.Build();

// StaffBranchAccessor: hub join group staff:branch:{id} cho staff user. Cấu hình SAU Build()
// vì cần app.Services.
StaffBranchAccessor.Configure((accountId, context) =>
    StaffBranchResolver.ResolveAsync(accountId, context, app.Services));

if (args.Contains("--migrate") || args.Contains("--seed-demo"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Development database commands require Development environment.");
    using var scope = app.Services.CreateScope();
    if (args.Contains("--migrate")) await scope.ServiceProvider.GetRequiredService<DcmsDbContext>().Database.MigrateAsync();
    if (args.Contains("--seed-demo"))
    {
        await scope.ServiceProvider.GetRequiredService<DevelopmentAccountSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentPatientLinkSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<DCMS.Infrastructure.Features.Scheduling.DevelopmentSchedulingSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<DCMS.Infrastructure.Features.Appointments.DevelopmentAppointmentSeeder>().SeedAsync();
    }
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseCors("WebDevelopment");
app.UseMiddleware<BusinessErrorMiddleware>();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthEndpoints();
app.MapAuthenticationEndpoints();
app.MapRecoveryEndpoints();
app.MapEmailVerificationEndpoints();
app.MapAccountEndpoints();
app.MapPatientLinkEndpoints();
app.MapPatientEndpoints();
app.MapTreatmentEndpoints();
DCMS.Api.Features.SystemSettings.SystemSettingsEndpoints.MapSystemSettingsEndpoints(app);
app.MapClinicalWorkEndpoints();
app.MapSchedulingEndpoints();
app.MapAppointmentEndpoints();
app.MapReceptionEndpoints();
DCMS.Api.Features.Examinations.ExaminationEndpoints.MapExaminationEndpoints(app);
app.MapAppointmentQueryEndpoints();
app.MapSupportRequestEndpoints();
app.MapNotificationEndpoints();
// S26+ SignalR: hub + test endpoint (chỉ Dev).
app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapRealtimeEndpoints(isDevelopment: app.Environment.IsDevelopment());
app.MapDevelopmentRecoveryEndpoints();

app.Run();

public partial class Program { }

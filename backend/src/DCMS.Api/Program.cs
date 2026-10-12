using DCMS.Api.Features.Health;
using DCMS.Infrastructure;
using System.Security.Claims;
using System.Text;
using DCMS.Api.Common;
using DCMS.Api.Features.Accounts;
using DCMS.Api.Features.Authentication;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
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
        }
    };
});
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("ManageAccounts", policy => policy.RequireRole(AccountRoles.Admin));

var webOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("WebDevelopment", policy =>
    policy.WithOrigins(webOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (args.Contains("--migrate") || args.Contains("--seed-demo"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Development database commands require Development environment.");
    using var scope = app.Services.CreateScope();
    if (args.Contains("--migrate")) await scope.ServiceProvider.GetRequiredService<DcmsDbContext>().Database.MigrateAsync();
    if (args.Contains("--seed-demo")) await scope.ServiceProvider.GetRequiredService<DevelopmentAccountSeeder>().SeedAsync();
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
app.MapAccountEndpoints();
app.MapDevelopmentRecoveryEndpoints();

app.Run();

public partial class Program { }

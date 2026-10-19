using DCMS.Application.Common;
using DCMS.Application.Features.Accounts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DCMS.Api.Common;

public sealed class BusinessErrorMiddleware(RequestDelegate next, ILogger<BusinessErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            PostgresException? postgres = null;
            for (Exception? cause = ex; cause != null; cause = cause.InnerException)
            {
                if (cause is PostgresException databaseError) { postgres = databaseError; break; }
            }
            var (status, response) = ex switch
            {
                BusinessException business => (business.Status, business.Response),
                BadHttpRequestException => (400, SystemMessages.Get("MSG04", "Yêu cầu")),
                DbUpdateConcurrencyException => (409, SystemMessages.Get("MSG111")),
                _ when (context.Request.Path.StartsWithSegments("/api/dossiers")
                        || context.Request.Path.Value?.Contains("/dossiers", StringComparison.Ordinal) == true)
                    && postgres?.SqlState is PostgresErrorCodes.UniqueViolation
                        or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected
                    => (409, SystemMessages.Get("MSG111")),
                _ when context.Request.Path.StartsWithSegments("/api/accounts")
                    && postgres?.SqlState == PostgresErrorCodes.UniqueViolation => (409, StaffAccountConflicts.ForConstraint(postgres.ConstraintName)),
                _ when (context.Request.Path.StartsWithSegments("/api/accounts")
                        || context.Request.Path.StartsWithSegments("/api/auth/register")
                        || context.Request.Path.StartsWithSegments("/api/auth/google"))
                    && postgres?.SqlState == PostgresErrorCodes.UniqueViolation => (409, SystemMessages.Get("MSG139")),
                _ when (context.Request.Path.StartsWithSegments("/api/accounts")
                        || context.Request.Path.StartsWithSegments("/api/auth/google"))
                    && postgres?.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected => (409, SystemMessages.Get("MSG111")),
                _ => (503, SystemMessages.Get("MSG144"))
            };
            if (status == 503) logger.LogError(ex, "Operation failed ({ExceptionType}); trace {TraceId}", ex.GetType().Name, context.TraceIdentifier);
            context.Response.StatusCode = status;
            if (status == 401) context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(response, context.RequestAborted);
        }
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace DCMS.Infrastructure.Features.PatientLinks;

internal static class PatientLinkSupport
{
    // Clinic dates are interpreted in Asia/Ho_Chi_Minh (UTC+7, no daylight saving).
    public static readonly TimeSpan ClinicOffset = TimeSpan.FromHours(7);

    public static DateOnly ClinicToday() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(ClinicOffset).DateTime);
    public static DateTimeOffset ClinicStartOfDay(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), ClinicOffset);

    public static string? Required(string? value, string field, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new BusinessException(400, SystemMessages.Get("MSG02", field));
        return Optional(value, field, max);
    }

    public static string? Optional(string? value, string field, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > max) throw new BusinessException(400, SystemMessages.Get("MSG03", field, max));
        return trimmed;
    }

    public static void Invalid(string field) => throw new BusinessException(400, SystemMessages.Get("MSG04", field));
    public static BusinessException NotFound() => new(404, SystemMessages.Get("MSG01"));
    public static BusinessException Stale() => new(409, SystemMessages.Get("MSG111"));

    public static string NewRequestCode(DateTimeOffset now) =>
        $"LK-{now.ToOffset(ClinicOffset):yyMMdd}-{RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 6)}";

    // Name comparison ignores case, Vietnamese diacritics and repeated whitespace [PROPOSED].
    public static string NormalizeName(string value)
    {
        var decomposed = value.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(char.ToUpperInvariant(c));
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static SystemMessage? PublicResult(string status) => status switch
    {
        PatientLinkStatus.Verified => SystemMessages.Get("MSG19"),
        PatientLinkStatus.Rejected or PatientLinkStatus.Revoked => SystemMessages.Get("MSG20"),
        _ => null
    };

    public static void AddAudit(DcmsDbContext db, PatientAccountLink link, int actor, string action, string? reason,
        object? before, DateTimeOffset at) =>
        db.PatientLinkAudits.Add(new PatientLinkAudit
        {
            Link = link, ActorAccountId = actor, Action = action, Reason = reason, OccurredAt = at,
            BeforeJson = JsonSerializer.Serialize(before), AfterJson = JsonSerializer.Serialize(Snapshot(link))
        });

    public static object Snapshot(PatientAccountLink x) => new
    {
        x.Id, x.RequestCode, x.AccountId, x.PatientId, x.Status, x.DeclaredFullName, x.DeclaredBirthDate,
        x.DeclaredPatientCode, x.RequestNote, x.ReviewingEmployeeId, x.VerificationNote, x.DecisionReason,
        x.PublicNote, x.ReviewedAt, x.RevokingEmployeeId, x.RevokedAt, x.WithdrawnAt, x.Version
    };

    // Saves and commits; database-level uniqueness and serialization conflicts become specified messages.
    public static async Task CommitAsync(DcmsDbContext db, IDbContextTransaction transaction, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (Postgres(ex) is { } error)
        {
            if (error.SqlState == PostgresErrorCodes.UniqueViolation)
                throw new BusinessException(409, SystemMessages.Get(error.ConstraintName?.EndsWith("_Verified", StringComparison.Ordinal) == true ? "MSG142" : "MSG111"));
            if (error.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected) throw Stale();
            throw;
        }
    }

    public static async Task<T> GuardAsync<T>(Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (Exception ex) when (Postgres(ex)?.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            throw Stale();
        }
    }

    private static PostgresException? Postgres(Exception ex)
    {
        for (Exception? cause = ex; cause != null; cause = cause.InnerException)
            if (cause is PostgresException error) return error;
        return null;
    }
}

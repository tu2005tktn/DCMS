using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Authentication;
using DCMS.Domain.Features.Settings;
using DCMS.Application.Features.Settings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Authentication;

public sealed class StaffIdleService(DcmsDbContext db, TimeProvider clock) : IStaffIdleService
{
    public static readonly int[] AllowedMinutes = [5, 10, 20, 30, 60, 90, 120];
    private static BusinessException Error(int status, string code) => new(status, SystemMessages.Get(code));
    public static bool Allowed(int minutes) => AllowedMinutes.Contains(minutes);
    public async Task<int> MinutesAsync(CancellationToken ct)
    {
        var json = await db.Set<SettingsVersion>().AsNoTracking().OrderByDescending(x => x.Version)
            .Select(x => x.ContentJson).FirstOrDefaultAsync(ct);
        return json == null ? GeneralSettings.Default.StaffIdleTimeoutMinutes
            : JsonSerializer.Deserialize<GeneralSettings>(json, DCMS.Infrastructure.Features.Settings.SettingsService.Json)!.StaffIdleTimeoutMinutes;
    }

    public async Task<StaffIdlePreviewDto> PreviewAsync(StaffIdlePreviewRequest request, CancellationToken ct)
    {
        if (!Allowed(request.StaffIdleTimeoutMinutes)) throw Error(400, "MSG04");
        var now = clock.GetUtcNow();
        var current = await MinutesAsync(ct);
        var affected = await db.AuthSessions.AsNoTracking().CountAsync(x => x.ClientKind == "StaffWeb"
            && x.RevokedAt == null && x.LastInteractiveActivityAt.AddMinutes(current) > now
            && x.LastInteractiveActivityAt.AddMinutes(request.StaffIdleTimeoutMinutes) <= now, ct);
        return new(request.StaffIdleTimeoutMinutes, affected, now);
    }

    public async Task<StaffIdleStatusDto> StatusAsync(int actorId, Guid sessionId, CancellationToken ct) =>
        await ReadOrTouchAsync(actorId, sessionId, false, ct);
    public async Task<StaffIdleStatusDto> ActivityAsync(int actorId, Guid sessionId, CancellationToken ct) =>
        await ReadOrTouchAsync(actorId, sessionId, true, ct);

    private async Task<StaffIdleStatusDto> ReadOrTouchAsync(int actorId, Guid sessionId, bool touch, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var session = await db.AuthSessions.FromSqlInterpolated(
            $"SELECT * FROM \"AUTH_SESSION\" WHERE \"Id\" = {sessionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (session == null || session.AccountId != actorId || session.ClientKind != "StaffWeb"
            || session.RevokedAt != null) throw Error(403, "MSG06");
        var now = clock.GetUtcNow(); var minutes = await MinutesAsync(ct);
        var deadline = session.LastInteractiveActivityAt.AddMinutes(minutes);
        if (deadline <= now)
        {
            await ExpireAsync(session, now, ct); await tx.CommitAsync(ct);
            throw Error(401, "MSG07");
        }
        if (touch && now - session.LastInteractiveActivityAt >= TimeSpan.FromSeconds(30))
        {
            session.LastInteractiveActivityAt = now;
            await db.SaveChangesAsync(ct);
            deadline = now.AddMinutes(minutes);
        }
        await tx.CommitAsync(ct);
        return new(minutes, session.LastInteractiveActivityAt, deadline, now);
    }

    public async Task ExpireAsync(AuthSession session, DateTimeOffset now, CancellationToken ct)
    {
        session.RevokedAt = now;
        await db.AuthRefreshTokens.Where(x => x.SessionId == session.Id && x.Status == "ACTIVE")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "REVOKED").SetProperty(x => x.RevokedAt, now), ct);
        await db.SaveChangesAsync(ct);
    }
}

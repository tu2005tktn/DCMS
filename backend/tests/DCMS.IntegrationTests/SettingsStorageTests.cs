using System.Text.Json;
using DCMS.Domain.Features.Settings;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

// docs/specs/SETTINGS-CATALOG-STORAGE.vi.md: catalog changes live in COMMAND_RECEIPT; the two retired /api/settings tables are archived.
public sealed class SettingsStorageTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Previous = "20261113072340_RemoveFinanceStorage";

    private static DateTimeOffset Now() =>
        new(DateTimeOffset.UtcNow.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, TimeSpan.Zero);

    [Fact]
    public async Task CatalogChangesAndLegacySettingsTablesArePreservedAcrossRollback()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var actor = await db.Users.Where(x => x.UserName == "owner.demo").Select(x => x.Id).SingleAsync();
        var change = new SystemCatalogChange
        {
            RequestKey = Guid.NewGuid(), ActorId = actor, Command = "SYNTHETIC:storage-check", ResourceId = Guid.NewGuid(),
            Fingerprint = new string('C', 64), BeforeJson = "{\"name\":\"before\"}", ResultJson = "{\"name\":\"after\"}", OccurredAt = Now()
        };
        db.SystemCatalogChanges.Add(change);
        await db.SaveChangesAsync();
        var archived = (await db.Set<SettingsLegacyArchive>().AsNoTracking().ToListAsync())
            .ToDictionary(x => JsonDocument.Parse(x.BeforeJson).RootElement.GetProperty("table").GetString()!,
                x => JsonDocument.Parse(x.BeforeJson).RootElement.GetProperty("rows").GetArrayLength());
        Assert.Equal(["OPERATIONAL_REFERENCE_ENTRY", "SETTINGS_VERSION"], archived.Keys.Order().ToArray());
        var migrator = db.GetService<IMigrator>();
        db.ChangeTracker.Clear();

        await migrator.MigrateAsync(Previous);
        Assert.Equal(1, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"SYSTEM_CATALOG_CHANGE\" WHERE \"RequestKey\"={change.RequestKey} AND \"ActorId\"={actor} AND \"Command\"='SYNTHETIC:storage-check' AND \"BeforeJson\"->>'name'='before'"));
        Assert.Equal(archived["OPERATIONAL_REFERENCE_ENTRY"], await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"OPERATIONAL_REFERENCE_ENTRY\""));
        Assert.Equal(archived["SETTINGS_VERSION"], await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"SETTINGS_VERSION\""));
        Assert.Equal(0, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"AUDIT_LOG\" WHERE \"AuditType\"='SettingsLegacyArchive'"));

        await db.Database.MigrateAsync();
        Assert.Equal(0, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM information_schema.tables WHERE table_schema=current_schema() AND table_name IN ('SYSTEM_CATALOG_CHANGE','SETTINGS_VERSION','OPERATIONAL_REFERENCE_ENTRY')"));
        var restored = await db.SystemCatalogChanges.AsNoTracking().SingleAsync(x => x.RequestKey == change.RequestKey);
        Assert.Equal((change.ActorId, change.Command, change.ResourceId, change.Fingerprint, change.OccurredAt),
            (restored.ActorId, restored.Command, restored.ResourceId, restored.Fingerprint, restored.OccurredAt));
        Assert.Equal("before", JsonDocument.Parse(restored.BeforeJson!).RootElement.GetProperty("name").GetString());
        Assert.Equal("after", JsonDocument.Parse(restored.ResultJson).RootElement.GetProperty("name").GetString());
        Assert.Equal(archived, (await db.Set<SettingsLegacyArchive>().AsNoTracking().ToListAsync())
            .ToDictionary(x => JsonDocument.Parse(x.BeforeJson).RootElement.GetProperty("table").GetString()!,
                x => JsonDocument.Parse(x.BeforeJson).RootElement.GetProperty("rows").GetArrayLength()));
    }

    [Fact]
    public async Task ReplayingACatalogRequestKeyIsRejectedByTheSharedReceiptStore()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var actor = await db.Users.Where(x => x.UserName == "owner.demo").Select(x => x.Id).SingleAsync();
        var key = Guid.NewGuid();
        SystemCatalogChange Change() => new() { RequestKey = key, ActorId = actor, Command = "SYNTHETIC:replay", ResourceId = Guid.NewGuid(),
            Fingerprint = new string('D', 64), ResultJson = "{}", OccurredAt = Now() };
        db.SystemCatalogChanges.Add(Change());
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        db.SystemCatalogChanges.Add(Change());
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.SystemCatalogChanges.CountAsync(x => x.RequestKey == key));
    }

    private static Task<int> Count(DcmsDbContext db, FormattableString sql) => db.Database.SqlQuery<int>(sql).SingleAsync();
}

using DCMS.Domain.Features.Appointments;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

// docs/specs/NOTIFICATION-STORAGE.vi.md: deliveries and staff handling items are row kinds of IN_APP_NOTIFICATION.
public sealed class NotificationStorageTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Previous = "20261113081111_ConsolidateSettingsStorage";

    private static DateTimeOffset Now() =>
        new(DateTimeOffset.UtcNow.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, TimeSpan.Zero);

    private static (InAppNotification Notification, StaffHandlingItem Item) NewRows(int accountId)
    {
        var now = Now();
        var notification = new InAppNotification
        {
            PublicId = "NTF-" + Guid.NewGuid().ToString("N")[..20], AccountId = accountId, BranchId = 1, Kind = "AppointmentBooked",
            Title = "SYNTHETIC title", Body = "SYNTHETIC body", CreatedAt = now, HasExternalFailure = true, DedupeKey = "storage:" + Guid.NewGuid()
        };
        notification.Deliveries.Add(new NotificationDelivery { NotificationId = notification.Id, Channel = "InApp", Status = "Delivered", AttemptCount = 1, LastAttemptAt = now });
        notification.Deliveries.Add(new NotificationDelivery
        {
            NotificationId = notification.Id, Channel = "Email", Status = "Failed", AttemptCount = 3, LastAttemptAt = now,
            NextRetryAt = now.AddMinutes(10), LastError = "SYNTHETIC smtp error"
        });
        var item = new StaffHandlingItem
        {
            Kind = "ExternalDeliveryFailed", BranchId = 1, NotificationId = notification.Id, AttemptCount = 3,
            Error = "SYNTHETIC smtp error", Status = "Open", CreatedAt = now
        };
        return (notification, item);
    }

    private static object Snapshot(NotificationDelivery x) => (x.Id, x.NotificationId, x.Channel, x.Status, x.AttemptCount, x.LastAttemptAt, x.NextRetryAt, x.LastError);
    private static object Snapshot(StaffHandlingItem x) => (x.Id, x.Kind, x.BranchId, x.AppointmentId, x.NotificationId, x.AttemptCount, x.Error, x.Status, x.CreatedAt);
    private static object Snapshot(InAppNotification x) => (x.Id, x.PublicId, x.AccountId, x.BranchId, x.Kind, x.Title, x.Body, x.CreatedAt, x.ReadAt, x.HasExternalFailure, x.DedupeKey);

    [Fact]
    public async Task DeliveriesAndStaffHandlingItemsArePreservedAcrossRollback()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var account = await db.Users.Where(x => x.UserName == "guest.demo").Select(x => x.Id).SingleAsync();
        var (notification, item) = NewRows(account);
        db.Add(notification); db.Add(item);
        await db.SaveChangesAsync();
        var expectedNotification = Snapshot(notification);
        var expectedDeliveries = notification.Deliveries.OrderBy(x => x.Channel).Select(Snapshot).ToArray();
        var expectedItem = Snapshot(item);
        var migrator = db.GetService<IMigrator>();
        db.ChangeTracker.Clear();

        await migrator.MigrateAsync(Previous);
        Assert.Equal(2, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"NOTIFICATION_DELIVERY\" WHERE \"NotificationId\"={notification.Id}"));
        Assert.Equal(1, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"STAFF_HANDLING_ITEM\" WHERE \"Id\"={item.Id} AND \"NotificationId\"={notification.Id} AND \"Status\"='Open'"));
        Assert.Equal(1, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"IN_APP_NOTIFICATION\" WHERE \"Id\"={notification.Id}"));

        await db.Database.MigrateAsync();
        Assert.Equal(0, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM information_schema.tables WHERE table_schema=current_schema() AND table_name IN ('NOTIFICATION_DELIVERY','STAFF_HANDLING_ITEM')"));
        var restored = await db.InAppNotifications.AsNoTracking().Include(x => x.Deliveries).SingleAsync(x => x.Id == notification.Id);
        Assert.Equal(expectedNotification, Snapshot(restored));
        Assert.Equal(expectedDeliveries, restored.Deliveries.OrderBy(x => x.Channel).Select(Snapshot).ToArray());
        Assert.Equal(expectedItem, Snapshot(await db.StaffHandlingItems.AsNoTracking().SingleAsync(x => x.Id == item.Id)));
        // Each row kind still reads only its own rows.
        Assert.False(await db.InAppNotifications.AnyAsync(x => x.Id == item.Id || restored.Deliveries.Select(d => d.Id).Contains(x.Id)));
        Assert.Equal(1, await db.NotificationDeliveries.CountAsync(x => x.NotificationId == notification.Id && x.Status == "Failed" && x.NextRetryAt != null));
    }

    [Fact]
    public async Task ASecondDeliveryForTheSameChannelIsRejected()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var account = await db.Users.Where(x => x.UserName == "guest.demo").Select(x => x.Id).SingleAsync();
        var (notification, _) = NewRows(account);
        db.Add(notification);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        db.NotificationDeliveries.Add(new NotificationDelivery { NotificationId = notification.Id, Channel = "Email", Status = "Pending" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Equal(2, await db.NotificationDeliveries.CountAsync(x => x.NotificationId == notification.Id));
    }

    private static Task<int> Count(DcmsDbContext db, FormattableString sql) => db.Database.SqlQuery<int>(sql).SingleAsync();
}

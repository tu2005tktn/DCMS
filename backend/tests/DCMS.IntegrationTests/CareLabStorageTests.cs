using System.Text.Json;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

// docs/specs/CARE-LAB-FINANCE-STORAGE.vi.md: care attempts, escalations and lab status history live in JSONB on their parent row.
public sealed class CareLabStorageTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Previous = "20261113013443_ConsolidateTreatmentStorage";

    // PostgreSQL keeps microseconds, so synthetic timestamps stay on a millisecond boundary.
    private static DateTimeOffset Now() =>
        new(DateTimeOffset.UtcNow.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, TimeSpan.Zero);

    private static (CareTask Task, ClinicalActivity Activity, LabOrder Order) NewRows(DateTimeOffset now)
    {
        var task = new CareTask
        {
            PatientId = 1, BranchId = 1, EpisodeKey = "care-lab-storage:" + Guid.NewGuid(), Type = "FollowUp", Content = "SYNTHETIC care",
            DueAt = now, Status = "Open", Version = 3, CreatedBy = 1, CreatedAt = now, UpdatedAt = now
        };
        task.ContactAttempts.Add(new CareContactAttempt { TaskId = task.Id, Channel = "Phone", Result = "NoAnswer", NextAction = "SYNTHETIC retry", ActorId = 1, OccurredAt = now.AddMinutes(-10) });
        task.ContactAttempts.Add(new CareContactAttempt { TaskId = task.Id, Channel = "Phone", Result = "Rebooked", RebookedAppointmentId = Guid.NewGuid(), ActorId = 2, OccurredAt = now });
        task.Escalations.Add(new CareEscalation
        {
            TaskId = task.Id, TargetClinicianAccountId = 3, Reason = "SYNTHETIC escalation", Resolution = "SYNTHETIC resolved", Status = "Resolved",
            CreatedBy = 1, CreatedAt = now, Version = 3, AcknowledgedAt = now.AddMinutes(1), ResolvedAt = now.AddMinutes(2)
        });
        var activity = new ClinicalActivity
        {
            SourceKind = "Dossier", PatientId = 1, BranchId = 1, DossierId = Guid.NewGuid(), ServiceId = 1, Unit = "răng", Quantity = 1,
            PerformedAt = now, PerformerId = 1, Outcome = "SYNTHETIC outcome", ChargingBasis = "Included", PriceSnapshotVnd = 0, State = "Posted", CreatedAt = now
        };
        var order = new LabOrder
        {
            ActivityId = activity.Id, DossierId = activity.DossierId!.Value, PatientId = 1, BranchId = 1, OrderedOn = new DateOnly(2026, 11, 13),
            Status = LabOrderStatus.Received, DentistId = 1, WorkName = "SYNTHETIC crown", Teeth = ["36"], Shade = new() { ["1"] = "A2" },
            Quantity = 1, TotalFeeVnd = 500000, PaidVnd = 200000, CreatedAt = now, CreatedBy = 1, UpdatedAt = now, UpdatedBy = 1
        };
        order.StatusLogs.Add(new LabOrderStatusLog { LabOrderId = order.Id, ToStatus = LabOrderStatus.NotSent, ChangedAt = now.AddMinutes(-2), ChangedBy = 1 });
        order.StatusLogs.Add(new LabOrderStatusLog { LabOrderId = order.Id, FromStatus = LabOrderStatus.NotSent, ToStatus = LabOrderStatus.Sent, ChangedAt = now.AddMinutes(-1), ChangedBy = 1, Note = "SYNTHETIC sent" });
        order.StatusLogs.Add(new LabOrderStatusLog { LabOrderId = order.Id, FromStatus = LabOrderStatus.Sent, ToStatus = LabOrderStatus.Received, ChangedAt = now, ChangedBy = 2 });
        return (task, activity, order);
    }

    [Fact]
    public async Task CareAttemptsEscalationsAndLabHistoryArePreservedAcrossRollback()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var (task, activity, order) = NewRows(Now());
        db.AddRange(task, activity, order);
        await db.SaveChangesAsync();
        var contacts = JsonSerializer.Serialize(task.ContactAttempts.OrderBy(x => x.Id));
        var escalations = JsonSerializer.Serialize(task.Escalations.OrderBy(x => x.Id));
        var logs = JsonSerializer.Serialize(order.StatusLogs.OrderBy(x => x.Id));
        var migrator = db.GetService<IMigrator>();
        db.ChangeTracker.Clear();

        await migrator.MigrateAsync(Previous);
        Assert.Equal(2, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"CARE_CONTACT_ATTEMPT\" WHERE \"TaskId\"={task.Id}"));
        Assert.Equal(1, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"CARE_ESCALATION\" WHERE \"TaskId\"={task.Id} AND \"Status\"='Resolved'"));
        Assert.Equal(3, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"LAB_ORDER_STATUS_LOG\" WHERE \"LabOrderId\"={order.Id}"));
        // The finance tables come back empty: their data is only kept in the backup taken before the removal.
        Assert.Equal(0, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"FINANCE_CHARGE\""));

        await db.Database.MigrateAsync();
        Assert.Equal(0, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM information_schema.tables WHERE table_schema=current_schema() AND table_name IN ('CARE_CONTACT_ATTEMPT','CARE_ESCALATION','LAB_ORDER_STATUS_LOG','FINANCE_CHARGE','PAYMENT_RECEIPT','TREATMENT_PAYOS_ORDER')"));
        var restoredTask = await db.CareTasks.AsNoTracking().SingleAsync(x => x.Id == task.Id);
        Assert.Equal(contacts, JsonSerializer.Serialize(restoredTask.ContactAttempts.OrderBy(x => x.Id)));
        Assert.Equal(escalations, JsonSerializer.Serialize(restoredTask.Escalations.OrderBy(x => x.Id)));
        var restoredOrder = await db.LabOrders.AsNoTracking().SingleAsync(x => x.Id == order.Id);
        Assert.Equal(logs, JsonSerializer.Serialize(restoredOrder.StatusLogs.OrderBy(x => x.Id)));
        // The read projections the services use still reach the JSONB elements.
        Assert.Equal(2, await db.CareContactAttempts.CountAsync(x => x.TaskId == task.Id));
        Assert.True(await db.CareEscalations.AnyAsync(x => x.TaskId == task.Id && x.Status == "Resolved"));
        Assert.Equal(3, await db.LabOrderStatusLogs.CountAsync(x => x.LabOrderId == order.Id));
    }

    [Fact]
    public async Task StaleCareTaskAndLabOrderCannotOverwriteAConcurrentCollectionChange()
    {
        var (task, activity, order) = NewRows(Now());
        using (var seed = fixture.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DcmsDbContext>();
            db.AddRange(task, activity, order);
            await db.SaveChangesAsync();
        }
        using var firstScope = fixture.Services.CreateScope();
        using var secondScope = fixture.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var staleTask = await second.CareTasks.SingleAsync(x => x.Id == task.Id);
        var staleOrder = await second.LabOrders.SingleAsync(x => x.Id == order.Id);

        var currentTask = await first.CareTasks.SingleAsync(x => x.Id == task.Id);
        currentTask.ContactAttempts.Add(new CareContactAttempt { TaskId = task.Id, Channel = "InPerson", Result = "Reached", ActorId = 1, OccurredAt = Now() });
        var currentOrder = await first.LabOrders.SingleAsync(x => x.Id == order.Id);
        currentOrder.StatusLogs.Add(new LabOrderStatusLog { LabOrderId = order.Id, FromStatus = LabOrderStatus.Received, ToStatus = LabOrderStatus.Delivered, ChangedAt = Now(), ChangedBy = 1 });
        await first.SaveChangesAsync();

        staleTask.Escalations.Add(new CareEscalation { TaskId = task.Id, TargetClinicianAccountId = 3, Reason = "SYNTHETIC stale", CreatedBy = 1, CreatedAt = Now() });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        second.ChangeTracker.Clear();
        staleOrder = await second.LabOrders.SingleAsync(x => x.Id == order.Id);
        second.Entry(staleOrder).Property(x => x.StorageStamp).OriginalValue = order.StorageStamp;
        staleOrder.Note = "SYNTHETIC stale note";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        second.ChangeTracker.Clear();

        var savedTask = await second.CareTasks.AsNoTracking().SingleAsync(x => x.Id == task.Id);
        Assert.Equal(3, savedTask.ContactAttempts.Count);
        Assert.Single(savedTask.Escalations);
        Assert.Equal(4, (await second.LabOrders.AsNoTracking().SingleAsync(x => x.Id == order.Id)).StatusLogs.Count);
    }

    private static Task<int> Count(DcmsDbContext db, FormattableString sql) => db.Database.SqlQuery<int>(sql).SingleAsync();
}

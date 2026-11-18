using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Scheduling;
using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class SchedulingStorageTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task BranchCommandsKeepSettingsVersionsAndEmbeddedResourcesTogether()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var actor = await db.Users.Where(x => x.UserName == "owner.demo").Select(x => x.Id).SingleAsync();
        var service = scope.ServiceProvider.GetRequiredService<IBranchManagementService>();
        var command = new SaveBranchCommand(Guid.NewGuid(), 0, "ST-" + Guid.NewGuid().ToString("N")[..12],
            "SYNTHETIC branch", "SYNTHETIC address", null, null);
        var created = await service.SaveBranchAsync(actor, null, command, default);
        var resource = new SchedulingChair { BranchId = created.Id, Name = "SYNTHETIC retained chair" };
        await db.AddSchedulingDataAsync(resource); await db.SaveChangesAsync();
        var afterChair = await service.BranchAsync(actor, created.Id, default);
        var changed = await service.SaveBranchAsync(actor, created.Id,
            command with { RequestKey = Guid.NewGuid(), ExpectedVersion = afterChair.Version, Name = "SYNTHETIC changed branch" }, default);
        Assert.Equal(afterChair.Version + 1, changed.Version);
        Assert.Equal(resource.Id, Assert.Single(changed.Chairs).Id);
        db.ChangeTracker.Clear();
        var parent = await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == created.Id);
        Assert.Equal("SYNTHETIC changed branch", parent.Name);
        Assert.Equal(resource.Id, Assert.Single(parent.Chairs).Id);
        Assert.Equal("SYNTHETIC address", parent.Address);
    }

    [Fact]
    public async Task ChairCommandsKeepVersionsReplayAndStateInsideTheBranch()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var actor = await db.Users.Where(x => x.UserName == "owner.demo").Select(x => x.Id).SingleAsync();
        var branch = new SchedulingBranch { Name = "SYNTHETIC chair commands" };
        db.Add(branch); await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IBranchManagementService>();
        var legacy = new SchedulingChair { BranchId = branch.Id, Name = "SYNTHETIC legacy chair", Version = 0 };
        await db.AddSchedulingDataAsync(legacy); await db.SaveChangesAsync();
        Assert.Equal(1, Assert.Single(await service.ChairsAsync(actor, branch.Id, default)).Version);
        var migrated = await service.SaveChairAsync(actor, branch.Id, legacy.Id,
            new(Guid.NewGuid(), 1, "LEGACY-01", "SYNTHETIC edited legacy", 0, 1440), default);
        Assert.Equal(2, migrated.Version);
        var command = new SaveChairCommand(Guid.NewGuid(), 0, "C-01", "SYNTHETIC original", 0, 1440);
        var created = await service.SaveChairAsync(actor, branch.Id, null, command, default);
        var replay = await service.SaveChairAsync(actor, branch.Id, null, command, default);
        Assert.Equal(created.Id, replay.Id);
        var updated = await service.SaveChairAsync(actor, branch.Id, created.Id,
            command with { RequestKey = Guid.NewGuid(), ExpectedVersion = created.Version, Name = "SYNTHETIC changed" }, default);
        Assert.Equal(created.Version + 1, updated.Version);
        var stale = await Assert.ThrowsAsync<BusinessException>(() => service.SaveChairAsync(actor, branch.Id, created.Id,
            command with { RequestKey = Guid.NewGuid(), ExpectedVersion = created.Version }, default));
        Assert.Equal(409, stale.Status);
        var deactivated = await service.SetChairStateAsync(actor, branch.Id, created.Id,
            new(Guid.NewGuid(), updated.Version, false, "SYNTHETIC maintenance"), default);
        Assert.False(deactivated.IsActive);
        db.ChangeTracker.Clear();
        var persisted = (await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == branch.Id)).Chairs.Single(x => x.Id == created.Id);
        Assert.Equal("SYNTHETIC changed", persisted.Name);
        Assert.Equal(deactivated.Version, persisted.Version);
        Assert.False(persisted.Active);
    }

    [Fact]
    public async Task RollbackPreservesBranchCollectionsAppointmentBookingInfoAndWorkflowData()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var dentist = await db.Employees.Join(db.Users, e => e.AccountId, u => u.Id, (e, u) => new { e, u.UserName })
            .Where(x => x.UserName == "dentist.demo").Select(x => x.e).SingleAsync();
        var branch = new SchedulingBranch { Name = "SYNTHETIC unified storage" };
        db.Add(branch); await db.SaveChangesAsync();
        var chair = new SchedulingChair { BranchId = branch.Id, Name = "SYNTHETIC chair", Code = "JSON-CHAIR", Version = 4 };
        await db.AddSchedulingDataAsync(chair, new SchedulingMember { BranchId = branch.Id, EmployeeId = dentist.Id, CanDentist = true });
        var service = new SchedulingService { BranchId = branch.Id, Name = "SYNTHETIC treatment", PublicDurationMinutes = 30 };
        db.Add(service); await db.SaveChangesAsync();
        var start = DateTimeOffset.UtcNow.AddDays(7);
        var appointment = new Appointment { BranchId = branch.Id, DentistId = dentist.Id,
            ChairId = chair.Id, ServiceId = service.Id, StartAt = start, EndAt = start.AddMinutes(30), Code = "SYNTHETIC-A",
            AttendeeName = "SYNTHETIC", CreatedAt = start.AddDays(-7), Version = 3, Status = "CheckedIn",
            BookingInfo = new AppointmentBookingInfo
            {
                BookerName = "SYNTHETIC Booker", BookerContact = "0912345678",
                AttendeeBirthDate = new DateOnly(1995, 4, 15), AttendeeRelation = "Relative",
                ExceptionReason = "SYNTHETIC exception"
            } };
        var request = new AppointmentSupportRequest { AppointmentId = appointment.Id, Code = "SYNTHETIC-SR", Version = 5,
            RequesterName = "SYNTHETIC", Kind = "General", RequestKey = Guid.NewGuid(), CreatedAt = start.AddDays(-7), UpdatedAt = start.AddDays(-7) };
        var reminder = new AppointmentReminder { AppointmentId = appointment.Id, AppointmentVersion = 3, AccountId = dentist.AccountId,
            LeadMinutes = 120, DueAt = start.AddMinutes(-120), CreatedAt = start.AddDays(-7) };
        appointment.SupportRequests.Add(request); appointment.Reminders.Add(reminder);
        var week = new WorkingWeek { BranchId = branch.Id, EmployeeId = dentist.Id, WeekStart = new(2026, 11, 9), Version = 4,
            PublishedBy = dentist.AccountId, PublishedAt = start, Periods = [new() { Day = 0, StartMinute = 480, EndMinute = 720 }] };
        var revision = new WorkingScheduleRevision { EmployeeId = dentist.Id, Date = new(2026, 11, 16), Version = 1,
            PublishedBy = dentist.AccountId, PublishedAt = start, Periods = [new() { BranchId = branch.Id, Day = 0, StartMinute = 480, EndMinute = 1020 }] };
        var history = new AppointmentSupportHistory { RequestId = request.Id, AppointmentId = appointment.Id,
            ActorAccountId = dentist.AccountId, Action = "Synthetic", OccurredAt = start };
        var outbox = new AppointmentOutboxEvent { AggregateId = appointment.Id, OccurredAt = start, PayloadJson = "{}" };
        db.AddRange(appointment, week, revision, history, outbox);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261112095901_ConsolidateAccountStorage");
        await migrator.MigrateAsync(); db.ChangeTracker.Clear();

        var read = await db.Appointments.SingleAsync(x => x.Id == appointment.Id);
        Assert.Equal(3, read.Version); Assert.Equal("CheckedIn", read.Status);
        Assert.Equal("SYNTHETIC Booker", read.BookingInfo.BookerName);
        Assert.Equal("0912345678", read.BookingInfo.BookerContact);
        Assert.Equal(new DateOnly(1995, 4, 15), read.BookingInfo.AttendeeBirthDate);
        Assert.Equal("Relative", read.BookingInfo.AttendeeRelation);
        Assert.Equal("SYNTHETIC exception", read.BookingInfo.ExceptionReason);
        Assert.Equal(request.Id, Assert.Single(read.SupportRequests).Id);
        Assert.Equal(5, read.SupportRequests[0].Version);
        Assert.Equal(reminder.Id, Assert.Single(read.Reminders).Id);
        var parent = await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == branch.Id);
        Assert.Equal(chair.Id, Assert.Single(parent.Chairs).Id); Assert.Equal(4, parent.Chairs[0].Version);
        Assert.Equal(dentist.Id, Assert.Single(parent.Members).EmployeeId);
        Assert.Equal(720, Assert.Single((await db.Set<WorkingWeek>().SingleAsync(x => x.Id == week.Id)).Periods).EndMinute);
        Assert.Equal(branch.Id, Assert.Single((await db.Set<WorkingScheduleRevision>().SingleAsync(x => x.Id == revision.Id)).Periods).BranchId);
        Assert.True(await db.AppointmentSupportHistories.AnyAsync(x => x.Id == history.Id));
        Assert.True(await db.AppointmentOutboxEvents.AnyAsync(x => x.Id == outbox.Id && x.Status == "Pending"));
    }

    [Fact]
    public async Task StaleBranchCollectionWriteCannotDiscardAnotherEditorsResources()
    {
        using var one = fixture.Services.CreateScope(); using var two = fixture.Services.CreateScope();
        var first = one.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var second = two.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var branch = new SchedulingBranch { Name = "SYNTHETIC concurrent branch" };
        first.Add(branch); await first.SaveChangesAsync();
        var old = await second.Set<SchedulingBranch>().SingleAsync(x => x.Id == branch.Id);
        branch.Chairs.Add(new() { Id = await first.NextChairIdAsync(default), BranchId = branch.Id, Name = "SYNTHETIC kept chair" });
        await first.SaveChangesAsync();
        var employeeId = await second.Employees.Select(x => x.Id).FirstAsync();
        old.Members.Add(new() { BranchId = old.Id, EmployeeId = employeeId });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        second.ChangeTracker.Clear();
        Assert.Single((await second.Set<SchedulingBranch>().SingleAsync(x => x.Id == branch.Id)).Chairs);
    }
}

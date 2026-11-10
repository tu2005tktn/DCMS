using DCMS.Application.Features.Notifications;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using DCMS.Infrastructure.Features.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class PatientNotificationTests(ReceptionFixture fixture) : IClassFixture<ReceptionFixture>
{
    [Fact]
    public async Task OfficialFollowUpAndPrescriptionNotifyVerifiedAccountOnceAndRevocationStopsAccess()
    {
        var reception = new ReceptionApiTests(fixture);
        var setup = await reception.Prepare();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var guest = await db.Users.SingleAsync(x => x.UserName == "guest.demo");
        var dentist = await db.Users.SingleAsync(x => x.UserName == "dentist.demo");
        var link = new PatientAccountLink
        {
            AccountId = guest.Id, PatientId = setup.PatientId, Status = PatientLinkStatus.Verified,
            RequestCode = "TEST-" + Guid.NewGuid().ToString("N")[..12],
            DeclaredFullName = "SYNTHETIC", RequestedAt = DateTimeOffset.UtcNow,
            ReviewedAt = DateTimeOffset.UtcNow
        };
        db.PatientAccountLinks.Add(link);
        await db.SaveChangesAsync();

        var bookingEvent = new AppointmentOutboxEvent
        {
            AggregateId = setup.AppointmentId, EventType = "appointment.booked",
            OccurredAt = DateTimeOffset.UtcNow, PayloadJson = "{}"
        };
        db.AppointmentOutboxEvents.Add(bookingEvent);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IAppointmentNotificationDispatcher>()
            .ProcessPendingAsync(default);
        Assert.Single(await db.InAppNotifications.Where(x => x.AccountId == guest.Id
            && x.DedupeKey == $"appointment-event:{bookingEvent.Id}:account:{guest.Id}").ToListAsync());

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var followUp = new FollowUpInstruction
        {
            PatientId = setup.PatientId, BranchId = setup.BranchId, Status = "NeedsBooking",
            EarliestOn = today, TargetOn = today, LatestOn = today.AddDays(7),
            DurationMinutes = 30,
            CreatedBy = dentist.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.FollowUpInstructions.Add(followUp);
        await db.SaveChangesAsync();
        Assert.Single(await db.InAppNotifications.Where(x => x.AccountId == guest.Id
            && x.DedupeKey.StartsWith($"patient:follow-up:{followUp.Id}:")).ToListAsync());
        await db.SaveChangesAsync();
        Assert.Single(await db.InAppNotifications.Where(x => x.AccountId == guest.Id
            && x.DedupeKey.StartsWith($"patient:follow-up:{followUp.Id}:")).ToListAsync());

        using var assistant = await reception.Login();
        var visit = await reception.Check(assistant, setup);
        var rx = new ExternalPrescription
        {
            VisitId = visit.VisitId, PatientId = setup.PatientId, BranchId = setup.BranchId,
            PrescribedByDoctorId = dentist.Id, CreatedBy = dentist.Id, UpdatedBy = dentist.Id,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Set<ExternalPrescription>().Add(rx);
        await db.SaveChangesAsync();
        Assert.Empty(await db.InAppNotifications.Where(x => x.DedupeKey.StartsWith($"patient:rx:{rx.Id}:")).ToListAsync());
        rx.Status = PrescriptionStatuses.Finalized;
        rx.FinalizedAt = DateTimeOffset.UtcNow;
        rx.FinalizedBy = dentist.Id;
        await db.SaveChangesAsync();
        Assert.Single(await db.InAppNotifications.Where(x => x.AccountId == guest.Id
            && x.Kind == nameof(NotificationKind.PrescriptionReady)
            && x.DedupeKey.StartsWith($"patient:rx:{rx.Id}:")).ToListAsync());

        var employeeId = await db.Employees.Where(x => x.AccountId == dentist.Id).Select(x => x.Id).SingleAsync();
        var profilePrescription = new PatientPrescription
        {
            PatientId = setup.PatientId, BranchId = setup.BranchId,
            PrescriberEmployeeId = employeeId, PrescribedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            Diagnosis = "Synthetic test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Set<PatientPrescription>().Add(profilePrescription);
        await db.SaveChangesAsync();
        Assert.Single(await db.InAppNotifications.Where(x => x.AccountId == guest.Id
            && x.DedupeKey.StartsWith($"patient:patient-rx:{profilePrescription.Id}:")).ToListAsync());
        await db.SaveChangesAsync();
        Assert.Single(await db.InAppNotifications.Where(x => x.AccountId == guest.Id
            && x.DedupeKey.StartsWith($"patient:patient-rx:{profilePrescription.Id}:")).ToListAsync());

        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        Assert.True((await notifications.CheckDeepLinkAsync(guest.Id, "/book-appointment", default)).Allowed);
        Assert.True((await notifications.CheckDeepLinkAsync(guest.Id, $"/prescriptions/{visit.VisitId}", default)).Allowed);
        Assert.True((await notifications.CheckDeepLinkAsync(guest.Id, $"/prescriptions/{profilePrescription.Id}", default)).Allowed);
        link.Status = PatientLinkStatus.Revoked;
        link.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        Assert.False((await notifications.CheckDeepLinkAsync(guest.Id, $"/prescriptions/{visit.VisitId}", default)).Allowed);
        Assert.False((await notifications.CheckDeepLinkAsync(guest.Id, "/book-appointment", default)).Allowed);

        var later = new FollowUpInstruction
        {
            PatientId = setup.PatientId, BranchId = setup.BranchId, Status = "NeedsBooking",
            EarliestOn = today, TargetOn = today, LatestOn = today.AddDays(7),
            DurationMinutes = 30,
            CreatedBy = dentist.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.FollowUpInstructions.Add(later);
        await db.SaveChangesAsync();
        Assert.Empty(await db.InAppNotifications.Where(x => x.DedupeKey.StartsWith($"patient:follow-up:{later.Id}:")).ToListAsync());
    }
}

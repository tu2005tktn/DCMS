using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class AppointmentModelTests
{
    [Fact]
    public void PublicLeadTimeIncludesExactSixtyMinuteBoundary()
    {
        var now = new DateTimeOffset(2030, 1, 1, 8, 0, 0, TimeSpan.FromHours(7));
        Assert.False(AppointmentRules.PublicLeadTimeValid(now, now.AddMinutes(59).AddSeconds(59)));
        Assert.True(AppointmentRules.PublicLeadTimeValid(now, now.AddMinutes(60)));
    }

    [Fact]
    public void ModelKeepsReservationAndBookingReceiptConstraints()
    {
        using var db = new DcmsDbContext(new DbContextOptionsBuilder<DcmsDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used;Username=none;Password=none").Options);
        var receipt = db.Model.FindEntityType(typeof(AppointmentReceipt))!;
        Assert.Contains(receipt.GetIndexes(), index => index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(["ReceiptType", nameof(AppointmentReceipt.RequestKey)]));
        var appointment = db.Model.FindEntityType(typeof(Appointment))!;
        Assert.Equal([nameof(Appointment.ReservationId)], appointment.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.Contains(appointment.GetKeys(), key => key.Properties.Select(x => x.Name).SequenceEqual([nameof(Appointment.Id)]));
        var service = db.Model.FindEntityType(typeof(ResourceReservation))!
            .FindProperty(nameof(ResourceReservation.ServiceId))!;
        Assert.True(service.IsNullable);
    }

    [Fact]
    public void SnapshotMatchesAppointmentModelWithoutDatabaseAccess()
    {
        using var db = new DcmsDbContext(new DbContextOptionsBuilder<DcmsDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used;Username=none;Password=none").Options);
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);
        var differ = db.GetService<IMigrationsModelDiffer>();
        var oldModel = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot!.Model, designTime: true);
        Assert.False(differ.HasDifferences(oldModel.GetRelationalModel(),
            db.GetService<IDesignTimeModel>().Model.GetRelationalModel()));
    }
}

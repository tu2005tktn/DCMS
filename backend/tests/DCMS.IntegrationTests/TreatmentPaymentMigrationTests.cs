using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class TreatmentPaymentMigrationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task UpgradePreservesOldPricesAndNeverPromotesAnOldDisplayFlagToClinicalCompletion()
    {
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var dentist = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "dentist.demo" select e).SingleAsync();
        var patient = new PatientProfile { PatientCode = "OLD-" + Guid.NewGuid().ToString("N"), FullName = "SYNTHETIC old", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var branch = new SchedulingBranch { Name = "SYNTHETIC migration" };
        db.AddRange(patient, branch); await db.SaveChangesAsync();
        var service = new SchedulingService { BranchId = branch.Id, Name = "SYNTHETIC legacy", Unit = "răng" };
        db.AddRange(service, new SchedulingMember { BranchId = branch.Id, EmployeeId = dentist.Id, CanDentist = true }); await db.SaveChangesAsync();
        var dossier = new TreatmentDossier { PatientId = patient.Id, BranchId = branch.Id, DentistId = dentist.Id,
            Code = "OLD-" + Guid.NewGuid().ToString("N"), StartedOn = new DateOnly(2026, 10, 26), Diagnosis = "SYNTHETIC old source",
            State = "Open", DisplayCompleted = true, Version = 4, PlanVersion = 2, PlanState = "Committed", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        dossier.Items.Add(new TreatmentItem { DossierId = dossier.Id, ServiceId = service.Id, ServiceName = "SYNTHETIC snapshot",
            Unit = "răng", Quantity = 2, Region = "25, 26", AgreedUnitPriceVnd = 350000, ReferenceUnitPriceVnd = 400000,
            Decision = "Agreed", DecisionPlanVersion = 2, Progress = "InProgress" });
        db.Add(dossier); await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261026233000_TreatmentSheetDrafts");
        await migrator.MigrateAsync();
        var read = await db.TreatmentDossiers.AsNoTracking().Include(x => x.Items).SingleAsync(x => x.Id == dossier.Id);
        Assert.True(read.DisplayCompleted); Assert.Equal("Open", read.State); Assert.Equal(4, read.Version); Assert.Equal(2, read.PlanVersion);
        var item = Assert.Single(read.Items); Assert.Equal(350000, item.AgreedUnitPriceVnd); Assert.Equal(400000, item.ReferenceUnitPriceVnd);
        Assert.False(item.DisplayCompleted); Assert.False(item.SheetDeleted); Assert.False(item.HiddenFromSheet); Assert.Null(item.EntrySource); Assert.Equal(2, item.Quantity);
        Assert.Equal("răng", item.Unit); Assert.Equal("InProgress", item.Progress); Assert.Null(read.SourceExaminationId);
        Assert.False(await db.ClinicalActivities.AnyAsync(x => x.DossierId == dossier.Id));
    }
}

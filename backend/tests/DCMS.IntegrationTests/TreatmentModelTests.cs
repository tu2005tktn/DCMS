using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class TreatmentModelTests
{
    [Fact]
    public void MigrationSnapshotMatchesCurrentModelWithoutConnectingToDatabase()
    {
        var options = new DbContextOptionsBuilder<DcmsDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=unused_snapshot_check;Username=unused;Password=unused")
            .Options;
        using var db = new DcmsDbContext(options);
        var snapshotModel = db.GetService<IModelRuntimeInitializer>().Initialize(
            db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        var snapshot = snapshotModel.GetRelationalModel();
        var current = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(snapshot, current);
        Assert.True(operations.Count == 0, string.Join("\n", operations.Select(operation =>
            $"{operation.GetType().Name}: {operation.GetType().GetProperty("Name")?.GetValue(operation)} " +
            $"{operation.GetType().GetProperty("Table")?.GetValue(operation)}")));
    }
}

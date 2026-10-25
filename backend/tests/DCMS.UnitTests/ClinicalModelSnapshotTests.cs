using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DCMS.UnitTests;

public sealed class ClinicalModelSnapshotTests
{
    [Fact]
    public void SnapshotMatchesCurrentModel()
    {
        using var db = new DcmsDbContext(new DbContextOptionsBuilder<DcmsDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used;Username=none;Password=none").Options);
        var snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(
            db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        var current = db.GetService<IDesignTimeModel>().Model;
        var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshot.GetRelationalModel(), current.GetRelationalModel());
        Assert.True(differences.Count == 0, string.Join("\n", differences.Select(x =>
            x.GetType().Name + " " + (x is ColumnOperation column
                ? column.Table + "." + column.Name + " " + column.ColumnType
                : x is TableOperation table ? table.Name : ""))));
    }
}

using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
        Assert.False(db.Database.HasPendingModelChanges());
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DCMS.Infrastructure.Persistence;

public sealed class DcmsDbContextFactory : IDesignTimeDbContextFactory<DcmsDbContext>
{
    public DcmsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("Set ConnectionStrings__DefaultConnection before running EF tools.");
        return new(new DbContextOptionsBuilder<DcmsDbContext>().UseNpgsql(connection).Options);
    }
}

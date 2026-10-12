using DCMS.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal sealed class DatabaseConnectionCheck(DcmsDbContext dbContext) : IDatabaseConnectionCheck
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.CanConnectAsync(cancellationToken);
}

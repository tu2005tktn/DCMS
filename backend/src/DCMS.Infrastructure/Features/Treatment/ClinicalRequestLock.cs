using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

// Serializes retries for one requestKey, including the receipt read and write.
internal sealed class ClinicalRequestLock(DcmsDbContext db, Guid key) : IAsyncDisposable
{
    public static async Task<ClinicalRequestLock> AcquireAsync(DcmsDbContext db, Guid key,
        CancellationToken ct)
    {
        if (key == Guid.Empty) throw new ArgumentException("Request key is required.", nameof(key));
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_lock(hashtext({key.ToString()}))", ct);
            return new ClinicalRequestLock(db, key);
        }
        catch
        {
            await db.Database.CloseConnectionAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_unlock(hashtext({key.ToString()}))", CancellationToken.None);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}

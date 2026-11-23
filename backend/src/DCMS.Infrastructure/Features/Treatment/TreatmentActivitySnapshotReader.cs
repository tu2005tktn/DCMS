using DCMS.Application.Features.Treatment;
using DCMS.Infrastructure.Persistence;

namespace DCMS.Infrastructure.Features.Treatment;

// The treatment sheet keeps corrections as events; consumers of prices must use the latest row.
public static class TreatmentActivitySnapshotReader
{
    public static Task<Dictionary<Guid, ActivityDto>> ReadOverridesAsync(
        DcmsDbContext db, Guid dossierId, CancellationToken ct)
        => TreatmentBookingQueries.ActivityOverridesAsync(db, dossierId, ct);
}

using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

internal static class TreatmentBookingQueries
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record SheetActivitySnapshot(Guid ActivityId, int SheetVersion, ActivityDto Activity);

    internal static async Task<Dictionary<Guid, ActivityDto>> ActivityOverridesAsync(DcmsDbContext db, Guid dossierId, CancellationToken ct)
    {
        var entries = await db.TreatmentEvents.AsNoTracking().Where(e => e.DossierId == dossierId &&
            (e.Kind == "SheetActivityEdited" || e.Kind == "SheetActivityDeleted")).OrderBy(e => e.DossierVersion).ToListAsync(ct);
        var result = new Dictionary<Guid, ActivityDto>();
        foreach (var entry in entries)
        {
            var snapshot = JsonSerializer.Deserialize<SheetActivitySnapshot>(entry.PayloadJson, Json)
                ?? throw new BusinessException(503, SystemMessages.Get("MSG144"));
            result[snapshot.ActivityId] = snapshot.Activity;
        }
        return result;
    }

    internal static async Task<HashSet<Guid>> PerformedItemIdsAsync(DcmsDbContext db, Guid dossierId, CancellationToken ct)
    {
        var deleted = (await ActivityOverridesAsync(db, dossierId, ct)).Where(x => x.Value.SheetDeleted).Select(x => x.Key).ToHashSet();
        return (await db.ClinicalActivities.AsNoTracking().Where(x => x.DossierId == dossierId && x.State == "Posted"
            && x.ItemId.HasValue && !deleted.Contains(x.Id)).Select(x => x.ItemId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
    }
}

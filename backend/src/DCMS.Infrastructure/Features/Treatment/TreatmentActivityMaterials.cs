using DCMS.Application.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed partial class TreatmentDossierService
{
    public async Task<IReadOnlyList<SheetActivityMaterial>> ActivityMaterialsAsync(int actorId, Guid dossierId, Guid activityId, CancellationToken ct)
    {
        await access.RequireAsync(actorId, "treatment.actuals.update", ct);
        await DossierAsync(actorId, dossierId, ct);
        if (!await db.ClinicalActivities.AnyAsync(x => x.Id == activityId && x.DossierId == dossierId, ct))
            throw Error("MSG113", 404);
        return await db.MaterialUsages.AsNoTracking().Where(x => x.ActivityId == activityId)
            .Select(x => new SheetActivityMaterial(x.MaterialId, x.Quantity)).ToListAsync(ct);
    }
}

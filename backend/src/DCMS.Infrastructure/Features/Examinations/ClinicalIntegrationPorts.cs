using DCMS.Application.Features.Reception;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.Examinations.ClinicalAccess;

namespace DCMS.Infrastructure.Features.Examinations;

public sealed class ClinicalVisitPort(DcmsDbContext db) : IClinicalVisitPort
{
    public async Task<ClinicalVisitState> GetAsync(Guid visitId, CancellationToken ct)
    {
        var v = await db.Visits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == visitId, ct) ?? throw Error("MSG113", 404);
        return new(true, VisitStatuses.Open.Contains(v.Status), v.PatientId, v.BranchId, v.Version);
    }
}

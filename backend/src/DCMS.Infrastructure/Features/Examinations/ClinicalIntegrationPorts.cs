using DCMS.Application.Features.Reception;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.Examinations.ClinicalAccess;

namespace DCMS.Infrastructure.Features.Examinations;

public sealed class ReceptionTreatmentSourceReader(DcmsDbContext db) : IReceptionSourceReader
{
    public async Task<IReadOnlyList<ReceptionCourse>> CoursesAsync(int branchId, int patientId, CancellationToken ct) =>
        await db.TreatmentDossiers.AsNoTracking().Where(x => x.PatientId == patientId && x.BranchId == branchId && x.State == "Open")
            .OrderByDescending(x => x.UpdatedAt).Select(x => new ReceptionCourse(x.Id, x.Code + " · " + x.Diagnosis, x.Version)).ToArrayAsync(ct);
    public async Task<VisitSource> ResolveAsync(int branchId, int patientId, Guid priorCourseId, CancellationToken ct)
    {
        var row = await db.TreatmentDossiers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == priorCourseId && x.PatientId == patientId && x.BranchId == branchId && x.State == "Open", ct) ?? throw Error("MSG113", 404);
        return new(PriorCourseId: row.Id, CourseVersion: row.Version, Label: row.Code + " · " + row.Diagnosis);
    }
}

public sealed class ClinicalVisitPort(DcmsDbContext db) : IClinicalVisitPort
{
    public async Task<ClinicalVisitState> GetAsync(Guid visitId, CancellationToken ct)
    {
        var v = await db.Visits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == visitId, ct) ?? throw Error("MSG113", 404);
        return new(true, v.Status == VisitStatuses.InProgress, v.PatientId, v.BranchId, v.Version);
    }
}

using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.Visits;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

public sealed partial class DcmsDbContext
{
    public IQueryable<InvestigationOrder> Investigations => Visits.AsNoTracking().SelectMany(x => x.Investigations);
    public IQueryable<DocumentReview> DocumentReviews => Set<ClinicalDocument>().AsNoTracking().SelectMany(x => x.Reviews);

    private async Task PrepareExaminationChangesAsync(CancellationToken ct)
    {
        var examinations = ChangeTracker.Entries<VisitExamination>().ToArray();
        foreach (var examination in examinations.Where(x => x.State is EntityState.Added or EntityState.Modified))
            _ = await Visits.FindAsync([examination.Entity.VisitId], ct)
                ?? throw new InvalidOperationException("The visit must exist before its examination is saved.");
        foreach (var visit in ChangeTracker.Entries<Visit>().ToArray())
        {
            var examination = examinations.SingleOrDefault(x => x.Entity.VisitId == visit.Entity.Id);
            var originalVersion = visit.State == EntityState.Added ? visit.Entity.Version : visit.Property(x => x.Version).OriginalValue;
            if (examination != null && examination.State != EntityState.Added
                && !Equals(examination.Property("Version").OriginalValue, originalVersion))
                throw new DbUpdateConcurrencyException("The visit changed while its examination was being edited.");
            // Any change to the row raises Version by exactly one, whether or not the service already did.
            if (visit.State == EntityState.Modified && visit.Entity.Version == originalVersion
                || visit.State == EntityState.Unchanged && examination?.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                visit.Entity.Version = originalVersion + 1;
            if (examination == null) continue;
            examination.Property("Version").CurrentValue = visit.Entity.Version;
            if (examination.State == EntityState.Added)
                examination.Property("Version").OriginalValue = originalVersion;
        }
    }
}

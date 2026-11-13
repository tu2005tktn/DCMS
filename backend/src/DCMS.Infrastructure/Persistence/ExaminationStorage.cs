using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.Visits;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

public sealed partial class DcmsDbContext
{
    public IQueryable<InvestigationOrder> Investigations => Visits.AsNoTracking().SelectMany(x => x.Investigations);
    public IQueryable<DocumentReview> DocumentReviews => Set<ClinicalDocument>().AsNoTracking().SelectMany(x => x.Reviews);

    public async Task<InvestigationOrder?> LoadInvestigationAsync(Guid visitId, Guid id, CancellationToken ct) =>
        (await Visits.SingleOrDefaultAsync(x => x.Id == visitId, ct))?.Investigations.SingleOrDefault(x => x.Id == id);

    public async Task<Visit?> ReloadVisitAsync(Guid id, CancellationToken ct)
    {
        var fresh = await Visits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (fresh == null) return null;
        var tracked = Visits.Local.SingleOrDefault(x => x.Id == id);
        if (tracked != null) Entry(tracked).State = EntityState.Detached;
        Attach(fresh);
        return fresh;
    }

    private async Task PrepareExaminationChangesAsync(CancellationToken ct)
    {
        var examinations = ChangeTracker.Entries<VisitExamination>().ToArray();
        foreach (var examination in examinations.Where(x => x.State is EntityState.Added or EntityState.Modified))
            _ = await Visits.FindAsync([examination.Entity.VisitId], ct)
                ?? throw new InvalidOperationException("The visit must exist before its examination is saved.");
        foreach (var visit in ChangeTracker.Entries<Visit>().ToArray())
        {
            var examination = examinations.SingleOrDefault(x => x.Entity.VisitId == visit.Entity.Id);
            if (examination != null && examination.State != EntityState.Added
                && !Equals(examination.Property("StorageStamp").OriginalValue, visit.Property(x => x.StorageStamp).OriginalValue))
                throw new DbUpdateConcurrencyException("The visit changed while its examination was being edited.");
            if (visit.State == EntityState.Modified || examination?.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                visit.Entity.StorageStamp = Guid.NewGuid();
            if (examination == null) continue;
            examination.Property("StorageStamp").CurrentValue = visit.Entity.StorageStamp;
            if (examination.State == EntityState.Added)
                examination.Property("StorageStamp").OriginalValue = visit.Property(x => x.StorageStamp).OriginalValue;
        }
    }
}

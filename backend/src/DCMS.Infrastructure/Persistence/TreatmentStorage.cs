using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

public sealed partial class DcmsDbContext
{
    public IQueryable<TreatmentTemplateItem> TreatmentTemplateItems => TreatmentTemplates.AsNoTracking().SelectMany(x => x.Items);

    public async Task<TreatmentTemplateItem?> LoadTemplateItemAsync(Guid id, CancellationToken ct = default) =>
        (await TreatmentTemplates.SingleOrDefaultAsync(x => x.Items.Any(item => item.Id == id), ct))?.Items.Single(x => x.Id == id);

    public async Task<TreatmentSheetDraft?> LoadSheetDraftAsync(IQueryable<TreatmentSheetDraft> query, CancellationToken ct)
    {
        var id = await query.Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (!id.HasValue) return null;
        return (await TreatmentDossiers.SingleAsync(x => x.SheetDrafts.Any(d => d.Id == id), ct)).SheetDrafts.Single(x => x.Id == id);
    }

    public Task AddTreatmentDataAsync(IEnumerable<object> rows) => AddTreatmentDataAsync(rows.ToArray());

    public async Task AddTreatmentDataAsync(params object[] rows)
    {
        foreach (var row in rows.Where(x => x is not (TreatmentSheetDraft or TreatmentTemplateItem or TreatmentTemplateVersion)))
            Add(row);
        foreach (var row in rows)
        {
            switch (row)
            {
                case TreatmentSheetDraft draft:
                    var sheet = await TreatmentDossiers.FindAsync(draft.DossierId)
                        ?? throw new InvalidOperationException("The treatment dossier must exist before its drafts are saved.");
                    if (!sheet.SheetDrafts.Contains(draft)) sheet.SheetDrafts.Add(draft);
                    break;
                case TreatmentTemplateItem templateItem:
                    var template = await TreatmentTemplates.FindAsync(templateItem.TemplateId)
                        ?? throw new InvalidOperationException("The treatment template must exist before its items are saved.");
                    if (!template.Items.Contains(templateItem)) template.Items.Add(templateItem);
                    break;
                case TreatmentTemplateVersion version:
                    var owner = await TreatmentTemplates.FindAsync(version.TemplateId)
                        ?? throw new InvalidOperationException("The treatment template must exist before its versions are saved.");
                    if (!owner.Versions.Contains(version)) owner.Versions.Add(version);
                    break;
            }
        }
    }

    public async Task RemoveTreatmentDataAsync(object row)
    {
        switch (row)
        {
            case TreatmentSheetDraft draft:
                var sheet = await TreatmentDossiers.FindAsync(draft.DossierId)
                    ?? throw new InvalidOperationException("The treatment dossier no longer exists.");
                sheet.SheetDrafts.RemoveAll(x => x.Id == draft.Id);
                break;
            default:
                Remove(row);
                break;
        }
    }

    private void PrepareTreatmentChanges()
    {
        // Every saved change to an activity moves Version on by exactly one (its concurrency token).
        foreach (var activity in ChangeTracker.Entries<ClinicalActivity>().Where(x => x.State == EntityState.Modified).ToArray())
            if (!activity.Property(x => x.Version).IsModified) activity.Entity.Version = activity.Property(x => x.Version).OriginalValue + 1;
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State == EntityState.Modified
            && x.Entity is TreatmentTemplate or CareTask or LabOrder).ToArray())
            entry.Property("StorageStamp").CurrentValue = Guid.NewGuid();
    }
}

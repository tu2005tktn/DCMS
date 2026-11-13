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

    public async Task<TreatmentRevisionDraft?> LoadRevisionDraftAsync(IQueryable<TreatmentRevisionDraft> query, CancellationToken ct)
    {
        var id = await query.Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (!id.HasValue) return null;
        return (await TreatmentDossiers.SingleAsync(x => x.RevisionDrafts.Any(d => d.Id == id), ct)).RevisionDrafts.Single(x => x.Id == id);
    }

    public async Task<List<TreatmentStep>> LoadTreatmentStepsAsync(IQueryable<TreatmentStep> query, CancellationToken ct)
    {
        var ids = await query.Select(x => x.Id).ToArrayAsync(ct);
        var owners = await TreatmentItems.Where(x => x.Steps.Any(step => ids.Contains(step.Id))).ToArrayAsync(ct);
        var steps = owners.SelectMany(x => x.Steps).ToDictionary(x => x.Id);
        return ids.Select(id => steps[id]).ToList();
    }

    public async Task<TreatmentStep?> LoadTreatmentStepAsync(IQueryable<TreatmentStep> query, CancellationToken ct) =>
        (await LoadTreatmentStepsAsync(query, ct)).SingleOrDefault();

    public Task AddTreatmentDataAsync(IEnumerable<object> rows) => AddTreatmentDataAsync(rows.ToArray());

    public async Task AddTreatmentDataAsync(params object[] rows)
    {
        foreach (var row in rows.Where(x => x is not (TreatmentStep or TreatmentSheetDraft or TreatmentRevisionDraft or TreatmentTemplateItem or TreatmentTemplateVersion)))
            Add(row);
        foreach (var row in rows)
        {
            switch (row)
            {
                case TreatmentStep step:
                    var item = await TreatmentItems.FindAsync(step.ItemId)
                        ?? throw new InvalidOperationException("The treatment item must exist before its steps are saved.");
                    if (!item.Steps.Contains(step)) item.Steps.Add(step);
                    break;
                case TreatmentSheetDraft draft:
                    var sheet = await TreatmentDossiers.FindAsync(draft.DossierId)
                        ?? throw new InvalidOperationException("The treatment dossier must exist before its drafts are saved.");
                    if (!sheet.SheetDrafts.Contains(draft)) sheet.SheetDrafts.Add(draft);
                    break;
                case TreatmentRevisionDraft revision:
                    var dossier = await TreatmentDossiers.FindAsync(revision.DossierId)
                        ?? throw new InvalidOperationException("The treatment dossier must exist before its revisions are saved.");
                    if (!dossier.RevisionDrafts.Contains(revision)) dossier.RevisionDrafts.Add(revision);
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
            case TreatmentRevisionDraft revision:
                var dossier = await TreatmentDossiers.FindAsync(revision.DossierId)
                    ?? throw new InvalidOperationException("The treatment dossier no longer exists.");
                dossier.RevisionDrafts.RemoveAll(x => x.Id == revision.Id);
                break;
            default:
                Remove(row);
                break;
        }
    }

    private void PrepareTreatmentChanges()
    {
        foreach (var activity in ChangeTracker.Entries<ClinicalActivity>().Where(x => x.State == EntityState.Added).ToArray())
            activity.Entity.StorageId = activity.Entity.Id;
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State == EntityState.Modified
            && x.Entity is TreatmentDossier or TreatmentItem or TreatmentTemplate or TreatmentProcessRecord).ToArray())
            entry.Property("StorageStamp").CurrentValue = Guid.NewGuid();
    }
}

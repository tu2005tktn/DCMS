using System.Text.Json;
using DCMS.Application.Features.Examinations;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.Examinations.ClinicalAccess;

namespace DCMS.Infrastructure.Features.Examinations;

// docs/specs/MOBILE-PATIENT-VISIT-RESULTS.vi.md §3: read-only official results for the caller's own verified patient record.
// Only confirmed examinations, non-cancelled investigations and Ready documents; staff-only notes, history facts, prices and
// providers are never projected. Another patient's ids answer 404 so their existence is not revealed.
public sealed class OwnClinicalRecordService(DcmsDbContext db, IClinicalDocumentService documents) : IOwnClinicalRecordService
{
    private static readonly string[] SaveActions = ["SaveExamination", "ConfirmExamination", "AmendExamination"];

    // Rechecked on every request so a revoked link stops the next read (GBR-075), including file content.
    private async Task<int> PatientAsync(int accountId, CancellationToken ct) =>
        await db.PatientAccountLinks.AsNoTracking()
            .Where(x => x.AccountId == accountId && x.Status == PatientLinkStatus.Verified && x.PatientId != null)
            .Select(x => x.PatientId).SingleOrDefaultAsync(ct) ?? throw Error("MSG06", 403);

    private static (int Page, int Size) Paging(int page, int pageSize)
    {
        if (page < 1 || page > 1_000_000 || pageSize < 1 || pageSize > 50) throw Error("MSG04");
        return (page, pageSize);
    }

    private IQueryable<Guid> VisitInvestigations(Guid visitId) =>
        db.Set<InvestigationOrder>().Where(o => o.VisitId == visitId).Select(o => o.Id);

    private IQueryable<ClinicalDocument> ReadyDocuments(int patientId) =>
        db.Set<ClinicalDocument>().AsNoTracking().Where(d => d.PatientId == patientId && d.FileState == "Ready");

    private IQueryable<OwnVisitRow> Visits(int patientId) =>
        from v in db.Visits.AsNoTracking()
        join e in db.Set<VisitExamination>() on v.Id equals e.VisitId
        join b in db.Set<SchedulingBranch>() on v.BranchId equals b.Id
        join d in db.Employees on v.DentistId equals d.Id
        where v.PatientId == patientId && e.Confirmed
        select new OwnVisitRow {
            Id = v.Id, ArrivalAt = v.ArrivalAt, BranchName = b.Name, DentistName = d.FullName, Status = v.Status, ContentJson = e.ContentJson,
            CompletedAt = v.Status == VisitStatuses.Completed
                ? db.Set<VisitAudit>().Where(a => a.VisitId == v.Id && a.Action == "CompleteVisit").Select(a => (DateTimeOffset?)a.OccurredAt).Min()
                : null,
            Version = db.Set<VisitAudit>().Count(a => a.VisitId == v.Id && SaveActions.Contains(a.Action)),
            DocumentCount = db.Set<ClinicalDocument>().Count(x => x.PatientId == patientId && x.FileState == "Ready"
                && (x.VisitId == v.Id || db.Set<InvestigationOrder>().Any(o => o.VisitId == v.Id && o.Id == x.InvestigationId))),
            HasPrescription = db.Set<ExternalPrescription>().Any(p => p.VisitId == v.Id && p.PatientId == patientId && p.Status == PrescriptionStatuses.Finalized) };

    // Member-init (not a positional record) so EF can order, filter and count on the projected columns.
    private sealed class OwnVisitRow
    {
        public Guid Id { get; init; }
        public DateTimeOffset ArrivalAt { get; init; }
        public string BranchName { get; init; } = "";
        public string DentistName { get; init; } = "";
        public string Status { get; init; } = "";
        public string ContentJson { get; init; } = "{}";
        public DateTimeOffset? CompletedAt { get; init; }
        public int Version { get; init; }
        public int DocumentCount { get; init; }
        public bool HasPrescription { get; init; }
    }

    private static ExaminationContent Content(string json) => JsonSerializer.Deserialize<ExaminationContent>(json, ExaminationService.Json)!;

    private static IReadOnlyList<string> Diagnoses(ExaminationContent c) =>
        c.Diagnoses is { Count: > 0 } list ? list : string.IsNullOrWhiteSpace(c.Diagnosis) ? [] : [c.Diagnosis.Trim()];

    private static OwnVisitItem Item(OwnVisitRow x)
    {
        var diagnoses = Diagnoses(Content(x.ContentJson));
        return new(x.Id, x.ArrivalAt, x.BranchName, x.DentistName, x.Status, x.CompletedAt, x.Version,
            diagnoses.Count == 0 ? null : string.Join("; ", diagnoses), x.DocumentCount, x.HasPrescription);
    }

    public async Task<OwnVisitPage> VisitsAsync(int accountId, int page, int pageSize, CancellationToken ct)
    {
        var (p, size) = Paging(page, pageSize);
        var patientId = await PatientAsync(accountId, ct);
        var query = Visits(patientId);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.ArrivalAt).ThenBy(x => x.Id).Skip((p - 1) * size).Take(size).ToArrayAsync(ct);
        return new(rows.Select(Item).ToArray(), total, p, size);
    }

    public async Task<OwnVisitDetail> VisitAsync(int accountId, Guid visitId, CancellationToken ct)
    {
        var patientId = await PatientAsync(accountId, ct);
        var row = await Visits(patientId).SingleOrDefaultAsync(x => x.Id == visitId, ct) ?? throw Error("MSG113", 404);
        var c = Content(row.ContentJson);
        var findings = c.Findings.Select(f => new OwnFinding(f.Tooth, f.Region, f.Condition, f.Surfaces ?? [], f.Diagnoses ?? [])).ToArray();
        var examination = new OwnExaminationResult(row.Version, c.Symptoms, findings, Diagnoses(c), c.Conclusion, c.Advice,
            c.Disposition, c.ReturnNeed, c.ReferralDestination);
        var orders = await db.Set<InvestigationOrder>().AsNoTracking().Where(o => o.VisitId == visitId && o.State != "Cancelled")
            .OrderBy(o => o.CreatedAt).ThenBy(o => o.Id).ToArrayAsync(ct);
        var investigations = orders.Select(o => o.State == "Reviewed"
            ? new OwnInvestigation(o.Id, o.Kind, o.Name, o.Region, o.State, o.ResultText, o.Interpretation, o.ReviewedAt)
            : new OwnInvestigation(o.Id, o.Kind, o.Name, o.Region, o.State, null, null, null)).ToArray();
        return new(Item(row), examination, investigations);
    }

    public async Task<OwnDocumentPage> DocumentsAsync(int accountId, Guid? visitId, int page, int pageSize, CancellationToken ct)
    {
        var (p, size) = Paging(page, pageSize);
        var patientId = await PatientAsync(accountId, ct);
        var query = ReadyDocuments(patientId);
        if (visitId.HasValue) {
            if (!await Visits(patientId).AnyAsync(x => x.Id == visitId, ct)) throw Error("MSG113", 404);
            var investigations = VisitInvestigations(visitId.Value);
            query = query.Where(d => d.VisitId == visitId || (d.InvestigationId != null && investigations.Contains(d.InvestigationId.Value)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(d => d.SourceDate).ThenByDescending(d => d.UploadedAt).ThenBy(d => d.Id)
            .Skip((p - 1) * size).Take(size)
            .Select(d => new OwnDocument(d.Id, d.FileName, d.ContentType, d.SizeBytes, d.DocumentType, d.Source, d.SourceDate,
                d.VisitId, d.DossierId, d.UploadedAt))
            .ToArrayAsync(ct);
        return new(items, total, p, size);
    }

    public async Task<DocumentContent> DocumentContentAsync(int accountId, Guid documentId, CancellationToken ct)
    {
        var patientId = await PatientAsync(accountId, ct);
        return await documents.PatientContentAsync(patientId, accountId, documentId, ct);
    }
}

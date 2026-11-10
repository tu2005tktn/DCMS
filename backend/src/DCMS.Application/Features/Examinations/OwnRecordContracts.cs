namespace DCMS.Application.Features.Examinations;

// Patient-facing official visit results (docs/specs/MOBILE-PATIENT-VISIT-RESULTS.vi.md §3).
// The patient comes from the caller's VERIFIED self link, rechecked on every request; no patientId is accepted from the client.
public sealed record OwnVisitItem(Guid VisitId, DateTimeOffset ArrivalAt, string BranchName, string DentistName, string VisitStatus,
    DateTimeOffset? CompletedAt, int ExaminationVersion, string? DiagnosisSummary, int DocumentCount, bool HasPrescription);
public sealed record OwnVisitPage(IReadOnlyList<OwnVisitItem> Items, int Total, int Page, int PageSize);
public sealed record OwnFinding(int? Tooth, string? Region, string Condition, IReadOnlyList<string> Surfaces, IReadOnlyList<string> Diagnoses);
public sealed record OwnExaminationResult(int Version, string? Symptoms, IReadOnlyList<OwnFinding> Findings, IReadOnlyList<string> Diagnoses,
    string? Conclusion, string? Advice, string? Disposition, string? ReturnNeed, string? ReferralDestination);
public sealed record OwnInvestigation(Guid Id, string Kind, string Name, string? Region, string State,
    string? ResultText, string? Interpretation, DateTimeOffset? ReviewedAt);
public sealed record OwnVisitDetail(OwnVisitItem Visit, OwnExaminationResult Examination, IReadOnlyList<OwnInvestigation> Investigations);
public sealed record OwnDocument(Guid DocumentId, string FileName, string ContentType, long SizeBytes, string DocumentType,
    string Source, DateOnly SourceDate, Guid? VisitId, Guid? DossierId, DateTimeOffset UploadedAt);
public sealed record OwnDocumentPage(IReadOnlyList<OwnDocument> Items, int Total, int Page, int PageSize);

public interface IOwnClinicalRecordService
{
    Task<OwnVisitPage> VisitsAsync(int accountId, int page, int pageSize, CancellationToken ct);
    Task<OwnVisitDetail> VisitAsync(int accountId, Guid visitId, CancellationToken ct);
    Task<OwnDocumentPage> DocumentsAsync(int accountId, Guid? visitId, int page, int pageSize, CancellationToken ct);
    Task<DocumentContent> DocumentContentAsync(int accountId, Guid documentId, CancellationToken ct);
}

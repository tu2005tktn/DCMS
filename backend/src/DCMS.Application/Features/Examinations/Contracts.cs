using DCMS.Application.Features.Reception;

namespace DCMS.Application.Features.Examinations;

public sealed record MedicalFact(string State = "Unknown", string? Detail = null, bool Verified = false);
public sealed record ToothFinding(int? Tooth, string? Region, string Condition, IReadOnlyList<string>? Surfaces = null, IReadOnlyList<string>? Diagnoses = null);
public sealed record ExaminationContent(string? Symptoms, MedicalFact MedicalHistory, MedicalFact Allergies,
    MedicalFact Medications, IReadOnlyList<ToothFinding> Findings, string? Diagnosis, string? Notes,
    string? Conclusion = null, string? Advice = null, string? Disposition = null,
    string? ReturnNeed = null, string? ReferralDestination = null, IReadOnlyList<string>? Diagnoses = null, IReadOnlyList<string>? UnscopedDiagnoses = null, IReadOnlyList<DCMS.Application.Features.Settings.ReferenceSnapshot>? DiagnosisReferences = null);
public sealed record StartExamination(int PatientId, int ExpectedVersion, Guid RequestKey);
public sealed record SaveExamination(int PatientId, int ExpectedVersion, Guid RequestKey,
    ExaminationContent Content, bool Confirm);
public sealed record ExaminationDto(ExaminationContent Content, bool Confirmed, int UpdatedBy, DateTimeOffset UpdatedAt,
    Guid Id = default, int Version = 0);
public sealed record AmendExamination(int PatientId, int ExpectedVersion, Guid RequestKey, ExaminationContent Content, string Reason);
public sealed record CompleteExaminationVisit(int PatientId, int ExpectedVersion, Guid RequestKey, bool WorkReviewed, DCMS.Application.Features.Finance.ExaminationFeeChoice? ExaminationFee = null);
public sealed record PatientExamination(Guid VisitId, string AppointmentCode, int BranchId, string BranchName,
    string DentistName, DateTimeOffset ArrivalAt, string VisitStatus, ExaminationDto? Examination);
public sealed record ExaminationVersion(int Version, ExaminationContent Content, bool Confirmed, int ActorId,
    DateTimeOffset RecordedAt, string? Reason);
public sealed record ExaminationPatient(int Id, string PatientCode, string FullName, DateOnly? BirthDate,
    string? Gender, int? AgeYears);
public sealed record ExaminationWorkspace(VisitResult Visit, ExaminationDto? Examination,
    ExaminationContent InitialContent, IReadOnlyList<string> AllowedActions, ExaminationPatient Patient, bool Replayed = false, IReadOnlyList<InvestigationDto>? Investigations = null);
public sealed record ExaminationHistory(Guid VisitId, DateTimeOffset ArrivalAt, ExaminationDto Examination);
public sealed record ClinicalHandoff(Guid VisitId, int PatientId, int BranchId, int Version, ExaminationContent Examination,
    IReadOnlyList<int> Teeth, IReadOnlyList<DocumentDto> Documents, VisitSource? Source, int ExaminationVersion = 0, string VisitStatus = "InProgress");
public interface IExaminationService
{
    Task<ExaminationWorkspace> GetAsync(int actorId, Guid visitId, CancellationToken ct);
    Task<ExaminationWorkspace> StartAsync(int actorId, Guid visitId, StartExamination request, CancellationToken ct);
    Task<ExaminationWorkspace> SaveAsync(int actorId, Guid visitId, SaveExamination request, CancellationToken ct);
    Task<IReadOnlyList<ExaminationHistory>> HistoryAsync(int actorId, Guid visitId, CancellationToken ct);
    Task<ClinicalHandoff> HandoffAsync(int actorId, Guid visitId, CancellationToken ct);
    Task<IReadOnlyList<PatientExamination>> ListAsync(int actorId, int patientId, CancellationToken ct);
    Task<IReadOnlyList<ExaminationVersion>> VersionsAsync(int actorId, Guid visitId, CancellationToken ct);
    Task<ExaminationWorkspace> AmendAsync(int actorId, Guid visitId, AmendExamination request, CancellationToken ct);
    Task<ExaminationWorkspace> CompleteAsync(int actorId, Guid visitId, CompleteExaminationVisit request, CancellationToken ct);
}

public sealed record DocumentMetadata(int PatientId, Guid VisitId, Guid? DossierId, Guid? PlanId,
    Guid? InvestigationId, string? ConsentVersion, string DocumentType, string Source,
    DateOnly SourceDate, string? Description, Guid RequestKey);
public sealed record DocumentContext(Guid VisitId, string VisitLabel, Guid? DossierId, string? DossierLabel,
    Guid? PlanId, string? PlanLabel, Guid? InvestigationId = null, string? InvestigationLabel = null);
public sealed record DocumentReviewDto(Guid VisitId, int ReviewedBy, DateTimeOffset ReviewedAt);
public sealed record DocumentDto(Guid DocumentId, int Version, string FileName, string ContentType, long SizeBytes,
    string DocumentType, string Source, DateOnly SourceDate, string? Description, DocumentContext Context,
    string UploaderName, DateTimeOffset UploadedAt, string FileState, string? RejectReason,
    IReadOnlyList<string> AllowedActions, IReadOnlyList<DocumentReviewDto> Reviews);
public sealed record DocumentConstraints(int MaxFiles, long MaxBytes, string[] ContentTypes, int DescriptionMax);
public sealed record DocumentPage(IReadOnlyList<DocumentDto> Items, int Total, int Page, int PageSize,
    DocumentConstraints Constraints, string[] AllowedActions);
public sealed record DocumentQuery(int PatientId, Guid? VisitId = null, Guid? DossierId = null, Guid? PlanId = null,
    string? Type = null, DateOnly? From = null, DateOnly? To = null, int Page = 1, int PageSize = 20);
public sealed record DocumentUpload(string FileName, string ContentType, byte[] Content);
public sealed record DocumentUploadResult(DocumentDto? Document, string? Error);
public sealed record UploadBatchResult(DocumentDto? Document, IReadOnlyList<DocumentUploadResult> Documents);
public sealed record ReviewDocument(int PatientId, Guid VisitId, int ExpectedVersion, Guid RequestKey);
public sealed record DocumentHistoryDto(string Action, string ActorName, DateTimeOffset OccurredAt, string? Note);
public sealed record DocumentContent(byte[] Bytes, string ContentType, string FileName);
public interface IClinicalDocumentService
{
    Task<object> ContextsAsync(int actorId, int patientId, CancellationToken ct);
    Task<DocumentPage> ListAsync(int actorId, DocumentQuery query, CancellationToken ct);
    Task<DocumentDto> DetailAsync(int actorId, Guid documentId, CancellationToken ct);
    Task<IReadOnlyList<DocumentHistoryDto>> HistoryAsync(int actorId, Guid documentId, CancellationToken ct);
    Task<UploadBatchResult> UploadAsync(int actorId, DocumentMetadata metadata, IReadOnlyList<DocumentUpload> files, CancellationToken ct);
    Task<DocumentContent> ContentAsync(int actorId, Guid documentId, bool inline, CancellationToken ct);
    Task<DocumentDto> ReviewAsync(int actorId, Guid documentId, ReviewDocument request, CancellationToken ct);
    Task ScanPendingAsync(CancellationToken ct);
}
public interface IClinicalFileScanner
{
    // Ready only after an affirmative clean result. Unknown/unavailable remains PendingScan.
    Task<string> ScanAsync(byte[] content, CancellationToken ct);
}

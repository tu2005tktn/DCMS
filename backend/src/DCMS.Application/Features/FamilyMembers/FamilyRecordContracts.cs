using DCMS.Application.Features.Examinations;

namespace DCMS.Application.Features.FamilyMembers;

public sealed record FamilyAccessRequest(string? PatientCode, string? MatchingInformation, Guid RequestKey);
public sealed record FamilyAccessWithdraw(int ExpectedVersion, Guid RequestKey);
public sealed record VerifyFamilyAccess(int PatientId, int BranchId, string[] Scopes, DateTimeOffset ExpiresAt,
    string SharingBasis, string VerificationNote, bool ConsentConfirmed, int ExpectedVersion, Guid RequestKey);
public sealed record DecideFamilyAccess(string DecisionReason, string? PublicNote, int ExpectedVersion, Guid RequestKey);
public sealed record FamilyAccessDto(Guid Id, Guid FamilyMemberId, string Code, string Status, string FullName,
    DateOnly? BirthDate, string? PatientCode, string? MatchingInformation, DateTimeOffset RequestedAt,
    DateTimeOffset? DecidedAt, DateTimeOffset? ExpiresAt, string[] Scopes, string? PublicNote, int Version);
public sealed record FamilyAccessOverview(FamilyAccessDto? Current, IReadOnlyList<FamilyAccessDto> History, bool CanRequest);
public sealed record FamilyPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record FamilyReviewRow(Guid Id, string Code, string AccountUserName, string AccountFullName,
    string FullName, string Status, DateTimeOffset RequestedAt, int? BranchId, int Version);
public sealed record FamilyBranch(int Id, string Name);
public sealed record FamilyPatientCandidate(int Id, string PatientCode, string FullName, DateOnly? BirthDate, int[] BranchIds);
public sealed record FamilyReviewDetail(FamilyAccessDto Access, int AccountId, string AccountUserName,
    string AccountFullName, string Relationship, FamilyPatientCandidate? Patient, int? BranchId,
    string? VerificationNote, string? SharingBasis, string? DecisionReason, IReadOnlyList<FamilyBranch> Branches);
public sealed record FamilyAuditDto(string Action, string ActorName, string? Reason, DateTimeOffset OccurredAt);
public sealed record FamilyRecordHeader(string PatientCode, string FullName, DateOnly? BirthDate,
    string BranchName, string[] Scopes, DateTimeOffset ExpiresAt);
public sealed record FamilyAppointmentDto(string Code, DateTimeOffset StartAt, DateTimeOffset EndAt,
    string Status, string BranchName, string DentistName);
public sealed record FamilyExaminationDto(Guid VisitId, DateTimeOffset ArrivalAt, string DentistName,
    string BranchName, string? Symptoms, string[] Diagnoses, string? Conclusion, string? Advice);
public sealed record FamilyTreatmentItem(string ServiceName, string? Region, string Progress,
    DateOnly? EstimatedStart, DateOnly? EstimatedEnd);
public sealed record FamilyTreatmentDto(Guid Id, string Code, string Diagnosis, string State,
    DateOnly StartedOn, IReadOnlyList<FamilyTreatmentItem> Items);
public sealed record FamilyMedicineDto(string MedicineName, string? Strength, string Unit, int Quantity,
    string Route, string MorningDose, string NoonDose, string AfternoonDose, string EveningDose,
    string MealTiming, int Days, string Instructions);
public sealed record FamilyPrescriptionDto(Guid Id, Guid VisitId, DateTimeOffset? FinalizedAt, string? Diagnosis,
    string GeneralAdvice, IReadOnlyList<FamilyMedicineDto> Lines);
public sealed record FamilyDocumentDto(Guid Id, string FileName, string ContentType, long SizeBytes,
    string DocumentType, DateOnly SourceDate);

public interface IFamilyRecordService
{
    Task<FamilyAccessOverview> OverviewAsync(int actor, Guid memberId, CancellationToken ct);
    Task<FamilyAccessDto> SubmitAsync(int actor, Guid memberId, FamilyAccessRequest command, CancellationToken ct);
    Task<FamilyAccessDto> WithdrawAsync(int actor, Guid memberId, Guid requestId, FamilyAccessWithdraw command, CancellationToken ct);
    Task<FamilyPage<FamilyReviewRow>> QueueAsync(int actor, string? status, string? keyword, int page, int pageSize, CancellationToken ct);
    Task<FamilyReviewDetail> DetailAsync(int actor, Guid requestId, CancellationToken ct);
    Task<IReadOnlyList<FamilyPatientCandidate>> CandidatesAsync(int actor, Guid requestId, string? keyword, CancellationToken ct);
    Task<IReadOnlyList<FamilyAuditDto>> AuditAsync(int actor, Guid requestId, CancellationToken ct);
    Task<FamilyAccessDto> VerifyAsync(int actor, Guid requestId, VerifyFamilyAccess command, CancellationToken ct);
    Task<FamilyAccessDto> DecideAsync(int actor, Guid requestId, bool revoke, DecideFamilyAccess command, CancellationToken ct);
    Task<FamilyPage<FamilyAppointmentDto>> AppointmentsAsync(int actor, Guid memberId, int page, int pageSize, CancellationToken ct);
    Task<FamilyRecordHeader> HeaderAsync(int actor, Guid memberId, CancellationToken ct);
    Task<FamilyPage<object>> RecordsAsync(int actor, Guid memberId, string section, int page, int pageSize, CancellationToken ct);
    Task<DocumentContent> DocumentAsync(int actor, Guid memberId, Guid documentId, CancellationToken ct);
}

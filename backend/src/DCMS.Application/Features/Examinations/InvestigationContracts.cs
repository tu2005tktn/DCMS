namespace DCMS.Application.Features.Examinations;

public sealed record CreateInvestigation(int PatientId, int ExpectedVisitVersion, Guid RequestKey,
    string Kind, string Name, string? Region, string Reason, bool RequiredForDiagnosis = true,
    int? ExpectedExaminationVersion = null, string? AmendmentReason = null);
public sealed record ReceiveInvestigationResult(int PatientId, int ExpectedVisitVersion, int ExpectedVersion,
    Guid RequestKey, string? ResultText);
public sealed record ReviewInvestigation(int PatientId, int ExpectedVisitVersion, int ExpectedVersion,
    Guid RequestKey, string? Interpretation = null);
public sealed record CancelInvestigation(int PatientId, int ExpectedVisitVersion, int ExpectedVersion,
    Guid RequestKey, string Reason);
public sealed record InvestigationDto(Guid Id, Guid VisitId, string Kind, string Name, string? Region,
    string Reason, bool RequiredForDiagnosis, string State, int Version, string CreatedByName,
    DateTimeOffset CreatedAt, string? ResultText, string? ResultByName, DateTimeOffset? ResultAt,
    string? Interpretation, string? ReviewedByName, DateTimeOffset? ReviewedAt,
    string? CancellationReason, DateTimeOffset? CancelledAt, IReadOnlyList<Guid> ResultDocumentIds,
    IReadOnlyList<DocumentDto> Documents, IReadOnlyList<string> AllowedActions);
public sealed record InvestigationPage(Guid VisitId, int VisitVersion, bool CanOrder, IReadOnlyList<InvestigationDto> Items);
public interface IInvestigationService
{
    Task<InvestigationPage> ListAsync(int actorId, Guid visitId, CancellationToken ct);
    Task<InvestigationPage> CreateAsync(int actorId, Guid visitId, CreateInvestigation request, CancellationToken ct);
    Task<InvestigationPage> ResultAsync(int actorId, Guid visitId, Guid id, ReceiveInvestigationResult request, CancellationToken ct);
    Task<InvestigationPage> ReviewAsync(int actorId, Guid visitId, Guid id, ReviewInvestigation request, CancellationToken ct);
    Task<InvestigationPage> CancelAsync(int actorId, Guid visitId, Guid id, CancelInvestigation request, CancellationToken ct);
}

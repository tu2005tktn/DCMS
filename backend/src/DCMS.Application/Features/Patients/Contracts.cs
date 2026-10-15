using DCMS.Application.Common;

namespace DCMS.Application.Features.Patients;

// S05/S06 (UC-08, UC-09, UC-38). Contract: docs/implementation/UC08-09-38-S05-S06.md.
public sealed record PatientListItem(int Id, string PatientCode, string FullName, DateOnly? BirthDate, string? Gender,
    string? Phone, bool IsArchived, DateTimeOffset? LastVisitAt, DateTimeOffset UpdatedAt);
public sealed record PatientPage(IReadOnlyList<PatientListItem> Items, int Total, int Page, int PageSize);

public sealed record PatientLinkInfo(string? VerifiedAccountUserName, DateTimeOffset? VerifiedAt);
public sealed record PatientDetail(int Id, string PatientCode, string FullName, DateOnly? BirthDate, string? Gender,
    string? Phone, string? Address, string? GuardianName, string? GuardianRelationship, string? GuardianPhone, string? Note,
    bool IsArchived, DateTimeOffset? ArchivedAt, string? ArchiveReason, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int Version, PatientLinkInfo Link);

public sealed record CreatePatientRequest(string? PatientCode, string? FullName, DateOnly? BirthDate, string? Gender,
    string? Phone, string? Address, string? GuardianName, string? GuardianRelationship, string? GuardianPhone, string? Note,
    bool DuplicatesReviewed);
public sealed record UpdatePatientRequest(int Version, string? FullName, DateOnly? BirthDate, string? Gender, string? Phone,
    string? Address, string? GuardianName, string? GuardianRelationship, string? GuardianPhone, string? Note,
    bool DuplicatesReviewed);
public sealed record ArchivePatientRequest(int Version, string? Reason);
public sealed record PatientChange(PatientDetail Patient, SystemMessage Notification);

public sealed record DuplicateCheckRequest(string? FullName, DateOnly? BirthDate, string? Phone, int? ExcludeId);
public sealed record DuplicateCandidate(PatientListItem Patient, IReadOnlyList<string> Reasons);

public sealed record PatientFieldChange(string Field, string? Before, string? After);
public sealed record PatientAuditEntry(long Id, string Action, string ActorName, string? Reason,
    IReadOnlyList<PatientFieldChange> Changes, DateTimeOffset OccurredAt);

public interface IPatientService
{
    Task<PatientPage> SearchAsync(string? keyword, DateOnly? birthDate, bool includeArchived, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<DuplicateCandidate>> DuplicatesAsync(DuplicateCheckRequest request, CancellationToken ct);
    Task<PatientDetail> GetAsync(int id, CancellationToken ct);
    Task<PatientChange> CreateAsync(int actorId, CreatePatientRequest request, CancellationToken ct);
    Task<PatientChange> UpdateAsync(int actorId, int id, UpdatePatientRequest request, CancellationToken ct);
    Task<PatientChange> ArchiveAsync(int actorId, int id, ArchivePatientRequest request, bool archive, CancellationToken ct);
    Task<IReadOnlyList<PatientAuditEntry>> AuditAsync(int id, CancellationToken ct);
}

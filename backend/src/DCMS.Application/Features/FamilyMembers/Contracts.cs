namespace DCMS.Application.Features.FamilyMembers;

public sealed record FamilyMemberDto(Guid Id, string FullName, string Relationship, DateOnly? BirthDate, int Version, string? Gender = null, string? ContactPhone = null);
public sealed record FamilyMemberPage(IReadOnlyList<FamilyMemberDto> Items, int Total, int Page, int PageSize);
public sealed record CreateFamilyMember(string FullName, string Relationship, DateOnly? BirthDate, Guid RequestKey, string? Gender = null, string? ContactPhone = null);
public sealed record UpdateFamilyMember(string FullName, string Relationship, DateOnly? BirthDate, int ExpectedVersion, Guid RequestKey, string? Gender = null, string? ContactPhone = null);
public sealed record DeleteFamilyMember(int ExpectedVersion, Guid RequestKey);
public sealed record DeletedFamilyMember(Guid Id, int Version, bool Deleted);
public sealed record FamilyMemberCreated(FamilyMemberDto Member, bool Replayed);
public interface IFamilyMemberService
{
    Task<FamilyMemberPage> ListAsync(int actor, string? keyword, int page, int pageSize, CancellationToken ct);
    Task<FamilyMemberDto> GetAsync(int actor, Guid id, CancellationToken ct);
    Task<FamilyMemberCreated> CreateAsync(int actor, CreateFamilyMember command, CancellationToken ct);
    Task<FamilyMemberDto> UpdateAsync(int actor, Guid id, UpdateFamilyMember command, CancellationToken ct);
    Task<DeletedFamilyMember> DeleteAsync(int actor, Guid id, DeleteFamilyMember command, CancellationToken ct);
}

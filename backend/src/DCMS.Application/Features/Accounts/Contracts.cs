using DCMS.Application.Common;

namespace DCMS.Application.Features.Accounts;

public sealed record AccountDto(int Id, string UserName, string FullName, string? Email, string? Phone,
    string AccountType, bool IsActive, int Version, string[] Roles, string? EmployeeCode,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record AccountPage(IReadOnlyList<AccountDto> Items, int Total, int Page, int PageSize);
public sealed record CreateStaffRequest(string UserName, string Password, string FullName, string EmployeeCode,
    string? Email, string? Phone, string[] Roles);
public sealed record UpdateStaffRequest(int Version, string UserName, string FullName, string? Email,
    string? Phone, string[] Roles, string Reason);
public sealed record AccountStateRequest(int Version, bool Locked, string Reason);
public sealed record AccountChange(AccountDto Account, SystemMessage Notification);
public sealed record AccountAuditDto(long Id, int ActorId, int TargetId, DateTimeOffset OccurredAt,
    string Action, string Reason, string BeforeJson, string AfterJson, string ActorUserName, string ActorFullName);

public interface IAccountService
{
    Task<AccountPage> ListAsync(string? search, int page, int pageSize, string? role, string? status,
        string? sortBy, string? sortDirection, CancellationToken ct);
    Task<AccountDto> DetailAsync(int id, CancellationToken ct);
    Task<AccountChange> CreateAsync(int actorId, CreateStaffRequest request, CancellationToken ct);
    Task<AccountChange> UpdateAsync(int actorId, int id, UpdateStaffRequest request, CancellationToken ct);
    Task<AccountChange> SetStateAsync(int actorId, int id, AccountStateRequest request, CancellationToken ct);
    Task<IReadOnlyList<AccountAuditDto>> AuditAsync(int id, CancellationToken ct);
}

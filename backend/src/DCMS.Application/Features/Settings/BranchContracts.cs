using System.Text.Json.Serialization;

namespace DCMS.Application.Features.Settings;

public sealed record BranchDto(
    int Id,
    string Code,
    string Name,
    string? Address,
    string? Phone,
    string? Email,
    int SlotIntervalMinutes,
    bool IsActive,
    int Version,
    int ChairCount);

public sealed record ChairDto(
    int Id,
    int BranchId,
    string Code,
    string Name,
    int OpenMinute,
    int CloseMinute,
    bool IsActive,
    int Version);

public sealed record BranchDetailDto(
    int Id,
    string Code,
    string Name,
    string? Address,
    string? Phone,
    string? Email,
    int SlotIntervalMinutes,
    bool IsActive,
    int Version,
    ChairDto[] Chairs);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveBranchCommand(
    Guid RequestKey,
    int ExpectedVersion,
    string Code,
    string Name,
    string? Address,
    string? Phone,
    string? Email);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveChairCommand(
    Guid RequestKey,
    int ExpectedVersion,
    string Code,
    string Name,
    int? OpenMinute,
    int? CloseMinute);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BranchStateCommand(
    Guid RequestKey,
    int ExpectedVersion,
    bool Active,
    string? Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChairStateCommand(
    Guid RequestKey,
    int ExpectedVersion,
    bool Active,
    string? Reason);

public sealed record BlockerItem(
    string Kind,
    string Message,
    int Count,
    string NavigationUrl);

public sealed record ImpactPreviewResult(
    bool Allowed,
    BlockerItem[] Blockers,
    string Impact);

public sealed record BranchObligationResult(bool Available, int Count);

// Lab orders are not modelled yet; the default adapter reports Available = false so branch deactivation stops with 503 MSG144.
public interface IBranchLabObligationPort
{
    Task<BranchObligationResult> OpenLabOrdersAsync(int branchId, CancellationToken ct);
}

public sealed record BranchMemberDto(
    int EmployeeId,
    string EmployeeCode,
    string FullName,
    string UserName,
    string[] Roles,
    bool IsMember,
    bool Active,
    bool CanDentist,
    bool CanManageSchedules);

public sealed record BranchMembersDto(int BranchId, int Version, BranchMemberDto[] Members);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveBranchMemberCommand(
    Guid RequestKey,
    int ExpectedVersion,
    bool Active,
    bool CanDentist,
    bool CanManageSchedules);

public interface IBranchManagementService
{
    Task<BranchMembersDto> MembersAsync(int actor, int branchId, CancellationToken ct);
    Task<BranchMembersDto> SaveMemberAsync(int actor, int branchId, int employeeId, SaveBranchMemberCommand command, CancellationToken ct);

    Task<BranchDto[]> BranchesAsync(int actor, CancellationToken ct);
    Task<BranchDetailDto> BranchAsync(int actor, int id, CancellationToken ct);
    Task<BranchDetailDto> SaveBranchAsync(int actor, int? id, SaveBranchCommand command, CancellationToken ct);
    Task<ImpactPreviewResult> BranchStatePreviewAsync(int actor, int id, bool targetActive, CancellationToken ct);
    Task<BranchDetailDto> SetBranchStateAsync(int actor, int id, BranchStateCommand command, CancellationToken ct);

    Task<ChairDto[]> ChairsAsync(int actor, int branchId, CancellationToken ct);
    Task<ChairDto> SaveChairAsync(int actor, int branchId, int? chairId, SaveChairCommand command, CancellationToken ct);
    Task<ImpactPreviewResult> ChairStatePreviewAsync(int actor, int branchId, int chairId, bool targetActive, CancellationToken ct);
    Task<ChairDto> SetChairStateAsync(int actor, int branchId, int chairId, ChairStateCommand command, CancellationToken ct);
}

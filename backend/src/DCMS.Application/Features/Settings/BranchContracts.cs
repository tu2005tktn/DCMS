using System.Text.Json.Serialization;

namespace DCMS.Application.Features.Settings;

public sealed record BranchDto(
    int Id,
    string Code,
    string Name,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    Guid? LogoAssetId,
    string TimeZoneId,
    string? BankCode,
    string? AccountNumber,
    string? PayeeName,
    int SlotIntervalMinutes,
    int BookingSettingsVersion,
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
    string? Website,
    Guid? LogoAssetId,
    string TimeZoneId,
    string? BankCode,
    string? AccountNumber,
    string? PayeeName,
    int SlotIntervalMinutes,
    int BookingSettingsVersion,
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
    string? Email,
    string? Website,
    Guid? LogoAssetId,
    string? TimeZoneId,
    string? BankCode,
    string? AccountNumber,
    string? PayeeName);

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
    string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChairStateCommand(
    Guid RequestKey,
    int ExpectedVersion,
    bool Active,
    string Reason);

public sealed record BlockerItem(
    string Kind,
    string Message,
    int Count,
    string NavigationUrl);

public sealed record ImpactPreviewResult(
    bool Allowed,
    BlockerItem[] Blockers,
    string Impact);

public sealed record BranchLogoUploadResult(
    Guid AssetId,
    string FileName,
    long SizeBytes);

public interface IBranchManagementService
{
    Task<BranchDto[]> BranchesAsync(int actor, CancellationToken ct);
    Task<BranchDetailDto> BranchAsync(int actor, int id, CancellationToken ct);
    Task<BranchDetailDto> SaveBranchAsync(int actor, int? id, SaveBranchCommand command, CancellationToken ct);
    Task<ImpactPreviewResult> BranchStatePreviewAsync(int actor, int id, bool targetActive, CancellationToken ct);
    Task<BranchDetailDto> SetBranchStateAsync(int actor, int id, BranchStateCommand command, CancellationToken ct);
    Task<BranchLogoUploadResult> UploadLogoAsync(int actor, int id, Stream fileStream, string fileName, string contentType, long length, CancellationToken ct);

    Task<ChairDto[]> ChairsAsync(int actor, int branchId, CancellationToken ct);
    Task<ChairDto> SaveChairAsync(int actor, int branchId, int? chairId, SaveChairCommand command, CancellationToken ct);
    Task<ImpactPreviewResult> ChairStatePreviewAsync(int actor, int branchId, int chairId, bool targetActive, CancellationToken ct);
    Task<ChairDto> SetChairStateAsync(int actor, int branchId, int chairId, ChairStateCommand command, CancellationToken ct);
}

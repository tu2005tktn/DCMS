using System.Text.Json.Serialization;

namespace DCMS.Application.Features.Settings;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequiredPatientFields(bool ProfileDate = false, bool Gender = false, bool BirthDate = false,
    bool Address = false, bool Phone = false, bool CustomerGroup = false, bool Source = false);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatientCodePolicy(string Mode = "AutoWithManualOverride", string Prefix = "BN");
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReceiptPrintPolicy(bool InternalUseNote = false, bool ShowReceiptNumber = true,
    bool PatientAddress = false, bool PatientPhone = false, bool PaymentMethod = true, bool DentistAssistant = false,
    bool TotalFee = true, bool Discount = true, bool PreviouslyPaid = true, bool Remaining = true,
    bool PatientTotalOutstanding = false, bool SignatureLines = false, bool DefaultPayerToPatient = false,
    bool DefaultCollectorToActor = false, bool PaymentNote = true);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GeneralSettings(int StaffIdleTimeoutMinutes, RequiredPatientFields RequiredFields,
    PatientCodePolicy PatientCode, ReceiptPrintPolicy ReceiptPrint)
{
    public static GeneralSettings Default => new(20, new(), new(), new());
}
public sealed record GeneralDto(int Version, DateTimeOffset? EffectiveAt, GeneralSettings Settings);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GeneralCommand(int ExpectedVersion, Guid RequestKey, string Reason, GeneralSettings Settings);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RestoreSettings(int SourceVersion, int ExpectedVersion, Guid RequestKey, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReferenceValue(string Name, string? Code = null, string? Description = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReferenceCommand(Guid RequestKey, ReferenceValue Entry, int ExpectedVersion = 0, string? Reason = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StateCommand(int ExpectedVersion, Guid RequestKey, bool Active, string Reason);
public sealed record ReferenceDto(Guid Id, string Kind, string Name, string? Code, string? Description, bool Active,
    bool Reserved, int Version, DateTimeOffset CreatedAt);
public sealed record ReferenceSnapshot(Guid Id, int Version, string Name, string? Code);
public sealed record Page<T>(IReadOnlyList<T> Items, int Total, int PageNumber, int PageSize);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AccessGroupValue(string Code, string Name, string BaseRole, string[] Grants, string? Description = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GroupCommand(Guid RequestKey, AccessGroupValue Entry, int ExpectedVersion = 0, string? Reason = null);
public sealed record GroupDto(Guid Id, string Code, string Name, string BaseRole, string? Description,
    bool Active, int Version, string[] Grants, int AssignedCount);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssignGroup(Guid GroupId, int ExpectedVersion, int GroupVersion, Guid RequestKey,
    int[] BranchIds, string Reason);
public sealed record StaffAccessDto(int EmployeeId, int AccountId, string Name, bool Active, int AccountVersion,
    Guid? GroupId, int AssignmentVersion, int[] BranchIds);
public sealed record SectionCapability(string Key, string Label, string Path, string[] Actions);
public sealed record ScopeBranch(int Id, string Name);
public sealed record SettingsCapabilities(string ClinicScope, SectionCapability[] Sections, string[] Permissions,
    int[] BranchIds, bool Owner, ScopeBranch[] Branches);
public sealed record GrantDefinition(string Key, string Label, string? RequiredRole, string? Parent);
public sealed record PermissionState(bool Owner, bool Assigned, string[] Roles, string[] Permissions, int[] BranchIds);

public interface ISettingsAccess
{
    Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct);
    Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct);
    Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct);
}
public interface ISettingsService
{
    Task<SettingsCapabilities> CapabilitiesAsync(int actor, CancellationToken ct);
    Task<GroupDto[]> GroupsAsync(int actor, CancellationToken ct);
    Task<object> GroupPreviewAsync(int actor, Guid id, GroupCommand command, CancellationToken ct);
    Task<GroupDto> SaveGroupAsync(int actor, Guid? id, GroupCommand command, CancellationToken ct);
    Task<GroupDto> GroupStateAsync(int actor, Guid id, StateCommand command, CancellationToken ct);
    Task<StaffAccessDto[]> StaffAsync(int actor, CancellationToken ct);
    Task<StaffAccessDto> AssignAsync(int actor, int employeeId, AssignGroup command, CancellationToken ct);
    Task<Page<object>> AuditAsync(int actor, string? section, int page, int pageSize, CancellationToken ct);
}

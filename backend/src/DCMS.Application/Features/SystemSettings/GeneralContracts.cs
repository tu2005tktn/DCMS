using DCMS.Application.Common;
namespace DCMS.Application.Features.SystemSettings;

// S58 Thiết lập chung. Contract: docs/specs/S58-GENERAL-SETTINGS.md.
public sealed record RequiredFieldsSettings(bool ProfileDate, bool Gender, bool BirthDate, bool Address, bool Phone, bool CustomerGroup, bool Source);
public sealed record PatientCodeSettings(string Mode, string Prefix);
public sealed record ReceiptPrintSettings(bool InternalUseNote, bool ShowReceiptNumber, bool PatientAddress, bool PatientPhone, bool PaymentMethod,
    bool DentistAssistant, bool TotalFee, bool Discount, bool PreviouslyPaid, bool Remaining, bool PatientTotalOutstanding, bool SignatureLines,
    bool DefaultPayerToPatient, bool DefaultCollectorToActor, bool PaymentNote);
public sealed record GeneralSettings(int StaffIdleTimeoutMinutes, RequiredFieldsSettings RequiredFields, PatientCodeSettings PatientCode, ReceiptPrintSettings ReceiptPrint)
{
    // Keeps the behaviour that ran before S58: no extra required fields and the BN + sequence patient code.
    public static GeneralSettings Default { get; } = new(20, new(false, false, false, false, false, false, false),
        new(PatientCodeModes.AutoWithManualOverride, "BN"),
        new(false, true, false, false, true, false, true, true, true, true, false, false, false, false, true));
}
public static class PatientCodeModes
{
    public const string AutoWithManualOverride = "AutoWithManualOverride";
    public const string AutoOnly = "AutoOnly";
    public const string ManualOnly = "ManualOnly";
    public static readonly string[] All = [AutoWithManualOverride, AutoOnly, ManualOnly];
}

public sealed record GeneralSettingsDto(int Version, DateTimeOffset? EffectiveAt, string? ActorName, string? Reason, GeneralSettings Settings);
public sealed record PreviewGeneralRequest(int ExpectedVersion, GeneralSettings? Settings);
public sealed record SaveGeneralRequest(int ExpectedVersion, Guid RequestKey, string? Reason, GeneralSettings? Settings);
public sealed record RestorePreviewRequest(int SourceVersion, int ExpectedVersion);
public sealed record RestoreGeneralRequest(int SourceVersion, int ExpectedVersion, Guid RequestKey, string? Reason);
public sealed record SettingsChange(string Field, string Label, string? Before, string? After);
public sealed record MissingPatients(string Field, string Label, int Count);
public sealed record GeneralPreview(int CurrentVersion, bool Stale, IReadOnlyList<SettingsChange> Changes, IReadOnlyList<string> Warnings,
    IReadOnlyList<MissingPatients> MissingPatients);
public sealed record GeneralHistoryItem(int Version, DateTimeOffset EffectiveAt, string ActorName, string Reason, int? RestoredFromVersion, GeneralSettings Settings);
public sealed record GeneralHistoryPage(IReadOnlyList<GeneralHistoryItem> Items, int Total, int Page, int PageSize);
public sealed record SettingsSection(string Key, bool CanManage);
public sealed record SettingsCapabilities(IReadOnlyList<SettingsSection> Sections);
public sealed record PatientFormPolicy(int Version, RequiredFieldsSettings RequiredFields, PatientCodeSettings PatientCode);

// DCMS-122: the shared patient code sequence. Reads never call nextval; the only write moves it forward.
public sealed record PatientCodeSequenceDto(string Prefix, long NextNumber, string NextCode, long? LastIssued, long? HighestExisting);
public sealed record PatientCodeSequencePreviewRequest(long ExpectedNextNumber, long NextNumber);
public sealed record PatientCodeSequencePreview(bool Stale, bool Allowed, string NextCode, IReadOnlyList<string> Issues, int SkippedCodes);
public sealed record UpdatePatientCodeSequenceRequest(long ExpectedNextNumber, Guid RequestKey, string? Reason, long NextNumber);

public static class GeneralSettingsMessages
{
    public static SystemMessage FieldUnavailable(string field) =>
        new("SETTINGS_FIELD_UNAVAILABLE", "Hồ sơ bệnh nhân chưa có trường này nên chưa thể đặt bắt buộc.") { Field = field };
    public static SystemMessage NoChange => new("SETTINGS_NO_CHANGE", "Thiết lập không có thay đổi để lưu.");
}

public interface IGeneralSettingsService
{
    Task<SettingsCapabilities> CapabilitiesAsync(int actorId, CancellationToken ct);
    Task<GeneralSettingsDto> GetAsync(int actorId, CancellationToken ct);
    Task<GeneralPreview> PreviewAsync(int actorId, PreviewGeneralRequest request, CancellationToken ct);
    Task<GeneralSettingsDto> SaveAsync(int actorId, SaveGeneralRequest request, CancellationToken ct);
    Task<GeneralHistoryPage> HistoryAsync(int actorId, int page, int pageSize, CancellationToken ct);
    Task<GeneralPreview> RestorePreviewAsync(int actorId, RestorePreviewRequest request, CancellationToken ct);
    Task<GeneralSettingsDto> RestoreAsync(int actorId, RestoreGeneralRequest request, CancellationToken ct);
    Task<PatientCodeSequenceDto> SequenceAsync(int actorId, CancellationToken ct);
    Task<PatientCodeSequencePreview> SequencePreviewAsync(int actorId, PatientCodeSequencePreviewRequest request, CancellationToken ct);
    Task<PatientCodeSequenceDto> UpdateSequenceAsync(int actorId, UpdatePatientCodeSequenceRequest request, CancellationToken ct);
}

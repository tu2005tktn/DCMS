using System.Text.Json.Nodes;

namespace DCMS.Application.Features.SystemSettings;

// DCMS-132: one read-only settings history across SYSTEM_CATALOG_CHANGE and SETTINGS_AUDIT (docs/specs/S58-DCMS-132-EXPORT-AUDIT.vi.md §3).
public static class AuditSections
{
    public const string General = "general";
    public const string Catalogs = "catalogs";
    public const string Supplies = "supplies";
    public const string Materials = "materials";
    public const string AccessGroups = "access-groups";
    public static readonly string[] All = [General, Catalogs, Supplies, Materials, AccessGroups];
}
public sealed record SettingsAuditItem(Guid Id, string Section, string Target, string Action, string ActorName, DateTimeOffset At,
    string? Reason, JsonNode? Before, JsonNode? After);
public sealed record SettingsAuditPage(IReadOnlyList<SettingsAuditItem> Items, int Total, int Page, int PageSize, IReadOnlyList<string> Sections);

public interface ISettingsAuditService
{
    Task<SettingsAuditPage> ListAsync(int actorId, string? section, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct);
}

// DCMS-132 services export (docs/specs/S58-DCMS-132-EXPORT-AUDIT.vi.md §4).
public interface IServiceCatalogExport
{
    Task<byte[]> ExportAsync(int actorId, string? q, string? status, CancellationToken ct);
}

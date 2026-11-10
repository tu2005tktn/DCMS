using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Settings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.SystemSettings;

// DCMS-132 settings history (docs/specs/S58-DCMS-132-EXPORT-AUDIT.vi.md §3). Read-only; every row is sanitized before it leaves.
public sealed partial class SettingsAuditService(DcmsDbContext db, ISettingsAccess access) : ISettingsAuditService
{
    private static readonly int[] PageSizes = [20, 50, 100];
    private static readonly TimeSpan ClinicOffset = TimeSpan.FromHours(7);
    private sealed record Row(Guid Id, string Section, string Target, string Action, int ActorId, DateTimeOffset At, string? Reason, string? Before, string? After);

    private static BusinessException Error(string code, int status = 400, string label = "Thông tin", string? field = null) =>
        new(status, SystemMessages.Get(code, label) with { Field = field });

    public async Task<SettingsAuditPage> ListAsync(int actorId, string? section, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        var state = await access.ResolveAsync(actorId, ct);
        if (!state.Owner && !state.Permissions.Contains("settings.audit.read")) throw Error("MSG06", 403);
        if (page < 1 || !PageSizes.Contains(pageSize) || page > 100_000) throw Error("MSG04", label: "Phân trang");
        if (section != null && !AuditSections.All.Contains(section)) throw Error("MSG04", label: "Nhóm thiết lập", field: "section");
        if (from > to) throw Error("MSG04", label: "Khoảng ngày", field: "from");

        var kinds = state.Owner ? ReferenceKinds.All : ReferenceKinds.All.Where(k => state.Permissions.Contains($"catalog.{k}.read")).ToArray();
        var dentist = state.Roles.Contains(AccountRoles.Dentist);
        var allowed = AuditSections.All.Where(s => s switch
        {
            AuditSections.General => state.Owner,
            AuditSections.Catalogs => kinds.Length > 0,
            AuditSections.Supplies or AuditSections.Materials => state.Owner || dentist,
            _ => state.Permissions.Contains("access-groups.read")
        }).ToArray();
        if (section != null && !allowed.Contains(section)) throw Error("MSG06", 403);
        bool Wants(string s) => allowed.Contains(s) && (section == null || section == s);

        // Clinic-day bounds converted to UTC: Npgsql only writes offset-0 values to timestamptz.
        DateTimeOffset? start = from == null ? null : new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), ClinicOffset).ToUniversalTime();
        DateTimeOffset? end = to == null ? null : new DateTimeOffset(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), ClinicOffset).ToUniversalTime();

        // SYSTEM_CATALOG_CHANGE: the command name decides the section.
        bool general = Wants(AuditSections.General), catalogs = Wants(AuditSections.Catalogs),
            supplies = Wants(AuditSections.Supplies), materials = Wants(AuditSections.Materials);
        var entryIds = db.ReferenceEntries.Where(r => kinds.Contains(r.Kind)).Select(r => r.Id);
        var changes = db.SystemCatalogChanges.AsNoTracking().Where(x =>
            (general && (x.Command.StartsWith("General") || x.Command == "PatientCodeSequence"))
            || (catalogs && x.Command.StartsWith("Reference") && (state.Owner || entryIds.Contains(x.ResourceId)))
            || (supplies && (x.Command.StartsWith("providers:") || x.Command.StartsWith("lab-work-types:") || x.Command.StartsWith("medicines:")))
            || (materials && (x.Command.StartsWith("Material") || x.Command.StartsWith("Category"))));
        if (start != null) changes = changes.Where(x => x.OccurredAt >= start);
        if (end != null) changes = changes.Where(x => x.OccurredAt < end);

        // SETTINGS_AUDIT keeps access-group history; outside the owner only rows inside the actor's branches (DCMS-128).
        var accessGroups = Wants(AuditSections.AccessGroups);
        var groups = db.Set<SettingsAudit>().AsNoTracking().Where(x => accessGroups && x.Section == AuditSections.AccessGroups);
        if (!state.Owner) groups = groups.Where(x => x.BranchIds.All(b => state.BranchIds.Contains(b)));
        if (start != null) groups = groups.Where(x => x.At >= start);
        if (end != null) groups = groups.Where(x => x.At < end);

        var take = page * pageSize;
        var total = await changes.CountAsync(ct) + await groups.CountAsync(ct);
        var rows = (await changes.OrderByDescending(x => x.OccurredAt).ThenBy(x => x.RequestKey).Take(take).ToListAsync(ct))
            .Select(x => new Row(x.RequestKey, SectionOf(x.Command), x.ResourceId.ToString(), x.Command, x.ActorId, x.OccurredAt, null, x.BeforeJson, x.ResultJson))
            .Concat((await groups.OrderByDescending(x => x.At).ThenBy(x => x.Id).Take(take).ToListAsync(ct))
                .Select(x => new Row(x.Id, AuditSections.AccessGroups, x.Target, x.Action, x.ActorId, x.At, x.Reason, x.BeforeJson, x.AfterJson)))
            .OrderByDescending(x => x.At).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var actorIds = rows.Select(x => x.ActorId).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(u => actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var costs = state.Permissions.Contains("finance.costs.read");
        var items = rows.Select(x =>
        {
            var before = Clean(x.Before, costs); var after = Clean(x.After, costs);
            var reason = x.Reason ?? Text(before, "reason") ?? Text(after, "reason");
            var target = x.Section == AuditSections.Catalogs && Text(after, "kind") is { } kind ? $"{kind}:{x.Target}" : x.Target;
            return new SettingsAuditItem(x.Id, x.Section, target, x.Action, names.GetValueOrDefault(x.ActorId) ?? $"Tài khoản #{x.ActorId}", x.At, reason, before, after);
        }).ToArray();
        return new(items, total, page, pageSize, allowed);
    }

    private static string SectionOf(string command) =>
        command.StartsWith("General") || command == "PatientCodeSequence" ? AuditSections.General
        : command.StartsWith("Reference") ? AuditSections.Catalogs
        : command.Contains(':') ? AuditSections.Supplies : AuditSections.Materials;

    private static string? Text(JsonNode? node, string key) =>
        node is JsonObject o && o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    [GeneratedRegex("password|token|secret|bank", RegexOptions.IgnoreCase)]
    private static partial Regex Secret();

    // Drops secrets everywhere and cost fields unless the reader may see costs (SET-05).
    internal static JsonNode? Clean(string? json, bool costs)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        JsonNode? node;
        try { node = JsonNode.Parse(json); } catch (System.Text.Json.JsonException) { return null; }
        void Walk(JsonNode? n)
        {
            if (n is JsonObject o)
            {
                foreach (var key in o.Select(p => p.Key).ToArray())
                    if (Secret().IsMatch(key) || !costs && key.Contains("cost", StringComparison.OrdinalIgnoreCase)) o.Remove(key);
                    else Walk(o[key]);
            }
            else if (n is JsonArray a) foreach (var item in a) Walk(item);
        }
        Walk(node);
        return node;
    }
}

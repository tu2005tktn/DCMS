using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Api.Features.Settings;

public sealed class ScreenPermissionScope(DcmsDbContext db, ISettingsAccess access)
{
    public async Task EnforceAsync(int actor, HttpContext http, JsonElement? body, CancellationToken ct)
    {
        var state = await access.ResolveAsync(actor, ct);
        var path = http.Request.Path.Value ?? "";
        if (!state.Assigned || state.Owner || path.StartsWith("/api/system-settings") || path.StartsWith("/api/accounts") || path.StartsWith("/api/notifications") || path.StartsWith("/api/patient-link-reviews")) return;
        var branches = new HashSet<int>();
        void Branch(JsonElement value) {
            if (value.ValueKind == JsonValueKind.Object) foreach (var property in value.EnumerateObject()) {
                if (property.NameEquals("branchId") && int.TryParse(property.Value.ToString(), out var branch) && branch > 0) branches.Add(branch);
                else if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) Branch(property.Value);
            }
            else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Branch(item);
        }
        if (body is { } json) Branch(json);
        if (int.TryParse(http.Request.RouteValues["branchId"]?.ToString() ?? http.Request.Query["branchId"].FirstOrDefault(), out var selected) && selected > 0) branches.Add(selected);
        async Task AddBranch(Task<int?> query) { if (await query is { } branch) branches.Add(branch); }
        if (Guid.TryParse(http.Request.RouteValues["visitId"]?.ToString(), out var visit))
            await AddBranch(db.Visits.Where(x => x.Id == visit).Select(x => (int?)x.BranchId).SingleOrDefaultAsync(ct));
        if (Guid.TryParse(http.Request.RouteValues["dossierId"]?.ToString(), out var dossier))
            await AddBranch(db.TreatmentDossiers.Where(x => x.Id == dossier).Select(x => (int?)x.BranchId).SingleOrDefaultAsync(ct));
        if (Guid.TryParse(http.Request.RouteValues["id"]?.ToString(), out var id)) {
            if (path.StartsWith("/api/appointments/")) await AddBranch(db.Appointments.Where(x => x.Id == id).Select(x => (int?)x.BranchId).SingleOrDefaultAsync(ct));
            if (path.StartsWith("/api/treatment-templates/")) await AddBranch(db.Set<TreatmentTemplate>().Where(x => x.Id == id).Select(x => (int?)x.BranchId).SingleOrDefaultAsync(ct));
            if (path.StartsWith("/api/follow-up-instructions/")) await AddBranch(db.Set<FollowUpInstruction>().Where(x => x.Id == id).Select(x => (int?)x.BranchId).SingleOrDefaultAsync(ct));
            if (path.StartsWith("/api/care-tasks/")) await AddBranch(db.Set<CareTask>().Where(x => x.Id == id).Select(x => (int?)x.BranchId).SingleOrDefaultAsync(ct));
        }
        if (http.Request.RouteValues["code"] is { } code && path.StartsWith("/api/appointments/")) {
            Guid.TryParse(code.ToString(), out var appointmentId);
            await AddBranch(db.Appointments.Where(x => x.Code == code.ToString() || x.Id == appointmentId).Select(x => (int?)x.BranchId).SingleOrDefaultAsync(ct));
        }
        if (int.TryParse(http.Request.RouteValues["serviceId"]?.ToString(), out var service))
            await AddBranch(db.Set<SchedulingService>().Where(x => x.Id == service).Select(x => (int?)x.BranchId).SingleOrDefaultAsync(ct));
        if (branches.Any(branch => !state.BranchIds.Contains(branch))) throw new BusinessException(403, SystemMessages.Get("MSG06"));
        if (http.Request.Method == "DELETE" && Guid.TryParse(http.Request.RouteValues["rowId"]?.ToString(), out var rowId)) {
            var draft = await db.TreatmentSheetDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == rowId, ct);
            if (draft != null && !state.Permissions.Contains(draft.Kind.Equals("actual", StringComparison.OrdinalIgnoreCase) ? "treatment.actuals.delete" : "treatment.items.delete")) throw new BusinessException(403, SystemMessages.Get("MSG06"));
        }
    }
}

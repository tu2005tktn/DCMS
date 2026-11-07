using System.Text.RegularExpressions;

namespace DCMS.Api.Features.Settings;

public sealed record ScreenEndpointRule(string[] AnyOf, string? Baseline = null)
{
    public static ScreenEndpointRule Need(string permission) => new([permission]);
    public static ScreenEndpointRule Any(params string[] permissions) => new(permissions);
    public static ScreenEndpointRule Open(string reason) => new([], reason);
}

// Use the selected endpoint's route template, never a substring of a caller-controlled URL.
public static class ScreenEndpointPermissions
{
    public static ScreenEndpointRule? Resolve(string template, string method, IReadOnlyDictionary<string, string?>? values = null, string? draftKind = null)
    {
        var p = Regex.Replace(template.TrimEnd('/'), @"\{([^}:]+):[^}]+\}", "{$1}");
        var read = method is "GET" or "HEAD";
        ScreenEndpointRule Need(string grant) => ScreenEndpointRule.Need(grant);
        if (p == "/api/payments/payos/webhook" && method == "POST") return ScreenEndpointRule.Open("Provider webhook; the service verifies the PayOS HMAC signature and payment identity");
        if (p.StartsWith("/api/admin/recovery/") || p.StartsWith("/api/dev/recovery/proof/")) return Need(read && !p.StartsWith("/api/dev/") ? "accounts.read" : "accounts.update");
        if (p is "/api/test/realtime/ping" or "/api/test/realtime/state") return ScreenEndpointRule.Open("Integration fixture realtime probes; not registered by the application");
        if (p == "/health" || p.StartsWith("/openapi/") || p.StartsWith("/api/auth") || p.StartsWith("/api/recovery") || p.StartsWith("/api/email-verification") || p.StartsWith("/api/development") || p.StartsWith("/api/dev/")
            || p.StartsWith("/api/customer/recovery/") || p.StartsWith("/api/me/") || p.StartsWith("/api/patients/me") || p.StartsWith("/api/patient-links/me") || p.StartsWith("/hubs/"))
            return ScreenEndpointRule.Open("Authentication, self ownership, health or authenticated realtime transport");
        if (p == "/api/system-settings/settings/capabilities" || p == "/api/finance/capabilities") return ScreenEndpointRule.Open("Capabilities contain no operational records");
        if (p == "/api/system-settings/reference-options") return ScreenEndpointRule.Any("patients.read", "examinations.read", "treatment.read", "settings.read");
        if (p.StartsWith("/api/system-settings/access-groups")) return Need(read ? "access-groups.read" : p.EndsWith("/preview") ? "access-groups.manage" : "access-groups.manage");
        if (p.StartsWith("/api/system-settings/staff/")) return Need("staff.access.assign");
        if (p == "/api/system-settings/access-capabilities") return Need("settings.read");
        if (p == "/api/system-settings/settings/audit") return Need("settings.audit.read");
        if (p.StartsWith("/api/system-settings/settings/general") || p.StartsWith("/api/system-settings/settings/patient-code-sequence")) return Need(read ? "settings.general.read" : "settings.general.manage");
        if (p.StartsWith("/api/system-settings/settings/catalogs/")) {
            var kind = p.Split('/')[5]; if (kind.StartsWith('{')) kind = values?.GetValueOrDefault("kind") ?? "{kind}";
            return Need($"catalog.{kind}.{(read ? p.EndsWith("/export") ? "export" : "read" : p.Contains("/state") ? "deactivate" : method == "POST" ? "create" : "update")}");
        }
        if (p == "/api/work-statistics") return Need("finance.work-statistics.read");
        if (p.StartsWith("/api/system-settings/material-purchases")) return Need($"materials.purchases.{(read ? "read" : method == "DELETE" ? "delete" : method == "POST" ? "create" : "update")}");
        if (p == "/api/system-settings/material-stock") return Need("materials.stock.read");
        if (p == "/api/system-settings/material-usage") return Need("materials.usage.read");
        if (p.StartsWith("/api/system-settings/material")) return Need($"catalog.materials.{(read ? "read" : method == "DELETE" ? "deactivate" : method == "POST" ? "create" : "update")}");
        if (p.StartsWith("/api/accounts")) return Need(read ? p.EndsWith("/audit") ? "accounts.audit" : "accounts.read" : p.EndsWith("/state") ? "accounts.state" : method == "POST" ? "accounts.create" : "accounts.update");
        if (p.StartsWith("/api/patient-link-reviews")) return Need(read ? "patient-links.read" : "patient-links.verify");
        if (p.StartsWith("/api/notifications")) return Need(read || p.EndsWith("/check-deep-link") ? "notifications.read" : "notifications.mark-read");
        if (p.StartsWith("/api/booking-settings")) return Need(read ? "booking-settings.read" : "booking-settings.manage");
        if (p.StartsWith("/api/working-schedules")) return Need(read ? "working-schedules.read" : "working-schedules.manage");
        if (p.StartsWith("/api/availability") || p == "/api/appointments/options") return ScreenEndpointRule.Any("appointments.read", "working-schedules.read", "booking-settings.read", "reception.read", "treatment.read", "treatment.templates.read", "treatment.prices.read");
        if (p.StartsWith("/api/support-requests")) return Need("appointments.support.handle");
        if (p.StartsWith("/api/appointments")) return Need(read ? "appointments.read" : p.EndsWith("/reschedule") ? "appointments.reschedule" : p.EndsWith("/cancel") ? "appointments.cancel"
            : p.EndsWith("/no-show") ? "appointments.no-show" : p.EndsWith("/attendance-confirmation") || p.Contains("/support-requests") ? "appointments.support.handle" : "appointments.create");
        if (p.StartsWith("/api/reception")) return Need(read ? "reception.read" : "reception.check-in");
        if (p.StartsWith("/api/prescriptions")) return Need(read && !p.EndsWith("/medicine-options") ? "prescriptions.read" : "prescriptions.manage");
        if (p.StartsWith("/api/clinical-documents")) return Need(read ? "documents.read" : p.EndsWith("/review") ? "documents.review" : "documents.upload");
        if (p.StartsWith("/api/finance")) {
            if (p == "/api/finance/revenue") return Need("finance.revenue.read");
            if (p.EndsWith("/examination-fee")) return read ? Need("finance.read") : ScreenEndpointRule.Any("finance.quotes.manage", "finance.payments.create");
            if (p == "/api/finance/providers") return Need(read ? "finance.costs.read" : "finance.costs.manage");
            if (p.Contains("/examination-services") || p.Contains("/services")) return Need(read ? "services.read" : "services.manage");
            if (p.EndsWith("/print")) return Need("finance.receipts.print");
            if (p.EndsWith("/receipt-sequence")) return Need("finance.receipt-sequence.manage");
            return Need(read ? "finance.read" : p.EndsWith("/confirm") ? "finance.payments.confirm" : p.EndsWith("/cancel") ? "finance.payments.cancel"
                : p.EndsWith("/payments") ? "finance.payments.create" : "finance.quotes.manage");
        }
        if (p.StartsWith("/api/visits")) {
            if (p == "/api/visits/{visitId}") return Need("reception.read");
            if (p.EndsWith("/treatment-context") || p.EndsWith("/reviews")) return Need(read ? "treatment.read" : "treatment.progress.manage");
            if (p.EndsWith("/activities/drafts") || p.EndsWith("/care-notes")) return Need("treatment.actuals.create");
            if (p.Contains("/billing")) return Need(read ? "finance.read" : p.EndsWith("/perform") ? "investigations.perform" : "finance.quotes.manage");
            if (p.Contains("/investigations/") && p.EndsWith("/result")) return Need("investigations.results.receive");
            return Need(read ? "examinations.read" : "examinations.manage");
        }
        if (p.StartsWith("/api/service-prices")) return read ? ScreenEndpointRule.Any("treatment.prices.read", "treatment.read") : Need("treatment.prices.manage");
        if (p.StartsWith("/api/commercial")) return Need(read ? "treatment.commercial.read" : "treatment.commercial.approve");
        if (p.StartsWith("/api/treatment-templates")) return Need(read ? "treatment.templates.read" : "treatment.templates.manage");
        if (p.StartsWith("/api/clinical-services")) return Need("treatment.read");
        if (p.StartsWith("/api/clinical-activities") || p.StartsWith("/api/activities")) return Need(read ? "treatment.read" : p.EndsWith("/correct") ? "treatment.actuals.update" : "treatment.actuals.create");
        if (p.StartsWith("/api/care-tasks")) return Need(read ? "care-tasks.read" : p.EndsWith("/escalations") ? "care-tasks.escalate" : "care-tasks.manage");
        if (p.StartsWith("/api/care-escalations")) return Need("care-tasks.escalate");
        if (p.StartsWith("/api/postoperative-reviews")) return Need("treatment.read");
        if (p.StartsWith("/api/reviews/")) return Need("treatment.read");
        if (p.StartsWith("/api/follow-up-instructions")) return Need(read ? "treatment.read" : "treatment.progress.manage");
        if (p.StartsWith("/api/dossiers")) {
            if (p.Contains("/payos-orders")) return Need(read ? "finance.read" : "finance.payos.create");
            if (p.Contains("/payments")) return Need(read ? "finance.read" : "finance.payments.create");
            if (p.EndsWith("/sheet-items/from-item-plan")) return Need("treatment.items.create");
            if (p.EndsWith("/plan/planned-dates")) return Need("treatment.items.update");
            if (p.EndsWith("/materials") && p.Contains("/sheet-activities/")) return Need("treatment.actuals.update");
            if (read) return Need("treatment.read");
            if (p.EndsWith("/lab-order")) return Need("treatment.lab.manage");
            if (p.Contains("/visits/")) return Need("treatment.visits.manage");
            if (p.Contains("/sheet-activities/")) return Need(method == "DELETE" ? "treatment.actuals.delete" : "treatment.actuals.update");
            if (p.Contains("/sheet-items/")) return Need(method == "DELETE" ? "treatment.items.delete" : p.EndsWith("/perform") ? "treatment.actuals.create" : "treatment.items.update");
            if (p.Contains("/sheet-drafts/")) return method == "DELETE" ? ScreenEndpointRule.Any("treatment.items.delete", "treatment.actuals.delete") : Need(p.EndsWith("/confirm-actual") || draftKind?.Equals("actual", StringComparison.OrdinalIgnoreCase) == true ? "treatment.actuals.create" : "treatment.items.create");
            if (p.Contains("/revision")) return Need("treatment.revisions.manage");
            if (p.Contains("/plan/items/")) return Need("treatment.items.update");
            if (p.Contains("/plan/")) return Need("treatment.plan.manage");
            if (p.Contains("/steps") || p.Contains("/warranty/")) return Need("treatment.progress.manage");
            return Need(method == "DELETE" ? "treatment.dossiers.delete" : "treatment.dossiers.update");
        }
        if (p.StartsWith("/api/patients/")) {
            if (p.EndsWith("/next-visit-candidates")) return Need("treatment.read");
            if (p.EndsWith("/dossiers/financial-context")) return Need("finance.read");
            if (p.Contains("/dossiers")) return Need(read ? "treatment.read" : "treatment.dossiers.create");
            if (p.EndsWith("/examinations")) return Need("examinations.read");
            if (p.EndsWith("/document-contexts")) return Need("documents.read");
            if (p.EndsWith("/appointments")) return Need("appointments.read");
            return Need(read || p.EndsWith("/duplicates") ? "patients.read" : p.EndsWith("/archive") || p.EndsWith("/restore") ? "patients.archive" : "patients.update");
        }
        if (p == "/api/patients") return Need(read ? "patients.read" : "patients.create");
        return null;
    }
}

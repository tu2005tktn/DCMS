using DCMS.Domain.Features.Accounts;

namespace DCMS.Application.Features.Settings;

public static class SettingsGrants
{
    public static readonly string[] CatalogKinds = ["diagnoses", "patient-sources", "occupations", "intake-history", "care-types", "expense-categories"];
    public static readonly string[] CatalogLabels = ["Chẩn đoán", "Nguồn khách hàng", "Nghề nghiệp", "Tiền sử hành chính", "Loại CSKH", "Loại chi"];
    public static readonly GrantDefinition[] Definitions = [
        new("settings.read", "Mở Thiết lập hệ thống", null, null),
        new("settings.general.read", "Xem cấu hình chung", null, "settings.read"),
        new("settings.general.manage", "Sửa cấu hình chung", null, "settings.general.read"),
        new("settings.audit.read", "Xem lịch sử thiết lập", null, "settings.read"),
        new("access-groups.read", "Xem nhóm quyền", AccountRoles.Admin, "settings.read"),
        new("access-groups.manage", "Quản lý nhóm quyền", AccountRoles.Admin, "access-groups.read"),
        new("staff.access.assign", "Gán nhóm cho nhân viên", AccountRoles.Admin, "access-groups.read"),
        new("accounts.manage", "Quản lý tài khoản", AccountRoles.Admin, null),
        new("patients.read", "Xem bệnh nhân", null, null),
        new("patients.manage", "Sửa hành chính bệnh nhân", AccountRoles.ClinicAssistant, "patients.read"),
        new("patient-links.verify", "Xác minh liên kết", AccountRoles.ClinicAssistant, "patients.read"),
        new("reception.read", "Xem tiếp nhận", null, null),
        new("reception.check-in", "Check-in", AccountRoles.ClinicAssistant, "reception.read"),
        new("appointments.support.handle", "Xử lý yêu cầu lịch hẹn", null, null),
        new("examinations.read", "Xem khám lâm sàng", null, null),
        new("examinations.manage", "Ghi và xác nhận khám", AccountRoles.Dentist, "examinations.read"),
        new("finance.read", "Xem phí và thanh toán", null, null),
        new("finance.quotes.manage", "Lập phí cận lâm sàng", null, "finance.read"),
        new("investigations.perform", "Xác nhận thực hiện cận lâm sàng", null, "finance.read"),
        new("finance.payments.create", "Lập phiếu thu", null, "finance.read"),
        new("finance.payments.confirm", "Xác nhận chuyển khoản", null, "finance.read"),
        new("finance.costs.read", "Xem chi phí thuê ngoài", null, "finance.read"),
        new("finance.costs.manage", "Ghi chi phí thuê ngoài", null, "finance.costs.read"),
        new("services.read", "Xem dịch vụ cận lâm sàng", null, null),
        new("services.manage", "Quản lý dịch vụ cận lâm sàng", null, "services.read"),
        .. CatalogKinds.SelectMany(kind => new[] {
            new GrantDefinition($"catalog.{kind}.read", $"{CatalogLabels[Array.IndexOf(CatalogKinds,kind)]}: Xem", null, "settings.read"),
            new GrantDefinition($"catalog.{kind}.create", $"{CatalogLabels[Array.IndexOf(CatalogKinds,kind)]}: Thêm", null, $"catalog.{kind}.read"),
            new GrantDefinition($"catalog.{kind}.update", $"{CatalogLabels[Array.IndexOf(CatalogKinds,kind)]}: Sửa", null, $"catalog.{kind}.read"),
            new GrantDefinition($"catalog.{kind}.deactivate", $"{CatalogLabels[Array.IndexOf(CatalogKinds,kind)]}: Ngừng dùng / khôi phục", null, $"catalog.{kind}.read")
        })
    ];
    public static bool RoleAllows(string permission, string[] roles)
    {
        var definition = Definitions.SingleOrDefault(x => x.Key == permission);
        if (definition == null) return false;
        if (roles.Contains(AccountRoles.ClinicOwner) && IsSettings(permission)) return true;
        if (permission.StartsWith("finance.") || permission == "investigations.perform" || permission.StartsWith("services.")) {
            if (roles.Contains(AccountRoles.ClinicOwner)) return true;
            if (permission.StartsWith("finance.costs.")) return false;
            if (permission.StartsWith("finance.payments.")) return roles.Contains(AccountRoles.ClinicAssistant);
            if (permission == "finance.quotes.manage") return roles.Contains(AccountRoles.Dentist);
            if (permission.StartsWith("services.")) return roles.Contains(AccountRoles.Dentist) || roles.Contains(AccountRoles.Admin);
            return roles.Contains(AccountRoles.Dentist) || roles.Contains(AccountRoles.ClinicAssistant);
        }
        if (definition.RequiredRole != null && !roles.Contains(definition.RequiredRole)) return false;
        if (permission is "patients.read" or "reception.read" or "examinations.read")
            return roles.Contains(AccountRoles.Dentist) || roles.Contains(AccountRoles.ClinicAssistant);
        return true;
    }
    public static bool IsSettings(string key) => key.StartsWith("settings.") || key.StartsWith("catalog.") || key.StartsWith("access-groups.") || key == "staff.access.assign";
    public static string[] Legacy(string[] roles) => Definitions.Where(x => !IsSettings(x.Key) && RoleAllows(x.Key, roles)).Select(x => x.Key).ToArray();
}

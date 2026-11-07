using DCMS.Domain.Features.Accounts;

namespace DCMS.Application.Features.Settings;

// Screen/action grants complement the feature services' clinical, status and membership checks.
public static class ScreenGrants
{
    public static readonly string[] SupplyKinds = ["materials", "providers", "lab-work-types", "medicines"];
    public static readonly string[] SupplyLabels = ["Dụng cụ / Vật liệu", "Nhà cung cấp", "Loại công việc labo", "Thuốc"];
    public static readonly GrantDefinition[] Definitions = [
        new("appointments.read", "Xem lịch hẹn và chi tiết", null, null),
        new("appointments.create", "Tạo lịch hẹn", null, "appointments.read"),
        new("appointments.reschedule", "Đổi lịch hẹn", null, "appointments.read"),
        new("appointments.cancel", "Hủy lịch hẹn", null, "appointments.read"),
        new("appointments.no-show", "Ghi nhận không đến", null, "appointments.read"),
        new("working-schedules.read", "Xem lịch làm việc", null, null),
        new("working-schedules.manage", "Thiết lập và xuất bản lịch làm việc", null, "working-schedules.read"),
        new("booking-settings.read", "Xem cài đặt đặt lịch", null, null),
        new("booking-settings.manage", "Sửa cài đặt đặt lịch", null, "booking-settings.read"),
        new("patient-links.read", "Xem xác minh liên kết hồ sơ", AccountRoles.ClinicAssistant, "patients.read"),
        new("documents.read", "Xem hình ảnh và tài liệu", null, "examinations.read"),
        new("documents.upload", "Tải tài liệu lên hồ sơ", null, "documents.read"),
        new("documents.review", "Đọc và xác nhận tài liệu lâm sàng", AccountRoles.Dentist, "documents.read"),
        new("investigations.results.receive", "Nhận kết quả cận lâm sàng", null, "examinations.read"),
        new("prescriptions.read", "Xem đơn thuốc", AccountRoles.Dentist, "examinations.read"),
        new("prescriptions.manage", "Kê và sửa đơn thuốc", AccountRoles.Dentist, "prescriptions.read"),
        new("treatment.read", "Xem hồ sơ điều trị", null, null),
        new("treatment.dossiers.create", "Tạo hồ sơ điều trị", AccountRoles.Dentist, "treatment.read"),
        new("treatment.dossiers.update", "Sửa ghi chú / trạng thái / đóng mở hồ sơ", AccountRoles.Dentist, "treatment.read"),
        new("treatment.dossiers.delete", "Xóa hồ sơ điều trị hợp lệ", AccountRoles.Dentist, "treatment.read"),
        new("treatment.plan.manage", "Lập và chốt kế hoạch điều trị", AccountRoles.Dentist, "treatment.items.update"),
        new("treatment.revisions.manage", "Điều chỉnh kế hoạch và đơn giá điều trị", AccountRoles.Dentist, "treatment.items.price"),
        new("treatment.visits.manage", "Bắt đầu / kết thúc buổi điều trị", AccountRoles.Dentist, "treatment.read"),
        new("treatment.items.create", "Thêm dòng công việc vào phiếu điều trị", AccountRoles.Dentist, "treatment.read"),
        new("treatment.items.update", "Sửa dòng công việc trong phiếu điều trị", AccountRoles.Dentist, "treatment.read"),
        new("treatment.items.price", "Sửa đơn giá dòng công việc điều trị", AccountRoles.Dentist, "treatment.items.update"),
        new("treatment.items.delete", "Xóa dòng công việc điều trị hợp lệ", AccountRoles.Dentist, "treatment.read"),
        new("treatment.actuals.create", "Thêm quá trình thực hiện điều trị", AccountRoles.Dentist, "treatment.read"),
        new("treatment.actuals.update", "Sửa quá trình thực hiện điều trị", AccountRoles.Dentist, "treatment.read"),
        new("treatment.actuals.delete", "Xóa quá trình điều trị hợp lệ", AccountRoles.Dentist, "treatment.read"),
        new("treatment.progress.manage", "Cập nhật bước điều trị / tái khám / bảo hành", AccountRoles.Dentist, "treatment.read"),
        new("treatment.templates.read", "Xem mẫu điều trị", AccountRoles.Dentist, null),
        new("treatment.templates.manage", "Tạo, sửa và phát hành mẫu điều trị", AccountRoles.Dentist, "treatment.templates.read"),
        new("treatment.prices.read", "Xem bảng giá điều trị", null, null),
        new("treatment.prices.manage", "Thiết lập giá điều trị", null, "treatment.prices.read"),
        new("treatment.commercial.read", "Xem duyệt thương mại điều trị", null, "treatment.prices.read"),
        new("treatment.commercial.approve", "Duyệt giảm giá / ngoại lệ đặt cọc", null, "treatment.commercial.read"),
        new("care-tasks.read", "Xem công việc chăm sóc khách hàng", null, null),
        new("care-tasks.manage", "Tạo, sửa và ghi nhận liên hệ CSKH", null, "care-tasks.read"),
        new("care-tasks.escalate", "Chuyển và xử lý công việc bác sĩ", null, "care-tasks.read"),
        new("notifications.read", "Xem thông báo của mình", null, null),
        new("notifications.mark-read", "Đánh dấu đã đọc thông báo", null, "notifications.read"),
        new("accounts.read", "Xem tài khoản nhân viên", AccountRoles.Admin, null),
        new("accounts.create", "Thêm tài khoản nhân viên", AccountRoles.Admin, "accounts.read"),
        new("accounts.update", "Sửa tài khoản và vai trò", AccountRoles.Admin, "accounts.read"),
        new("accounts.state", "Khóa / mở khóa tài khoản", AccountRoles.Admin, "accounts.read"),
        new("accounts.audit", "Xem nhật ký tài khoản", AccountRoles.Admin, "accounts.read"),
        new("finance.payments.cancel", "Hủy chuyển khoản đang chờ", null, "finance.read"),
        new("finance.receipts.print", "In phiếu thu", null, "finance.read"),
        new("finance.receipt-sequence.manage", "Thiết lập số phiếu thu tiếp theo", null, "finance.read"),
        new("finance.revenue.read", "Xem doanh thu đã thu", null, "finance.read"),
        new("finance.work-statistics.read", "Xem thống kê công việc / dịch vụ", null, "finance.read"),
        new("finance.payos.create", "Tạo QR chuyển khoản PayOS", null, "finance.payments.create"),
        new("treatment.lab.manage", "Lập, sửa và xóa phiếu labo", AccountRoles.Dentist, "treatment.actuals.update"),
        new("materials.purchases.read", "Xem nhập hàng dụng cụ / vật liệu", null, "catalog.materials.read"),
        new("materials.purchases.create", "Thêm lần nhập hàng", null, "materials.purchases.read"),
        new("materials.purchases.update", "Sửa lần nhập hàng và thanh toán nhà cung cấp", null, "materials.purchases.read"),
        new("materials.purchases.delete", "Xóa lần nhập hàng hợp lệ", null, "materials.purchases.read"),
        new("materials.stock.read", "Xem tồn kho theo lô", null, "catalog.materials.read"),
        new("materials.usage.read", "Xem lịch sử xuất dùng vật liệu", null, "catalog.materials.read"),
        .. SupplyKinds.SelectMany((kind, index) => new[] {
            new GrantDefinition($"catalog.{kind}.read", $"{SupplyLabels[index]}: Xem", null, "settings.read"),
            new GrantDefinition($"catalog.{kind}.create", $"{SupplyLabels[index]}: Thêm", null, $"catalog.{kind}.read"),
            new GrantDefinition($"catalog.{kind}.update", $"{SupplyLabels[index]}: Sửa", null, $"catalog.{kind}.read"),
            new GrantDefinition($"catalog.{kind}.deactivate", $"{SupplyLabels[index]}: Ngừng dùng / khôi phục", null, $"catalog.{kind}.read")
        }),
        new("catalog.medicines.export", "Thuốc: Xuất Excel", null, "catalog.medicines.read")
    ];

    public static bool? Allows(string permission, string[] roles)
    {
        bool Has(params string[] values) => roles.Any(values.Contains);
        if (permission.StartsWith("materials.")) return Has(AccountRoles.ClinicOwner, AccountRoles.Admin, AccountRoles.Dentist);
        if (permission is "finance.revenue.read" or "finance.work-statistics.read") return Has(AccountRoles.ClinicOwner, AccountRoles.Admin, AccountRoles.Dentist, AccountRoles.ClinicAssistant);
        if (permission == "finance.payos.create") return Has(AccountRoles.ClinicOwner, AccountRoles.ClinicAssistant);
        if (permission is "settings.general.read" or "settings.general.manage") return Has(AccountRoles.ClinicOwner, AccountRoles.Admin);
        if (permission.StartsWith("catalog.") && SupplyKinds.Any(kind => permission.StartsWith($"catalog.{kind}.")))
            return Has(AccountRoles.ClinicOwner, AccountRoles.Admin) || !permission.StartsWith("catalog.medicines.") && Has(AccountRoles.Dentist);
        if (permission.StartsWith("working-schedules.") || permission.StartsWith("booking-settings.") || permission.StartsWith("notifications.")) return roles.Any(AccountRoles.Staff.Contains);
        if (permission.StartsWith("appointments.") && permission != "appointments.support.handle" || permission.StartsWith("care-tasks.") || permission.StartsWith("documents."))
            return permission == "documents.review" ? Has(AccountRoles.Dentist) : Has(AccountRoles.Dentist, AccountRoles.ClinicAssistant);
        if (permission.StartsWith("treatment.prices.") || permission.StartsWith("treatment.commercial.")) return Has(AccountRoles.Admin, AccountRoles.Dentist);
        if (permission.StartsWith("treatment.")) return Has(AccountRoles.Dentist);
        if (permission == "finance.receipt-sequence.manage") return Has(AccountRoles.ClinicOwner, AccountRoles.Admin);
        if (permission == "investigations.results.receive") return Has(AccountRoles.Dentist, AccountRoles.ClinicAssistant);
        if (permission == "finance.receipts.print") return Has(AccountRoles.Dentist, AccountRoles.ClinicAssistant);
        return null;
    }

    public static bool LegacySettings(string permission, string[] roles) => roles.Contains(AccountRoles.Dentist)
        && (permission == "settings.read" || SupplyKinds.Where(kind => kind != "medicines").Any(kind => permission.StartsWith($"catalog.{kind}.")));

    public static string[] Expand(IEnumerable<string> grants)
    {
        var result = grants.ToHashSet(StringComparer.Ordinal);
        if (result.Contains("accounts.manage")) result.UnionWith(new[] { "accounts.read", "accounts.create", "accounts.update", "accounts.state", "accounts.audit" });
        if (result.Contains("patients.manage")) result.UnionWith(new[] { "patients.create", "patients.update", "patients.archive" });
        if (result.Contains("patient-links.verify")) result.Add("patient-links.read");
        return result.Order(StringComparer.Ordinal).ToArray();
    }
}

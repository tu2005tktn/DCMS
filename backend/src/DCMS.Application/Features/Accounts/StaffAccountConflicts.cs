using DCMS.Application.Common;

namespace DCMS.Application.Features.Accounts;

// Only authenticated staff administration may expose the conflicting field.
public static class StaffAccountConflicts
{
    public static SystemMessage ForField(string? field)
    {
        var label = field switch
        {
            "userName" => "Tên đăng nhập",
            "employeeCode" => "Mã nhân viên",
            "email" => "Email",
            "phone" => "Số điện thoại",
            _ => null
        };
        return label == null ? SystemMessages.Get("MSG139")
            : new SystemMessage("MSG139", $"{label} đã được sử dụng. Vui lòng nhập {label.ToLowerInvariant()} khác.") { Field = field };
    }

    public static SystemMessage ForConstraint(string? constraint) => ForField(constraint switch
    {
        "UserNameIndex" => "userName",
        "IX_EMPLOYEE_PROFILE_EmployeeCode" => "employeeCode",
        "EmailIndex" => "email",
        "IX_USER_ACCOUNT_NormalizedPhone" => "phone",
        _ => null
    });
}

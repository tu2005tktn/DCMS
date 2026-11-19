using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Settings;
using DCMS.Infrastructure.Features.Settings;
using DCMS.Infrastructure.Persistence;
using Xunit;

namespace DCMS.UnitTests.Create;

/// <summary>SettingsService.SaveGroupAsync with no group id (create a permission group). SQLite in-memory database.</summary>
public sealed class AccessGroupCreateTests
{
    private static readonly Guid Key = new("9c3f6e42-5d70-4183-9c94-7e20af3d4b55");
    private const string BadFormat = "Thông tin không đúng định dạng.";

    private static async Task<(DcmsDbContext Db, SettingsService Service, int Admin)> ArrangeAsync()
    {
        var db = CreateTestKit.NewSqliteDb();
        var actor = await CreateTestKit.SeedStaffAsync(db, "admin.test", "Admin", 1, member: false);
        db.Set<AccessGroup>().Add(new() { Code = "BS-CHINH", Name = "Main dentist group", BaseRole = "Dentist" });
        await db.SaveChangesAsync();
        return (db, new SettingsService(db, new CreateTestKit.AllowAll(), CreateTestKit.NewRoleUserManager(db), TimeProvider.System), actor.Id);
    }

    private static GroupCommand Cmd(string code = "BS-MOI", string name = "Nhóm bác sĩ mới", string role = "Dentist",
        string[]? grants = null, string? description = null, Guid? key = null) =>
        new(key ?? Key, new AccessGroupValue(code, name, role, grants ?? [], description));

    public static TheoryData<string, GroupCommand, int, string> Rejected => new()
    {
        { "UT-GRP-01 Mã nhóm để trống", Cmd(code: ""), 400, BadFormat },
        { "UT-GRP-02 Mã nhóm 41 ký tự", Cmd(code: new string('A', 41)), 400, BadFormat },
        { "UT-GRP-03 Tên nhóm để trống", Cmd(name: ""), 400, BadFormat },
        { "UT-GRP-04 Tên nhóm 161 ký tự", Cmd(name: new string('A', 161)), 400, BadFormat },
        { "UT-GRP-05 Mô tả 1001 ký tự", Cmd(description: new string('a', 1001)), 400, BadFormat },
        { "UT-GRP-06 Vai trò gốc không hợp lệ (Patient)", Cmd(role: "Patient"), 400, BadFormat },
        { "UT-GRP-07 Quyền không có trong hệ thống", Cmd(grants: ["not.a.permission"]), 400, BadFormat },
        { "UT-GRP-08 Danh sách quyền bị lặp", Cmd(grants: ["branches.read", "branches.read"]), 400, BadFormat },
        { "UT-GRP-09 Thiếu mã yêu cầu (RequestKey rỗng)", Cmd(key: Guid.Empty), 400, BadFormat },
        { "UT-GRP-10 Mã nhóm đã tồn tại (viết hoa/thường khác nhau)", Cmd(code: "bs-chinh"), 409, "Mã code/name đã tồn tại. Vui lòng chọn mã khác." },
        { "UT-GRP-11 Tên nhóm đã tồn tại (viết hoa/thường khác nhau)", Cmd(name: "main DENTIST group"), 409, "Mã code/name đã tồn tại. Vui lòng chọn mã khác." },
    };

    [Theory, MemberData(nameof(Rejected))]
    public async Task CreateGroup_rejects_invalid_input(string caseName, GroupCommand command, int status, string message)
    {
        var (db, service, admin) = await ArrangeAsync();
        using var _ = db;

        var error = await CreateTestKit.ErrorOf(() => service.SaveGroupAsync(admin, null, command, default));

        Assert.Equal(status, error.Status);
        Assert.Equal(message, error.Message);
    }
}

using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Settings;
using DCMS.Infrastructure.Persistence;
using Xunit;

namespace DCMS.UnitTests.Create;

/// <summary>BranchManagementService.SaveBranchAsync with no branch id (create a branch). SQLite in-memory database.</summary>
public sealed class BranchCreateTests
{
    private static readonly Guid Key = new("7a1d4c20-3b5e-4f61-9a72-5c0e8d1b2f33");
    private const string Duplicate = "Mã chi nhánh đã tồn tại. Vui lòng chọn mã khác.";

    private static async Task<(DcmsDbContext Db, BranchManagementService Service, int Admin)> ArrangeAsync()
    {
        var db = CreateTestKit.NewSqliteDb();
        var actor = await CreateTestKit.SeedStaffAsync(db, "admin.test", "Admin", 1, member: false); // branch 1 has no code, shown as BR-01
        db.Set<SchedulingBranch>().Add(new() { Id = 2, Name = "Chi nhánh Cầu Giấy", Code = "HN01" });
        await db.SaveChangesAsync();
        return (db, new BranchManagementService(db, new CreateTestKit.AllowAll(), null!, TimeProvider.System), actor.Id);
    }

    private static SaveBranchCommand Cmd(string code = "HN02", string name = "Chi nhánh Đống Đa", string? address = null,
        string? phone = null, string? email = null, int version = 0, Guid? key = null) =>
        new(key ?? Key, version, code, name, address, phone, email);

    public static TheoryData<string, SaveBranchCommand, int, string> Rejected => new()
    {
        { "UT-BR-01 Mã chi nhánh để trống", Cmd(code: ""), 400, "Vui lòng nhập Mã chi nhánh." },
        { "UT-BR-02 Mã chi nhánh có ký tự đặc biệt (HN 02!)", Cmd(code: "HN 02!"), 400, "Mã chi nhánh không đúng định dạng." },
        { "UT-BR-03 Mã chi nhánh 41 ký tự", Cmd(code: new string('A', 41)), 400, "Mã chi nhánh không đúng định dạng." },
        { "UT-BR-04 Tên chi nhánh để trống", Cmd(name: ""), 400, "Vui lòng nhập Tên chi nhánh." },
        { "UT-BR-05 Tên chi nhánh 161 ký tự", Cmd(name: new string('A', 161)), 400, "Tên chi nhánh không đúng định dạng." },
        { "UT-BR-06 Số điện thoại có chữ (09abc)", Cmd(phone: "09abc"), 400, "Số điện thoại không đúng định dạng." },
        { "UT-BR-07 Email sai định dạng", Cmd(email: "abc.com"), 400, "Email không đúng định dạng." },
        { "UT-BR-08 Địa chỉ 1001 ký tự", Cmd(address: new string('a', 1001)), 400, "Địa chỉ không đúng định dạng." },
        { "UT-BR-09 Thiếu mã yêu cầu (RequestKey rỗng)", Cmd(key: Guid.Empty), 400, "Thông tin không đúng định dạng." },
        { "UT-BR-10 Mã chi nhánh đã tồn tại (viết hoa/thường khác nhau)", Cmd(code: "hn01"), 409, Duplicate },
        { "UT-BR-11 Mã trùng mã tự sinh của chi nhánh chưa có mã (BR-01)", Cmd(code: "br-01"), 409, Duplicate },
        { "UT-BR-12 Tạo mới nhưng ExpectedVersion khác 0", Cmd(version: 3), 409,
            "Dữ liệu đã được cập nhật bởi thao tác khác. Vui lòng tải lại và kiểm tra trước khi xác nhận." },
    };

    [Theory, MemberData(nameof(Rejected))]
    public async Task CreateBranch_rejects_invalid_input(string caseName, SaveBranchCommand command, int status, string message)
    {
        var (db, service, admin) = await ArrangeAsync();
        using var _ = db;

        var error = await CreateTestKit.ErrorOf(() => service.SaveBranchAsync(admin, null, command, default));

        Assert.Equal(status, error.Status);
        Assert.Equal(message, error.Message);
    }
}

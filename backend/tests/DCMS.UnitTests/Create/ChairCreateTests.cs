using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Settings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Create;

/// <summary>BranchManagementService.SaveChairAsync with no chair id (add a chair to a branch). SQLite in-memory database. A duplicate chair code is not covered here: that check uses a query SQLite cannot translate (needs PostgreSQL).</summary>
public sealed class ChairCreateTests
{
    private static readonly Guid Key = new("8b2e5d31-4c6f-4072-8b83-6d1f9e2c3a44");

    private static async Task<(DcmsDbContext Db, BranchManagementService Service, int Admin)> ArrangeAsync()
    {
        var db = CreateTestKit.NewSqliteDb();
        var actor = await CreateTestKit.SeedStaffAsync(db, "admin.test", "Admin", 1, member: false);
        var branch = await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == 1);
        branch.Chairs.Add(new() { Id = 1, BranchId = 1, Name = "Ghế số 1", Code = "G01" });
        db.Set<SchedulingBranch>().Add(new() { Id = 2, Name = "Chi nhánh đã ngừng", Active = false });
        await db.SaveChangesAsync();
        return (db, new BranchManagementService(db, new CreateTestKit.AllowAll(), null!, TimeProvider.System), actor.Id);
    }

    private static SaveChairCommand Cmd(string code = "G02", string name = "Ghế số 2", int version = 0, Guid? key = null) =>
        new(key ?? Key, version, code, name, null, null);

    public static TheoryData<string, int, SaveChairCommand, int, string> Rejected => new()
    {
        { "UT-CH-01 Mã ghế để trống", 1, Cmd(code: ""), 400, "Vui lòng nhập Mã ghế." },
        { "UT-CH-02 Mã ghế có ký tự đặc biệt (G 02!)", 1, Cmd(code: "G 02!"), 400, "Mã ghế không đúng định dạng." },
        { "UT-CH-03 Tên ghế để trống", 1, Cmd(name: ""), 400, "Vui lòng nhập Tên ghế." },
        { "UT-CH-04 Tên ghế 161 ký tự", 1, Cmd(name: new string('A', 161)), 400, "Tên ghế không đúng định dạng." },
        { "UT-CH-05 Thiếu mã yêu cầu (RequestKey rỗng)", 1, Cmd(key: Guid.Empty), 400, "Thông tin không đúng định dạng." },
        { "UT-CH-06 Chi nhánh không tồn tại", 99, Cmd(), 404, "Không tìm thấy dữ liệu được yêu cầu. Vui lòng tải lại danh sách." },
        { "UT-CH-07 Tạo mới nhưng ExpectedVersion khác 0", 1, Cmd(version: 2), 409,
            "Dữ liệu đã được cập nhật bởi thao tác khác. Vui lòng tải lại và kiểm tra trước khi xác nhận." },
        { "UT-CH-08 Thêm ghế vào chi nhánh đã ngừng hoạt động", 2, Cmd(), 409,
            "Danh mục đã được sử dụng. Bạn có thể ngừng áp dụng cho giao dịch mới." },
    };

    [Theory, MemberData(nameof(Rejected))]
    public async Task CreateChair_rejects_invalid_input(string caseName, int branchId, SaveChairCommand command, int status, string message)
    {
        var (db, service, admin) = await ArrangeAsync();
        using var _ = db;

        var error = await CreateTestKit.ErrorOf(() => service.SaveChairAsync(admin, branchId, null, command, default));

        Assert.Equal(status, error.Status);
        Assert.Equal(message, error.Message);
    }
}

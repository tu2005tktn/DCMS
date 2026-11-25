using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Features.SystemSettings;
using Xunit;

namespace DCMS.UnitTests.Create;

/// <summary>
/// LabWorkTypeCatalog.CreateAsync ("Loại công việc labo" in system settings, button "Thêm mới").
/// Runs on SQLite: the command takes a Postgres advisory lock, replaced here by a no-op SQL function.
/// </summary>
public sealed class LabWorkTypeCreateTests
{
    private static readonly Guid Key = new("5b0c2b7e-1d3a-4c8e-9f4a-2a6d7e8f9a10");

    private static LabWorkTypeInput Input(string? code = "LW-01", string? name = "Mão răng sứ", string? unit = "Tooth",
        long? price = 1500000, string? warranty = null) => new(code, name, null, unit, price, warranty);

    public static TheoryData<string, LabWorkTypeInput, int, string> Rejected => new()
    {
        { "UT-LWT-01 Mã để trống", Input(code: ""), 400, "Vui lòng nhập Mã." },
        { "UT-LWT-02 Mã 41 ký tự", Input(code: new string('A', 41)), 400, "Mã không được vượt quá 40 ký tự." },
        { "UT-LWT-03 Mã có khoảng trắng (LW 01)", Input(code: "LW 01"), 400, "Mã không đúng định dạng." },
        { "UT-LWT-04 Tên để trống", Input(name: ""), 400, "Vui lòng nhập Tên." },
        { "UT-LWT-05 Tên 161 ký tự", Input(name: new string('A', 161)), 400, "Tên không được vượt quá 160 ký tự." },
        { "UT-LWT-06 Chưa chọn Đơn vị", Input(unit: null), 400, "Đơn vị không đúng định dạng." },
        { "UT-LWT-07 Giá tham khảo âm (-1)", Input(price: -1), 400,
            "Giá dịch vụ không được âm. Giảm giá, voucher hoặc khấu trừ vui lòng ghi nhận bằng chương trình khuyến mãi hoặc giảm trừ có lý do." },
        { "UT-LWT-08 Giá tham khảo vượt tối đa (1.000.000.000.001)", Input(price: 1_000_000_000_001), 400, "Giá tham khảo không đúng định dạng." },
        { "UT-LWT-09 Bảo hành 1001 ký tự", Input(warranty: new string('a', 1001)), 400, "Bảo hành không được vượt quá 1000 ký tự." },
        { "UT-LWT-10 Mã đã tồn tại (EXIST-01)", Input(code: "exist-01", name: "Cầu răng sứ"), 409, "Mã danh mục đã tồn tại. Vui lòng chọn mã khác." },
        { "UT-LWT-11 Tên đã tồn tại (viết hoa/thường khác nhau)", Input(name: "mão răng toàn sứ"), 409, "Tên đã tồn tại trong danh mục. Vui lòng nhập tên khác." },
    };

    [Theory, MemberData(nameof(Rejected))]
    public async Task Create_rejects_invalid_input(string caseName, LabWorkTypeInput input, int status, string message)
    {
        using var db = CreateTestKit.NewSqliteDb();
        var admin = await CreateTestKit.SeedStaffAsync(db, "admin.test", "Admin", 1, member: false);
        db.Add(new LabWorkType { Code = "EXIST-01", Name = "Mão răng toàn sứ", Unit = "Tooth" });
        await db.SaveChangesAsync();
        var catalog = new LabWorkTypeCatalog(db, TimeProvider.System, new CreateTestKit.AllowAll());

        var error = await CreateTestKit.ErrorOf(() => catalog.CreateAsync(admin.Id, new CreateSupplyRequest<LabWorkTypeInput>(Key, input), default));

        Assert.Equal(status, error.Status);
        Assert.Equal(message, error.Message);
    }
}

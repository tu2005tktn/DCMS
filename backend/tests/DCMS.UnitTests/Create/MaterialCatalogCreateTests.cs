using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Features.SystemSettings;
using Xunit;

namespace DCMS.UnitTests.Create;

/// <summary>SystemCatalogService.CreateCategoryAsync / CreateMaterialAsync (material groups and materials in system settings).</summary>
public sealed class MaterialCatalogCreateTests
{
    private const string BadFormat = "Thông tin không đúng định dạng.";
    private const string Duplicate = "Tên đã tồn tại. Vui lòng nhập tên khác.";
    private static readonly Guid Key = new("5b0c2b7e-1d3a-4c8e-9f4a-2a6d7e8f9a10");

    private static async Task<(DCMS.Infrastructure.Persistence.DcmsDbContext Db, SystemCatalogService Service, int Admin)> ArrangeAsync(string role = "Admin")
    {
        var db = CreateTestKit.NewSqliteDb();
        var actor = await CreateTestKit.SeedStaffAsync(db, "admin.test", role, 1, member: false);
        return (db, new SystemCatalogService(db, TimeProvider.System, new CreateTestKit.AllowAll()), actor.Id);
    }

    private static MaterialCategory Category(string name) =>
        new() { Name = name };

    public static TheoryData<string, string, string, int> CategoryRejected => new()
    {
        { "UT-CAT-01 Tên nhóm để trống", "", BadFormat, 400 },
        { "UT-CAT-02 Tên nhóm 161 ký tự", new string('A', 161), BadFormat, 400 },
        { "UT-CAT-03 Tên nhóm đã tồn tại (viết hoa/thường khác nhau)", "dụng cụ nha khoa", Duplicate, 409 },
    };

    [Theory, MemberData(nameof(CategoryRejected))]
    public async Task CreateCategory_rejects_invalid_input(string caseName, string name, string message, int status)
    {
        var (db, service, admin) = await ArrangeAsync();
        using var _ = db;
        db.MaterialCategories.Add(Category("DỤNG CỤ NHA KHOA"));
        await db.SaveChangesAsync();

        var error = await CreateTestKit.ErrorOf(() => service.CreateCategoryAsync(admin, new CreateCategoryRequest(name, Key), default));

        Assert.Equal(status, error.Status);
        Assert.Equal(message, error.Message);
    }

    public static TheoryData<string, string, string?, bool, string, int> MaterialRejected => new()
    {
        { "UT-MAT-01 Tên vật tư để trống", "", null, true, BadFormat, 400 },
        { "UT-MAT-02 Tên vật tư 161 ký tự", new string('A', 161), null, true, BadFormat, 400 },
        { "UT-MAT-03 Đơn vị sử dụng 41 ký tự", "Composite B1", new string('g', 41), true, BadFormat, 400 },
        { "UT-MAT-04 Nhóm vật tư không tồn tại", "Composite B1", null, false, "Không tìm thấy dữ liệu được yêu cầu. Vui lòng tải lại danh sách.", 404 },
        { "UT-MAT-05 Tên vật tư đã có trong nhóm (viết hoa/thường khác nhau)", "composite a2", null, true, Duplicate, 409 },
    };

    [Theory, MemberData(nameof(MaterialRejected))]
    public async Task CreateMaterial_rejects_invalid_input(string caseName, string name, string? usageUnit, bool categoryExists, string message, int status)
    {
        var (db, service, admin) = await ArrangeAsync();
        using var _ = db;
        var category = Category("Vật liệu trám");
        db.MaterialCategories.Add(category);
        db.MaterialCatalogItems.Add(new() { Name = "COMPOSITE A2", CategoryId = category.Id });
        await db.SaveChangesAsync();
        var categoryId = categoryExists ? category.Id : Guid.NewGuid();

        var error = await CreateTestKit.ErrorOf(() => service.CreateMaterialAsync(admin, new CreateMaterialRequest(name, categoryId, Key, usageUnit), default));

        Assert.Equal(status, error.Status);
        Assert.Equal(message, error.Message);
    }
}

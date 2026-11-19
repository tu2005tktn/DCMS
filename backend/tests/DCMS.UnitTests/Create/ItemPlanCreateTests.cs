using DCMS.Application.Features.Treatment;
using DCMS.Infrastructure.Features.Treatment;
using Xunit;

namespace DCMS.UnitTests.Create;

/// <summary>
/// ItemPlanService.CreateAsync (a dentist creates a reusable treatment plan: "Kế hoạch điều trị mẫu").
/// Runs on PostgreSQL (shared test schema) because the method takes pg_advisory_xact_lock and reads dentist
/// membership from a JSON collection that InMemory and SQLite cannot query.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ItemPlanCreateTests
{
    private const string Invalid = "Thông tin kế hoạch không hợp lệ.";
    private const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
    private static readonly Guid Key = new("5b0c2b7e-1d3a-4c8e-9f4a-2a6d7e8f9a10");

    private static async Task<(CreateTestKit.PostgresDb Pg, ItemPlanService Service, int Actor, int BranchId)> ArrangeAsync(string role = "Dentist", bool member = true)
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var branchId = CreateTestKit.NextId();
        var actor = await CreateTestKit.SeedStaffAsync(pg.Db, "bs." + branchId, role, branchId, member);
        return (pg, new ItemPlanService(pg.Db, CreateTestKit.NewRoleUserManager(pg.Db), TimeProvider.System, new CreateTestKit.AllowAll()), actor.Id, branchId);
    }

    [Theory]
    [InlineData("UT-IPL-01 Tên kế hoạch để trống", "")]
    [InlineData("UT-IPL-02 Tên kế hoạch chỉ gồm khoảng trắng", "   ")]
    [InlineData("UT-IPL-03 Tên kế hoạch 161 ký tự", "LONG_NAME")]
    public async Task Create_rejects_an_invalid_name(string caseName, string name)
    {
        var (pg, service, dentist, branchId) = await ArrangeAsync();
        await using var _ = pg;
        name = name == "LONG_NAME" ? new string('A', 161) : name;

        var error = await CreateTestKit.ErrorOf(() => service.CreateAsync(dentist, new ItemPlanCreate(branchId, name, Key), default));

        Assert.Equal(400, error.Status);
        Assert.Equal(Invalid, error.Message);
    }

    [Theory]
    [InlineData("UT-IPL-04 Tài khoản không phải bác sĩ (ClinicAssistant)", "ClinicAssistant", true)]
    [InlineData("UT-IPL-05 Bác sĩ chưa được gán vào chi nhánh nào", "Dentist", false)]
    public async Task Create_rejects_an_actor_who_is_not_a_dentist_of_the_branch(string caseName, string role, bool member)
    {
        var (pg, service, actor, branchId) = await ArrangeAsync(role, member);
        await using var _ = pg;

        var error = await CreateTestKit.ErrorOf(() => service.CreateAsync(actor, new ItemPlanCreate(branchId, "Điều trị cạo vôi răng", Key), default));

        Assert.Equal(403, error.Status);
        Assert.Equal(NoPermission, error.Message);
    }
}

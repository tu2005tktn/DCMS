using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Features.Treatment;
using Xunit;

namespace DCMS.UnitTests.Create;

/// <summary>
/// ItemPlanService.SaveRowAsync with no row id (button "+ Thêm hạng mục" in a treatment plan).
/// Runs on PostgreSQL (shared test schema): the method takes pg_advisory_xact_lock and reads branch membership
/// from a JSON collection.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ItemPlanRowCreateTests
{
    private const string Invalid = "Thông tin kế hoạch không hợp lệ.";

    private static ItemPlanRowInput Row(int version, string[]? teeth = null, int serviceId = 1, int? minutes = 30,
        string? note = null, decimal cost = 300000) =>
        new(null, teeth ?? ["25"], serviceId, minutes, note, null, null, cost, version, Guid.NewGuid());

    public static TheoryData<string, string[]?, int, int?, string?, decimal> Rejected => new()
    {
        { "UT-IPR-01 Mã răng không hợp lệ (99)", new[] { "99" }, 1, 30, null, 300000 },
        { "UT-IPR-02 Nhập 53 mã răng", Enumerable.Repeat("11", 53).ToArray(), 1, 30, null, 300000 },
        { "UT-IPR-03 Thời gian dự kiến = 0 phút", null, 1, 0, null, 300000 },
        { "UT-IPR-04 Thời gian dự kiến = 481 phút", null, 1, 481, null, 300000 },
        { "UT-IPR-05 Ghi chú 1001 ký tự", null, 1, 30, "LONG_NOTE", 300000 },
        { "UT-IPR-06 Chi phí âm (-1)", null, 1, 30, null, -1 },
        { "UT-IPR-07 Chi phí vượt tối đa (1.000.000.000.000)", null, 1, 30, null, 1000000000000 },
        { "UT-IPR-08 Chi phí có phần lẻ (1000,5)", null, 1, 30, null, 1000.5m },
        { "UT-IPR-09 Chưa chọn điều trị/dịch vụ", null, 0, 30, null, 300000 },
    };

    private static async Task<(CreateTestKit.PostgresDb Pg, ItemPlanService Service, int Actor, ItemPlanDto Plan)> ArrangeAsync()
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var branchId = CreateTestKit.NextId();
        var actor = await CreateTestKit.SeedStaffAsync(pg.Db, "bs." + branchId, "Dentist", branchId);
        var service = new ItemPlanService(pg.Db, CreateTestKit.NewRoleUserManager(pg.Db), TimeProvider.System, new CreateTestKit.AllowAll());
        var plan = await service.CreateAsync(actor.Id, new ItemPlanCreate(branchId, "Điều trị cạo vôi răng", Guid.NewGuid()), default);
        return (pg, service, actor.Id, plan);
    }

    [Theory, MemberData(nameof(Rejected))]
    public async Task AddRow_rejects_invalid_input(string caseName, string[]? teeth, int serviceId, int? minutes, string? note, decimal cost)
    {
        var (pg, service, dentist, plan) = await ArrangeAsync();
        await using var _ = pg;
        note = note == "LONG_NOTE" ? new string('a', 1001) : note;

        var error = await CreateTestKit.ErrorOf(() => service.SaveRowAsync(dentist, plan.Id, null,
            Row(plan.Version, teeth, serviceId, minutes, note, cost), default));

        Assert.Equal(400, error.Status);
        Assert.Equal(Invalid, error.Message);
    }

    [Fact]
    public async Task AddRow_rejects_a_plan_that_already_has_100_items()
    {
        var (pg, service, dentist, plan) = await ArrangeAsync();
        await using var _ = pg;
        var entity = pg.Db.TreatmentTemplates.Single(x => x.Id == plan.Id);
        for (var i = 1; i <= 100; i++)
            entity.Items.Add(new TreatmentTemplateItem { TemplateId = plan.Id, Position = i, ServiceId = 1, ServiceName = "Cạo vôi răng" });
        await pg.Db.SaveChangesAsync();

        var error = await CreateTestKit.ErrorOf(() => service.SaveRowAsync(dentist, plan.Id, null, Row(plan.Version), default));

        Assert.Equal(400, error.Status);
        Assert.Equal(Invalid, error.Message);
    }
}

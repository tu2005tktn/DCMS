using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Patients;
using DCMS.Infrastructure.Features.Treatment;
using Xunit;

namespace DCMS.UnitTests.Create;

/// <summary>
/// TreatmentDossierService.QuickCreateAsync (POST /api/patients/{id}/dossiers/quick, the call the "Tạo hồ sơ điều trị"
/// screen makes: the dentist only picks a branch). Runs on PostgreSQL (shared test schema) because the dentist's
/// branches come from a JSON collection that InMemory and SQLite cannot query.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TreatmentDossierCreateTests
{
    private const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
    private const string NotFound = "Không tìm thấy dữ liệu được yêu cầu. Vui lòng tải lại danh sách.";
    private static readonly Guid Key = new("5b0c2b7e-1d3a-4c8e-9f4a-2a6d7e8f9a10");

    private sealed record Arranged(CreateTestKit.PostgresDb Pg, TreatmentDossierService Service, int Actor, int PatientId, int BranchId);

    private static async Task<Arranged> ArrangeAsync(string role = "Dentist", bool member = true)
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var db = pg.Db;
        var branchId = CreateTestKit.NextId();
        var actor = await CreateTestKit.SeedStaffAsync(db, "bs." + branchId, role, branchId, member);
        var patient = new PatientProfile { PatientCode = "BN" + branchId, FullName = "Trần Thị Mai", NormalizedName = "TRAN THI MAI",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        var service = new TreatmentDossierService(db, CreateTestKit.NewRoleUserManager(db), TimeProvider.System, null!, null!, new CreateTestKit.AllowAll());
        return new(pg, service, actor.Id, patient.Id, branchId);
    }

    [Fact]
    public async Task QuickCreate_rejects_a_patient_that_does_not_exist()
    {
        var a = await ArrangeAsync();
        await using var _ = a.Pg;

        var error = await CreateTestKit.ErrorOf(() => a.Service.QuickCreateAsync(a.Actor, 999999, new QuickDossierRequest(a.BranchId, Key), default));

        Assert.Equal(404, error.Status);
        Assert.Equal(NotFound, error.Message);
    }

    [Theory]
    [InlineData("UT-DOS-02 Tài khoản không phải bác sĩ (ClinicAssistant)", "ClinicAssistant", true)]
    [InlineData("UT-DOS-03 Bác sĩ chưa được gán vào chi nhánh nào", "Dentist", false)]
    public async Task QuickCreate_rejects_an_actor_who_is_not_a_dentist_of_the_branch(string caseName, string role, bool member)
    {
        var a = await ArrangeAsync(role, member);
        await using var _ = a.Pg;

        var error = await CreateTestKit.ErrorOf(() => a.Service.QuickCreateAsync(a.Actor, a.PatientId, new QuickDossierRequest(a.BranchId, Key), default));

        Assert.Equal(403, error.Status);
        Assert.Equal(NoPermission, error.Message);
    }
}

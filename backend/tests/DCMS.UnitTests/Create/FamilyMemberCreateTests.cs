using DCMS.Application.Features.FamilyMembers;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.FamilyMembers;
using Xunit;

namespace DCMS.UnitTests.Create;

/// <summary>FamilyMemberService.CreateAsync (add a relative to a customer account).</summary>
public sealed class FamilyMemberCreateTests
{
    private static readonly Guid SampleKey = new("5b0c2b7e-1d3a-4c8e-9f4a-2a6d7e8f9a10");

    private static CreateFamilyMember Valid(string name = "Nguyễn Thị Lan", string relationship = "Mother", DateOnly? birth = null,
        Guid? key = null, string? gender = null, string? phone = null) =>
        new(name, relationship, birth ?? new DateOnly(1965, 3, 8), key ?? SampleKey, gender, phone);

    public static TheoryData<string, CreateFamilyMember, string> Rejected => new()
    {
        { "UT-FAM-01 Họ tên để trống", Valid(name: ""), "Thông tin fullName không hợp lệ." },
        { "UT-FAM-02 Quan hệ không có trong danh sách (Cousin)", Valid(relationship: "Cousin"), "Thông tin relationship không hợp lệ." },
        { "UT-FAM-03 Ngày sinh ở tương lai", Valid(birth: new DateOnly(2099, 1, 1)), "Thông tin birthDate không hợp lệ." },
        { "UT-FAM-04 Giới tính không hợp lệ", Valid(gender: "UNKNOWN"), "Thông tin gender không hợp lệ." },
        { "UT-FAM-05 Số điện thoại không đúng đầu số", Valid(gender: "MALE", phone: "0212345678"), "Thông tin contactPhone không hợp lệ." },
    };

    [Theory, MemberData(nameof(Rejected))]
    public async Task Create_rejects_invalid_input(string caseName, CreateFamilyMember command, string message)
    {
        using var db = CreateTestKit.NewDb();
        var owner = new ApplicationUser { UserName = "khach.mai", FullName = "Khách Mai", AccountType = "Public", IsActive = true };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var service = new FamilyMemberService(db, TimeProvider.System);

        var error = await CreateTestKit.ErrorOf(() => service.CreateAsync(owner.Id, command, default));

        Assert.Equal(400, error.Status);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public async Task Create_rejects_an_account_that_is_not_a_customer()
    {
        using var db = CreateTestKit.NewDb();
        var staff = new ApplicationUser { UserName = "nv.an", FullName = "Nhân viên", AccountType = "Staff", IsActive = true };
        db.Users.Add(staff);
        await db.SaveChangesAsync();

        var error = await CreateTestKit.ErrorOf(() =>
            new FamilyMemberService(db, TimeProvider.System).CreateAsync(staff.Id, Valid(), default));

        Assert.Equal(403, error.Status);
        Assert.Equal("Bạn không có quyền thực hiện thao tác này.", error.Message);
    }
}

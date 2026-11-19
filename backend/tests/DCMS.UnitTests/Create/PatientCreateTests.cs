using DCMS.Application.Features.Patients;
using DCMS.Infrastructure.Features.Patients;
using Xunit;

namespace DCMS.UnitTests.Create;

/// <summary>PatientService.CreateAsync (create a patient record). Each row = one input set and the exact message the code throws.</summary>
public sealed class PatientCreateTests
{
    private static CreatePatientRequest Valid(string? fullName = "Trần Thị Mai", DateOnly? birthDate = null, string? gender = "FEMALE",
        string? phone = "0912345678", string? address = "12 Lê Lợi, Hà Nội", string? guardianName = null,
        string? guardianRelationship = null, string? guardianPhone = null, string? note = null, Guid? requestKey = null) =>
        new(null, fullName, birthDate ?? new DateOnly(1990, 5, 20), gender, phone, address, guardianName, guardianRelationship,
            guardianPhone, note, false, RequestKey: requestKey);

    public static TheoryData<string, CreatePatientRequest, string> Rejected => new()
    {
        { "UT-PAT-01 Họ tên để trống", Valid(fullName: ""), "Vui lòng nhập Họ tên." },
        { "UT-PAT-02 Họ tên 101 ký tự", Valid(fullName: new string('A', 101)), "Họ tên không được vượt quá 100 ký tự." },
        { "UT-PAT-03 Ngày sinh ở tương lai", Valid(birthDate: new DateOnly(2099, 1, 1)), "Ngày sinh không đúng định dạng." },
        { "UT-PAT-04 Giới tính không có trong danh sách", Valid(gender: "UNKNOWN"), "Giới tính không đúng định dạng." },
        { "UT-PAT-05 Số điện thoại có chữ", Valid(phone: "09abc12345"), "Số điện thoại không đúng định dạng." },
        { "UT-PAT-06 Số điện thoại 21 ký tự", Valid(phone: new string('1', 21)), "Số điện thoại không được vượt quá 20 ký tự." },
        { "UT-PAT-07 Địa chỉ 256 ký tự", Valid(address: new string('a', 256)), "Địa chỉ không được vượt quá 255 ký tự." },
        { "UT-PAT-08 Tên người giám hộ 161 ký tự", Valid(guardianName: new string('a', 161), guardianRelationship: "Mẹ"), "Tên người giám hộ không được vượt quá 160 ký tự." },
        { "UT-PAT-09 Có quan hệ nhưng thiếu tên người giám hộ", Valid(guardianRelationship: "Mẹ"), "Vui lòng nhập tên người giám hộ." },
        { "UT-PAT-10 Có tên người giám hộ nhưng thiếu quan hệ", Valid(guardianName: "Trần Văn Bình"), "Vui lòng nhập quan hệ với bệnh nhân." },
        { "UT-PAT-11 Số điện thoại người giám hộ sai", Valid(guardianName: "Trần Văn Bình", guardianRelationship: "Cha", guardianPhone: "abc"), "Số điện thoại người giám hộ không đúng định dạng." },
        { "UT-PAT-12 Ghi chú 1001 ký tự", Valid(note: new string('a', 1001)), "Ghi chú không được vượt quá 1000 ký tự." },
    };

    [Theory, MemberData(nameof(Rejected))]
    public async Task Create_rejects_invalid_input(string caseName, CreatePatientRequest request, string message)
    {
        using var db = CreateTestKit.NewDb();
        var service = new PatientService(db, null!);

        var error = await CreateTestKit.ErrorOf(() => service.CreateAsync(1, request, default));

        Assert.Equal(400, error.Status);
        Assert.Equal(message, error.Message);
    }
}

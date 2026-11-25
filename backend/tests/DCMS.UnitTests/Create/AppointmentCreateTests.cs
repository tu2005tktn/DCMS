using DCMS.Application.Features.Appointments;
using DCMS.Infrastructure.Features.Appointments;
using Xunit;

namespace DCMS.UnitTests.Create;

/// <summary>AppointmentService.CreateAsync (a guest books an appointment from the public booking form).</summary>
public sealed class AppointmentCreateTests
{
    private const string BadFormat = "Thông tin không đúng định dạng.";
    private static readonly Guid SampleKey = new("5b0c2b7e-1d3a-4c8e-9f4a-2a6d7e8f9a10");

    private static CreateAppointmentRequest Valid(int branchId = 1, int dentistId = 1, int scheduleVersion = 1, Guid? requestKey = null,
        DateTimeOffset? startAt = null, string? bookerName = "Lê Văn Nam", string? bookerPhone = "0987654321", string? bookerEmail = "nam@gmail.com",
        string? attendeeName = "Lê Văn Nam", DateOnly? attendeeBirth = null, string? relation = "Self", string? kind = "Standard") =>
        new(branchId, new BookerContact(bookerName, bookerPhone, bookerEmail),
            new AppointmentAttendee(null, attendeeName, attendeeBirth ?? new DateOnly(1990, 1, 1)),
            relation, kind, "App", ServiceId: null, Reason: "Đau răng", DentistId: dentistId, ChairId: null,
            StartAt: startAt ?? new DateTimeOffset(2030, 1, 15, 9, 0, 0, TimeSpan.FromHours(7)), DurationMinutes: 30,
            DossierId: null, ExceptionReason: null, RequestKey: requestKey ?? SampleKey, ScheduleVersion: scheduleVersion,
            AssistantScheduleVersion: null);

    public static TheoryData<string, CreateAppointmentRequest, string> Rejected => new()
    {
        { "UT-APT-01 Chi nhánh không hợp lệ (BranchId = 0)", Valid(branchId: 0), BadFormat },
        { "UT-APT-02 Bác sĩ không hợp lệ (DentistId = 0)", Valid(dentistId: 0), BadFormat },
        { "UT-APT-03 Giờ hẹn chưa chọn (StartAt mặc định)", Valid(startAt: default(DateTimeOffset)), BadFormat },
        { "UT-APT-04 Người đặt để trống họ tên", Valid(bookerName: ""), BadFormat },
        { "UT-APT-05 Người đặt không có số điện thoại và email", Valid(bookerPhone: "", bookerEmail: ""), BadFormat },
        { "UT-APT-06 Số điện thoại người đặt sai (5 chữ số)", Valid(bookerPhone: "12345"), BadFormat },
        { "UT-APT-07 Email người đặt thiếu ký tự @", Valid(bookerEmail: "nam-gmail.com"), BadFormat },
        { "UT-APT-08 Người khám để trống họ tên", Valid(attendeeName: ""), "Vui lòng nhập thông tin người khám thực tế." },
        { "UT-APT-09 Ngày sinh người khám ở tương lai", Valid(attendeeBirth: new DateOnly(2099, 1, 1)), BadFormat },
        { "UT-APT-10 Quan hệ người khám không hợp lệ (Friend)", Valid(relation: "Friend"), BadFormat },
    };

    [Theory, MemberData(nameof(Rejected))]
    public async Task Create_rejects_invalid_input(string caseName, CreateAppointmentRequest request, string message)
    {
        using var db = CreateTestKit.NewDb();
        var service = new AppointmentService(db, null!, null!, TimeProvider.System, null!, null!);

        var error = await CreateTestKit.ErrorOf(() => service.CreateAsync(null, request, default));

        Assert.Equal(400, error.Status);
        Assert.Equal(message, error.Message);
    }
}

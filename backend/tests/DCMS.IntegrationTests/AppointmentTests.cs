using DCMS.Application.Features.Appointments;
using Xunit;

namespace DCMS.IntegrationTests;

/// <summary>Booking an appointment from the booking form: POST /api/appointments (validation of the submitted form).</summary>
[Collection(ApiCollection.Name)]
public sealed class AppointmentTests(ApiHost host)
{
    private const string Guest = "Khách chưa đăng nhập (đặt lịch công khai).";

    private CreateAppointmentRequest Body(int? branchId = null, int? dentistId = null, string? bookerName = "Lê Văn Nam", string? bookerPhone = "0987654321",
        string? bookerEmail = "nam@gmail.com", string? attendeeName = "Lê Văn Nam", DateOnly? birth = null, string? relation = "Self", DateTimeOffset? startAt = null) =>
        new(branchId ?? host.BranchA, new BookerContact(bookerName, bookerPhone, bookerEmail),
            new AppointmentAttendee(null, attendeeName, birth ?? new DateOnly(1990, 1, 1)),
            relation, "Standard", "App", host.ExamServiceA, "Đau răng", dentistId ?? host.DentistEmployeeId, null,
            startAt ?? new DateTimeOffset(2030, 1, 15, 9, 0, 0, TimeSpan.FromHours(7)), 30, null, null, Guid.NewGuid(), 1, null);

    private async Task<ApiResponse> PostAsync(CreateAppointmentRequest body)
    {
        var client = await host.ClientAsync();
        return await client.PostAsync("/api/appointments", body);
    }

    [Fact, Case("APT-01", "A branch that was not chosen is rejected", Guest, "Step 1: Call POST /api/appointments with BranchId = 0.")]
    public async Task Missing_branch_is_rejected() => Assert.Equal(400, (await PostAsync(Body(branchId: 0))).Status);

    [Fact, Case("APT-02", "A dentist that was not chosen is rejected", Guest, "Step 1: Call POST /api/appointments with DentistId = 0.")]
    public async Task Missing_dentist_is_rejected() => Assert.Equal(400, (await PostAsync(Body(dentistId: 0))).Status);

    [Fact, Case("APT-03", "A time that was not chosen is rejected", Guest, "Step 1: Call POST /api/appointments with StartAt left at its default value.")]
    public async Task Missing_time_is_rejected() => Assert.Equal(400, (await PostAsync(Body(startAt: default(DateTimeOffset)))).Status);

    [Fact, Case("APT-04", "A booker name left empty is rejected", Guest, "Step 1: Call POST /api/appointments with Booker.FullName = \"\".")]
    public async Task Empty_booker_name_is_rejected() => Assert.Equal(400, (await PostAsync(Body(bookerName: ""))).Status);

    [Fact, Case("APT-05", "A booker with neither phone nor email is rejected", Guest, "Step 1: Call POST /api/appointments with Booker phone and email both empty.")]
    public async Task Missing_contact_is_rejected() => Assert.Equal(400, (await PostAsync(Body(bookerPhone: "", bookerEmail: ""))).Status);

    [Fact, Case("APT-06", "A booker phone with 5 digits is rejected", Guest, "Step 1: Call POST /api/appointments with Booker.Phone = \"12345\".")]
    public async Task Short_phone_is_rejected() => Assert.Equal(400, (await PostAsync(Body(bookerPhone: "12345"))).Status);

    [Fact, Case("APT-07", "A booker email without @ is rejected", Guest, "Step 1: Call POST /api/appointments with Booker.Email = \"nam-gmail.com\".")]
    public async Task Email_without_at_is_rejected() => Assert.Equal(400, (await PostAsync(Body(bookerEmail: "nam-gmail.com"))).Status);

    [Fact, Case("APT-08", "An attendee name left empty is rejected", Guest, "Step 1: Call POST /api/appointments with Attendee.FullName = \"\".")]
    public async Task Empty_attendee_name_is_rejected() => Assert.Equal(400, (await PostAsync(Body(attendeeName: ""))).Status);

    [Fact, Case("APT-09", "An attendee birth date in the future is rejected", Guest, "Step 1: Call POST /api/appointments with Attendee.BirthDate = 2099-01-01.")]
    public async Task Future_birth_date_is_rejected() => Assert.Equal(400, (await PostAsync(Body(birth: new DateOnly(2099, 1, 1)))).Status);

    [Fact, Case("APT-10", "A relationship outside the allowed list is rejected", Guest, "Step 1: Call POST /api/appointments with AttendeeRelation = \"Friend\".")]
    public async Task Unknown_relation_is_rejected() => Assert.Equal(400, (await PostAsync(Body(relation: "Friend"))).Status);}


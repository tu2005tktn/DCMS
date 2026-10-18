using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Scheduling;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

// S10/S11 read API and /me appointments (UC-05). Each test uses its own branch so data never leaks between tests.
public sealed class AppointmentQueryApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed record Setup(int BranchId, int DentistId, int OtherDentistId, int ServiceId, DateOnly Date);

    private async Task<Setup> PrepareAsync(bool assistantMember = true)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var staff = await (from employee in db.Employees join user in db.Users on employee.AccountId equals user.Id
                           where user.UserName == "dentist.demo" || user.UserName == "owner.demo" || user.UserName == "assistant.demo"
                           select new { employee.Id, employee.AccountId, user.UserName }).ToListAsync();
        var dentist = staff.Single(x => x.UserName == "dentist.demo");
        var owner = staff.Single(x => x.UserName == "owner.demo");
        var branch = new SchedulingBranch { Name = "S10 fixture " + Guid.NewGuid() };
        db.Add(branch); await db.SaveChangesAsync();
        db.AddRange(new SchedulingMember { BranchId = branch.Id, EmployeeId = dentist.Id, CanDentist = true },
            new SchedulingMember { BranchId = branch.Id, EmployeeId = owner.Id, CanDentist = true });
        if (assistantMember) db.Add(new SchedulingMember { BranchId = branch.Id, EmployeeId = staff.Single(x => x.UserName == "assistant.demo").Id });
        var service = new SchedulingService { BranchId = branch.Id, Name = "Khám", PublicDurationMinutes = 30 };
        db.Add(service);
        db.Add(new SchedulingChair { BranchId = branch.Id, Name = "Ghế 1", OpenMinute = 0, CloseMinute = 1440 });
        db.Add(new SchedulingChair { BranchId = branch.Id, Name = "Ghế 2", OpenMinute = 0, CloseMinute = 1440 });
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        foreach (var employee in new[] { dentist, owner })
        {
            var week = new WorkingWeek { BranchId = branch.Id, EmployeeId = employee.Id, WeekStart = monday,
                PublishedBy = owner.AccountId, PublishedAt = DateTimeOffset.UtcNow };
            for (var day = 0; day < 7; day++)
            {
                week.Periods.Add(new WorkingPeriod { WeekId = week.Id, Day = day, StartMinute = 480, EndMinute = 1020, Kind = "Work" });
                week.Periods.Add(new WorkingPeriod { WeekId = week.Id, Day = day, StartMinute = 720, EndMinute = 780, Kind = "Break", Reason = "Nghỉ trưa" });
            }
            db.Add(week);
        }
        await db.SaveChangesAsync();
        return new(branch.Id, dentist.Id, owner.Id, service.Id, date);
    }

    private async Task<HttpClient> Login(string name)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    private async Task<(HttpClient Client, int AccountId)> NewPublic()
    {
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var now = DateTimeOffset.UtcNow;
        var user = new ApplicationUser { UserName = "pub." + Guid.NewGuid().ToString("N")[..12], FullName = "Người đặt thử",
            Email = $"{Guid.NewGuid():N}@example.invalid", AccountType = "Public", CreatedAt = now, UpdatedAt = now };
        Assert.True((await users.CreateAsync(user, fixture.Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, AccountRoles.Guest)).Succeeded);
        return (await Login(user.UserName), user.Id);
    }

    private static async Task<AvailabilitySlot> Slot(HttpClient client, Setup s, int dentistId, int duration, TimeSpan after)
    {
        var result = (await client.GetFromJsonAsync<AvailabilityResult>(
            $"/api/availability?branchId={s.BranchId}&dentistId={dentistId}&serviceId={s.ServiceId}&date={s.Date:yyyy-MM-dd}&durationMinutes={duration}"))!;
        return result.Slots.First(x => x.Start.ToOffset(TimeSpan.FromHours(7)).TimeOfDay >= after);
    }

    private static async Task<AppointmentResult> Book(HttpClient client, CreateAppointmentRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/appointments", request);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AppointmentResult>())!;
    }

    private static CreateAppointmentRequest Public(Setup s, AvailabilitySlot slot, string relation = "Self", string attendee = "Người khám") =>
        new(s.BranchId, new BookerContact(null, "0912345678", null), new AppointmentAttendee(null, attendee, null), relation,
            "Standard", "App", s.ServiceId, null, s.DentistId, null, null, slot.Start, 30, null, null, Guid.NewGuid(), slot.ScheduleVersion, null);

    private static CreateAppointmentRequest Staff(Setup s, AvailabilitySlot slot, int dentistId, int duration, int? patientId = null, string kind = "WalkIn") =>
        new(s.BranchId, new BookerContact("Lễ tân ghi nhận", "0987654321", null), new AppointmentAttendee(patientId, "Khách tại quầy", null), "Self",
            kind, "InPerson", s.ServiceId, null, dentistId, null, null, slot.Start, duration, null, null, Guid.NewGuid(), slot.ScheduleVersion, null);

    private static string Range(Setup s) => $"branchId={s.BranchId}&from={s.Date:yyyy-MM-dd}&to={s.Date:yyyy-MM-dd}";

    private static async Task AssertMessage(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
    }

    [Fact]
    public async Task AppBookingAppearsOnStaffCalendarAndCounterBooks120MinutesThroughTheSharedCommand()
    {
        var s = await PrepareAsync();
        var (app, _) = await NewPublic();
        var fromApp = await Book(app, Public(s, await Slot(app, s, s.DentistId, 30, TimeSpan.FromHours(9))));

        var assistant = await Login("assistant.demo");
        var calendar = (await assistant.GetFromJsonAsync<CalendarView>($"/api/appointments/calendar?{Range(s)}"))!;
        var item = Assert.Single(calendar.Appointments, x => x.Id == fromApp.AppointmentId);
        Assert.Equal("App", item.Source);
        Assert.Contains(calendar.Days, d => d.DentistId == s.DentistId && d.Periods.Any(p => p.Kind == "Break" && p.StartMinute == 720));

        var slot = await Slot(assistant, s, s.DentistId, 120, TimeSpan.FromHours(14));
        var walkIn = await Book(assistant, Staff(s, slot, s.DentistId, 120));
        Assert.Equal("Confirmed", walkIn.Status);
        var detail = (await assistant.GetFromJsonAsync<StaffAppointmentDetail>($"/api/appointments/{walkIn.AppointmentId}"))!;
        Assert.Equal(120, detail.Appointment.DurationMinutes);
        Assert.Equal("WalkIn", detail.Appointment.Kind);
        Assert.Equal("InPerson", detail.Appointment.Source);
        Assert.StartsWith("09", detail.BookerPhoneMasked);
        Assert.DoesNotContain("0987654321", await (await assistant.GetAsync($"/api/appointments/{walkIn.AppointmentId}")).Content.ReadAsStringAsync());

        // The lunch break and the booked 120 minutes are not offered for another 120-minute booking.
        var again = (await assistant.GetFromJsonAsync<AvailabilityResult>(
            $"/api/availability?branchId={s.BranchId}&dentistId={s.DentistId}&serviceId={s.ServiceId}&date={s.Date:yyyy-MM-dd}&durationMinutes=120"))!;
        Assert.DoesNotContain(again.Slots, x => x.Start < walkIn.EndAt && walkIn.StartAt < x.End && x.ChairId == detail.Appointment.ChairId);
        Assert.DoesNotContain(again.Slots, x => x.Start.ToOffset(TimeSpan.FromHours(7)).TimeOfDay is { Hours: >= 11 and < 13 });

        var page = (await assistant.GetFromJsonAsync<AppointmentListPage>($"/api/appointments?{Range(s)}&pageSize=20"))!;
        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task DentistSeesOnlyOwnAppointmentsAndAdminOrNonMemberIsDenied()
    {
        var s = await PrepareAsync();
        var assistant = await Login("assistant.demo");
        var mine = await Book(assistant, Staff(s, await Slot(assistant, s, s.DentistId, 30, TimeSpan.FromHours(9)), s.DentistId, 30, kind: "Standard"));
        var other = await Book(assistant, Staff(s, await Slot(assistant, s, s.OtherDentistId, 30, TimeSpan.FromHours(9)), s.OtherDentistId, 30, kind: "Standard"));

        var dentist = await Login("dentist.demo");
        foreach (var url in new[] { $"/api/appointments/calendar?{Range(s)}", $"/api/appointments/calendar?{Range(s)}&dentistId={s.OtherDentistId}" })
        {
            var view = (await dentist.GetFromJsonAsync<CalendarView>(url))!;
            Assert.True(view.OwnScheduleOnly);
            Assert.Contains(view.Appointments, x => x.Id == mine.AppointmentId);
            Assert.DoesNotContain(view.Appointments, x => x.Id == other.AppointmentId);
        }
        await AssertMessage(await dentist.GetAsync($"/api/appointments/{other.AppointmentId}"), HttpStatusCode.Forbidden, "MSG06");
        var options = (await dentist.GetFromJsonAsync<AppointmentOptions>("/api/appointments/options"))!;
        Assert.DoesNotContain(options.Dentists, x => x.BranchId == s.BranchId && x.Id == s.OtherDentistId);

        await AssertMessage(await (await Login("admin.demo")).GetAsync($"/api/appointments/calendar?{Range(s)}"), HttpStatusCode.Forbidden, "MSG06");
        var outsider = await PrepareAsync(assistantMember: false);
        await AssertMessage(await assistant.GetAsync($"/api/appointments/calendar?{Range(outsider)}"), HttpStatusCode.Forbidden, "MSG06");
        var (publicClient, _) = await NewPublic();
        await AssertMessage(await publicClient.GetAsync($"/api/appointments/{mine.AppointmentId}"), HttpStatusCode.Forbidden, "MSG06");
    }

    [Fact]
    public async Task BookerSeesOwnBookingsAndVerifiedSelfSeesOwnUntilRevoked()
    {
        var s = await PrepareAsync();
        var (booker, _) = await NewPublic();
        var relative = await Book(booker, Public(s, await Slot(booker, s, s.DentistId, 30, TimeSpan.FromHours(9)), "Relative", "Mẹ của người đặt"));
        var list = (await booker.GetFromJsonAsync<MyAppointmentPage>("/api/me/appointments?scope=UPCOMING&pageSize=10"))!;
        var card = Assert.Single(list.Items, x => x.Code == relative.Code);
        Assert.Equal("BOOKED_BY_ME", card.Access);
        Assert.Equal("RELATIVE", card.AttendeeKind);
        var detail = (await booker.GetFromJsonAsync<MyAppointmentDetail>($"/api/me/appointments/{relative.Code}"))!;
        Assert.True(detail.Eligibility.CanSelfService);

        var (stranger, _) = await NewPublic();
        await AssertMessage(await stranger.GetAsync($"/api/me/appointments/{relative.Code}"), HttpStatusCode.NotFound, "MSG102");

        // A clinic booking for a Patient becomes visible to that Patient's verified account, and disappears on revocation.
        var (linked, linkedId) = await NewPublic();
        int patientId, linkId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var now = DateTimeOffset.UtcNow;
            var patient = new PatientProfile { PatientCode = "BN-S10-" + Guid.NewGuid().ToString("N")[..8], FullName = "Bệnh nhân S10",
                NormalizedName = "BENH NHAN S10", CreatedAt = now, UpdatedAt = now };
            db.Add(patient); await db.SaveChangesAsync();
            var link = new PatientAccountLink { RequestCode = "LK-S10-" + Guid.NewGuid().ToString("N")[..8], AccountId = linkedId,
                PatientId = patient.Id, Status = PatientLinkStatus.Verified, DeclaredFullName = patient.FullName, RequestedAt = now, ReviewedAt = now };
            db.Add(link); await db.SaveChangesAsync();
            (patientId, linkId) = (patient.Id, link.Id);
        }
        var assistant = await Login("assistant.demo");
        var clinic = await Book(assistant, Staff(s, await Slot(assistant, s, s.DentistId, 30, TimeSpan.FromHours(14)), s.DentistId, 30, patientId, "Standard"));
        var own = (await linked.GetFromJsonAsync<MyAppointmentPage>("/api/me/appointments?scope=UPCOMING&pageSize=10"))!;
        Assert.Equal("MY_OWN", Assert.Single(own.Items, x => x.Code == clinic.Code).Access);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var link = await db.PatientAccountLinks.FindAsync(linkId);
            link!.Status = PatientLinkStatus.Revoked; link.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        Assert.DoesNotContain((await linked.GetFromJsonAsync<MyAppointmentPage>("/api/me/appointments?scope=UPCOMING&pageSize=10"))!.Items,
            x => x.Code == clinic.Code);
        await AssertMessage(await linked.GetAsync($"/api/me/appointments/{clinic.Code}"), HttpStatusCode.NotFound, "MSG102");
        await AssertMessage(await assistant.GetAsync("/api/me/appointments"), HttpStatusCode.Forbidden, "MSG06");
    }

    [Fact]
    public async Task InvalidRangesAndPagingUseTheSpecifiedMessage()
    {
        var s = await PrepareAsync();
        var assistant = await Login("assistant.demo");
        await AssertMessage(await assistant.GetAsync($"/api/appointments/calendar?branchId={s.BranchId}&from={s.Date:yyyy-MM-dd}&to={s.Date.AddDays(7):yyyy-MM-dd}"),
            HttpStatusCode.BadRequest, "MSG04");
        await AssertMessage(await assistant.GetAsync($"/api/appointments?{Range(s)}&pageSize=7"), HttpStatusCode.BadRequest, "MSG04");
        await AssertMessage(await assistant.GetAsync($"/api/appointments?{Range(s)}&status=Unknown"), HttpStatusCode.BadRequest, "MSG04");
        await AssertMessage(await assistant.GetAsync($"/api/appointments/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "MSG113");
        var (app, _) = await NewPublic();
        await AssertMessage(await app.GetAsync("/api/me/appointments?scope=LATER"), HttpStatusCode.BadRequest, "MSG04");
    }
}

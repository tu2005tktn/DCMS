using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

// Cách ly dữ liệu giữa chi nhánh: nhân viên chi nhánh B (dentist.b.demo, assistant.b.demo) không đọc/sửa dữ liệu chi nhánh A.
// Tài khoản và chi nhánh B do DevelopmentAccountSeeder/DevelopmentSchedulingSeeder tạo (docs/implementation/RUN-ACCOUNTS.vi.md).
public sealed class BranchIsolationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed record World(int PatientA, int BranchA, int DentistA, int ServiceA, int BranchB, Guid DossierA, Guid ActivityA, Guid LabOrderA, string PatientName, string ServiceName);

    private async Task<HttpClient> Login(string name)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.True(response.IsSuccessStatusCode, $"{name}: {await response.Content.ReadAsStringAsync()}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        return client;
    }

    private async Task SeedBranchesAsync()
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DCMS.Infrastructure.Features.Scheduling.DevelopmentSchedulingSeeder>().SeedAsync();
    }

    private async Task<World> BuildAsync(HttpClient dentistA)
    {
        await SeedBranchesAsync();
        int patient, branchA, dentist, service, branchB;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var staff = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "dentist.demo" select e).SingleAsync();
            branchB = await db.Set<SchedulingBranch>().Where(x => x.Name == "S09 - Chi nhánh B").Select(x => x.Id).SingleAsync();
            var branch = new SchedulingBranch { Name = "SYNTHETIC iso " + Guid.NewGuid().ToString("N") };
            var p = new PatientProfile { PatientCode = "ISO-" + Guid.NewGuid().ToString("N"), FullName = "SYNTHETIC cách ly " + Guid.NewGuid().ToString("N")[..6], CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            db.AddRange(branch, p); await db.SaveChangesAsync();
            var svc = new SchedulingService { BranchId = branch.Id, Name = "SYNTHETIC Trám iso " + Guid.NewGuid().ToString("N")[..6], Unit = "răng", ReferenceUnitPriceVnd = 350000, PublicBookable = false };
            var chair = new SchedulingChair { BranchId = branch.Id, Name = "SYNTHETIC chair" };
            await db.AddSchedulingDataAsync(svc, chair, new SchedulingMember { BranchId = branch.Id, EmployeeId = staff.Id, CanDentist = true });
            await db.SaveChangesAsync();
            var start = DateTimeOffset.UtcNow.AddDays(2); var id = Guid.NewGuid();
            await db.AddSchedulingDataAsync(new ResourceReservation { Id = id, Code = "RV-" + id.ToString("N"), BranchId = branch.Id, DentistId = staff.Id, ChairId = chair.Id, ServiceId = svc.Id,
                Start = start, End = start.AddHours(1), AttendeeKey = $"patient:{p.Id}", AttendeeLabel = p.FullName });
            await db.AddSchedulingDataAsync(new Appointment { Id = id, ReservationId = id, Code = "AP-" + id.ToString("N"), BranchId = branch.Id, PatientId = p.Id, DentistId = staff.Id, ChairId = chair.Id,
                ServiceId = svc.Id, BookerName = "SYNTHETIC", BookerPhone = "0900000000", AttendeeName = p.FullName, StartAt = start, EndAt = start.AddHours(1), CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            patient = p.Id; branchA = branch.Id; dentist = staff.Id; service = svc.Id;
        }
        var created = await dentistA.PostAsJsonAsync($"/api/patients/{patient}/dossiers", new CreateDossierRequest(branchA, dentist, "SYNTHETIC", null, null, Guid.NewGuid()));
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var dossier = (await created.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var draftId = Guid.NewGuid();
        var row = JsonSerializer.SerializeToElement(new { serviceId = service, dentistId = dentist, regions = new[] { "25" }, quantity = 1m, unitPriceVnd = 300000L, costVnd = 300000L,
            note = "SYNTHETIC", sourceItemId = (Guid?)null, discountType = "VND", discountValue = 0m, discountNote = (string?)null });
        var draftResponse = await dentistA.PutAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{draftId}", new SaveSheetDraftRequest("Plan", null, row, 0, Guid.NewGuid()));
        Assert.True(draftResponse.IsSuccessStatusCode, await draftResponse.Content.ReadAsStringAsync());
        var draft = (await draftResponse.Content.ReadFromJsonAsync<SheetDraftDto>())!;
        var plan = await dentistA.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{draft.Id}/confirm-plan", new ConfirmSheetPlanRequest(dossier.Version, draft.Version, null, Guid.NewGuid()));
        dossier = (await plan.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var performed = await dentistA.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-items/{Assert.Single(dossier.Items).Id}/perform", new SheetItemActionRequest(dossier.Version, Guid.NewGuid()));
        var activity = (await performed.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!.Activity;
        var url = $"/api/dossiers/{dossier.Id}/sheet-activities/{activity.Id}/lab-order";
        var ctx = (await dentistA.GetFromJsonAsync<LabOrderContext>(url))!;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var save = await dentistA.PutAsJsonAsync(url, new SaveLabOrderRequest(null, today, today.AddDays(3), null, ctx.Options.DefaultDentistId ?? ctx.Options.Dentists[0].Id, null, null,
            "Mão sứ cách ly", null, null, null, [], false, ["36"], new(), null, null, 1, 500_000, 0, null, null));
        Assert.True(save.IsSuccessStatusCode, await save.Content.ReadAsStringAsync());
        var order = (await save.Content.ReadFromJsonAsync<LabOrderDto>())!;
        return new(patient, branchA, dentist, service, branchB, dossier.Id, activity.Id, order.Id, ctx.Patient.Name, activity.ServiceNameSnapshot ?? "");
    }

    private static string Range => $"from={DateTime.UtcNow.AddDays(0):yyyy-MM-dd}&to={DateTime.UtcNow.AddDays(5):yyyy-MM-dd}";
    private static string Today => DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd");

    [Fact]
    public async Task SeededBranchBStaffBelongOnlyToBranchB()
    {
        await SeedBranchesAsync();
        using var owner = await Login("owner.demo");
        using var dentistB = await Login("dentist.b.demo"); using var assistantB = await Login("assistant.b.demo");
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var branchB = await db.Set<SchedulingBranch>().SingleAsync(x => x.Name == "S09 - Chi nhánh B");
        var rows = await (from m in db.SchedulingMembers join e in db.Employees on m.EmployeeId equals e.Id join u in db.Users on e.AccountId equals u.Id
            where u.UserName == "dentist.b.demo" || u.UserName == "assistant.b.demo" select new { u.UserName, m.BranchId, m.CanDentist }).ToListAsync();
        Assert.Equal(2, rows.Count); Assert.All(rows, r => Assert.Equal(branchB.Id, r.BranchId));
        Assert.True(rows.Single(r => r.UserName == "dentist.b.demo").CanDentist); Assert.False(rows.Single(r => r.UserName == "assistant.b.demo").CanDentist);
        Assert.Equal(HttpStatusCode.OK, (await dentistB.GetAsync($"/api/appointments/calendar?branchId={branchB.Id}&{Range}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await assistantB.GetAsync($"/api/reception/queue?branchId={branchB.Id}&date={Today}")).StatusCode);
    }

    [Fact]
    public async Task BranchBStaffCannotReadOrChangeBranchAData()
    {
        using var dentistA = await Login("dentist.demo"); using var owner = await Login("owner.demo");
        using var dentistB = await Login("dentist.b.demo"); using var assistantB = await Login("assistant.b.demo");
        var w = await BuildAsync(dentistA);

        // Lịch hẹn, tiếp nhận: chi nhánh khác bị từ chối.
        Assert.Equal(HttpStatusCode.Forbidden, (await dentistB.GetAsync($"/api/appointments/calendar?branchId={w.BranchA}&{Range}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await dentistB.GetAsync($"/api/appointments?branchId={w.BranchA}&{Range}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await assistantB.GetAsync($"/api/reception/appointments?branchId={w.BranchA}&date={Today}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await assistantB.GetAsync($"/api/reception/queue?branchId={w.BranchA}&date={Today}")).StatusCode);

        // Hồ sơ điều trị của chi nhánh A.
        Assert.Equal(HttpStatusCode.Forbidden, (await dentistB.GetAsync($"/api/dossiers/{w.DossierA}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await dentistB.GetAsync($"/api/dossiers/{w.DossierA}/lab-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await dentistB.GetAsync($"/api/dossiers/{w.DossierA}/sheet-activities/{w.ActivityA}/lab-order")).StatusCode);
        var create = await dentistB.PostAsJsonAsync($"/api/patients/{w.PatientA}/dossiers", new CreateDossierRequest(w.BranchA, w.DentistA, "SYNTHETIC lén", null, null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        // Quản lý công việc labo: danh sách không chứa phiếu chi nhánh A; đổi trạng thái/lịch sử phiếu A bị coi như không tồn tại.
        var listB = (await dentistB.GetFromJsonAsync<LabWorkPage>("/api/lab-orders?pageSize=100"))!;
        Assert.DoesNotContain(listB.Items, x => x.Id == w.LabOrderA);
        Assert.Equal(HttpStatusCode.Forbidden, (await dentistB.GetAsync($"/api/lab-orders?branchId={w.BranchA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await dentistB.GetAsync($"/api/lab-orders/{w.LabOrderA}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await dentistB.PutAsJsonAsync($"/api/lab-orders/{w.LabOrderA}/status", new ChangeLabStatusRequest("Sent", 1, null))).StatusCode);

        // Thống kê công việc không lộ việc của chi nhánh A cho nha sĩ B.
        var q = Uri.EscapeDataString(w.PatientName);
        Assert.Empty((await dentistB.GetFromJsonAsync<WorkStatisticsPage>($"/api/work-statistics?q={q}&pageSize=100"))!.Items);
        Assert.NotEmpty((await dentistA.GetFromJsonAsync<WorkStatisticsPage>($"/api/work-statistics?q={q}&pageSize=100"))!.Items);

        // Phía chi nhánh A và chủ phòng khám vẫn thấy dữ liệu của mình.
        Assert.Contains((await dentistA.GetFromJsonAsync<LabWorkPage>("/api/lab-orders?pageSize=100"))!.Items, x => x.Id == w.LabOrderA);
        Assert.Contains((await owner.GetFromJsonAsync<LabWorkPage>("/api/lab-orders?pageSize=100"))!.Items, x => x.Id == w.LabOrderA);
        Assert.Equal(HttpStatusCode.OK, (await dentistA.GetAsync($"/api/appointments/calendar?branchId={w.BranchA}&{Range}")).StatusCode);
    }

    [Fact]
    public async Task BranchAStaffCannotReachBranchBAndAssistantBHasNoTreatmentAccess()
    {
        using var dentistA = await Login("dentist.demo"); using var assistantB = await Login("assistant.b.demo");
        var w = await BuildAsync(dentistA);
        Assert.Equal(HttpStatusCode.Forbidden, (await dentistA.GetAsync($"/api/appointments/calendar?branchId={w.BranchB}&{Range}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await dentistA.GetAsync($"/api/lab-orders?branchId={w.BranchB}")).StatusCode);
        // Trợ lý chi nhánh B không phải nha sĩ nên không có quyền điều trị và labo mặc định.
        Assert.Equal(HttpStatusCode.Forbidden, (await assistantB.GetAsync($"/api/dossiers/{w.DossierA}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await assistantB.GetAsync("/api/lab-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await assistantB.PutAsJsonAsync($"/api/lab-orders/{w.LabOrderA}/status", new ChangeLabStatusRequest("Sent", 1, null))).StatusCode);
    }

    // Bệnh nhân chỉ thấy ở chi nhánh có hoạt động; chủ thấy tất cả (docs/specs/S58-BRANCHES-AND-CHAIRS.md §5).
    [Fact]
    public async Task PatientListFollowsTheBranchWhereThePatientIsActive()
    {
        using var dentistA = await Login("dentist.demo"); using var dentistB = await Login("dentist.b.demo"); using var owner = await Login("owner.demo");
        var w = await BuildAsync(dentistA);
        string code; using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var row = await db.Patients.SingleAsync(x => x.Id == w.PatientA); row.PatientCode = row.PatientCode.ToUpperInvariant(); await db.SaveChangesAsync(); code = row.PatientCode; }
        var q = Uri.EscapeDataString(code);
        async Task<int[]> Ids(HttpClient c) => (await c.GetFromJsonAsync<DCMS.Application.Features.Patients.PatientPage>($"/api/patients?keyword={q}&pageSize=100"))!.Items.Select(x => x.Id).ToArray();
        Assert.Contains(w.PatientA, await Ids(dentistA));
        Assert.DoesNotContain(w.PatientA, await Ids(dentistB));
        Assert.Contains(w.PatientA, await Ids(owner));
        Assert.Equal(HttpStatusCode.OK, (await dentistA.GetAsync($"/api/patients/{w.PatientA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await dentistB.GetAsync($"/api/patients/{w.PatientA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await dentistB.GetAsync($"/api/patients/{w.PatientA}/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/patients/{w.PatientA}")).StatusCode);
        // Chủ lọc theo chi nhánh; nhân viên thường không được lọc chi nhánh ngoài phạm vi.
        async Task<int[]> InBranch(HttpClient c, int branch) => (await c.GetFromJsonAsync<DCMS.Application.Features.Patients.PatientPage>($"/api/patients?keyword={q}&branchId={branch}&pageSize=100"))!.Items.Select(x => x.Id).ToArray();
        Assert.Contains(w.PatientA, await InBranch(owner, w.BranchA));
        Assert.DoesNotContain(w.PatientA, await InBranch(owner, w.BranchB));
        Assert.Equal(HttpStatusCode.Forbidden, (await dentistB.GetAsync($"/api/patients?branchId={w.BranchA}")).StatusCode);
    }
}

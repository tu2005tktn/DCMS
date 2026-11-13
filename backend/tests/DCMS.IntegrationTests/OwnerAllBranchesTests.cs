using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class OwnerAllBranchesTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task OwnerSeesAndReadsEveryBranch()
    {
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DCMS.Infrastructure.Features.Scheduling.DevelopmentSchedulingSeeder>().SeedAsync();
        using var owner = fixture.CreateClient();
        var r = await owner.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest("owner.demo", fixture.Password));
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await r.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        int a, b;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            a = await db.Set<SchedulingBranch>().Where(x => x.Name == "S09 - Chi nhánh thử nghiệm").Select(x => x.Id).SingleAsync();
            b = await db.Set<SchedulingBranch>().Where(x => x.Name == "S09 - Chi nhánh B").Select(x => x.Id).SingleAsync();
        }
        var from = DateTime.UtcNow.ToString("yyyy-MM-dd"); var to = DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd");
        var lines = new List<string>();
        foreach (var (name, branch) in new[] { ("A", a), ("B", b) })
            foreach (var url in new[] { $"/api/appointments/calendar?branchId={branch}&from={from}&to={to}", $"/api/appointments?branchId={branch}&from={from}&to={to}",
                $"/api/reception/appointments?branchId={branch}&date={from}", $"/api/reception/queue?branchId={branch}&date={from}",
                $"/api/lab-orders?branchId={branch}", $"/api/settings/branches/{branch}", $"/api/appointments/options", "/api/reception/options" })
                { var code = (int)(await owner.GetAsync(url)).StatusCode; lines.Add($"{name} {code} {url}"); Assert.True(code == 200, $"{name} {code} {url}"); }
        lines.Add($"ALL {(int)(await owner.GetAsync("/api/work-statistics")).StatusCode} /api/work-statistics");
        // Lịch làm việc: chủ chọn chi nhánh B rồi xem lịch của bác sĩ chi nhánh B.
        var opts = (await owner.GetFromJsonAsync<DCMS.Application.Features.Scheduling.ScheduleOptions>("/api/working-schedules/options"))!;
        Assert.Contains(opts.Branches, x => x.Id == b);
        var dentistB = opts.Employees.First(x => x.BranchId == b && x.CanDentist);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await owner.GetAsync($"/api/working-schedules/recurring?dentistId={dentistB.Id}&date={from}")).StatusCode);
        await File.WriteAllLinesAsync(Path.Combine(Path.GetTempPath(), "owner-probe.txt"), lines);
    }
}

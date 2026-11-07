using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Domain.Features.Finance;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
namespace DCMS.IntegrationTests;

public sealed class ItemPlanApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Root = "/api/treatment-templates/item-plans";
    private async Task<HttpClient> Login(string user = "dentist.demo") {
        var client = fixture.CreateClient();
        var r = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(user, fixture.Password)); r.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await r.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        return client;
    }
    private async Task<(int Branch, int Dentist, int Service)> Setup() {
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var dentist = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "dentist.demo" select e.Id).SingleAsync();
        var branch = new SchedulingBranch { Name = "SYNTHETIC item-plan " + Guid.NewGuid() }; db.Add(branch); await db.SaveChangesAsync();
        var service = new SchedulingService { BranchId = branch.Id, Name = "SYNTHETIC service", PublicBookable = false, ReferenceUnitPriceVnd = 600000 };
        db.AddRange(service, new SchedulingMember { BranchId = branch.Id, EmployeeId = dentist, CanDentist = true }); await db.SaveChangesAsync();
        return (branch.Id, dentist, service.Id);
    }
    private static async Task<ItemPlanDto> Create(HttpClient client, int branch) {
        var r = await client.PostAsJsonAsync(Root, new ItemPlanCreate(branch, "  SYNTHETIC plan  ", Guid.NewGuid())); r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<ItemPlanDto>())!;
    }
    private static ItemPlanRowInput Row((int Branch, int Dentist, int Service) s, int version) => new(null, ["25", "26", "46"], s.Service, 25, "Text only", s.Dentist, null, 600000, version, Guid.NewGuid());
    private static Task<HttpResponseMessage> Delete(HttpClient c, string path, int version, Guid? key = null) => c.SendAsync(new HttpRequestMessage(HttpMethod.Delete, path) { Content = JsonContent.Create(new ItemPlanDelete(version, key ?? Guid.NewGuid())) });
    private async Task<int[]> Counts() {
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        return [await db.Patients.CountAsync(), await db.TreatmentDossiers.CountAsync(), await db.ClinicalActivities.CountAsync(), await db.Appointments.CountAsync(),
            await db.Set<ResourceReservation>().CountAsync(), await db.Set<FinanceCharge>().CountAsync(), await db.Set<PaymentReceipt>().CountAsync()];
    }
    [Fact]
    public async Task CrudSnapshotsSoftDeleteAndNoClinicalOrFinancialSideEffects() {
        var setup = await Setup(); using var client = await Login(); var counts = await Counts();
        var plan = await Create(client, setup.Branch); Assert.Equal("SYNTHETIC plan", plan.Name); Assert.Empty(plan.Items);
        var opts = (await client.GetFromJsonAsync<ItemPlanOptions>($"{Root}/options?branchId={setup.Branch}"))!; Assert.Contains(opts.Services, x => x.Id == setup.Service);
        var add = await client.PostAsJsonAsync($"{Root}/{plan.Id}/items", Row(setup, plan.Version)); add.EnsureSuccessStatusCode(); plan = (await add.Content.ReadFromJsonAsync<ItemPlanDto>())!;
        var row = Assert.Single(plan.Items); Assert.Equal(new[] { "25", "26", "46" }, row.Teeth); Assert.Null(row.Date); Assert.Equal(25, row.EstimatedMinutes); Assert.Equal(600000, plan.TotalCostVnd);
        using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var service = await db.Set<SchedulingService>().FindAsync(setup.Service); service!.Name = "Changed catalog"; service.ReferenceUnitPriceVnd = 900000; await db.SaveChangesAsync(); }
        plan = (await client.GetFromJsonAsync<ItemPlanDto>($"{Root}/{plan.Id}"))!; Assert.Equal("SYNTHETIC service", plan.Items[0].ServiceName); Assert.Equal(600000, plan.TotalCostVnd);
        var update = await client.PutAsJsonAsync($"{Root}/{plan.Id}/items/{row.Id}", Row(setup, plan.Version) with { CostVnd = 0, EstimatedMinutes = null }); update.EnsureSuccessStatusCode(); plan = (await update.Content.ReadFromJsonAsync<ItemPlanDto>())!;
        Assert.Equal("SYNTHETIC service", plan.Items[0].ServiceName); Assert.Equal(0, plan.TotalCostVnd);
        var rename = await client.PutAsJsonAsync($"{Root}/{plan.Id}", new ItemPlanRename("Renamed", plan.Version, Guid.NewGuid())); rename.EnsureSuccessStatusCode(); plan = (await rename.Content.ReadFromJsonAsync<ItemPlanDto>())!;
        (await Delete(client, $"{Root}/{plan.Id}/items/{row.Id}", plan.Version)).EnsureSuccessStatusCode(); plan = (await client.GetFromJsonAsync<ItemPlanDto>($"{Root}/{plan.Id}"))!; Assert.Empty(plan.Items);
        var key = Guid.NewGuid(); (await Delete(client, $"{Root}/{plan.Id}", plan.Version, key)).EnsureSuccessStatusCode(); (await Delete(client, $"{Root}/{plan.Id}", plan.Version, key)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Root}/{plan.Id}")).StatusCode); Assert.Equal(counts, await Counts());
        using var check = fixture.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.NotNull((await context.Set<TreatmentTemplateItem>().FindAsync(row.Id))!.DeletedAt);
        Assert.True(await context.TreatmentProgressionEvents.AnyAsync(x => x.AggregateId == plan.Id && x.Kind == "ItemPlan.SaveRow"));
        Assert.False(context.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task StaffAndNoteCanBeOmittedAssignedAndClearedIndependently() {
        var setup = await Setup(); using var client = await Login(); var plan = await Create(client, setup.Branch);
        var command = Row(setup, plan.Version) with { DentistId = null, AssistantId = null, Note = null };
        var created = await client.PostAsJsonAsync($"{Root}/{plan.Id}/items", command); created.EnsureSuccessStatusCode();
        plan = (await created.Content.ReadFromJsonAsync<ItemPlanDto>())!; var row = Assert.Single(plan.Items);
        Assert.Null(row.DentistId); Assert.Null(row.DentistName); Assert.Null(row.AssistantId); Assert.Null(row.Note);
        plan = (await client.GetFromJsonAsync<ItemPlanDto>($"{Root}/{plan.Id}"))!; Assert.Null(plan.Items[0].DentistId);
        int assistant;
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            assistant = await (from e in db.Employees join a in db.Users on e.AccountId equals a.Id where a.UserName == "assistant.demo" select e.Id).SingleAsync();
            db.Add(new SchedulingMember { BranchId = setup.Branch, EmployeeId = assistant, CanDentist = false }); await db.SaveChangesAsync();
        }
        var assigned = await client.PutAsJsonAsync($"{Root}/{plan.Id}/items/{row.Id}", Row(setup, plan.Version) with { AssistantId = assistant }); assigned.EnsureSuccessStatusCode();
        plan = (await assigned.Content.ReadFromJsonAsync<ItemPlanDto>())!; Assert.Equal(setup.Dentist, plan.Items[0].DentistId); Assert.NotNull(plan.Items[0].DentistName);
        var assistantOnly = await client.PutAsJsonAsync($"{Root}/{plan.Id}/items/{row.Id}", Row(setup, plan.Version) with { DentistId = null, AssistantId = assistant, Note = null }); assistantOnly.EnsureSuccessStatusCode();
        plan = (await assistantOnly.Content.ReadFromJsonAsync<ItemPlanDto>())!; Assert.Null(plan.Items[0].DentistName); Assert.Equal(assistant, plan.Items[0].AssistantId);
        var cleared = await client.PutAsJsonAsync($"{Root}/{plan.Id}/items/{row.Id}", Row(setup, plan.Version) with { DentistId = null, AssistantId = null, Note = "   " }); cleared.EnsureSuccessStatusCode();
        plan = (await cleared.Content.ReadFromJsonAsync<ItemPlanDto>())!; Assert.Null(plan.Items[0].DentistId); Assert.Null(plan.Items[0].AssistantName); Assert.Null(plan.Items[0].Note);
    }
    [Fact]
    public async Task RequestReplayAndConcurrentVersionsAreAtomic() {
        var setup = await Setup(); using var client = await Login();
        var create = new ItemPlanCreate(setup.Branch, "Replay", Guid.NewGuid());
        var responses = await Task.WhenAll(client.PostAsJsonAsync(Root, create), client.PostAsJsonAsync(Root, create));
        foreach (var r in responses) r.EnsureSuccessStatusCode();
        var one = (await responses[0].Content.ReadFromJsonAsync<ItemPlanDto>())!;
        var two = (await responses[1].Content.ReadFromJsonAsync<ItemPlanDto>())!; Assert.Equal(one.Id, two.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Root, create with { Name = "Other" })).StatusCode);
        var row = Row(setup, one.Version);
        var add = await Task.WhenAll(client.PostAsJsonAsync($"{Root}/{one.Id}/items", row), client.PostAsJsonAsync($"{Root}/{one.Id}/items", row));
        foreach (var r in add) r.EnsureSuccessStatusCode(); var current = (await client.GetFromJsonAsync<ItemPlanDto>($"{Root}/{one.Id}"))!; Assert.Single(current.Items);
        var changes = await Task.WhenAll(client.PutAsJsonAsync($"{Root}/{one.Id}", new ItemPlanRename("A", current.Version, Guid.NewGuid())), client.PutAsJsonAsync($"{Root}/{one.Id}", new ItemPlanRename("B", current.Version, Guid.NewGuid())));
        Assert.Single(changes, x => x.IsSuccessStatusCode); Assert.Single(changes, x => x.StatusCode == HttpStatusCode.Conflict);
    }
    [Fact]
    public async Task ValidationBranchRolesAndReplayAuthorization() {
        var s = await Setup(); using var client = await Login(); var plan = await Create(client, s.Branch);
        foreach (var bad in new[] { Row(s, plan.Version) with { Teeth = ["99"] }, Row(s, plan.Version) with { EstimatedMinutes = 0 }, Row(s, plan.Version) with { EstimatedMinutes = 481 },
            Row(s, plan.Version) with { CostVnd = 1.5m }, Row(s, plan.Version) with { CostVnd = -1 }, Row(s, plan.Version) with { ServiceId = int.MaxValue }, Row(s, plan.Version) with { DentistId = int.MaxValue }, Row(s, plan.Version) with { Note = new string('x', 1001) } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Root}/{plan.Id}/items", bad)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Root, new ItemPlanCreate(s.Branch, "  ", Guid.NewGuid()))).StatusCode);
        using var assistant = await Login("assistant.demo"); Assert.Equal(HttpStatusCode.Forbidden, (await assistant.GetAsync($"{Root}/{plan.Id}")).StatusCode);
        using var anonymous = fixture.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{Root}/{plan.Id}")).StatusCode);
        int otherBranch;
        using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var b = new SchedulingBranch { Name = "UNASSIGNED" }; db.Add(b); await db.SaveChangesAsync(); otherBranch = b.Id; }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Root}?branchId={otherBranch}")).StatusCode);
        var key = Guid.NewGuid(); var input = Row(s, plan.Version) with { RequestKey = key }; (await client.PostAsJsonAsync($"{Root}/{plan.Id}/items", input)).EnsureSuccessStatusCode();
        using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var m = await db.Set<SchedulingMember>().FindAsync(s.Branch, s.Dentist); m!.Active = false; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{Root}/{plan.Id}/items", input)).StatusCode);
    }
    [Fact]
    public async Task LegacySourcesStaySeparateAndCannotMutateItemPlans() {
        var s = await Setup(); using var c = await Login(); var plan = await Create(c, s.Branch);
        var definition = new StepDefinition(Guid.NewGuid(), null, "Legacy step", null, 20, 30, [], null, null, null, null, null, "IncludedInItem");
        var created = await c.PostAsJsonAsync("/api/treatment-templates", new CreateTemplateRequest(s.Branch, "Legacy", [definition], Guid.NewGuid())); created.EnsureSuccessStatusCode();
        var legacy = (await created.Content.ReadFromJsonAsync<TreatmentTemplateDto>())!;
        Assert.Single((await c.GetFromJsonAsync<TemplateListItem[]>($"/api/treatment-templates?branchId={s.Branch}"))!);
        var list = (await c.GetFromJsonAsync<ItemPlanPage>($"{Root}?branchId={s.Branch}"))!; Assert.Equal(plan.Id, Assert.Single(list.Items).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/treatment-templates/{plan.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"{Root}/{legacy.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync($"/api/treatment-templates/{plan.Id}/versions", new NewTemplateVersionRequest(plan.Version, [definition], Guid.NewGuid()))).StatusCode);
        var active = await c.PostAsJsonAsync($"/api/treatment-templates/{legacy.Id}/versions/1/activate", new TemplateStateRequest(legacy.Version, Guid.NewGuid())); active.EnsureSuccessStatusCode();
    }
    [Fact]
    public async Task MigrationKeepsLegacyRowsAndIdentifiers() {
        var schema = "item_plan_migration_" + Guid.NewGuid().ToString("N");
        var conn = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { SearchPath = schema }.ConnectionString;
        await using var connection = new NpgsqlConnection(conn); await connection.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection)) await command.ExecuteNonQueryAsync();
        try {
            await using var db = new DcmsDbContext(new DbContextOptionsBuilder<DcmsDbContext>().UseNpgsql(conn).Options);
            var migrations = db.Database.GetMigrations().ToArray();
            var itemPlanIndex = Array.FindIndex(migrations, x => x.EndsWith("_TreatmentItemPlans", StringComparison.Ordinal));
            await db.GetService<IMigrator>().MigrateAsync(migrations[itemPlanIndex - 1]);
            var id = Guid.NewGuid();
            await db.Database.ExecuteSqlRawAsync("INSERT INTO \"SCHEDULING_BRANCH\" (\"Id\",\"Name\",\"Active\",\"SlotIntervalMinutes\",\"BookingSettingsVersion\") VALUES (919191,'SYNTHETIC',true,15,1)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"TREATMENT_TEMPLATE\" (\"Id\",\"BranchId\",\"Name\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ({id},919191,'Legacy preserved',7,1,{DateTimeOffset.UtcNow})");
            await db.Database.MigrateAsync();
            var old = await db.TreatmentTemplates.SingleAsync(x => x.Id == id); Assert.Equal("LegacySteps", old.DefinitionKind); Assert.Equal(7, old.Version); Assert.Null(old.DeletedAt); Assert.Equal("Legacy preserved", old.Name);
        } finally { await using var command = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", connection); await command.ExecuteNonQueryAsync(); }
    }
}

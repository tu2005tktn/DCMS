using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.FamilyMembers;
using DCMS.Domain.Features.FamilyMembers;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;
public sealed class FamilyMemberApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Route = "/api/me/family-members";
    private async Task<HttpClient> Client(string? seeded = null)
    {
        var userName = seeded ?? "family." + Guid.NewGuid().ToString("N");
        if (seeded == null)
        {
            using var scope = fixture.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = userName, FullName = "SYNTHETIC family owner", AccountType = "Public", IsActive = true };
            Assert.True((await users.CreateAsync(user, fixture.Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Guest")).Succeeded);
        }
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(userName, fixture.Password));
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        return client;
    }
    private static async Task<FamilyMemberDto> Create(HttpClient client, string name = "SYNTHETIC child")
    {
        var response = await client.PostAsJsonAsync(Route, new CreateFamilyMember(name, "Child", new DateOnly(2015, 3, 12), Guid.NewGuid(), "MALE", "0912345678"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<FamilyMemberDto>())!;
    }
    private async Task<(int Patients, int Appointments, int Links, int Reservations)> Counts()
    {
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        return (await db.Patients.CountAsync(), await db.Appointments.CountAsync(), await db.PatientAccountLinks.CountAsync(), await db.Set<ResourceReservation>().CountAsync());
    }
    private static Task<HttpResponseMessage> Delete(HttpClient client, FamilyMemberDto member, Guid key) => client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"{Route}/{member.Id}")
    { Content = JsonContent.Create(new DeleteFamilyMember(member.Version, key)) });

    [Fact]
    public async Task CrudSearchAndDeleteNeverCreateClinicalOrBookingRecords()
    {
        using var client = await Client(); var before = await Counts();
        var member = await Create(client, "  SYNTHETIC First  "); Assert.Equal("SYNTHETIC First", member.FullName);
        await Create(client, "SYNTHETIC First"); // Same name is deliberately allowed.
        var page = (await client.GetFromJsonAsync<FamilyMemberPage>(Route + "?keyword=first&page=1&pageSize=1"))!;
        Assert.Equal(2, page.Total); Assert.Single(page.Items);
        var changed = await client.PutAsJsonAsync($"{Route}/{member.Id}", new UpdateFamilyMember("SYNTHETIC Updated", "Mother", null, member.Version, Guid.NewGuid(), "FEMALE", "0912345678"));
        changed.EnsureSuccessStatusCode(); var current = (await changed.Content.ReadFromJsonAsync<FamilyMemberDto>())!;
        Assert.Equal(2, current.Version); Assert.Null(current.BirthDate);
        var key = Guid.NewGuid(); Assert.Equal(HttpStatusCode.OK, (await Delete(client, current, key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Delete(client, current, key)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route}/{member.Id}")).StatusCode);
        Assert.Equal(before, await Counts());
    }
    [Fact]
    public async Task GenderAndContactRoundTripValidateAndKeepLegacyReplay()
    {
        using var client = await Client();
        var command = new CreateFamilyMember("SYNTHETIC contact", "Child", null, Guid.NewGuid(), "female", " 0987654321 ");
        var response = await client.PostAsJsonAsync(Route, command); response.EnsureSuccessStatusCode();
        var member = (await response.Content.ReadFromJsonAsync<FamilyMemberDto>())!;
        Assert.Equal("FEMALE", member.Gender); Assert.Equal("0987654321", member.ContactPhone);
        var read = (await client.GetFromJsonAsync<FamilyMemberDto>($"{Route}/{member.Id}"))!; Assert.Equal(member, read);
        var replay = await client.PostAsJsonAsync(Route, command); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(member, await replay.Content.ReadFromJsonAsync<FamilyMemberDto>());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Route, command with { ContactPhone = "0912345678" })).StatusCode);
        foreach (var invalid in new[] { command with { Gender = "Unknown", RequestKey = Guid.NewGuid() }, command with { ContactPhone = "abc", RequestKey = Guid.NewGuid() },
            command with { ContactPhone = "1234567890", RequestKey = Guid.NewGuid() }, command with { ContactPhone = "09123456789", RequestKey = Guid.NewGuid() },
            command with { Gender = null, RequestKey = Guid.NewGuid() }, command with { Gender = " ", RequestKey = Guid.NewGuid() },
            command with { ContactPhone = null, RequestKey = Guid.NewGuid() }, command with { ContactPhone = " ", RequestKey = Guid.NewGuid() } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, invalid)).StatusCode);
        var clear = await client.PutAsJsonAsync($"{Route}/{member.Id}", new UpdateFamilyMember(member.FullName, "Child", null, member.Version, Guid.NewGuid(), null, "  "));
        Assert.Equal(HttpStatusCode.BadRequest, clear.StatusCode);
        var oldKey = Guid.NewGuid();
        var oldBody = new { fullName = "SYNTHETIC legacy", relationship = "Child", birthDate = (string?)null, requestKey = oldKey };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, oldBody)).StatusCode);
        var legacy = new FamilyMemberDto(Guid.NewGuid(), "SYNTHETIC legacy", "Child", null, 1);
        var owner = (await client.GetFromJsonAsync<CurrentUser>("/api/auth/me"))!.Id;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            // Emulate a member/receipt committed before contact fields became required.
            db.Add(new FamilyMember { Id = legacy.Id, OwnerAccountId = owner, FullName = legacy.FullName, Relationship = legacy.Relationship,
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            var oldPayload = System.Text.Json.JsonSerializer.Serialize(new { action = "Create", name = legacy.FullName, Relationship = "Child", BirthDate = (DateOnly?)null });
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(oldPayload)));
            db.Add(new FamilyMemberReceipt { OwnerAccountId = owner, RequestKey = oldKey, Fingerprint = hash, CreatedAt = DateTimeOffset.UtcNow,
                ResultJson = System.Text.Json.JsonSerializer.Serialize(new { legacy.Id, legacy.FullName, legacy.Relationship, legacy.BirthDate, legacy.Version }) });
            await db.SaveChangesAsync();
        }
        var oldReplay = await client.PostAsJsonAsync(Route, oldBody); oldReplay.EnsureSuccessStatusCode();
        Assert.Equal(legacy, await oldReplay.Content.ReadFromJsonAsync<FamilyMemberDto>());
        Assert.Equal(legacy, await client.GetFromJsonAsync<FamilyMemberDto>($"{Route}/{legacy.Id}"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{Route}/{legacy.Id}", new UpdateFamilyMember("Updated", "Child", null, 1, Guid.NewGuid()))).StatusCode);
        var completed = await client.PutAsJsonAsync($"{Route}/{legacy.Id}", new UpdateFamilyMember("Updated", "Child", null, 1, Guid.NewGuid(), "MALE", "0912345678"));
        completed.EnsureSuccessStatusCode();
    }
    [Fact]
    public async Task OwnerScopePublicRoleAndInactiveSessionsAreEnforced()
    {
        using var owner = await Client(); using var other = await Client(); using var staff = await Client("dentist.demo"); using var anonymous = fixture.CreateClient();
        var member = await Create(owner);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync(Route, new CreateFamilyMember("Name", "Child", null, Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Route}/{member.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"{Route}/{member.Id}", new UpdateFamilyMember("Hacked", "Other", null, 1, Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Delete(other, member, Guid.NewGuid())).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<FamilyMemberPage>(Route))!.Items);
        var me = (await owner.GetFromJsonAsync<CurrentUser>("/api/auth/me"))!;
        using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var account = await db.Users.SingleAsync(x => x.Id == me.Id); account.IsActive = false; await db.SaveChangesAsync(); }
        var inactive = await owner.GetAsync(Route); Assert.True(inactive.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
    }
    [Fact]
    public async Task ValidationAndRequestReplayAreStable()
    {
        using var client = await Client();
        foreach (var invalid in new[] { new CreateFamilyMember(" ", "Child", null, Guid.NewGuid()), new(new string('x', 101), "Child", null, Guid.NewGuid()),
            new("Name", "Invalid", null, Guid.NewGuid()), new("Name", "Child", new DateOnly(2100, 1, 1), Guid.NewGuid()), new("Name", "Child", null, Guid.Empty) })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, invalid)).StatusCode);
        var command = new CreateFamilyMember("SYNTHETIC retry", "Child", null, Guid.NewGuid(), "MALE", "0912345678");
        var results = await Task.WhenAll(client.PostAsJsonAsync(Route, command), client.PostAsJsonAsync(Route, command));
        var members = await Task.WhenAll(results.Select(x => x.Content.ReadFromJsonAsync<FamilyMemberDto>()));
        Assert.All(results, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK)); Assert.Equal(members[0]!.Id, members[1]!.Id);
        Assert.Single((await client.GetFromJsonAsync<FamilyMemberPage>(Route))!.Items);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Route, command with { FullName = "Other" })).StatusCode);
    }
    [Fact]
    public async Task ConcurrentEditsHaveOneWinnerAndUpdateReplayKeepsOriginalResult()
    {
        using var client = await Client(); var member = await Create(client);
        var command = new UpdateFamilyMember("First", "Child", null, member.Version, Guid.NewGuid(), "MALE", "0912345678");
        var results = await Task.WhenAll(client.PutAsJsonAsync($"{Route}/{member.Id}", command), client.PutAsJsonAsync($"{Route}/{member.Id}", command with { FullName = "Second", RequestKey = Guid.NewGuid() }));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        var current = (await client.GetFromJsonAsync<FamilyMemberDto>($"{Route}/{member.Id}"))!;
        var update = new UpdateFamilyMember("Final", "Father", null, current.Version, Guid.NewGuid(), "MALE", "0912345678");
        var saved = await client.PutAsJsonAsync($"{Route}/{member.Id}", update); saved.EnsureSuccessStatusCode();
        var final = (await saved.Content.ReadFromJsonAsync<FamilyMemberDto>())!;
        (await Delete(client, final, Guid.NewGuid())).EnsureSuccessStatusCode();
        var replay = await client.PutAsJsonAsync($"{Route}/{member.Id}", update); replay.EnsureSuccessStatusCode();
        Assert.Equal(final, await replay.Content.ReadFromJsonAsync<FamilyMemberDto>());
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.False(db.Database.HasPendingModelChanges());
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.SystemSettings;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace DCMS.IntegrationTests;

public sealed class SystemCatalogApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Categories = "/api/system-settings/material-categories";
    private const string Materials = "/api/system-settings/materials";
    private async Task<HttpClient> Login(string name = "dentist.demo")
    {
        var client = fixture.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        return client;
    }
    private static string Unique(string text) => text + Guid.NewGuid().ToString("N");
    private async Task<MaterialCategoryDto> Category(HttpClient c, string? name = null)
    {
        var r = await c.PostAsJsonAsync(Categories, new CreateCategoryRequest(name ?? Unique("SYNTHETIC group "), Guid.NewGuid()));
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync()); return (await r.Content.ReadFromJsonAsync<MaterialCategoryDto>())!;
    }
    private async Task<MaterialDto> Material(HttpClient c, Guid category, string? name = null)
    {
        var r = await c.PostAsJsonAsync(Materials, new CreateMaterialRequest(name ?? Unique("SYNTHETIC material "), category, Guid.NewGuid()));
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync()); return (await r.Content.ReadFromJsonAsync<MaterialDto>())!;
    }
    private static Task<HttpResponseMessage> Delete(HttpClient c, string path, int version, Guid? key = null) =>
        c.SendAsync(new HttpRequestMessage(HttpMethod.Delete, path) { Content = JsonContent.Create(new DeleteCatalogRequest(version, key ?? Guid.NewGuid())) });

    [Fact]
    public async Task CrudMoveAndSoftDeletePreserveAuditAndRejectNonemptyGroup()
    {
        using var c = await Login(); var a = await Category(c); var b = await Category(c); var item = await Material(c, a.Id);
        var blocked = await Delete(c, $"{Categories}/{a.Id}", a.Version);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode); Assert.Contains("CATALOG_CATEGORY_IN_USE", await blocked.Content.ReadAsStringAsync());
        var movedResponse = await c.PutAsJsonAsync($"{Materials}/{item.Id}", new UpdateMaterialRequest("SYNTHETIC renamed", b.Id, item.Version, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, movedResponse.StatusCode); var moved = (await movedResponse.Content.ReadFromJsonAsync<MaterialDto>())!;
        Assert.Equal(item.Id, moved.Id); Assert.Equal(b.Name, moved.CategoryName);
        Assert.Equal(0, (await c.GetFromJsonAsync<MaterialCategoryDto>($"{Categories}/{a.Id}"))!.ItemCount);
        Assert.Equal(1, (await c.GetFromJsonAsync<MaterialCategoryDto>($"{Categories}/{b.Id}"))!.ItemCount);
        Assert.Equal(HttpStatusCode.OK, (await Delete(c, $"{Categories}/{a.Id}", a.Version)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"{Categories}/{a.Id}")).StatusCode);
        Assert.NotEqual(a.Id, (await Category(c, a.Name)).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync(Materials, new CreateMaterialRequest("SYNTHETIC deleted parent", a.Id, Guid.NewGuid()))).StatusCode);
        var key = Guid.NewGuid(); var deleted = await Delete(c, $"{Materials}/{item.Id}", moved.Version, key);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode); Assert.Equal(HttpStatusCode.OK, (await Delete(c, $"{Materials}/{item.Id}", moved.Version, key)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"{Materials}/{item.Id}")).StatusCode);
        var reused = await Material(c, b.Id, moved.Name); Assert.NotEqual(item.Id, reused.Id);
        Assert.Equal(HttpStatusCode.OK, (await Delete(c, $"{Materials}/{reused.Id}", reused.Version)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Delete(c, $"{Categories}/{b.Id}", b.Version)).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.True((await db.MaterialCatalogItems.SingleAsync(x => x.Id == item.Id)).IsDeleted);
        Assert.Equal(3, await db.SystemCatalogChanges.CountAsync(x => x.ResourceId == item.Id));
        Assert.NotNull((await db.SystemCatalogChanges.SingleAsync(x => x.RequestKey == key)).BeforeJson);
    }

    [Fact]
    public async Task NamesAreValidatedNormalizedAndUniqueWithinCategory()
    {
        using var c = await Login(); var name = Unique("SYNTHETIC Nhóm "); var group = await Category(c, "  " + name + "  ");
        Assert.Equal(name, group.Name);
        var duplicate = await c.PostAsJsonAsync(Categories, new CreateCategoryRequest(name.ToUpperInvariant(), Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode); Assert.Contains("CATALOG_NAME_DUPLICATE", await duplicate.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Categories, new CreateCategoryRequest("   ", Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Categories, new CreateCategoryRequest(new string('x',161), Guid.NewGuid()))).StatusCode);
        var item = await Material(c, group.Id, "SYNTHETIC Composite");
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Materials, new CreateMaterialRequest("  synthetic COMPOSITE ", group.Id, Guid.NewGuid()))).StatusCode);
        var other = await Category(c); Assert.NotNull(await Material(c, other.Id, item.Name));
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync(Materials, new CreateMaterialRequest("SYNTHETIC", Guid.NewGuid(), Guid.NewGuid()))).StatusCode);
        var filtered = await c.GetFromJsonAsync<CatalogPage<MaterialDto>>($"{Materials}?categoryId={group.Id}&q=COMP&page=1&pageSize=20");
        Assert.Single(filtered!.Items); Assert.Equal(item.Id, filtered.Items[0].Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"{Categories}?page=0")).StatusCode);
    }

    [Fact]
    public async Task DentistWithoutBranchCanShareGlobalCatalogButOtherRolesAndInactiveCannot()
    {
        using var c = await Login(); var group = await Category(c);
        using (var scope = fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var doctor = new ApplicationUser { UserName = "global."+Guid.NewGuid().ToString("N"), FullName = "SYNTHETIC second dentist", AccountType = "Staff", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            Assert.True((await users.CreateAsync(doctor, fixture.Password)).Succeeded); Assert.True((await users.AddToRoleAsync(doctor,"Dentist")).Succeeded);
            var catalogDb = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            catalogDb.Employees.Add(new DCMS.Domain.Features.Accounts.EmployeeProfile { AccountId = doctor.Id, EmployeeCode = "CAT-"+Guid.NewGuid().ToString("N"), FullName = doctor.FullName });
            await catalogDb.SaveChangesAsync();
            using var second = await Login(doctor.UserName);
            Assert.Equal(group.Name, (await second.GetFromJsonAsync<MaterialCategoryDto>($"{Categories}/{group.Id}"))!.Name);
            Assert.Equal(HttpStatusCode.OK, (await second.PutAsJsonAsync($"{Categories}/{group.Id}", new UpdateCategoryRequest(Unique("SYNTHETIC shared "),group.Version,Guid.NewGuid()))).StatusCode);
            doctor.IsActive = false; await users.UpdateAsync(doctor);
            Assert.Contains((await second.GetAsync(Categories)).StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized });
        }
        using var anonymous = fixture.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Categories)).StatusCode);
        using var assistant = await Login("assistant.demo"); Assert.Equal(HttpStatusCode.Forbidden, (await assistant.GetAsync(Materials)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await assistant.PostAsJsonAsync(Categories, new CreateCategoryRequest("SYNTHETIC denied",Guid.NewGuid()))).StatusCode);
        using var admin = await Login("admin.demo"); Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(Categories)).StatusCode);
    }

    [Fact]
    public async Task RetryConflictAndConcurrentWritesDoNotDuplicate()
    {
        using var c = await Login(); var request = new CreateCategoryRequest(Unique("SYNTHETIC retry "),Guid.NewGuid());
        var results = await Task.WhenAll(c.PostAsJsonAsync(Categories,request),c.PostAsJsonAsync(Categories,request));
        Assert.All(results,r=>Assert.Equal(HttpStatusCode.Created,r.StatusCode));
        var row = (await results[0].Content.ReadFromJsonAsync<MaterialCategoryDto>())!;
        Assert.Equal(row.Id,(await results[1].Content.ReadFromJsonAsync<MaterialCategoryDto>())!.Id);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync(Categories,request with { Name=Unique("Different ") })).StatusCode);
        var concurrent = await Task.WhenAll(c.PutAsJsonAsync($"{Categories}/{row.Id}",new UpdateCategoryRequest(Unique("First "),row.Version,Guid.NewGuid())),
            c.PutAsJsonAsync($"{Categories}/{row.Id}",new UpdateCategoryRequest(Unique("Second "),row.Version,Guid.NewGuid())));
        Assert.Single(concurrent,r=>r.IsSuccessStatusCode); Assert.Single(concurrent,r=>r.StatusCode==HttpStatusCode.Conflict);
        var name = Unique("SYNTHETIC duplicate ");
        var dup = await Task.WhenAll(c.PostAsJsonAsync(Categories,new CreateCategoryRequest(name,Guid.NewGuid())),c.PostAsJsonAsync(Categories,new CreateCategoryRequest(name,Guid.NewGuid())));
        Assert.Single(dup,r=>r.IsSuccessStatusCode); Assert.Single(dup,r=>r.StatusCode==HttpStatusCode.Conflict);
        using var scope=fixture.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(1,await db.SystemCatalogChanges.CountAsync(x=>x.RequestKey==request.RequestKey));
    }

    [Fact]
    public async Task CreateOrMoveRacingGroupDeletionCannotLeaveOrphans()
    {
        using var c=await Login(); var category=await Category(c);
        var results=await Task.WhenAll(c.PostAsJsonAsync(Materials,new CreateMaterialRequest(Unique("SYNTHETIC race "),category.Id,Guid.NewGuid())),Delete(c,$"{Categories}/{category.Id}",category.Version));
        if(results[0].IsSuccessStatusCode) Assert.Equal(HttpStatusCode.Conflict,results[1].StatusCode);
        else { Assert.Equal(HttpStatusCode.NotFound,results[0].StatusCode); Assert.Equal(HttpStatusCode.OK,results[1].StatusCode); }
        var source=await Category(c); var target=await Category(c); var item=await Material(c,source.Id);
        var move=await Task.WhenAll(c.PutAsJsonAsync($"{Materials}/{item.Id}",new UpdateMaterialRequest(item.Name,target.Id,item.Version,Guid.NewGuid())),Delete(c,$"{Categories}/{target.Id}",target.Version));
        if(move[0].IsSuccessStatusCode) Assert.Equal(HttpStatusCode.Conflict,move[1].StatusCode);
        else { Assert.Equal(HttpStatusCode.NotFound,move[0].StatusCode); Assert.Equal(HttpStatusCode.OK,move[1].StatusCode); }
        using var scope=fixture.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.False(await db.MaterialCatalogItems.AnyAsync(x=>!x.IsDeleted && x.Category.IsDeleted));
    }
}

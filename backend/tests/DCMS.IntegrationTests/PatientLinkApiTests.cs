using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.PatientLinks;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class PatientLinkApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<HttpClient> Login(string name)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    // Every test uses its own public account and Patient so tests stay independent within one schema.
    private async Task<HttpClient> NewPublicAccount()
    {
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var now = DateTimeOffset.UtcNow;
        var user = new ApplicationUser
        {
            UserName = "pub." + Guid.NewGuid().ToString("N")[..12], FullName = "Tài khoản kiểm thử",
            AccountType = "Public", CreatedAt = now, UpdatedAt = now
        };
        Assert.True((await users.CreateAsync(user, fixture.Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, AccountRoles.Patient)).Succeeded);
        return await Login(user.UserName);
    }

    private async Task<PatientProfile> NewPatient(string fullName = "Phạm Thị Dung", DateOnly? birth = null, string? phone = null)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var now = DateTimeOffset.UtcNow;
        var patient = new PatientProfile
        {
            PatientCode = "BN-T-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), FullName = fullName,
            BirthDate = birth ?? new DateOnly(1992, 2, 20), Phone = phone, CreatedAt = now, UpdatedAt = now
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return patient;
    }

    private static SubmitLinkRequest Request(string fullName = "Pham Thi Dung", DateOnly? birth = null) =>
        new(fullName, birth ?? new DateOnly(1992, 2, 20), null, "Khám ngày 01/10/2026 với nha sĩ demo", true);

    private static async Task<SelfLinkChange> Submit(HttpClient client, SubmitLinkRequest? request = null)
    {
        var response = await client.PostAsJsonAsync("/api/patient-links/me/requests", request ?? Request());
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<SelfLinkChange>())!;
    }

    private static Task<HttpResponseMessage> Verify(HttpClient assistant, int id, int version, int patientId, string note = "Đối chiếu CCCD tại quầy") =>
        assistant.PostAsJsonAsync($"/api/patient-link-reviews/{id}/verify", new VerifyLinkRequest(version, patientId, note));

    private static async Task AssertMessage(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var message = (await response.Content.ReadFromJsonAsync<SystemMessage>())!;
        Assert.Equal(code, message.Code);
        // MSG02/03/04 embed the field name; the others must match the specified wording exactly.
        if (code is not ("MSG02" or "MSG03" or "MSG04")) Assert.Equal(SystemMessages.Get(code).Message, message.Message);
        else Assert.False(string.IsNullOrWhiteSpace(message.Message));
    }

    [Fact]
    public async Task SubmitCreatesPendingWithoutRecordAccessAndRepeatDoesNotDuplicate()
    {
        var client = await NewPublicAccount();
        var overview = (await client.GetFromJsonAsync<SelfLinkOverview>("/api/patient-links/me"))!;
        Assert.Equal(PatientLinkStatus.NotLinked, overview.LinkStatus);
        Assert.True(overview.CanSubmit);

        var first = await Submit(client);
        Assert.True(first.Created);
        Assert.Equal("MSG18", first.Notification.Code);
        Assert.Equal(PatientLinkStatus.Pending, first.Request.Status);
        var repeat = await Submit(client);
        Assert.False(repeat.Created);
        Assert.Equal(first.Request.Id, repeat.Request.Id);

        overview = (await client.GetFromJsonAsync<SelfLinkOverview>("/api/patient-links/me"))!;
        Assert.Equal(PatientLinkStatus.Pending, overview.LinkStatus);
        Assert.Single(overview.History);
        Assert.False(overview.CanSubmit);
        await AssertMessage(await client.GetAsync("/api/patient-links/me/record"), HttpStatusCode.Forbidden, "MSG06");
        Assert.False((await client.GetFromJsonAsync<CurrentUser>("/api/auth/me"))!.HasVerifiedPatientLink);
    }

    [Fact]
    public async Task ConcurrentSubmissionsCreateAtMostOnePending()
    {
        var client = await NewPublicAccount();
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsJsonAsync("/api/patient-links/me/requests", Request())));
        Assert.Contains(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, x => Assert.True(x.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.Conflict));
        var overview = (await client.GetFromJsonAsync<SelfLinkOverview>("/api/patient-links/me"))!;
        Assert.Single(overview.History);
    }

    [Fact]
    public async Task AnotherAccountsRequestIdIsNotFound()
    {
        var owner = await NewPublicAccount();
        var other = await NewPublicAccount();
        var request = (await Submit(owner)).Request;
        await AssertMessage(await other.GetAsync($"/api/patient-links/me/requests/{request.Id}"), HttpStatusCode.NotFound, "MSG01");
        await AssertMessage(await other.PostAsJsonAsync($"/api/patient-links/me/requests/{request.Id}/withdraw", new WithdrawLinkRequest(request.Version)), HttpStatusCode.NotFound, "MSG01");
        Assert.Empty((await other.GetFromJsonAsync<SelfLinkOverview>("/api/patient-links/me"))!.History);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/patient-links/me/requests/{request.Id}")).StatusCode);
    }

    [Fact]
    public async Task ValidationRequiresNameAndSelfConfirmation()
    {
        var client = await NewPublicAccount();
        await AssertMessage(await client.PostAsJsonAsync("/api/patient-links/me/requests", Request() with { FullName = " " }), HttpStatusCode.BadRequest, "MSG02");
        await AssertMessage(await client.PostAsJsonAsync("/api/patient-links/me/requests", Request() with { ConfirmSelf = false }), HttpStatusCode.BadRequest, "MSG02");
        await AssertMessage(await client.PostAsJsonAsync("/api/patient-links/me/requests", Request() with { MatchingInformation = new string('x', 1001) }), HttpStatusCode.BadRequest, "MSG03");
        await AssertMessage(await client.PostAsJsonAsync("/api/patient-links/me/requests", Request() with { PatientCode = "bad code!" }), HttpStatusCode.BadRequest, "MSG04");
        await AssertMessage(await client.PostAsJsonAsync("/api/patient-links/me/requests", Request() with { BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)) }), HttpStatusCode.BadRequest, "MSG04");
        Assert.Empty((await client.GetFromJsonAsync<SelfLinkOverview>("/api/patient-links/me"))!.History);
    }

    [Fact]
    public async Task OnlyPublicAccountsUseSelfApiAndOnlyAssistantsReview()
    {
        await AssertMessage(await fixture.CreateClient().GetAsync("/api/patient-links/me"), HttpStatusCode.Unauthorized, "MSG07");
        await AssertMessage(await fixture.CreateClient().GetAsync("/api/patient-link-reviews"), HttpStatusCode.Unauthorized, "MSG07");
        foreach (var staff in new[] { "admin.demo", "dentist.demo", "owner.demo", "assistant.demo" })
            await AssertMessage(await (await Login(staff)).PostAsJsonAsync("/api/patient-links/me/requests", Request()), HttpStatusCode.Forbidden, "MSG06");
        var request = (await Submit(await NewPublicAccount())).Request;
        var patient = await NewPatient();
        foreach (var name in new[] { "admin.demo", "dentist.demo", "owner.demo", "guest.demo", "patient.demo" })
        {
            var client = await Login(name);
            await AssertMessage(await client.GetAsync("/api/patient-link-reviews"), HttpStatusCode.Forbidden, "MSG06");
            await AssertMessage(await Verify(client, request.Id, request.Version, patient.Id), HttpStatusCode.Forbidden, "MSG06");
        }
        Assert.Equal(HttpStatusCode.OK, (await (await Login("dual.demo")).GetAsync("/api/patient-link-reviews")).StatusCode);
    }

    [Fact]
    public async Task VerifyGrantsOwnRecordAndRevokeBlocksTheNextReadOfTheSameSession()
    {
        var client = await NewPublicAccount();
        var patient = await NewPatient();
        var request = (await Submit(client)).Request;
        var assistant = await Login("assistant.demo");

        var queue = (await assistant.GetFromJsonAsync<LinkQueuePage>($"/api/patient-link-reviews?keyword={request.RequestCode}"))!;
        Assert.Equal(request.Id, Assert.Single(queue.Items).Id);
        var candidates = (await assistant.GetFromJsonAsync<PatientCandidate[]>($"/api/patient-link-reviews/{request.Id}/patients?keyword={patient.PatientCode}"))!;
        Assert.False(Assert.Single(candidates).HasActiveLink);

        var verified = await Verify(assistant, request.Id, request.Version, patient.Id);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var decision = (await verified.Content.ReadFromJsonAsync<LinkDecisionResult>())!;
        Assert.Equal("MSG19", decision.Notification.Code);
        Assert.Equal(PatientLinkStatus.Verified, decision.Request.Status);
        Assert.Equal("Trợ lý demo", decision.Request.ReviewerName);

        var record = (await client.GetFromJsonAsync<OwnRecordSummary>("/api/patient-links/me/record"))!;
        Assert.Equal(patient.PatientCode, record.PatientCode);
        Assert.True((await client.GetFromJsonAsync<CurrentUser>("/api/auth/me"))!.HasVerifiedPatientLink);
        var overview = (await client.GetFromJsonAsync<SelfLinkOverview>("/api/patient-links/me"))!;
        Assert.Equal(PatientLinkStatus.Verified, overview.LinkStatus);
        Assert.Equal("MSG19", overview.Current!.Result!.Code);

        await AssertMessage(await assistant.PostAsJsonAsync($"/api/patient-link-reviews/{request.Id}/revoke",
            new RevokeLinkRequest(decision.Request.Version, " ", null)), HttpStatusCode.BadRequest, "MSG02");
        var revoked = await assistant.PostAsJsonAsync($"/api/patient-link-reviews/{request.Id}/revoke",
            new RevokeLinkRequest(decision.Request.Version, "Nội bộ: phát hiện nhầm hồ sơ", "Vui lòng đến quầy lễ tân"));
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal("MSG20", (await revoked.Content.ReadFromJsonAsync<LinkDecisionResult>())!.Notification.Code);

        // Same token, next request: access is gone; Patient record is retained.
        await AssertMessage(await client.GetAsync("/api/patient-links/me/record"), HttpStatusCode.Forbidden, "MSG06");
        Assert.False((await client.GetFromJsonAsync<CurrentUser>("/api/auth/me"))!.HasVerifiedPatientLink);
        overview = (await client.GetFromJsonAsync<SelfLinkOverview>("/api/patient-links/me"))!;
        Assert.Equal(PatientLinkStatus.Revoked, overview.LinkStatus);
        Assert.True(overview.CanSubmit);
        Assert.Equal("Vui lòng đến quầy lễ tân", overview.Current!.PublicNote);
        var json = await client.GetStringAsync($"/api/patient-links/me/requests/{request.Id}");
        Assert.DoesNotContain("Nội bộ", json);
        Assert.DoesNotContain("CCCD", json);
        Assert.DoesNotContain(patient.PatientCode, json);
        using var scope = fixture.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<DcmsDbContext>().Patients.AnyAsync(x => x.Id == patient.Id));

        var audit = (await assistant.GetFromJsonAsync<LinkAuditDto[]>($"/api/patient-link-reviews/{request.Id}/audit"))!;
        Assert.Equal(["Revoke", "OwnRecordRead", "Verify", "Submit"], audit.Select(x => x.Action).ToArray());
        Assert.Equal("Nội bộ: phát hiện nhầm hồ sơ", audit[0].Reason);
    }

    [Fact]
    public async Task IdentityMismatchAndMissingEvidenceAreNotApproved()
    {
        var client = await NewPublicAccount();
        var request = (await Submit(client)).Request;
        var assistant = await Login("assistant.demo");
        var otherPerson = await NewPatient("Phạm Thị Dung", new DateOnly(1970, 1, 1), phone: "0911111111");
        await AssertMessage(await Verify(assistant, request.Id, request.Version, otherPerson.Id), HttpStatusCode.BadRequest, "MSG143");
        var wrongName = await NewPatient("Hoàng Văn Em");
        await AssertMessage(await Verify(assistant, request.Id, request.Version, wrongName.Id), HttpStatusCode.BadRequest, "MSG143");
        var match = await NewPatient();
        await AssertMessage(await Verify(assistant, request.Id, request.Version, match.Id, " "), HttpStatusCode.BadRequest, "MSG02");
        await AssertMessage(await Verify(assistant, request.Id, request.Version, int.MaxValue), HttpStatusCode.BadRequest, "MSG17");
        await AssertMessage(await Verify(assistant, request.Id, request.Version + 1, match.Id), HttpStatusCode.Conflict, "MSG111");
        Assert.Equal(PatientLinkStatus.Pending, (await client.GetFromJsonAsync<SelfLinkOverview>("/api/patient-links/me"))!.LinkStatus);
    }

    [Fact]
    public async Task PatientWithActiveLinkCannotBeLinkedAgainAndConcurrentVerifyCommitsOnce()
    {
        var patient = await NewPatient();
        var first = (await Submit(await NewPublicAccount())).Request;
        var second = (await Submit(await NewPublicAccount())).Request;
        var a = await Login("assistant.demo");
        var b = await Login("dual.demo");
        var results = await Task.WhenAll(Verify(a, first.Id, first.Version, patient.Id), Verify(b, second.Id, second.Version, patient.Id));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        var loser = Assert.Single(results, x => x.StatusCode != HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        Assert.Contains((await loser.Content.ReadFromJsonAsync<SystemMessage>())!.Code, new[] { "MSG142", "MSG111" });
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(1, await db.PatientAccountLinks.CountAsync(x => x.PatientId == patient.Id && x.Status == PatientLinkStatus.Verified));

        // Sequential attempt on a still-pending request now reports the active-link conflict.
        var pending = await db.PatientAccountLinks.AsNoTracking().SingleAsync(x => (x.Id == first.Id || x.Id == second.Id) && x.Status == PatientLinkStatus.Pending);
        await AssertMessage(await Verify(a, pending.Id, pending.Version, patient.Id), HttpStatusCode.Conflict, "MSG142");
    }

    [Fact]
    public async Task VerifiedAccountCannotSubmitAgainAndRejectRequiresReasonThenAllowsResubmit()
    {
        var client = await NewPublicAccount();
        var request = (await Submit(client)).Request;
        var assistant = await Login("assistant.demo");
        await AssertMessage(await assistant.PostAsJsonAsync($"/api/patient-link-reviews/{request.Id}/reject",
            new RejectLinkRequest(request.Version, "", null, null)), HttpStatusCode.BadRequest, "MSG02");
        var rejected = await assistant.PostAsJsonAsync($"/api/patient-link-reviews/{request.Id}/reject",
            new RejectLinkRequest(request.Version, "Ghi chú nội bộ: không khớp", "Không có lượt khám", "Vui lòng mang giấy tờ tùy thân"));
        Assert.Equal("MSG20", (await rejected.Content.ReadFromJsonAsync<LinkDecisionResult>())!.Notification.Code);
        var overview = (await client.GetFromJsonAsync<SelfLinkOverview>("/api/patient-links/me"))!;
        Assert.Equal(PatientLinkStatus.Rejected, overview.LinkStatus);
        Assert.True(overview.CanSubmit);
        Assert.DoesNotContain("nội bộ", System.Text.Json.JsonSerializer.Serialize(overview), StringComparison.OrdinalIgnoreCase);
        await AssertMessage(await client.GetAsync("/api/patient-links/me/record"), HttpStatusCode.Forbidden, "MSG06");

        var again = (await Submit(client)).Request;
        Assert.NotEqual(request.Id, again.Id);
        var patient = await NewPatient();
        Assert.Equal(HttpStatusCode.OK, (await Verify(assistant, again.Id, again.Version, patient.Id)).StatusCode);
        await AssertMessage(await client.PostAsJsonAsync("/api/patient-links/me/requests", Request()), HttpStatusCode.Conflict, "MSG142");
    }

    [Fact]
    public async Task WithdrawPendingRequestGrantsNothingAndQueueFiltersValidate()
    {
        var client = await NewPublicAccount();
        var request = (await Submit(client)).Request;
        var withdrawn = await client.PostAsJsonAsync($"/api/patient-links/me/requests/{request.Id}/withdraw", new WithdrawLinkRequest(request.Version));
        Assert.Equal(PatientLinkStatus.Withdrawn, (await withdrawn.Content.ReadFromJsonAsync<SelfLinkRequestDto>())!.Status);
        Assert.Equal(PatientLinkStatus.NotLinked, (await client.GetFromJsonAsync<SelfLinkOverview>("/api/patient-links/me"))!.LinkStatus);
        var assistant = await Login("assistant.demo");
        await AssertMessage(await Verify(assistant, request.Id, request.Version + 1, (await NewPatient()).Id), HttpStatusCode.Conflict, "MSG111");

        var pendingOnly = (await assistant.GetFromJsonAsync<LinkQueuePage>($"/api/patient-link-reviews?keyword={request.RequestCode}"))!;
        Assert.Empty(pendingOnly.Items);
        var all = (await assistant.GetFromJsonAsync<LinkQueuePage>($"/api/patient-link-reviews?status=ALL&keyword={request.RequestCode}"))!;
        Assert.Equal(PatientLinkStatus.Withdrawn, Assert.Single(all.Items).Status);
        await AssertMessage(await assistant.GetAsync("/api/patient-link-reviews?status=UNKNOWN"), HttpStatusCode.BadRequest, "MSG04");
        await AssertMessage(await assistant.GetAsync("/api/patient-link-reviews?from=2026-10-10&to=2026-10-01"), HttpStatusCode.BadRequest, "MSG04");
        await AssertMessage(await assistant.GetAsync($"/api/patient-link-reviews?keyword={new string('k', 101)}"), HttpStatusCode.BadRequest, "MSG03");
        await AssertMessage(await assistant.GetAsync("/api/patient-link-reviews?pageSize=101"), HttpStatusCode.BadRequest, "MSG04");
    }
}

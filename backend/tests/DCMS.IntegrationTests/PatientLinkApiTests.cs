using DCMS.Application.Features.PatientLinks;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

/// <summary>UC-36 account ↔ patient record linking: S07 public account requests, S08 clinic assistant review.</summary>
[Collection(ApiCollection.Name)]
public sealed class PatientLinkApiTests(ApiHost host)
{
    private const string NewGuest = "A new public (Guest) account with no linked record is signed in.";

    private async Task<string> NewPublicAccountAsync()
    {
        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var now = DateTimeOffset.UtcNow;
        var user = new ApplicationUser
        {
            UserName = "link." + Guid.NewGuid().ToString("N")[..10], FullName = "Nguyễn Thị Lan", AccountType = "Public",
            CreatedAt = now, UpdatedAt = now, Email = Guid.NewGuid().ToString("N")[..10] + "@example.invalid", EmailConfirmed = true
        };
        Assert.True((await users.CreateAsync(user, host.Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, AccountRoles.Guest)).Succeeded);
        return user.UserName;
    }

    private async Task<PatientProfile> NewPatientAsync(string fullName, DateOnly birth)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var now = DateTimeOffset.UtcNow;
        var patient = new PatientProfile
        {
            PatientCode = "LK" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), FullName = fullName,
            NormalizedName = fullName.ToUpperInvariant(), BirthDate = birth, Gender = "FEMALE", Phone = "0912000111", CreatedAt = now, UpdatedAt = now
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return patient;
    }

    private static SubmitLinkRequest Submit(string? code, DateOnly birth) =>
        new("Nguyễn Thị Lan", birth, code, "Khám lần đầu tháng 10/2026", true);

    [Fact, Case("LNK-01", "A customer sends a link request, the assistant verifies it, and the customer can see their own record", NewGuest,
        "Step 1: The customer calls GET /api/patient-links/me.",
        "Step 2: The customer calls POST /api/patient-links/me/requests with full name, date of birth and patient code.",
        "Step 3: The assistant calls GET /api/patient-link-reviews, GET /{id}, GET /{id}/patients?keyword=<code>.",
        "Step 4: The assistant calls POST /api/patient-link-reviews/{id}/verify.",
        "Step 5: The customer calls GET /api/patient-links/me/record; the assistant calls GET /{id}/audit.")]
    public async Task Guest_link_request_is_verified_by_assistant()
    {
        var birth = new DateOnly(1992, 4, 18);
        var patient = await NewPatientAsync("Nguyễn Thị Lan", birth);
        var guest = await host.ClientAsync(await NewPublicAccountAsync());
        var assistant = await host.ClientAsync("assistant.demo");

        var overview = await guest.GetAsync("/api/patient-links/me");
        var submitted = await guest.PostAsync("/api/patient-links/me/requests", Submit(patient.PatientCode, birth));
        Assert.True(submitted.Status is 200 or 201, submitted.Body);
        var request = submitted.Json<SelfLinkChange>().Request;
        var queue = await assistant.GetAsync($"/api/patient-link-reviews?status=PENDING&keyword={request.RequestCode}&page=1&pageSize=20");
        var detail = await assistant.GetAsync($"/api/patient-link-reviews/{request.Id}");
        var candidates = await assistant.GetAsync($"/api/patient-link-reviews/{request.Id}/patients?keyword={patient.PatientCode}");
        var verified = await assistant.PostAsync($"/api/patient-link-reviews/{request.Id}/verify",
            new VerifyLinkRequest(detail.Json<LinkReviewDetail>().Version, patient.Id, "Đối chiếu CCCD và số điện thoại khớp"));
        var record = await guest.GetAsync("/api/patient-links/me/record");
        var audit = await assistant.GetAsync($"/api/patient-link-reviews/{request.Id}/audit");

        Assert.Equal(200, overview.Status);
        Assert.True(overview.Json<SelfLinkOverview>().CanSubmit);
        Assert.Equal(200, queue.Status);
        Assert.Contains(request.RequestCode, queue.Body);
        Assert.Equal(200, candidates.Status);
        Assert.Contains(patient.PatientCode, candidates.Body);
        Assert.Equal(200, verified.Status);
        Assert.Equal(PatientLinkStatus.Verified, verified.Json<LinkDecisionResult>().Request.Status);
        Assert.Equal(200, record.Status);
        Assert.Equal(patient.PatientCode, record.Json<OwnRecordSummary>().PatientCode);
        Assert.Equal(200, audit.Status);
        Assert.Contains("Verify", audit.Body);
    }

    [Fact, Case("LNK-02", "A customer withdraws a pending request and sends it again; the assistant rejects the new request", NewGuest,
        "Step 1: The customer sends a link request.", "Step 2: The customer calls POST /api/patient-links/me/requests/{id}/withdraw.",
        "Step 3: The customer sends a new request.", "Step 4: The assistant calls POST /api/patient-link-reviews/{id}/reject with a reason.")]
    public async Task Guest_withdraws_and_assistant_rejects()
    {
        var guest = await host.ClientAsync(await NewPublicAccountAsync());
        var assistant = await host.ClientAsync("assistant.demo");
        var birth = new DateOnly(1990, 1, 1);

        var first = (await guest.PostAsync("/api/patient-links/me/requests", Submit(null, birth))).Json<SelfLinkChange>().Request;
        var withdrawn = await guest.PostAsync($"/api/patient-links/me/requests/{first.Id}/withdraw", new WithdrawLinkRequest(first.Version));
        var second = (await guest.PostAsync("/api/patient-links/me/requests", Submit(null, birth))).Json<SelfLinkChange>().Request;
        var detail = (await assistant.GetAsync($"/api/patient-link-reviews/{second.Id}")).Json<LinkReviewDetail>();
        var rejected = await assistant.PostAsync($"/api/patient-link-reviews/{second.Id}/reject",
            new RejectLinkRequest(detail.Version, "Thông tin không khớp hồ sơ", "Không tìm thấy bệnh nhân trùng ngày sinh", "Vui lòng mang CCCD đến quầy lễ tân"));
        var noRecord = await guest.GetAsync("/api/patient-links/me/record");

        Assert.Equal(200, withdrawn.Status);
        Assert.Equal(PatientLinkStatus.Withdrawn, withdrawn.Json<SelfLinkRequestDto>().Status);
        Assert.Equal(200, rejected.Status);
        Assert.Equal(PatientLinkStatus.Rejected, rejected.Json<LinkDecisionResult>().Request.Status);
        Assert.Equal(403, noRecord.Status);
    }

    [Fact, Case("LNK-03", "The assistant revokes a verified link", NewGuest,
        "Step 1: The customer sends a request and the assistant verifies it.", "Step 2: The assistant calls POST /api/patient-link-reviews/{id}/revoke with a reason.",
        "Step 3: The customer calls GET /api/patient-links/me/record.")]
    public async Task Assistant_revokes_a_verified_link()
    {
        var birth = new DateOnly(1985, 6, 30);
        var patient = await NewPatientAsync("Nguyễn Thị Lan", birth);
        var guest = await host.ClientAsync(await NewPublicAccountAsync());
        var assistant = await host.ClientAsync("assistant.demo");
        var request = (await guest.PostAsync("/api/patient-links/me/requests", Submit(patient.PatientCode, birth))).Json<SelfLinkChange>().Request;
        var detail = (await assistant.GetAsync($"/api/patient-link-reviews/{request.Id}")).Json<LinkReviewDetail>();
        var verified = (await assistant.PostAsync($"/api/patient-link-reviews/{request.Id}/verify",
            new VerifyLinkRequest(detail.Version, patient.Id, "Khớp thông tin"))).Json<LinkDecisionResult>();

        var revoked = await assistant.PostAsync($"/api/patient-link-reviews/{request.Id}/revoke",
            new RevokeLinkRequest(verified.Request.Version, "Khách báo nhầm tài khoản", "Liên kết đã được gỡ theo yêu cầu"));
        var record = await guest.GetAsync("/api/patient-links/me/record");
        var history = await assistant.GetAsync("/api/patient-link-reviews?status=ALL&page=1&pageSize=20");

        Assert.Equal(200, revoked.Status);
        Assert.Equal(PatientLinkStatus.Revoked, revoked.Json<LinkDecisionResult>().Request.Status);
        Assert.Equal(403, record.Status);
        Assert.Equal(200, history.Status);
    }

    [Fact, Case("LNK-04", "Link requests with invalid data, a stale version or the wrong user are rejected", NewGuest,
        "Step 1: The customer sends a request without confirming it is their own record.", "Step 2: The assistant verifies it against a patient with a different name.",
        "Step 3: The assistant verifies it with a stale version.", "Step 4: A staff member calls the customer API; the customer calls the assistant API.",
        "Step 5: The assistant lists requests with an invalid status.")]
    public async Task Invalid_link_operations_are_rejected()
    {
        var guestName = await NewPublicAccountAsync();
        var guest = await host.ClientAsync(guestName);
        var assistant = await host.ClientAsync("assistant.demo");
        var dentist = await host.ClientAsync("dentist.demo");
        var other = await NewPatientAsync("Trần Văn Bình", new DateOnly(1970, 2, 2));

        var notConfirmed = await guest.PostAsync("/api/patient-links/me/requests", Submit(null, new DateOnly(1990, 1, 1)) with { ConfirmSelf = false });
        var request = (await guest.PostAsync("/api/patient-links/me/requests", Submit(null, new DateOnly(1990, 1, 1)))).Json<SelfLinkChange>().Request;
        var detail = (await assistant.GetAsync($"/api/patient-link-reviews/{request.Id}")).Json<LinkReviewDetail>();
        var mismatch = await assistant.PostAsync($"/api/patient-link-reviews/{request.Id}/verify", new VerifyLinkRequest(detail.Version, other.Id, "Thử"));
        var stale = await assistant.PostAsync($"/api/patient-link-reviews/{request.Id}/reject", new RejectLinkRequest(detail.Version + 7, "Sai", null, null));
        var staffOnGuestApi = await dentist.GetAsync("/api/patient-links/me");
        var guestOnReviewApi = await guest.GetAsync("/api/patient-link-reviews");
        var badStatus = await assistant.GetAsync("/api/patient-link-reviews?status=WHATEVER");
        var missing = await assistant.GetAsync("/api/patient-link-reviews/999999");

        Assert.Equal(400, notConfirmed.Status);
        Assert.InRange(mismatch.Status, 400, 422);
        Assert.Equal(409, stale.Status);
        Assert.Equal(403, staffOnGuestApi.Status);
        Assert.Equal(403, guestOnReviewApi.Status);
        Assert.Equal(400, badStatus.Status);
        Assert.Equal(404, missing.Status);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Examinations;
using DCMS.Application.Features.FamilyMembers;
using DCMS.Application.Features.Scheduling;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.FamilyMembers;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using DCMS.Domain.Features.Treatment;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class FamilyRecordApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed record Setup(HttpClient Owner, HttpClient Assistant, FamilyMemberDto Member, int OwnerId, int PatientId,
        int BranchId, int DentistId, int DentistAccountId, int ServiceId, Guid VisitId);
    private static string Url(Guid member) => $"/api/me/family-members/{member}";
    private async Task<HttpClient> Client(string? name = null)
    {
        if (name == null) {
            name = "familyaccess." + Guid.NewGuid().ToString("N");
            using var scope = fixture.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = name, FullName = "SYNTHETIC delegate", AccountType = "Public", IsActive = true };
            Assert.True((await users.CreateAsync(user, fixture.Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Guest")).Succeeded);
        }
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        return client;
    }
    private async Task<Setup> Prepare(bool linkedBooking = false)
    {
        var owner = await Client(); var assistant = await Client("assistant.demo");
        var ownerId = (await owner.GetFromJsonAsync<CurrentUser>("/api/auth/me"))!.Id;
        var name = "SYNTHETIC relative " + Guid.NewGuid().ToString("N")[..8];
        var created = await owner.PostAsJsonAsync("/api/me/family-members", new CreateFamilyMember(name, "Child", new(2015, 3, 12), Guid.NewGuid(), "MALE", "0912345678"));
        created.EnsureSuccessStatusCode(); var member = (await created.Content.ReadFromJsonAsync<FamilyMemberDto>())!;
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var dentist = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "dentist.demo" select e).SingleAsync();
        var assistantId = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "assistant.demo" select e.Id).SingleAsync();
        var branch = new SchedulingBranch { Name = name }; var patient = new PatientProfile { PatientCode = "FAM-" + Guid.NewGuid().ToString("N"), FullName = name, NormalizedName = name.ToUpperInvariant(), BirthDate = member.BirthDate, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.AddRange(branch, patient); await db.SaveChangesAsync();
        var service = new SchedulingService { BranchId = branch.Id, Name = "Khám", PublicDurationMinutes = 30, IsInitialExamination = true };
        var chair = new SchedulingChair { BranchId = branch.Id, Name = "SYNTHETIC chair", OpenMinute = 0, CloseMinute = 1440 };
        db.AddRange(service, chair, new SchedulingMember { BranchId = branch.Id, EmployeeId = dentist.Id, CanDentist = true }, new SchedulingMember { BranchId = branch.Id, EmployeeId = assistantId }); await db.SaveChangesAsync();
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        db.Add(new ResourceReservation { Id = id, Code = "RV-" + id.ToString("N"), BranchId = branch.Id, DentistId = dentist.Id, ChairId = chair.Id, ServiceId = service.Id, Start = now.AddDays(-1), End = now.AddDays(-1).AddHours(1), AttendeeLabel = name });
        db.Add(new Appointment { Id = id, ReservationId = id, Code = "AP-" + id.ToString("N"), BranchId = branch.Id, PatientId = patient.Id, DentistId = dentist.Id, ChairId = chair.Id, ServiceId = service.Id, BookerName = "SYNTHETIC", AttendeeName = name, StartAt = now.AddDays(-1), EndAt = now.AddDays(-1).AddHours(1), CreatedAt = now,
            BookerAccountId = linkedBooking ? ownerId : null, FamilyMemberId = linkedBooking ? member.Id : null, AttendeeBirthDate = linkedBooking ? member.BirthDate : null });
        await db.SaveChangesAsync();
        var visit = new Visit { AppointmentId = id, PatientId = patient.Id, BranchId = branch.Id, DentistId = dentist.Id, Status = VisitStatuses.Completed, ArrivalAt = now.AddDays(-1), RecordedAt = now, RecordedBy = (await db.Employees.Where(e => e.Id == assistantId).Select(e => e.AccountId).SingleAsync()), Note = "SYNTHETIC INTERNAL VISIT NOTE" };
        db.Add(visit); await db.SaveChangesAsync();
        var content = new ExaminationContent("SYNTHETIC symptoms", new(), new(), new(), [], "SYNTHETIC diagnosis", "SYNTHETIC INTERNAL EXAM NOTE", "SYNTHETIC conclusion", "SYNTHETIC advice");
        db.Add(new VisitExamination { VisitId = visit.Id, Confirmed = true, UpdatedBy = dentist.AccountId, UpdatedAt = now, ContentJson = JsonSerializer.Serialize(content, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        await db.SaveChangesAsync();
        return new(owner, assistant, member, ownerId, patient.Id, branch.Id, dentist.Id, dentist.AccountId, service.Id, visit.Id);
    }
    private static async Task<FamilyAccessDto> Submit(Setup setup)
    {
        var response = await setup.Owner.PostAsJsonAsync(Url(setup.Member.Id) + "/record-access/requests", new FamilyAccessRequest(null, "SYNTHETIC identity evidence", Guid.NewGuid()));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<FamilyAccessDto>())!;
    }
    private static VerifyFamilyAccess Verify(Setup s, FamilyAccessDto row, string[]? scopes = null) => new(s.PatientId, s.BranchId, scopes ?? FamilyRecordScopes.All,
        DateTimeOffset.UtcNow.AddDays(90), "PatientConsent", "SYNTHETIC consent and identity checked", true, row.Version, Guid.NewGuid());
    private static async Task<FamilyAccessDto> Grant(Setup s, FamilyAccessDto row, string[]? scopes = null)
    {
        var response = await s.Assistant.PostAsJsonAsync($"/api/family-record-reviews/{row.Id}/verify", Verify(s, row, scopes));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()); return (await response.Content.ReadFromJsonAsync<FamilyAccessDto>())!;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReceivedFamilyBookingDefaultsToOfficialRecordsAndRevocationIsNeverBypassed(bool openOverviewFirst)
    {
        var s = await Prepare(linkedBooking: true);
        var first = await s.Owner.GetAsync(Url(s.Member.Id) + (openOverviewFirst ? "/record-access" : "/record"));
        Assert.True(first.IsSuccessStatusCode, await first.Content.ReadAsStringAsync());
        var parallel = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => s.Owner.GetAsync(Url(s.Member.Id) + "/record-access")));
        foreach (var response in parallel) Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var overview = (await s.Owner.GetFromJsonAsync<FamilyAccessOverview>(Url(s.Member.Id) + "/record-access"))!;
        Assert.Equal("Verified", overview.Current!.Status); Assert.False(overview.CanRequest);
        Assert.Equal(FamilyRecordScopes.All.Order(), overview.Current.Scopes.Order());
        var records = await s.Owner.GetAsync(Url(s.Member.Id) + "/record/examinations"); records.EnsureSuccessStatusCode();
        var json = await records.Content.ReadAsStringAsync(); Assert.Contains("SYNTHETIC conclusion", json); Assert.DoesNotContain("INTERNAL", json);
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var grant = Assert.Single(await db.Set<FamilyRecordAccess>().Where(x => x.FamilyMemberId == s.Member.Id).ToArrayAsync());
            Assert.Equal("DefaultFamilyBooking", grant.SharingBasis); Assert.Equal(s.PatientId, grant.PatientId); Assert.Equal(s.BranchId, grant.BranchId);
            Assert.Single(await db.Set<FamilyRecordAudit>().Where(x => x.AccessId == grant.Id && x.Action == "DefaultGranted").ToArrayAsync());
            Assert.False(await db.PatientAccountLinks.AnyAsync(x => x.PatientId == s.PatientId));
            Assert.False(await db.Users.AnyAsync(x => x.FullName == s.Member.FullName));
        }
        using var stranger = await Client();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(Url(s.Member.Id) + "/record")).StatusCode);
        var revoked = await s.Assistant.PostAsJsonAsync($"/api/family-record-reviews/{overview.Current.Id}/revoke",
            new DecideFamilyAccess("SYNTHETIC stop default sharing", null, overview.Current.Version, Guid.NewGuid()));
        revoked.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Owner.GetAsync(Url(s.Member.Id) + "/record/examinations")).StatusCode);
        Assert.Equal("Revoked", (await s.Owner.GetFromJsonAsync<FamilyAccessOverview>(Url(s.Member.Id) + "/record-access"))!.Current!.Status);
        await Submit(s);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Owner.GetAsync(Url(s.Member.Id) + "/record")).StatusCode);
    }

    [Fact]
    public async Task PendingRequestBecomesDefaultAccessButManualScopeIsPreserved()
    {
        var automatic = await Prepare(linkedBooking: true); var pending = await Submit(automatic);
        var overview = (await automatic.Owner.GetFromJsonAsync<FamilyAccessOverview>(Url(automatic.Member.Id) + "/record-access"))!;
        Assert.Equal(pending.Id, overview.Current!.Id); Assert.Equal("Verified", overview.Current.Status);
        var manual = await Prepare(linkedBooking: true); var limited = await Grant(manual, await Submit(manual), ["Examinations"]);
        var manualOverview = (await manual.Owner.GetFromJsonAsync<FamilyAccessOverview>(Url(manual.Member.Id) + "/record-access"))!;
        Assert.Equal(limited.Version, manualOverview.Current!.Version); Assert.Equal(new[] { "Examinations" }, manualOverview.Current.Scopes);
        Assert.Equal(HttpStatusCode.Forbidden, (await manual.Owner.GetAsync(Url(manual.Member.Id) + "/record/prescriptions")).StatusCode);
    }

    [Theory]
    [InlineData("NoVisit")]
    [InlineData("WrongOwner")]
    [InlineData("SnapshotName")]
    [InlineData("SnapshotBirth")]
    [InlineData("PatientName")]
    [InlineData("Archived")]
    [InlineData("WrongPatient")]
    [InlineData("WrongBranch")]
    [InlineData("Cancelled")]
    public async Task UnprovenBookingIdentityDoesNotDefaultToRecords(string invalid)
    {
        var s = await Prepare(linkedBooking: true);
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var visit = await db.Visits.SingleAsync(x => x.Id == s.VisitId);
            var appointment = await db.Appointments.SingleAsync(x => x.Id == visit.AppointmentId);
            var patient = await db.Patients.SingleAsync(x => x.Id == s.PatientId);
            switch (invalid) {
                case "NoVisit": db.Remove(await db.Set<VisitExamination>().SingleAsync(x => x.VisitId == s.VisitId)); db.Remove(visit); break;
                case "WrongOwner": appointment.BookerAccountId = null; break;
                case "SnapshotName": appointment.AttendeeName = "SYNTHETIC different attendee"; break;
                case "SnapshotBirth": appointment.AttendeeBirthDate = new DateOnly(2014, 3, 12); break;
                case "PatientName": patient.FullName = "SYNTHETIC different patient"; break;
                case "Archived": patient.IsArchived = true; break;
                case "WrongPatient": appointment.PatientId = null; break;
                case "WrongBranch": appointment.BranchId = await db.Set<SchedulingBranch>().Where(x => x.Id != s.BranchId).Select(x => x.Id).FirstAsync(); break;
                case "Cancelled": visit.Status = VisitStatuses.Cancelled; break;
            }
            await db.SaveChangesAsync();
        }
        Assert.Null((await s.Owner.GetFromJsonAsync<FamilyAccessOverview>(Url(s.Member.Id) + "/record-access"))!.Current);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Owner.GetAsync(Url(s.Member.Id) + "/record")).StatusCode);
    }

    [Fact]
    public async Task ConflictingPatientIdentitiesAndChangedMemberDoNotRecreateDefaultAccess()
    {
        var s = await Prepare(linkedBooking: true); var other = await Prepare();
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var otherVisit = await db.Visits.SingleAsync(x => x.Id == other.VisitId);
            var appointment = await db.Appointments.SingleAsync(x => x.Id == otherVisit.AppointmentId);
            appointment.BookerAccountId = s.OwnerId; appointment.FamilyMemberId = s.Member.Id;
            appointment.AttendeeName = s.Member.FullName; appointment.AttendeeBirthDate = s.Member.BirthDate;
            var patient = await db.Patients.SingleAsync(x => x.Id == other.PatientId); patient.FullName = s.Member.FullName; patient.BirthDate = s.Member.BirthDate;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Owner.GetAsync(Url(s.Member.Id) + "/record")).StatusCode);
        var clean = await Prepare(linkedBooking: true);
        (await clean.Owner.GetAsync(Url(clean.Member.Id) + "/record")).EnsureSuccessStatusCode();
        var changed = await clean.Owner.PutAsJsonAsync(Url(clean.Member.Id), new UpdateFamilyMember(clean.Member.FullName + " changed", "Child", clean.Member.BirthDate,
            clean.Member.Version, Guid.NewGuid(), "MALE", "0912345678")); changed.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await clean.Owner.GetAsync(Url(clean.Member.Id) + "/record")).StatusCode);
    }

    [Fact]
    public async Task RelativeWithoutAccountRequestsAccessPendingCannotReadAndOwnershipIsEnforced()
    {
        var s = await Prepare(); using var stranger = await Client();
        var row = await Submit(s); Assert.Equal("Pending", row.Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Owner.GetAsync(Url(s.Member.Id) + "/record")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(Url(s.Member.Id) + "/record-access")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync(Url(s.Member.Id) + "/record-access/requests", new FamilyAccessRequest(null, null, Guid.NewGuid()))).StatusCode);
        using var dentist = await Client("dentist.demo"); using var admin = await Client("admin.demo");
        Assert.Equal(HttpStatusCode.Forbidden, (await dentist.GetAsync("/api/family-record-reviews")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/family-record-reviews/{row.Id}/verify", Verify(s, row))).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.False(await db.PatientAccountLinks.AnyAsync(x => x.PatientId == s.PatientId));
        Assert.False(await db.Users.AnyAsync(x => x.FullName == s.Member.FullName));
    }

    [Fact]
    public async Task VerifiedGrantReadsOnlyItsScopeAndNeverInternalOrOtherPatientData()
    {
        var s = await Prepare(); var row = await Grant(s, await Submit(s), ["Examinations"]);
        var header = await s.Owner.GetAsync(Url(s.Member.Id) + "/record"); header.EnsureSuccessStatusCode();
        Assert.Contains("no-store", header.Headers.CacheControl!.ToString());
        var records = await s.Owner.GetAsync(Url(s.Member.Id) + "/record/examinations"); records.EnsureSuccessStatusCode();
        var json = await records.Content.ReadAsStringAsync(); Assert.Contains("SYNTHETIC conclusion", json); Assert.DoesNotContain("INTERNAL", json);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Owner.GetAsync(Url(s.Member.Id) + "/record/treatment")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Owner.GetAsync(Url(s.Member.Id) + "/record/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Owner.GetAsync(Url(s.Member.Id) + $"/record/documents/{Guid.NewGuid()}/content")).StatusCode);
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var exam = await db.Set<VisitExamination>().SingleAsync(x => x.VisitId == s.VisitId); exam.Confirmed = false; await db.SaveChangesAsync();
        }
        var empty = await s.Owner.GetFromJsonAsync<FamilyPage<JsonElement>>(Url(s.Member.Id) + "/record/examinations"); Assert.Empty(empty!.Items);
        var revoked = await s.Assistant.PostAsJsonAsync($"/api/family-record-reviews/{row.Id}/revoke", new DecideFamilyAccess("SYNTHETIC revoked", null, row.Version, Guid.NewGuid())); revoked.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Owner.GetAsync(Url(s.Member.Id) + "/record/examinations")).StatusCode);
    }

    [Fact]
    public async Task IdentityConsentExpiryAndBranchCannotBeBypassed()
    {
        var s = await Prepare(); var row = await Submit(s); var command = Verify(s, row);
        foreach (var invalid in new[] { command with { ConsentConfirmed = false }, command with { Scopes = [] }, command with { Scopes = ["Finance"] },
            command with { ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) }, command with { ExpiresAt = DateTimeOffset.UtcNow.AddDays(366) }, command with { VerificationNote = " " }, command with { SharingBasis = "Family" } })
            Assert.Equal(HttpStatusCode.BadRequest, (await s.Assistant.PostAsJsonAsync($"/api/family-record-reviews/{row.Id}/verify", invalid with { RequestKey = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Assistant.PostAsJsonAsync($"/api/family-record-reviews/{row.Id}/verify", command with { BranchId = int.MaxValue })).StatusCode);
        using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var patient = await db.Patients.SingleAsync(x => x.Id == s.PatientId); patient.FullName = "SYNTHETIC different patient"; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Assistant.PostAsJsonAsync($"/api/family-record-reviews/{row.Id}/verify", command)).StatusCode);
    }

    [Fact]
    public async Task IdempotentConcurrentSubmitVerifyAndStaleDecisionsKeepOneGrant()
    {
        var s = await Prepare(); var command = new FamilyAccessRequest(null, "SYNTHETIC retry", Guid.NewGuid());
        var url = Url(s.Member.Id) + "/record-access/requests";
        var responses = await Task.WhenAll(s.Owner.PostAsJsonAsync(url, command), s.Owner.PostAsJsonAsync(url, command));
        foreach (var response in responses) Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var row = (await responses[0].Content.ReadFromJsonAsync<FamilyAccessDto>())!;
        Assert.Equal(row.Id, (await responses[1].Content.ReadFromJsonAsync<FamilyAccessDto>())!.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await s.Owner.PostAsJsonAsync(url, command with { MatchingInformation = "changed" })).StatusCode);
        var decision = Verify(s, row); var verified = await s.Assistant.PostAsJsonAsync($"/api/family-record-reviews/{row.Id}/verify", decision); verified.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await s.Assistant.PostAsJsonAsync($"/api/family-record-reviews/{row.Id}/verify", decision)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await s.Assistant.PostAsJsonAsync($"/api/family-record-reviews/{row.Id}/reject", new DecideFamilyAccess("stale", null, row.Version, Guid.NewGuid()))).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(1, await db.Set<FamilyRecordAccess>().CountAsync(x => x.FamilyMemberId == s.Member.Id));
        Assert.Equal(1, await db.Set<FamilyRecordAudit>().CountAsync(x => x.AccessId == row.Id && x.Action == "Verified"));
    }

    [Theory]
    [InlineData("withdraw")]
    [InlineData("rename")]
    [InlineData("delete")]
    [InlineData("expire")]
    public async Task EndingOrChangingTheRelativeBlocksNextReadWithTheSameToken(string action)
    {
        var s = await Prepare(); var row = await Grant(s, await Submit(s));
        if (action == "withdraw") (await s.Owner.PostAsJsonAsync(Url(s.Member.Id) + $"/record-access/requests/{row.Id}/withdraw", new FamilyAccessWithdraw(row.Version, Guid.NewGuid()))).EnsureSuccessStatusCode();
        if (action == "rename") (await s.Owner.PutAsJsonAsync(Url(s.Member.Id), new UpdateFamilyMember(s.Member.FullName + " changed", "Child", s.Member.BirthDate, s.Member.Version, Guid.NewGuid(), "MALE", "0912345678"))).EnsureSuccessStatusCode();
        if (action == "delete") (await s.Owner.SendAsync(new HttpRequestMessage(HttpMethod.Delete, Url(s.Member.Id)) { Content = JsonContent.Create(new DeleteFamilyMember(s.Member.Version, Guid.NewGuid())) })).EnsureSuccessStatusCode();
        if (action == "expire") { using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var grant = await db.Set<FamilyRecordAccess>().SingleAsync(x => x.Id == row.Id); grant.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync(); }
        Assert.Equal(action == "delete" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, (await s.Owner.GetAsync(Url(s.Member.Id) + "/record")).StatusCode);
        if (action == "expire") { var next = await Submit(s); Assert.NotEqual(row.Id, next.Id); Assert.Equal("Pending", next.Status); }
    }

    [Fact]
    public async Task PrivateDocumentsRequireReadyOfficialContextAndRecheckRevocation()
    {
        var s = await Prepare(); var row = await Grant(s, await Submit(s)); var id = Guid.NewGuid();
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.4 SYNTHETIC family document %%EOF"); string file;
        using (var scope = fixture.Services.CreateScope()) {
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>(); var root = config["ClinicalDocuments:StoragePath"]!;
            Directory.CreateDirectory(root); file = Path.Combine(root, id.ToString("N")); await File.WriteAllBytesAsync(file, bytes);
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            db.Add(new ClinicalDocument { Id = id, PatientId = s.PatientId, VisitId = s.VisitId, FileName = "synthetic.pdf", ContentType = "application/pdf", SizeBytes = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)), DocumentType = "LabResult", SourceDate = DateOnly.FromDateTime(DateTime.UtcNow), FileState = "Ready", UploaderId = s.DentistAccountId, UploadedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync();
        }
        try {
            var url = Url(s.Member.Id) + $"/record/documents/{id}/content";
            var response = await s.Owner.GetAsync(url); response.EnsureSuccessStatusCode(); Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
            using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var doc = await db.Set<ClinicalDocument>().SingleAsync(x => x.Id == id); doc.FileState = "PendingScan"; await db.SaveChangesAsync(); }
            Assert.Equal(HttpStatusCode.NotFound, (await s.Owner.GetAsync(url)).StatusCode);
            (await s.Owner.PostAsJsonAsync(Url(s.Member.Id) + $"/record-access/requests/{row.Id}/withdraw", new FamilyAccessWithdraw(row.Version, Guid.NewGuid()))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Forbidden, (await s.Owner.GetAsync(url)).StatusCode);
        } finally { File.Delete(file); }
    }

    [Fact]
    public async Task TreatmentAndPrescriptionViewsExcludeDraftsAndInternalFields()
    {
        var s = await Prepare(); await Grant(s, await Submit(s));
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var committed = new TreatmentDossier { PatientId = s.PatientId, BranchId = s.BranchId, DentistId = s.DentistId,
                Code = "OFF-" + Guid.NewGuid().ToString("N"), PlanState = "Committed", PlanVersion = 1,
                Diagnosis = "SYNTHETIC official treatment", Note = "SYNTHETIC INTERNAL COST NOTE", StartedOn = DateOnly.FromDateTime(DateTime.UtcNow) };
            committed.Items.Add(new TreatmentItem { DossierId = committed.Id, ServiceId = s.ServiceId, ServiceName = "SYNTHETIC agreed item", Unit = "lần", Quantity = 1, Decision = "Agreed", AgreedUnitPriceVnd = 987654321 });
            committed.Items.Add(new TreatmentItem { DossierId = committed.Id, ServiceId = s.ServiceId, ServiceName = "SYNTHETIC unagreed item", Unit = "lần", Quantity = 1, Decision = "Proposed" });
            db.AddRange(committed, new TreatmentDossier { PatientId = s.PatientId, BranchId = s.BranchId, DentistId = s.DentistId,
                Code = "DRAFT-" + Guid.NewGuid().ToString("N"), PlanState = "Draft", Diagnosis = "SYNTHETIC DRAFT TREATMENT", StartedOn = DateOnly.FromDateTime(DateTime.UtcNow) });
            var now = DateTimeOffset.UtcNow;
            db.Add(new ExternalPrescription { VisitId = s.VisitId, PatientId = s.PatientId, BranchId = s.BranchId,
                PrescribedByDoctorId = s.DentistAccountId, CreatedBy = s.DentistAccountId, UpdatedBy = s.DentistAccountId, Revision = 1,
                Status = "Finalized", FinalizedAt = now, FinalizedBy = s.DentistAccountId, Diagnosis = "SYNTHETIC signed prescription", GeneralAdvice = "SYNTHETIC signed advice", CreatedAt = now, UpdatedAt = now });
            db.Add(new ExternalPrescription { VisitId = s.VisitId, PatientId = s.PatientId, BranchId = s.BranchId,
                PrescribedByDoctorId = s.DentistAccountId, CreatedBy = s.DentistAccountId, UpdatedBy = s.DentistAccountId, Revision = 2,
                Status = "Draft", Diagnosis = "SYNTHETIC DRAFT PRESCRIPTION", CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();
        }
        var treatment = await s.Owner.GetStringAsync(Url(s.Member.Id) + "/record/treatment");
        Assert.Contains("SYNTHETIC agreed item", treatment); Assert.DoesNotContain("unagreed", treatment); Assert.DoesNotContain("DRAFT", treatment);
        Assert.DoesNotContain("INTERNAL", treatment); Assert.DoesNotContain("987654321", treatment);
        var prescription = await s.Owner.GetStringAsync(Url(s.Member.Id) + "/record/prescriptions");
        Assert.Contains("SYNTHETIC signed advice", prescription); Assert.DoesNotContain("DRAFT", prescription);
    }

    [Fact]
    public async Task ReadOnlyStaffGrantBlocksHttpAndServiceDecisions()
    {
        var s = await Prepare(); var row = await Submit(s); var userName = "limited." + Guid.NewGuid().ToString("N"); int actor;
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var staff = new ApplicationUser { UserName = userName, FullName = "SYNTHETIC readonly assistant", AccountType = "Staff", IsActive = true };
            Assert.True((await users.CreateAsync(staff, fixture.Password)).Succeeded); Assert.True((await users.AddToRoleAsync(staff, "ClinicAssistant")).Succeeded); actor = staff.Id;
            var employee = new EmployeeProfile { AccountId = staff.Id, FullName = staff.FullName, EmployeeCode = "LIMIT-" + Guid.NewGuid().ToString("N") };
            db.Add(employee); await db.SaveChangesAsync();
            var code = "LIMIT-" + Guid.NewGuid().ToString("N");
            var group = new AccessGroup { Code = code, Name = code, NormalizedCode = code.ToUpperInvariant(), NormalizedName = code.ToUpperInvariant(), BaseRole = "ClinicAssistant" };
            group.Grants.Add(new AccessGroupGrant { GroupId = group.Id, Permission = "patient-links.read" });
            db.AddRange(group, new StaffAccessAssignment { AccountId = actor, GroupId = group.Id, BranchIds = [s.BranchId] }, new SchedulingMember { BranchId = s.BranchId, EmployeeId = employee.Id });
            await db.SaveChangesAsync();
        }
        using var client = await Client(userName);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/family-record-reviews")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/family-record-reviews/{row.Id}/verify", Verify(s, row))).StatusCode);
        using var directScope = fixture.Services.CreateScope();
        var exception = await Assert.ThrowsAsync<DCMS.Application.Common.BusinessException>(() => directScope.ServiceProvider.GetRequiredService<IFamilyRecordService>().VerifyAsync(actor, row.Id, Verify(s, row), CancellationToken.None));
        Assert.Equal(403, exception.Status);
    }

    [Fact]
    public async Task BookingUsesOwnedRelativeSnapshotAndDoesNotCreatePatientOrGrant()
    {
        var s = await Prepare(); var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12));
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var week = new WorkingWeek { BranchId = s.BranchId, EmployeeId = s.DentistId, WeekStart = targetDate.AddDays(-(((int)targetDate.DayOfWeek + 6) % 7)), PublishedBy = s.DentistAccountId, PublishedAt = DateTimeOffset.UtcNow };
            for (var day = 0; day < 7; day++) week.Periods.Add(new WorkingPeriod { WeekId = week.Id, Day = day, StartMinute = 0, EndMinute = 1440, Kind = "Work" });
            db.Add(week); await db.SaveChangesAsync();
        }
        var slots = (await s.Owner.GetFromJsonAsync<AvailabilityResult>($"/api/availability?branchId={s.BranchId}&dentistId={s.DentistId}&serviceId={s.ServiceId}&date={targetDate:yyyy-MM-dd}&durationMinutes=30"))!.Slots;
        var slot = slots.First();
        var request = new CreateAppointmentRequest(s.BranchId, new("SYNTHETIC delegate", "0912345678", null), new(null, "Untrusted name", null), "Relative", "Standard", "App", s.ServiceId, null, s.DentistId, null, null, slot.Start, 30, null, null, Guid.NewGuid(), slot.ScheduleVersion, null, FamilyMemberId: s.Member.Id);
        using var stranger = await Client(); Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync("/api/appointments", request)).StatusCode);
        var response = await s.Owner.PostAsJsonAsync("/api/appointments", request); Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<AppointmentResult>())!;
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var appointment = await db.Appointments.SingleAsync(x => x.Id == result.AppointmentId);
            Assert.Equal(s.Member.FullName, appointment.AttendeeName); Assert.Equal(s.Member.BirthDate, appointment.AttendeeBirthDate); Assert.Null(appointment.PatientId); Assert.Equal(s.OwnerId, appointment.BookerAccountId);
            Assert.False(await db.Set<FamilyRecordAccess>().AnyAsync(x => x.FamilyMemberId == s.Member.Id));
        }
        var booked = await s.Owner.GetFromJsonAsync<FamilyPage<FamilyAppointmentDto>>(Url(s.Member.Id) + "/appointments"); Assert.Single(booked!.Items);
        (await s.Owner.SendAsync(new HttpRequestMessage(HttpMethod.Delete, Url(s.Member.Id)) { Content = JsonContent.Create(new DeleteFamilyMember(s.Member.Version, Guid.NewGuid())) })).EnsureSuccessStatusCode();
        var replay = await s.Owner.PostAsJsonAsync("/api/appointments", request); replay.EnsureSuccessStatusCode(); Assert.Equal(result.AppointmentId, (await replay.Content.ReadFromJsonAsync<AppointmentResult>())!.AppointmentId);
    }
}

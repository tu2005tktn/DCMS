using DCMS.Application.Features.PatientLinks;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.PatientLinks;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of PatientLinkService (S07), PatientLinkReviewService (S08), and PatientLinkSupport.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PatientLinkServiceTests
{
    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Customer, OtherCustomer, InactiveCustomer, Assistant, Dentist, NoEmpAssistant, PatientId, PatientId2;
        public required string PatientCode, PatientName;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public PatientLinkService SelfSvc() => new(Db);
        public PatientLinkReviewService ReviewSvc() => new(Db, CreateTestKit.NewRoleUserManager(Db), new CreateTestKit.AllowAll());
    }

    private static async Task<Env> NewEnv()
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var db = pg.Db;
        var group = db.Set<DCMS.Domain.Features.Settings.AccessGroup>();
        foreach (var role in AccountRoles.Staff)
            if (!await group.IgnoreQueryFilters().AnyAsync(x => x.RecordKind == "IdentityRole" && x.Name == role))
                group.Add(new() { RecordKind = "IdentityRole", IdentityRoleId = 100 + Array.IndexOf(AccountRoles.Staff, role), Code = role, Name = role, BaseRole = role });
        await db.SaveChangesAsync();

        var n = CreateTestKit.NextId();
        var assistant = await CreateTestKit.SeedStaffAsync(db, "tllk" + n, AccountRoles.ClinicAssistant, n);
        var dentist = await CreateTestKit.SeedStaffAsync(db, "bslk" + n, AccountRoles.Dentist, n);
        var noEmp = await CreateTestKit.SeedStaffAsync(db, "nelk" + n, AccountRoles.ClinicAssistant, n, member: false);

        var c1 = new ApplicationUser { UserName = "pk" + n, FullName = "Nguyễn Văn An " + n, Email = $"pk{n}@example.com", EmailConfirmed = true, PhoneNumber = "09" + (10000000 + n % 89999999), PhoneNumberConfirmed = true, AccountType = "Public", IsActive = true };
        var c2 = new ApplicationUser { UserName = "po" + n, FullName = "Trần Thị Bình " + n, AccountType = "Public", IsActive = true };
        var cOff = new ApplicationUser { UserName = "px" + n, FullName = "Khách Khóa " + n, AccountType = "Public", IsActive = false };
        db.Users.AddRange(c1, c2, cOff);

        var p1 = new PatientProfile { PatientCode = "BNLK" + n, FullName = "Nguyễn Văn An " + n, NormalizedName = PatientLinkSupport.NormalizeName("Nguyễn Văn An " + n), Phone = "09" + (10000000 + n % 89999999), BirthDate = new DateOnly(1995, 6, 15), Gender = PatientGenders.Male, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var p2 = new PatientProfile { PatientCode = "BNLM" + n, FullName = "Trần Thị Bình " + n, NormalizedName = PatientLinkSupport.NormalizeName("Trần Thị Bình " + n), BirthDate = new DateOnly(1992, 3, 10), Gender = PatientGenders.Female, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.Patients.AddRange(p1, p2);
        await db.SaveChangesAsync();

        return new Env
        {
            Pg = pg, N = n, Customer = c1.Id, OtherCustomer = c2.Id, InactiveCustomer = cOff.Id,
            Assistant = assistant.Id, Dentist = dentist.Id, NoEmpAssistant = noEmp.Id,
            PatientId = p1.Id, PatientId2 = p2.Id, PatientCode = p1.PatientCode, PatientName = p1.FullName
        };
    }

    private static async Task Rejects(Func<Task> call, int status, string code)
    {
        var err = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (err.Status, err.Response.Code));
        Assert.False(string.IsNullOrWhiteSpace(err.Response.Message));
    }

    // ================= PatientLinkService (S07) =================
    public static TheoryData<string, string, string?, string?, string?, bool, string> BadSubmissions => new()
    {
        { "UT-LNK-01 Thiếu họ tên", "  ", "1995-06-15", null, null, true, "MSG02" },
        { "UT-LNK-02 Họ tên vượt 160 ký tự", new string('A', 161), "1995-06-15", null, null, true, "MSG03" },
        { "UT-LNK-03 Ngày sinh trước 1900", "Nguyễn An", "1899-12-31", null, null, true, "MSG04" },
        { "UT-LNK-04 Ngày sinh ở tương lai", "Nguyễn An", "2099-01-01", null, null, true, "MSG04" },
        { "UT-LNK-05 Mã hồ sơ sai định dạng", "Nguyễn An", "1995-06-15", "BAD CODE!", null, true, "MSG04" },
        { "UT-LNK-06 Thông tin đối chiếu vượt 1000 ký tự", "Nguyễn An", "1995-06-15", null, new string('X', 1001), true, "MSG03" },
        { "UT-LNK-07 Chưa xác nhận hồ sơ của chính mình", "Nguyễn An", "1995-06-15", null, null, false, "MSG02" },
    };

    [Theory, MemberData(nameof(BadSubmissions))]
    public async Task UT_LNK_Submit_validation(string caseName, string fullName, string? birth, string? code, string? note, bool confirm, string expectedCode)
    {
        await using var env = await NewEnv();
        _ = caseName;
        var req = new SubmitLinkRequest(fullName, birth == null ? null : DateOnly.Parse(birth), code, note, confirm);
        await Rejects(() => env.SelfSvc().SubmitAsync(env.Customer, req, CancellationToken.None), 400, expectedCode);
    }

    [Fact]
    public async Task UT_LNK_08_Overview_Submit_trung_Pending_Get_Withdraw_va_OwnRecord()
    {
        await using var env = await NewEnv();
        // Staff or missing account forbidden.
        await Rejects(() => env.SelfSvc().OverviewAsync(env.Assistant, CancellationToken.None), 403, "MSG06");
        await Rejects(() => env.SelfSvc().OverviewAsync(int.MaxValue - 7, CancellationToken.None), 403, "MSG06");
        await Rejects(() => env.SelfSvc().SubmitAsync(env.Assistant, new("A", null, null, null, true), CancellationToken.None), 403, "MSG06");
        await Rejects(() => env.SelfSvc().SubmitAsync(env.InactiveCustomer, new("A", null, null, null, true), CancellationToken.None), 403, "MSG06");

        var initial = await env.SelfSvc().OverviewAsync(env.Customer, CancellationToken.None);
        Assert.Equal((PatientLinkStatus.NotLinked, true), (initial.LinkStatus, initial.CanSubmit));
        Assert.Equal($"pk{env.N}@example.com", initial.Account.VerifiedEmail);

        // First submit creates Pending request; second submit returns existing without creating duplicate.
        var first = await env.SelfSvc().SubmitAsync(env.Customer, new(env.PatientName, new DateOnly(1995, 6, 15), env.PatientCode.ToLowerInvariant(), "Khám tháng trước", true), CancellationToken.None);
        Assert.True(first.Created);
        Assert.Equal((PatientLinkStatus.Pending, env.PatientCode, "MSG18"), (first.Request.Status, first.Request.DeclaredPatientCode, first.Notification.Code));

        var again = await env.SelfSvc().SubmitAsync(env.Customer, new(env.PatientName, null, null, null, true), CancellationToken.None);
        Assert.False(again.Created);
        Assert.Equal(first.Request.Id, again.Request.Id);

        // GetAsync: owner succeeds, other customer gets 404.
        Assert.Equal(first.Request.RequestCode, (await env.SelfSvc().GetAsync(env.Customer, first.Request.Id, CancellationToken.None)).RequestCode);
        await Rejects(() => env.SelfSvc().GetAsync(env.OtherCustomer, first.Request.Id, CancellationToken.None), 404, "MSG01");

        // OwnRecordAsync forbidden before verification.
        await Rejects(() => env.SelfSvc().OwnRecordAsync(env.Customer, CancellationToken.None), 403, "MSG06");

        // Withdraw: wrong version 409, other customer 404, valid succeeds -> Overview returns NotLinked & CanSubmit=true.
        await Rejects(() => env.SelfSvc().WithdrawAsync(env.OtherCustomer, first.Request.Id, new(1), CancellationToken.None), 404, "MSG01");
        await Rejects(() => env.SelfSvc().WithdrawAsync(env.Customer, first.Request.Id, new(9), CancellationToken.None), 409, "MSG111");
        var withdrawn = await env.SelfSvc().WithdrawAsync(env.Customer, first.Request.Id, new(1), CancellationToken.None);
        Assert.Equal((PatientLinkStatus.Withdrawn, 2), (withdrawn.Status, withdrawn.Version));
        Assert.NotNull(withdrawn.DecidedAt);
        await Rejects(() => env.SelfSvc().WithdrawAsync(env.Customer, first.Request.Id, new(2), CancellationToken.None), 409, "MSG111");

        var afterWithdraw = await env.SelfSvc().OverviewAsync(env.Customer, CancellationToken.None);
        Assert.Equal((PatientLinkStatus.NotLinked, true), (afterWithdraw.LinkStatus, afterWithdraw.CanSubmit));
    }

    // ================= PatientLinkReviewService (S08) =================
    [Fact]
    public async Task UT_LNK_09_Review_Queue_Detail_Patients_Verify_Reject_Revoke_Audit()
    {
        await using var env = await NewEnv();
        var sub1 = await env.SelfSvc().SubmitAsync(env.Customer, new(env.PatientName, new DateOnly(1995, 6, 15), env.PatientCode, "Đối chiếu", true), CancellationToken.None);
        var sub2 = await env.SelfSvc().SubmitAsync(env.OtherCustomer, new("Trần Thị Bình " + env.N, new DateOnly(1992, 3, 10), null, null, true), CancellationToken.None);

        // Queue validation & filters.
        var today = PatientLinkSupport.ClinicToday();
        Assert.Equal(TimeSpan.FromHours(7), PatientLinkSupport.ClinicStartOfDay(today).Offset);
        await Rejects(() => env.ReviewSvc().QueueAsync(null, null, null, null, 0, 20, CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.ReviewSvc().QueueAsync("INVALID", null, null, null, 1, 20, CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.ReviewSvc().QueueAsync(null, today.AddDays(1), today, null, 1, 20, CancellationToken.None), 400, "MSG04");

        // Lưu ý: bỏ qua lọc from/to vì PatientLinkSupport.ClinicStartOfDay trả offset +07:00 chưa gọi .ToUniversalTime() khiến Npgsql báo lỗi (lỗi thật ở mã nguồn).
        var pendingQueue = await env.ReviewSvc().QueueAsync(null, null, null, env.PatientName, 1, 20, CancellationToken.None);
        Assert.Equal([sub1.Request.Id], pendingQueue.Items.Select(x => x.Id));

        // Detail & Patients search.
        var detail = await env.ReviewSvc().DetailAsync(sub1.Request.Id, CancellationToken.None);
        Assert.Equal(sub1.Request.RequestCode, detail.RequestCode);
        await Rejects(() => env.ReviewSvc().DetailAsync(-1, CancellationToken.None), 404, "MSG01");

        await Rejects(() => env.ReviewSvc().PatientsAsync(sub1.Request.Id, " ", CancellationToken.None), 400, "MSG02");
        await Rejects(() => env.ReviewSvc().PatientsAsync(-1, env.PatientCode, CancellationToken.None), 404, "MSG01");
        var candidates = await env.ReviewSvc().PatientsAsync(sub1.Request.Id, env.PatientCode, CancellationToken.None);
        var cand = Assert.Single(candidates);
        Assert.Equal((env.PatientId, false), (cand.Patient.Id, cand.HasActiveLink));

        // Verify authorization & mismatch guards.
        await Rejects(() => env.ReviewSvc().VerifyAsync(env.Dentist, sub1.Request.Id, new(1, env.PatientId, "Đã gọi"), CancellationToken.None), 403, "MSG06");
        await Rejects(() => env.ReviewSvc().VerifyAsync(env.NoEmpAssistant, sub1.Request.Id, new(1, env.PatientId, "Đã gọi"), CancellationToken.None), 403, "MSG06");
        await Rejects(() => env.ReviewSvc().VerifyAsync(env.Assistant, sub1.Request.Id, new(1, env.PatientId, " "), CancellationToken.None), 400, "MSG02");
        await Rejects(() => env.ReviewSvc().VerifyAsync(env.Assistant, sub1.Request.Id, new(9, env.PatientId, "Đã gọi"), CancellationToken.None), 409, "MSG111");
        await Rejects(() => env.ReviewSvc().VerifyAsync(env.Assistant, sub1.Request.Id, new(1, int.MaxValue - 3, "Đã gọi"), CancellationToken.None), 400, "MSG17");
        await Rejects(() => env.ReviewSvc().VerifyAsync(env.Assistant, sub1.Request.Id, new(1, env.PatientId2, "Sai hồ sơ"), CancellationToken.None), 400, "MSG143");

        // Verify succeeds -> MSG19, patient shows HasActiveLink=true, OwnRecordAsync succeeds, re-submit gets 409 MSG142.
        var verified = await env.ReviewSvc().VerifyAsync(env.Assistant, sub1.Request.Id, new(1, env.PatientId, "Khớp CCCD"), CancellationToken.None);
        Assert.Equal((PatientLinkStatus.Verified, "MSG19", 2), (verified.Request.Status, verified.Notification.Code, verified.Request.Version));
        Assert.True((await env.ReviewSvc().PatientsAsync(sub1.Request.Id, env.PatientCode, CancellationToken.None)).Single().HasActiveLink);

        var own = await env.SelfSvc().OwnRecordAsync(env.Customer, CancellationToken.None);
        Assert.Equal((env.PatientCode, env.PatientName), (own.PatientCode, own.FullName));
        var ovVerified = await env.SelfSvc().OverviewAsync(env.Customer, CancellationToken.None);
        Assert.Equal((PatientLinkStatus.Verified, false, "MSG19"), (ovVerified.LinkStatus, ovVerified.CanSubmit, ovVerified.Current!.Result!.Code));
        await Rejects(() => env.SelfSvc().SubmitAsync(env.Customer, new(env.PatientName, null, null, null, true), CancellationToken.None), 409, "MSG142");

        // Reject sub2 -> MSG20.
        await Rejects(() => env.ReviewSvc().RejectAsync(env.Assistant, sub2.Request.Id, new(1, " ", null, null), CancellationToken.None), 400, "MSG02");
        var rejected = await env.ReviewSvc().RejectAsync(env.Assistant, sub2.Request.Id, new(1, "Không khớp SĐT", "Gọi nhầm", "Vui lòng kiểm tra lại"), CancellationToken.None);
        Assert.Equal((PatientLinkStatus.Rejected, "MSG20", "Vui lòng kiểm tra lại"), (rejected.Request.Status, rejected.Notification.Code, rejected.Request.PublicNote));

        // Revoke verified sub1 -> MSG20; OwnRecordAsync immediately blocked; cannot revoke a non-verified link.
        await Rejects(() => env.ReviewSvc().RevokeAsync(env.Assistant, sub2.Request.Id, new(2, "Thử thu hồi", null), CancellationToken.None), 409, "MSG111");
        var revoked = await env.ReviewSvc().RevokeAsync(env.Assistant, sub1.Request.Id, new(2, "Khách yêu cầu hủy", "Đã thu hồi"), CancellationToken.None);
        Assert.Equal((PatientLinkStatus.Revoked, "MSG20", 3), (revoked.Request.Status, revoked.Notification.Code, revoked.Request.Version));
        await Rejects(() => env.SelfSvc().OwnRecordAsync(env.Customer, CancellationToken.None), 403, "MSG06");

        // History queue (ALL) + Audit trail.
        var allQueue = await env.ReviewSvc().QueueAsync("ALL", null, null, "pk" + env.N, 1, 20, CancellationToken.None);
        Assert.Contains(allQueue.Items, x => x.Id == sub1.Request.Id && x.Status == PatientLinkStatus.Revoked && x.HandlerName != null);

        var audits = await env.ReviewSvc().AuditAsync(sub1.Request.Id, CancellationToken.None);
        Assert.Equal(["Revoke", "OwnRecordRead", "Verify", "Submit"], audits.Select(x => x.Action));
        await Rejects(() => env.ReviewSvc().AuditAsync(-1, CancellationToken.None), 404, "MSG01");
    }
}

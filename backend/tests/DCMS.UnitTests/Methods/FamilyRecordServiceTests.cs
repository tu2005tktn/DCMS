using DCMS.Application.Common;
using DCMS.Application.Features.FamilyMembers;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.FamilyMembers;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.FamilyMembers;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of FamilyRecordService (a customer asks to view a relative's clinical record; the assistant verifies, rejects or revokes):
/// OverviewAsync, SubmitAsync, WithdrawAsync, QueueAsync, DetailAsync, CandidatesAsync, AuditAsync, VerifyAsync, DecideAsync, EndAccessAsync
/// and helpers (Text, Page, Status, PublicAsync, MemberAsync, StaffAsync, ReviewRowAsync, WriteAsync idempotency). Runs on PostgreSQL.
/// The shared schema keeps other tests' rows, so queue/list assertions always filter by a per-test tag in the declared name.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class FamilyRecordServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private const string Denied = "FAMILY_ACCESS_DENIED", Missing = "FAMILY_ACCESS_NOT_FOUND", Invalid = "FAMILY_ACCESS_VALIDATION";

    private sealed class Access : ISettingsAccess
    {
        public int[] Branches = [];
        public string[] Roles = [AccountRoles.ClinicAssistant];
        public string[] Denied = [];
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(false, false, Roles, [], Branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) =>
            Denied.Contains(permission) ? throw new BusinessException(403, SystemMessages.Get("MSG06")) : ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Other, Assistant, Owner, DentistEmp, PatientId, ChairId;
        public required Guid MemberId;
        public readonly Access Access = new();
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public string Tag => $"T{N}x";
        public int seq;
        public FamilyRecordService Svc() { Db.ChangeTracker.Clear(); return new(Db, new TestClock(Now), Access, new ConfigurationBuilder().Build()); }

        public async Task<int> NewOwner(string type = "Public", bool active = true, bool locked = false)
        {
            var u = new ApplicationUser { UserName = $"chu{N}x{++seq}", FullName = $"Chủ tài khoản {N}x{seq}", AccountType = type, IsActive = active };
            if (locked) { u.LockoutEnabled = true; u.LockoutEnd = Now.AddHours(1); }
            Db.Users.Add(u); await Db.SaveChangesAsync(); return u.Id;
        }
        public async Task<Guid> NewMember(int? owner = null, string? name = null, DateOnly? birth = null, bool deleted = false)
        {
            var m = new FamilyMember { OwnerAccountId = owner ?? Owner, FullName = name ?? $"Người thân {Tag} {++seq}", Relationship = "Mother", BirthDate = birth, DeletedAt = deleted ? Now : null };
            Db.Set<FamilyMember>().Add(m); await Db.SaveChangesAsync(); return m.Id;
        }
        public async Task<FamilyRecordAccess> Seed(string status, int? branch = null, DateTimeOffset? expires = null, int? owner = null, string? name = null, int minutes = 0, Guid? member = null, int version = 1)
        {
            var o = owner ?? Owner;
            var memberId = member ?? await NewMember(o, name);
            var memberRow = await Db.Set<FamilyMember>().AsNoTracking().SingleAsync(x => x.Id == memberId); var fullName = memberRow.FullName;
            var row = new FamilyRecordAccess { FamilyMemberId = memberId, OwnerAccountId = o, Code = $"FA-{Tag}-{++seq}", Status = status, BranchId = branch, ExpiresAt = expires,
                RequestedAt = Now.AddMinutes(minutes), DeclaredFullName = fullName, DeclaredBirthDate = memberRow.BirthDate, Relationship = "Mother", Version = version, Scopes = status == "Verified" ? ["Treatment"] : [] };
            Db.Set<FamilyRecordAccess>().Add(row); await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); return row;
        }
        public async Task<FamilyRecordAccess> Row(Guid id) { Db.ChangeTracker.Clear(); return await Db.Set<FamilyRecordAccess>().AsNoTracking().SingleAsync(x => x.Id == id); }
        public async Task<List<FamilyRecordAudit>> Audits(Guid id) { Db.ChangeTracker.Clear(); return await Db.Set<FamilyRecordAudit>().AsNoTracking().Where(x => x.AccessId == id).OrderBy(x => x.Id).ToListAsync(); }

        public Task<FamilyAccessDto> Submit(int? owner = null, Guid? member = null, string? code = null, string? note = null, Guid? key = null) =>
            Svc().SubmitAsync(owner ?? Owner, member ?? MemberId, new FamilyAccessRequest(code, note, key ?? Guid.NewGuid()), default);

        /// <summary>A pending request of the main member (same name/birth date as the patient) ready to be verified.</summary>
        public async Task<FamilyRecordAccess> Pending() => await Seed("Pending", member: MemberId, owner: Owner);
        public VerifyFamilyAccess Cmd(int version = 1, Func<VerifyFamilyAccess, VerifyFamilyAccess>? change = null)
        {
            var c = new VerifyFamilyAccess(PatientId, N, ["Treatment", "Examinations"], Now.AddDays(30), "PatientConsent", "Đã đối chiếu giấy tờ", true, version, Guid.NewGuid());
            return change == null ? c : change(c);
        }
        public Task<FamilyAccessDto> Verify(Guid id, VerifyFamilyAccess cmd, int? actor = null) => Svc().VerifyAsync(actor ?? Assistant, id, cmd, default);
        public Task<FamilyAccessDto> Decide(Guid id, bool revoke, string reason = "Không đủ căn cứ", string? note = null, int version = 1, Guid? key = null) =>
            Svc().DecideAsync(Assistant, id, revoke, new DecideFamilyAccess(reason, note, version, key ?? Guid.NewGuid()), default);
    }

    private static async Task<Env> NewEnv()
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var db = pg.Db;
        var n = CreateTestKit.NextId();
        var other = CreateTestKit.NextId();
        var assistant = await CreateTestKit.SeedStaffAsync(db, "tl" + n, AccountRoles.ClinicAssistant, n);
        var dentist = await CreateTestKit.SeedStaffAsync(db, "bs" + n, AccountRoles.Dentist, n);
        await CreateTestKit.SeedStaffAsync(db, "ot" + n, AccountRoles.ClinicAssistant, other);
        var owner = new ApplicationUser { UserName = $"chu{n}", FullName = "Chủ tài khoản " + n, AccountType = "Public", IsActive = true };
        db.Users.Add(owner);
        var patient = new PatientProfile { PatientCode = "BN" + n, FullName = "Trần Thị Mai", NormalizedName = "TRAN THI MAI", BirthDate = new DateOnly(1990, 1, 1), Phone = $"09{n:D8}", CreatedAt = Now, UpdatedAt = Now };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        (await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == n)).Chairs.Add(new SchedulingChair { Id = n, BranchId = n, Name = "Ghế " + n });
        await db.SaveChangesAsync();
        var member = new FamilyMember { OwnerAccountId = owner.Id, FullName = "Trần Thị Mai", Relationship = "Mother", BirthDate = new DateOnly(1990, 1, 1) };
        db.Set<FamilyMember>().Add(member);
        await db.SaveChangesAsync();
        var env = new Env { Pg = pg, N = n, Other = other, Assistant = assistant.Id, Owner = owner.Id, MemberId = member.Id, PatientId = patient.Id, ChairId = n,
            DentistEmp = (await db.Employees.SingleAsync(x => x.AccountId == dentist.Id)).Id };
        env.Access.Branches = [n];
        return env;
    }

    /// <summary>A visit of the patient in the branch (this is how a patient "belongs" to a branch).</summary>
    private static async Task AddVisit(Env env, int? patient = null, int? branch = null, string status = VisitStatuses.Completed)
    {
        var appointment = new Appointment { Code = "LH" + CreateTestKit.NextId(), BranchId = branch ?? env.N, DentistId = env.DentistEmp, ChairId = env.ChairId, AttendeeName = "Mai", StartAt = Now, EndAt = Now.AddHours(1),
            Status = "CheckedIn", Version = 1, CreatedAt = Now, BookingInfo = new AppointmentBookingInfo { BookerName = "Mai", BookerContact = "0987654321" } };
        env.Db.Appointments.Add(appointment); await env.Db.SaveChangesAsync();
        env.Db.Visits.Add(new Visit { AppointmentId = appointment.Id, BranchId = branch ?? env.N, PatientId = patient ?? env.PatientId, DentistId = env.DentistEmp, ArrivalAt = Now, RecordedBy = env.Assistant, Status = status });
        await env.Db.SaveChangesAsync();
    }

    private static void Same(FamilyAccessDto expected, FamilyAccessDto actual)
    {
        Assert.Equal(expected with { Scopes = [] }, actual with { Scopes = [] });
        Assert.Equal(expected.Scopes, actual.Scopes);
    }

    private static async Task Rejects(Func<Task> call, int status, string code, string? field = null)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal(status, error.Status);
        Assert.Equal(code, error.Response.Code);
        if (field != null) Assert.Equal(field, error.Response.Field);
    }

    // ================= Owner gate (PublicAsync / MemberAsync) =================
    [Theory]
    [InlineData("UT-FAM-01 Tài khoản nhân viên (không phải Public)", "Staff", true, false)]
    [InlineData("UT-FAM-02 Tài khoản Public đã bị khóa hoạt động", "Public", false, false)]
    [InlineData("UT-FAM-03 Tài khoản Public đang bị khóa đăng nhập", "Public", true, true)]
    public async Task Submit_Overview_Withdraw_reject_owners_that_are_not_active_public_accounts(string caseName, string type, bool active, bool locked)
    {
        await using var env = await NewEnv();
        var owner = await env.NewOwner(type, active, locked);
        var member = await env.NewMember(owner);
        await Rejects(() => env.Submit(owner, member), 403, Denied);
        await Rejects(() => env.Svc().OverviewAsync(owner, member, default), 403, Denied);
        await Rejects(() => env.Svc().WithdrawAsync(owner, member, Guid.NewGuid(), new(1, Guid.NewGuid()), default), 403, Denied);
    }

    [Fact]
    public async Task Submit_UT_FAM_04_unknown_account_is_denied_and_an_expired_lockout_is_allowed()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Submit(2147483000, env.MemberId), 403, Denied);
        var owner = await env.NewOwner();
        env.Db.ChangeTracker.Clear(); var tracked = await env.Db.Users.SingleAsync(x => x.Id == owner); tracked.LockoutEnabled = true; tracked.LockoutEnd = Now.AddMinutes(-1); await env.Db.SaveChangesAsync();
        Assert.Equal("Pending", (await env.Submit(owner, await env.NewMember(owner))).Status);
    }

    [Theory]
    [InlineData("UT-FAM-05 Người thân không tồn tại", "missing")]
    [InlineData("UT-FAM-06 Người thân của tài khoản khác", "other")]
    [InlineData("UT-FAM-07 Người thân đã bị xóa", "deleted")]
    public async Task Submit_Overview_Withdraw_return_404_for_a_member_the_owner_cannot_use(string caseName, string kind)
    {
        await using var env = await NewEnv();
        var other = await env.NewOwner();
        var member = kind switch { "missing" => Guid.NewGuid(), "other" => await env.NewMember(other), _ => await env.NewMember(env.Owner, deleted: true) };
        await Rejects(() => env.Submit(env.Owner, member), 404, Missing);
        await Rejects(() => env.Svc().OverviewAsync(env.Owner, member, default), 404, Missing);
        await Rejects(() => env.Svc().WithdrawAsync(env.Owner, member, Guid.NewGuid(), new(1, Guid.NewGuid()), default), 404, Missing);
    }

    // ================= SubmitAsync =================
    [Theory]
    [InlineData("UT-FAM-08 Mã bệnh nhân 41 ký tự", 41, 0, "patientCode")]
    [InlineData("UT-FAM-09 Thông tin đối chiếu 1001 ký tự", 0, 1001, "matchingInformation")]
    public async Task Submit_rejects_text_over_the_limit(string caseName, int codeLength, int noteLength, string field)
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Submit(code: codeLength == 0 ? null : new string('a', codeLength), note: noteLength == 0 ? null : new string('b', noteLength)), 400, Invalid, field);
    }

    [Fact]
    public async Task Submit_UT_FAM_10_empty_request_key_is_rejected()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Submit(key: Guid.Empty), 400, Invalid, "requestKey");
    }

    [Fact]
    public async Task Submit_UT_FAM_11_creates_a_pending_request_copying_the_member_data_and_writes_audit_and_receipt()
    {
        await using var env = await NewEnv();
        var key = Guid.NewGuid();

        var dto = await env.Submit(code: "  " + new string('a', 40) + "  ", note: new string('b', 1000), key: key);

        Assert.Equal(("Pending", "Trần Thị Mai", new DateOnly(1990, 1, 1), new string('a', 40), 1), (dto.Status, dto.FullName, dto.BirthDate, dto.PatientCode, dto.Version));
        Assert.StartsWith("FA-", dto.Code);
        Assert.Equal(Now, dto.RequestedAt);
        Assert.Equal(new string('b', 1000), dto.MatchingInformation);
        Assert.Empty(dto.Scopes);
        var row = await env.Row(dto.Id);
        Assert.Equal((env.Owner, env.MemberId, "Mother"), (row.OwnerAccountId, row.FamilyMemberId, row.Relationship));
        var audit = Assert.Single(await env.Audits(dto.Id));
        Assert.Equal(("Submitted", env.Owner), (audit.Action, audit.ActorAccountId));
        Assert.True(await env.Db.Set<FamilyRecordReceipt>().AnyAsync(x => x.ActorAccountId == env.Owner && x.RequestKey == key && x.AccessId == dto.Id));
    }

    [Fact]
    public async Task Submit_UT_FAM_12_blank_texts_are_stored_as_null()
    {
        await using var env = await NewEnv();
        var dto = await env.Submit(code: "   ", note: "");
        Assert.Equal(((string?)null, (string?)null), (dto.PatientCode, dto.MatchingInformation));
    }

    [Fact]
    public async Task Submit_UT_FAM_13_same_key_and_same_content_replays_the_first_result()
    {
        await using var env = await NewEnv();
        var key = Guid.NewGuid();
        var first = await env.Submit(code: "BN1", key: key);
        var second = await env.Submit(code: "BN1", key: key);
        Same(first, second);
        Assert.Equal(1, await env.Db.Set<FamilyRecordAccess>().CountAsync(x => x.FamilyMemberId == env.MemberId));
        Assert.Single(await env.Audits(first.Id));
    }

    [Fact]
    public async Task Submit_UT_FAM_14_same_key_with_different_content_returns_409_MSG111()
    {
        await using var env = await NewEnv();
        var key = Guid.NewGuid();
        await env.Submit(code: "BN1", key: key);
        await Rejects(() => env.Submit(code: "BN2", key: key), 409, "MSG111");
    }

    [Fact]
    public async Task Submit_UT_FAM_15_a_second_submit_while_pending_returns_the_existing_request()
    {
        await using var env = await NewEnv();
        var first = await env.Submit();
        var second = await env.Submit();
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await env.Db.Set<FamilyRecordAccess>().CountAsync(x => x.FamilyMemberId == env.MemberId));
    }

    [Fact]
    public async Task Submit_UT_FAM_16_submit_while_a_verified_access_is_still_valid_returns_409()
    {
        await using var env = await NewEnv();
        await env.Seed("Verified", env.N, Now.AddDays(10), member: env.MemberId);
        await Rejects(() => env.Submit(), 409, "MSG111");
    }

    [Fact]
    public async Task Submit_UT_FAM_17_submit_after_a_verified_access_expired_closes_the_old_one_and_opens_a_new_pending_one()
    {
        await using var env = await NewEnv();
        var old = await env.Seed("Verified", env.N, Now.AddSeconds(-1), member: env.MemberId);

        var dto = await env.Submit();

        Assert.NotEqual(old.Id, dto.Id);
        Assert.Equal("Pending", dto.Status);
        var closed = await env.Row(old.Id);
        Assert.Equal(("Expired", 2), (closed.Status, closed.Version));
        Assert.Equal("Expired", (await env.Audits(old.Id)).Single().Action);
    }

    [Fact]
    public async Task Submit_UT_FAM_18_a_rejected_or_withdrawn_request_does_not_block_a_new_one()
    {
        await using var env = await NewEnv();
        await env.Seed("Rejected", env.N, member: env.MemberId);
        await env.Seed("Withdrawn", env.N, member: env.MemberId);
        Assert.Equal("Pending", (await env.Submit()).Status);
    }

    // ================= WithdrawAsync =================
    [Theory]
    [InlineData("UT-FAM-19 Rút yêu cầu đang chờ", "Pending", "Withdrawn")]
    [InlineData("UT-FAM-20 Chấm dứt quyền đã xác minh", "Verified", "Revoked")]
    public async Task Withdraw_ends_a_pending_or_verified_access(string caseName, string status, string expected)
    {
        await using var env = await NewEnv();
        var row = await env.Seed(status, env.N, Now.AddDays(5), member: env.MemberId);

        var dto = await env.Svc().WithdrawAsync(env.Owner, env.MemberId, row.Id, new(1, Guid.NewGuid()), default);

        Assert.Equal((expected, 2, "Người yêu cầu đã chấm dứt yêu cầu/quyền theo dõi."), (dto.Status, dto.Version, dto.PublicNote));
        var saved = await env.Row(row.Id);
        Assert.Equal((env.Owner, Now), (saved.DecidedBy, saved.DecidedAt));
        Assert.Equal(expected, (await env.Audits(row.Id)).Single().Action);
    }

    [Theory]
    [InlineData("UT-FAM-21 Sai phiên bản", "Pending", 2)]
    [InlineData("UT-FAM-22 Đã bị từ chối", "Rejected", 1)]
    [InlineData("UT-FAM-23 Đã rút trước đó", "Withdrawn", 1)]
    [InlineData("UT-FAM-24 Đã bị thu hồi", "Revoked", 1)]
    [InlineData("UT-FAM-25 Đã hết hạn", "Expired", 1)]
    public async Task Withdraw_returns_409_for_a_stale_version_or_a_finished_request(string caseName, string status, int expectedVersion)
    {
        await using var env = await NewEnv();
        var row = await env.Seed(status, env.N, member: env.MemberId);
        await Rejects(() => env.Svc().WithdrawAsync(env.Owner, env.MemberId, row.Id, new(expectedVersion, Guid.NewGuid()), default), 409, "MSG111");
        Assert.Equal(status, (await env.Row(row.Id)).Status);
    }

    [Fact]
    public async Task Withdraw_UT_FAM_26_unknown_request_or_request_of_another_member_returns_404()
    {
        await using var env = await NewEnv();
        var otherMember = await env.NewMember();
        var row = await env.Seed("Pending", member: otherMember);
        await Rejects(() => env.Svc().WithdrawAsync(env.Owner, env.MemberId, Guid.NewGuid(), new(1, Guid.NewGuid()), default), 404, Missing);
        await Rejects(() => env.Svc().WithdrawAsync(env.Owner, env.MemberId, row.Id, new(1, Guid.NewGuid()), default), 404, Missing);
        Assert.Equal("Pending", (await env.Row(row.Id)).Status);
    }

    [Fact]
    public async Task Withdraw_UT_FAM_27_empty_request_key_is_rejected_and_the_same_key_replays()
    {
        await using var env = await NewEnv();
        var row = await env.Seed("Pending", member: env.MemberId);
        await Rejects(() => env.Svc().WithdrawAsync(env.Owner, env.MemberId, row.Id, new(1, Guid.Empty), default), 400, Invalid, "requestKey");
        var key = Guid.NewGuid();
        var first = await env.Svc().WithdrawAsync(env.Owner, env.MemberId, row.Id, new(1, key), default);
        Same(first, await env.Svc().WithdrawAsync(env.Owner, env.MemberId, row.Id, new(1, key), default));
        Assert.Single(await env.Audits(row.Id));
    }

    // ================= OverviewAsync =================
    [Fact]
    public async Task Overview_UT_FAM_28_member_without_requests_can_request()
    {
        await using var env = await NewEnv();
        var overview = await env.Svc().OverviewAsync(env.Owner, env.MemberId, default);
        Assert.Equal((null, 0, true), (overview.Current, overview.History.Count, overview.CanRequest));
    }

    [Fact]
    public async Task Overview_UT_FAM_29_pending_or_valid_verified_request_is_current_and_blocks_a_new_request()
    {
        await using var env = await NewEnv();
        var rejected = await env.Seed("Rejected", env.N, member: env.MemberId, minutes: -20);
        var pending = await env.Seed("Pending", member: env.MemberId, minutes: -5);
        var overview = await env.Svc().OverviewAsync(env.Owner, env.MemberId, default);
        Assert.Equal((pending.Id, false), (overview.Current!.Id, overview.CanRequest));
        Assert.Equal([pending.Id, rejected.Id], overview.History.Select(x => x.Id));      // newest first
    }

    [Theory]
    [InlineData("UT-FAM-30 Yêu cầu mới nhất bị từ chối", "Rejected", false)]
    [InlineData("UT-FAM-31 Quyền đã hết hạn theo ngày", "Verified", true)]
    [InlineData("UT-FAM-32 Quyền đã bị thu hồi", "Revoked", false)]
    public async Task Overview_allows_a_new_request_when_nothing_is_active(string caseName, string status, bool expired)
    {
        await using var env = await NewEnv();
        var row = await env.Seed(status, env.N, expired ? Now.AddMinutes(-1) : Now.AddDays(1), member: env.MemberId);
        var overview = await env.Svc().OverviewAsync(env.Owner, env.MemberId, default);
        Assert.Equal((row.Id, true), (overview.Current!.Id, overview.CanRequest));
        Assert.Equal(expired ? "Expired" : status, overview.Current.Status);                // a verified access past its expiry is shown as Expired
    }

    [Fact]
    public async Task Overview_UT_FAM_33_history_is_limited_to_50_rows_of_the_member_only()
    {
        await using var env = await NewEnv();
        for (var i = 0; i < 52; i++) await env.Seed("Rejected", env.N, member: env.MemberId, minutes: i);
        await env.Seed("Pending", member: await env.NewMember());                         // another member's row
        Assert.Equal(50, (await env.Svc().OverviewAsync(env.Owner, env.MemberId, default)).History.Count);
    }

    // ================= QueueAsync (staff) =================
    private Task<FamilyPage<FamilyReviewRow>> Queue(Env env, string? status = null, string? keyword = null, int page = 1, int size = 20) =>
        env.Svc().QueueAsync(env.Assistant, status, keyword ?? env.Tag, page, size, default);

    [Theory]
    [InlineData("UT-FAM-34 Trang 0", 0, 20)]
    [InlineData("UT-FAM-35 Trang 1.000.001", 1_000_001, 20)]
    [InlineData("UT-FAM-36 Cỡ trang 0", 1, 0)]
    [InlineData("UT-FAM-37 Cỡ trang 51", 1, 51)]
    public async Task Queue_rejects_invalid_paging(string caseName, int page, int size)
    {
        await using var env = await NewEnv();
        await Rejects(() => Queue(env, page: page, size: size), 400, Invalid, "pagination");
    }

    [Fact]
    public async Task Queue_UT_FAM_38_paging_boundaries_are_accepted()
    {
        await using var env = await NewEnv();
        foreach (var (page, size) in new[] { (1, 1), (1_000_000, 50) }) Assert.Equal((page, size), (await Queue(env, page: page, size: size)).Page == page ? (page, size) : (0, 0));
    }

    [Fact]
    public async Task Queue_UT_FAM_39_unknown_status_and_over_long_keyword_are_rejected()
    {
        await using var env = await NewEnv();
        await Rejects(() => Queue(env, status: "Approved"), 400, Invalid, "status");
        await Rejects(() => Queue(env, keyword: new string('a', 101)), 400, Invalid, "keyword");
        Assert.NotNull(await Queue(env, keyword: new string('a', 100)));
    }

    [Fact]
    public async Task Queue_UT_FAM_40_needs_the_assistant_role_and_the_read_permission()
    {
        await using var env = await NewEnv();
        env.Access.Roles = [AccountRoles.Dentist];
        await Rejects(() => Queue(env), 403, Denied);
        env.Access.Roles = [AccountRoles.ClinicAssistant]; env.Access.Denied = ["patient-links.read"];
        await Rejects(() => Queue(env), 403, "MSG06");
    }

    [Fact]
    public async Task Queue_UT_FAM_41_filters_by_status_and_branch_scope()
    {
        await using var env = await NewEnv();
        var pendingNoBranch = await env.Seed("Pending");
        var pendingMine = await env.Seed("Pending", env.N);
        var pendingOther = await env.Seed("Pending", env.Other);                        // another branch: hidden
        var verified = await env.Seed("Verified", env.N, Now.AddDays(3));
        var verifiedExpired = await env.Seed("Verified", env.N, Now.AddSeconds(-1));
        var rejected = await env.Seed("Rejected", env.N);
        var revoked = await env.Seed("Revoked", env.N);
        var withdrawn = await env.Seed("Withdrawn", env.N);
        var expired = await env.Seed("Expired", env.N);

        async Task<Guid[]> Ids(string? status) => (await Queue(env, status)).Items.Select(x => x.Id).Order().ToArray();
        Assert.Equal(new[] { pendingNoBranch.Id, pendingMine.Id }.Order(), await Ids(null));                       // default filter is Pending
        Assert.Equal(await Ids(null), await Ids("Pending"));
        Assert.Equal([verified.Id], await Ids("Verified"));                                                         // expired verified rows are excluded
        Assert.Equal(new[] { verifiedExpired.Id, expired.Id }.Order(), await Ids("Expired"));
        Assert.Equal([rejected.Id], await Ids("Rejected"));
        Assert.Equal([revoked.Id], await Ids("Revoked"));
        Assert.Equal([withdrawn.Id], await Ids("Withdrawn"));
        var all = await Ids("All");
        Assert.Equal(8, all.Length);
        Assert.DoesNotContain(pendingOther.Id, all);
        Assert.Equal("Expired", (await Queue(env, "All")).Items.Single(x => x.Id == verifiedExpired.Id).Status);
    }

    [Fact]
    public async Task Queue_UT_FAM_42_keyword_matches_declared_name_code_or_account_user_name_and_pages_newest_first()
    {
        await using var env = await NewEnv();
        var a = await env.Seed("Pending", minutes: 1, name: $"Nguyễn {env.Tag} A");
        var b = await env.Seed("Pending", minutes: 3, name: $"Nguyễn {env.Tag} B");
        var c = await env.Seed("Pending", minutes: 2, name: $"Nguyễn {env.Tag} C");

        Assert.Equal([b.Id, c.Id, a.Id], (await Queue(env)).Items.Select(x => x.Id));                              // RequestedAt descending
        Assert.Equal([a.Id], (await Queue(env, keyword: a.Code)).Items.Select(x => x.Id));                         // by code
        Assert.Equal(new[] { a.Id, b.Id, c.Id }.Order(), (await Queue(env, keyword: $"chu{env.N}")).Items.Select(x => x.Id).Intersect([a.Id, b.Id, c.Id]).Order());   // by account user name
        var second = await Queue(env, page: 2, size: 2);
        Assert.Equal((3, 2, 2), (second.Total, second.Page, second.PageSize)); Assert.Equal([a.Id], second.Items.Select(x => x.Id));
        Assert.Empty((await Queue(env, keyword: "zzz-không-có")).Items);
    }

    [Fact]
    public async Task Queue_UT_FAM_43_a_blank_keyword_means_no_keyword_filter()
    {
        await using var env = await NewEnv();
        var row = await env.Seed("Pending", env.N);
        var page = await env.Svc().QueueAsync(env.Assistant, "All", "   ", 1, 50, default);
        Assert.True(page.Total >= 1);
        Assert.Contains(page.Items, x => x.Id == row.Id && x.AccountUserName.StartsWith("chu"));
    }

    // ================= DetailAsync =================
    [Fact]
    public async Task Detail_UT_FAM_44_unknown_request_or_other_branch_returns_404()
    {
        await using var env = await NewEnv();
        var hidden = await env.Seed("Pending", env.Other);
        await Rejects(() => env.Svc().DetailAsync(env.Assistant, Guid.NewGuid(), default), 404, Missing);
        await Rejects(() => env.Svc().DetailAsync(env.Assistant, hidden.Id, default), 404, Missing);
    }

    [Fact]
    public async Task Detail_UT_FAM_45_requires_staff_permission_and_assistant_role()
    {
        await using var env = await NewEnv();
        var row = await env.Seed("Pending");
        env.Access.Roles = [AccountRoles.Dentist];
        await Rejects(() => env.Svc().DetailAsync(env.Assistant, row.Id, default), 403, Denied);
        env.Access.Roles = [AccountRoles.ClinicAssistant]; env.Access.Denied = ["patient-links.read"];
        await Rejects(() => env.Svc().DetailAsync(env.Assistant, row.Id, default), 403, "MSG06");
    }

    [Fact]
    public async Task Detail_UT_FAM_46_pending_request_has_no_patient_and_lists_only_active_branches_of_the_staff()
    {
        await using var env = await NewEnv();
        var row = await env.Seed("Pending");
        await env.Db.Set<SchedulingBranch>().Where(x => x.Id == env.Other).ExecuteUpdateAsync(s => s.SetProperty(x => x.Active, false));
        env.Access.Branches = [env.N, env.Other];

        var detail = await env.Svc().DetailAsync(env.Assistant, row.Id, default);

        Assert.Equal((row.Id, row.OwnerAccountId, "Mother", (FamilyPatientCandidate?)null, (int?)null), (detail.Access.Id, detail.AccountId, detail.Relationship, detail.Patient, detail.BranchId));
        Assert.StartsWith("chu", detail.AccountUserName);
        Assert.Equal([env.N], detail.Branches.Select(x => x.Id));                           // inactive branch is not offered
    }

    [Fact]
    public async Task Detail_UT_FAM_47_verified_request_returns_the_linked_patient_and_decision_details()
    {
        await using var env = await NewEnv();
        var row = await env.Seed("Pending", member: env.MemberId);
        await AddVisit(env);
        await env.Verify(row.Id, env.Cmd());

        var detail = await env.Svc().DetailAsync(env.Assistant, row.Id, default);

        Assert.Equal((env.PatientId, "BN" + env.N, "Trần Thị Mai"), (detail.Patient!.Id, detail.Patient.PatientCode, detail.Patient.FullName)); Assert.Equal([env.N], detail.Patient.BranchIds);
        Assert.Equal((env.N, "PatientConsent", "Đã đối chiếu giấy tờ", "Verified"), (detail.BranchId, detail.SharingBasis, detail.VerificationNote, detail.Access.Status));
    }

    // ================= CandidatesAsync =================
    [Fact]
    public async Task Candidates_UT_FAM_48_keyword_is_required_and_limited()
    {
        await using var env = await NewEnv();
        var row = await env.Seed("Pending");
        await Rejects(() => env.Svc().CandidatesAsync(env.Assistant, row.Id, null, default), 400, Invalid, "keyword");
        await Rejects(() => env.Svc().CandidatesAsync(env.Assistant, row.Id, "   ", default), 400, Invalid, "keyword");
        await Rejects(() => env.Svc().CandidatesAsync(env.Assistant, row.Id, new string('a', 101), default), 400, Invalid, "keyword");
    }

    [Fact]
    public async Task Candidates_UT_FAM_49_needs_the_verify_permission_and_a_visible_request()
    {
        await using var env = await NewEnv();
        var hidden = await env.Seed("Pending", env.Other);
        var row = await env.Seed("Pending");
        await Rejects(() => env.Svc().CandidatesAsync(env.Assistant, hidden.Id, "x", default), 404, Missing);
        env.Access.Denied = ["patient-links.verify"];
        await Rejects(() => env.Svc().CandidatesAsync(env.Assistant, row.Id, "x", default), 403, "MSG06");
    }

    [Fact]
    public async Task Candidates_UT_FAM_50_finds_patients_with_activity_in_the_staff_branches_by_name_code_or_phone()
    {
        await using var env = await NewEnv();
        var row = await env.Seed("Pending");
        await AddVisit(env);
        var patient = await env.Db.Patients.AsNoTracking().SingleAsync(x => x.Id == env.PatientId);

        foreach (var keyword in new[] { "Trần Thị", patient.PatientCode, patient.Phone![^6..] })
        {
            var found = Assert.Single(await env.Svc().CandidatesAsync(env.Assistant, row.Id, keyword, default), x => x.Id == env.PatientId);
            Assert.Equal(patient.PatientCode, found.PatientCode); Assert.Equal([env.N], found.BranchIds);
        }
        Assert.DoesNotContain(await env.Svc().CandidatesAsync(env.Assistant, row.Id, patient.PatientCode + "zz", default), x => x.Id == env.PatientId);
    }

    [Fact]
    public async Task Candidates_UT_FAM_51_excludes_archived_patients_and_patients_without_activity_in_the_staff_branches()
    {
        await using var env = await NewEnv();
        var row = await env.Seed("Pending");
        await AddVisit(env, branch: env.Other);                                            // the only activity is in a branch the staff cannot see
        Assert.DoesNotContain(await env.Svc().CandidatesAsync(env.Assistant, row.Id, env.Tag.Replace("T", "BN").TrimEnd('x'), default), x => x.Id == env.PatientId);
        await AddVisit(env);
        Assert.Contains(await env.Svc().CandidatesAsync(env.Assistant, row.Id, "BN" + env.N, default), x => x.Id == env.PatientId);
        await env.Db.Patients.Where(x => x.Id == env.PatientId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsArchived, true));
        Assert.DoesNotContain(await env.Svc().CandidatesAsync(env.Assistant, row.Id, "BN" + env.N, default), x => x.Id == env.PatientId);
    }

    [Fact]
    public async Task Candidates_UT_FAM_52_a_treatment_dossier_also_links_a_patient_to_a_branch_but_a_deleted_one_does_not()
    {
        await using var env = await NewEnv();
        var row = await env.Seed("Pending");
        var dossier = new TreatmentDossier { Code = "HS" + env.N, PatientId = env.PatientId, BranchId = env.N, DentistId = env.DentistEmp, StartedOn = new DateOnly(2030, 1, 1), Diagnosis = "d", State = "Deleted", Version = 1, CreatedAt = Now, UpdatedAt = Now };
        env.Db.TreatmentDossiers.Add(dossier); await env.Db.SaveChangesAsync();
        Assert.DoesNotContain(await env.Svc().CandidatesAsync(env.Assistant, row.Id, "BN" + env.N, default), x => x.Id == env.PatientId);
        await env.Db.TreatmentDossiers.Where(x => x.Id == dossier.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Open"));
        Assert.Contains(await env.Svc().CandidatesAsync(env.Assistant, row.Id, "BN" + env.N, default), x => x.Id == env.PatientId);
    }

    // ================= AuditAsync =================
    [Fact]
    public async Task Audit_UT_FAM_53_returns_the_history_with_actor_names_and_hides_other_branches()
    {
        await using var env = await NewEnv();
        var dto = await env.Submit();
        var decided = await env.Decide(dto.Id, false, "Không khớp thông tin");

        var rows = await env.Svc().AuditAsync(env.Assistant, dto.Id, default);

        Assert.Equal(["Submitted", "Rejected"], rows.Select(x => x.Action));
        Assert.Equal(("Chủ tài khoản " + env.N, (string?)null, "tl" + env.N, "Không khớp thông tin"), (rows[0].ActorName, rows[0].Reason, rows[1].ActorName, rows[1].Reason));
        Assert.Equal("Rejected", decided.Status);
        var hidden = await env.Seed("Pending", env.Other);
        await Rejects(() => env.Svc().AuditAsync(env.Assistant, hidden.Id, default), 404, Missing);
        await Rejects(() => env.Svc().AuditAsync(env.Assistant, Guid.NewGuid(), default), 404, Missing);
    }

    // ================= VerifyAsync =================
    [Fact]
    public async Task Verify_UT_FAM_54_success_links_the_patient_and_records_scopes_sorted_and_distinct()
    {
        await using var env = await NewEnv();
        var row = await env.Pending();
        await AddVisit(env);
        var cmd = env.Cmd(change: c => c with { Scopes = ["Treatment", "Examinations", "Treatment"], VerificationNote = "  Đã đối chiếu giấy tờ  ", ExpiresAt = Now.AddDays(365) });

        var dto = await env.Verify(row.Id, cmd);

        Assert.Equal(("Verified", 2, Now, Now.AddDays(365)), (dto.Status, dto.Version, dto.DecidedAt, dto.ExpiresAt));
        Assert.Equal(["Examinations", "Treatment"], dto.Scopes);
        var saved = await env.Row(row.Id);
        Assert.Equal((env.PatientId, env.N, "PatientConsent", "Đã đối chiếu giấy tờ", env.Assistant), (saved.PatientId, saved.BranchId, saved.SharingBasis, saved.VerificationNote, saved.DecidedBy));
        var audit = Assert.Single(await env.Audits(row.Id));
        Assert.Equal(("Verified", env.Assistant, $"Patient:{env.PatientId}; Branch:{env.N}; PatientConsent; Examinations,Treatment; {Now.AddDays(365).ToUniversalTime():O}"), (audit.Action, audit.ActorAccountId, audit.Reason));
    }

    [Fact]
    public async Task Verify_UT_FAM_55_legal_representative_basis_and_the_same_key_replay()
    {
        await using var env = await NewEnv();
        var row = await env.Pending();
        await AddVisit(env);
        var cmd = env.Cmd(change: c => c with { SharingBasis = "LegalRepresentative" });
        var first = await env.Verify(row.Id, cmd);
        Same(first, await env.Verify(row.Id, cmd));
        Assert.Single(await env.Audits(row.Id));
        await Rejects(() => env.Verify(row.Id, cmd with { VerificationNote = "Khác" }), 409, "MSG111");
    }

    [Fact]
    public async Task Verify_UT_FAM_56_stale_version_and_non_pending_status_return_409()
    {
        await using var env = await NewEnv();
        await AddVisit(env);
        var row = await env.Pending();
        await Rejects(() => env.Verify(row.Id, env.Cmd(version: 2)), 409, "MSG111");
        var rejected = await env.Seed("Rejected", env.N, member: await env.NewMember(env.Owner, "Trần Thị Mai", new DateOnly(1990, 1, 1)));
        await Rejects(() => env.Verify(rejected.Id, env.Cmd()), 409, "MSG111");
        Assert.Equal("Pending", (await env.Row(row.Id)).Status);
    }

    [Fact]
    public async Task Verify_UT_FAM_57_authorization_failures()
    {
        await using var env = await NewEnv();
        await AddVisit(env);
        var row = await env.Pending();
        var hidden = await env.Seed("Pending", env.Other);
        await Rejects(() => env.Verify(Guid.NewGuid(), env.Cmd()), 404, Missing);
        await Rejects(() => env.Verify(hidden.Id, env.Cmd()), 404, Missing);
        await Rejects(() => env.Verify(row.Id, env.Cmd(change: c => c with { BranchId = env.Other })), 403, Denied);       // branch outside the staff's scope
        env.Access.Denied = ["patient-links.verify"];
        await Rejects(() => env.Verify(row.Id, env.Cmd()), 403, "MSG06");
        env.Access.Denied = []; env.Access.Roles = [AccountRoles.Dentist];
        await Rejects(() => env.Verify(row.Id, env.Cmd()), 403, Denied);
    }

    [Fact]
    public async Task Verify_UT_FAM_58_missing_or_archived_patient_returns_404_and_a_patient_without_activity_in_the_branch_returns_403()
    {
        await using var env = await NewEnv();
        var row = await env.Pending();
        await Rejects(() => env.Verify(row.Id, env.Cmd(change: c => c with { PatientId = 2147483000 })), 404, Missing);
        await Rejects(() => env.Verify(row.Id, env.Cmd()), 403, Denied);                      // no visit or dossier in the branch yet
        await AddVisit(env);
        await env.Db.Patients.Where(x => x.Id == env.PatientId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsArchived, true));
        await Rejects(() => env.Verify(row.Id, env.Cmd()), 404, Missing);
    }

    [Theory]
    [InlineData("UT-FAM-59 Tên bệnh nhân khác tên người thân", "name")]
    [InlineData("UT-FAM-60 Ngày sinh bệnh nhân khác khai báo", "birth")]
    [InlineData("UT-FAM-61 Mã bệnh nhân khác mã đã khai", "code")]
    [InlineData("UT-FAM-62 Người thân đã đổi tên sau khi gửi yêu cầu", "member")]
    public async Task Verify_rejects_a_patient_that_does_not_match_the_declared_identity(string caseName, string mismatch)
    {
        await using var env = await NewEnv();
        var row = await env.Pending();
        await AddVisit(env);
        switch (mismatch)
        {
            case "name": await env.Db.Patients.Where(x => x.Id == env.PatientId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FullName, "Lê Văn Khác")); break;
            case "birth": await env.Db.Patients.Where(x => x.Id == env.PatientId).ExecuteUpdateAsync(s => s.SetProperty(x => x.BirthDate, new DateOnly(1991, 1, 1))); break;
            case "code":
                env.Db.ChangeTracker.Clear();
                var tracked = await env.Db.Set<FamilyRecordAccess>().SingleAsync(x => x.Id == row.Id); tracked.DeclaredPatientCode = "KHAC-" + env.N; await env.Db.SaveChangesAsync(); break;
            default: await env.Db.Set<FamilyMember>().Where(x => x.Id == env.MemberId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FullName, "Trần Thị Mai Mới")); break;
        }
        await Rejects(() => env.Verify(row.Id, env.Cmd(version: mismatch == "code" ? 1 : 1)), 400, Invalid, "patientId");
    }

    [Fact]
    public async Task Verify_UT_FAM_63_name_comparison_ignores_accents_case_and_extra_spaces_and_the_declared_code_ignores_case()
    {
        await using var env = await NewEnv();
        var row = await env.Pending();
        await AddVisit(env);
        await env.Db.Patients.Where(x => x.Id == env.PatientId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FullName, "  TRAN   thi mai "));
        env.Db.ChangeTracker.Clear();
        var tracked = await env.Db.Set<FamilyRecordAccess>().SingleAsync(x => x.Id == row.Id); tracked.DeclaredPatientCode = ("bn" + env.N).ToLowerInvariant(); await env.Db.SaveChangesAsync();
        Assert.Equal("Verified", (await env.Verify(row.Id, env.Cmd())).Status);
    }

    public static TheoryData<string, Func<VerifyFamilyAccess, VerifyFamilyAccess>, string> VerifyInvalid => new()
    {
        { "UT-FAM-64 Ghi chú xác minh rỗng", c => c with { VerificationNote = "   " }, "verificationNote" },
        { "UT-FAM-65 Ghi chú xác minh 1001 ký tự", c => c with { VerificationNote = new string('a', 1001) }, "verificationNote" },
        { "UT-FAM-66 Chưa xác nhận đồng ý", c => c with { ConsentConfirmed = false }, "consentConfirmed" },
        { "UT-FAM-67 Căn cứ chia sẻ không hợp lệ", c => c with { SharingBasis = "Other" }, "consentConfirmed" },
        { "UT-FAM-68 Không có phạm vi", c => c with { Scopes = [] }, "scopes" },
        { "UT-FAM-69 Phạm vi null", c => c with { Scopes = null! }, "scopes" },
        { "UT-FAM-70 Phạm vi không tồn tại", c => c with { Scopes = ["Treatment", "Billing"] }, "scopes" },
        { "UT-FAM-71 Hết hạn đúng thời điểm hiện tại", c => c with { ExpiresAt = Now }, "expiresAt" },
        { "UT-FAM-72 Hết hạn trong quá khứ", c => c with { ExpiresAt = Now.AddDays(-1) }, "expiresAt" },
        { "UT-FAM-73 Hết hạn sau 365 ngày 1 giây", c => c with { ExpiresAt = Now.AddDays(365).AddSeconds(1) }, "expiresAt" },
    };

    [Theory, MemberData(nameof(VerifyInvalid))]
    public async Task Verify_rejects_invalid_decision_data_and_leaves_the_request_pending(string caseName, Func<VerifyFamilyAccess, VerifyFamilyAccess> change, string field)
    {
        await using var env = await NewEnv();
        var row = await env.Pending();
        await AddVisit(env);
        await Rejects(() => env.Verify(row.Id, env.Cmd(change: change)), 400, Invalid, field);
        Assert.Equal("Pending", (await env.Row(row.Id)).Status);
        Assert.Empty(await env.Audits(row.Id));
    }

    [Fact]
    public async Task Verify_UT_FAM_74_expiry_one_second_in_the_future_is_accepted()
    {
        await using var env = await NewEnv();
        var row = await env.Pending();
        await AddVisit(env);
        Assert.Equal("Verified", (await env.Verify(row.Id, env.Cmd(change: c => c with { ExpiresAt = Now.AddSeconds(1) }))).Status);
    }

    [Fact]
    public async Task Verify_UT_FAM_75_empty_request_key_is_rejected()
    {
        await using var env = await NewEnv();
        var row = await env.Pending();
        await Rejects(() => env.Verify(row.Id, env.Cmd(change: c => c with { RequestKey = Guid.Empty })), 400, Invalid, "requestKey");
    }

    // ================= DecideAsync =================
    [Fact]
    public async Task Decide_UT_FAM_76_reject_a_pending_request()
    {
        await using var env = await NewEnv();
        var row = await env.Seed("Pending");

        var dto = await env.Decide(row.Id, false, "  Không đủ căn cứ  ", note: "  Vui lòng liên hệ phòng khám  ");

        Assert.Equal(("Rejected", 2, "Vui lòng liên hệ phòng khám"), (dto.Status, dto.Version, dto.PublicNote));
        var saved = await env.Row(row.Id);
        Assert.Equal(("Không đủ căn cứ", env.Assistant, Now), (saved.DecisionReason, saved.DecidedBy, saved.DecidedAt));
        var audit = Assert.Single(await env.Audits(row.Id));
        Assert.Equal(("Rejected", "Không đủ căn cứ"), (audit.Action, audit.Reason));
    }

    [Fact]
    public async Task Decide_UT_FAM_77_revoke_a_verified_request()
    {
        await using var env = await NewEnv();
        var row = await env.Seed("Verified", env.N, Now.AddDays(10));
        var dto = await env.Decide(row.Id, true, "Người bệnh yêu cầu");
        Assert.Equal(("Revoked", 2, (string?)null), (dto.Status, dto.Version, dto.PublicNote));
        Assert.Equal("Revoked", (await env.Audits(row.Id)).Single().Action);
    }

    [Theory]
    [InlineData("UT-FAM-78 Từ chối quyền đã xác minh", false, "Verified", 1)]
    [InlineData("UT-FAM-79 Thu hồi yêu cầu đang chờ", true, "Pending", 1)]
    [InlineData("UT-FAM-80 Từ chối yêu cầu đã bị từ chối", false, "Rejected", 1)]
    [InlineData("UT-FAM-81 Sai phiên bản khi từ chối", false, "Pending", 5)]
    public async Task Decide_returns_409_for_the_wrong_status_or_a_stale_version(string caseName, bool revoke, string status, int expectedVersion)
    {
        await using var env = await NewEnv();
        var row = await env.Seed(status, env.N, Now.AddDays(3));
        await Rejects(() => env.Decide(row.Id, revoke, version: expectedVersion), 409, "MSG111");
        Assert.Equal(status, (await env.Row(row.Id)).Status);
    }

    [Theory]
    [InlineData("UT-FAM-82 Lý do rỗng", "   ", null, "decisionReason")]
    [InlineData("UT-FAM-83 Lý do 501 ký tự", "L", "", "decisionReason")]
    [InlineData("UT-FAM-84 Ghi chú công khai 501 ký tự", "Lý do", "N", "publicNote")]
    public async Task Decide_rejects_invalid_texts(string caseName, string reason, string? note, string field)
    {
        await using var env = await NewEnv();
        var row = await env.Seed("Pending");
        reason = reason == "L" ? new string('a', 501) : reason;
        note = note == "N" ? new string('b', 501) : note == "" ? null : note;
        await Rejects(() => env.Decide(row.Id, false, reason, note), 400, Invalid, field);
    }

    [Fact]
    public async Task Decide_UT_FAM_85_texts_of_exactly_500_characters_are_accepted()
    {
        await using var env = await NewEnv();
        var row = await env.Seed("Pending");
        Assert.Equal("Rejected", (await env.Decide(row.Id, false, new string('a', 500), new string('b', 500))).Status);
    }

    [Fact]
    public async Task Decide_UT_FAM_86_authorization_replay_and_request_key()
    {
        await using var env = await NewEnv();
        var row = await env.Seed("Pending");
        var hidden = await env.Seed("Pending", env.Other);
        await Rejects(() => env.Decide(hidden.Id, false), 404, Missing);
        await Rejects(() => env.Decide(Guid.NewGuid(), false), 404, Missing);
        await Rejects(() => env.Decide(row.Id, false, key: Guid.Empty), 400, Invalid, "requestKey");
        env.Access.Denied = ["patient-links.verify"];
        await Rejects(() => env.Decide(row.Id, false), 403, "MSG06");
        env.Access.Denied = [];
        var key = Guid.NewGuid();
        var first = await env.Decide(row.Id, false, key: key);
        Same(first, await env.Decide(row.Id, false, key: key));
        Assert.Single(await env.Audits(row.Id));
        await Rejects(() => env.Decide(row.Id, false, "Lý do khác", key: key), 409, "MSG111");
    }

    // ================= EndAccessAsync (called when a family member is deleted) =================
    [Fact]
    public async Task EndAccess_UT_FAM_87_ends_pending_and_verified_accesses_of_the_member_only()
    {
        await using var env = await NewEnv();
        var member = await env.NewMember();
        var pending = await env.Seed("Pending", member: member);
        var memberB = await env.NewMember();
        var verified = await env.Seed("Verified", env.N, Now.AddDays(5), member: memberB);
        var rejected = await env.Seed("Rejected", env.N, member: member);
        _ = rejected;
        var otherMember = await env.Seed("Pending");

        env.Db.ChangeTracker.Clear();
        await FamilyRecordService.EndAccessAsync(env.Db, env.Owner, member, "Người thân đã bị xóa", Now, default);
        await FamilyRecordService.EndAccessAsync(env.Db, env.Owner, memberB, "Người thân đã bị xóa", Now, default);
        await env.Db.SaveChangesAsync();

        var p = await env.Row(pending.Id); var v = await env.Row(verified.Id);
        Assert.Equal(("Withdrawn", 2, "Người thân đã bị xóa", env.Owner, Now), (p.Status, p.Version, p.PublicNote, p.DecidedBy, p.DecidedAt));
        Assert.Equal(("Revoked", 2), (v.Status, v.Version));
        Assert.Equal("Rejected", (await env.Row(rejected.Id)).Status);
        Assert.Equal("Pending", (await env.Row(otherMember.Id)).Status);
        Assert.Equal(("Withdrawn", "Người thân đã bị xóa"), ((await env.Audits(pending.Id)).Single().Action, (await env.Audits(pending.Id)).Single().Reason));
        Assert.Equal("Revoked", (await env.Audits(verified.Id)).Single().Action);
    }
}

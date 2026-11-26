using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>Method-level tests of WorkStatisticsService.ListAsync (guards, grouping, cost/paid/due rules, search, filters, paging).</summary>
[Collection(PostgresCollection.Name)]
public sealed class WorkStatisticsServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private sealed class StatsAccess(bool deny, int[] branches, string[]? roles = null) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(roles == null, true, roles ?? [], [], branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) =>
            deny ? throw new BusinessException(403, SystemMessages.Get("MSG06")) : ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Other, Dentist, DentistAccount, Assistant, Creator, P1, P2, S1, S2;
        public required string P1Code;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public WorkStatisticsService Svc(bool deny = false, int[]? branches = null, string[]? roles = null) => new(Db, new TestClock(Now), new StatsAccess(deny, branches ?? [N], roles));
        public Task<WorkStatisticsPage> List(DateOnly? from = null, DateOnly? to = null, int? branch = null, string? q = null, int page = 1, int size = 20, Guid? dossier = null, int[]? scope = null) =>
            Svc(branches: scope).ListAsync(Creator, from, to, branch, q, page, size, CancellationToken.None, dossier);

        private int seq;
        public async Task<TreatmentDossier> Dossier(string state = "Open", int patient = 0, int branch = 0, string startedOn = "2030-01-05", string? note = null, bool created = true)
        {
            var d = new TreatmentDossier
            {
                Code = $"HS{N}-{++seq}", PatientId = patient == 0 ? P1 : patient, BranchId = branch == 0 ? N : branch, DentistId = Dentist, StartedOn = DateOnly.Parse(startedOn),
                Diagnosis = "d", Note = note, State = state, CreatedAt = Now.AddMinutes(seq), UpdatedAt = Now
            };
            Db.TreatmentDossiers.Add(d);
            if (created) Db.TreatmentEvents.Add(new TreatmentEvent { DossierId = d.Id, Kind = "DossierCreated", ActorId = Creator, OccurredAt = Now, DossierVersion = 1 });
            await Db.SaveChangesAsync();
            return d;
        }

        public async Task<ClinicalActivity> Activity(TreatmentDossier d, int service, decimal qty, decimal price, long discount = 0, string performed = "2030-01-05T03:00:00Z", int? assistant = null, string? name = null)
        {
            var a = new ClinicalActivity
            {
                DossierId = d.Id, ServiceId = service, ServiceNameSnapshot = name, Unit = "lần", Quantity = qty, PriceSnapshotVnd = price, DiscountSnapshotVnd = discount,
                PerformedAt = DateTimeOffset.Parse(performed), PerformerId = Dentist, AssistantId = assistant, Outcome = "ok", CreatedAt = Now
            };
            Db.ClinicalActivities.Add(a);
            await Db.SaveChangesAsync();
            return a;
        }

        public Task Ledger(TreatmentDossier d, string action, object lines, int? patient = null, object? labPriority = null, string? target = null) =>
            Db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AUDIT_LOG" ("StorageId","AuditType","Action","Target","PatientId","BranchId","PayloadJson")
                VALUES ({Guid.NewGuid()}, 'FinanceLedger', {action}, {target ?? d.Id.ToString()}, {patient ?? d.PatientId}, {d.BranchId},
                        CAST({(labPriority == null ? JsonSerializer.Serialize(new { lines }, Web) : JsonSerializer.Serialize(new { lines, labPriority }, Web))} AS jsonb))
                """);

        public async Task SheetEvent(TreatmentDossier d, string kind, ClinicalActivity a, int version, Func<ActivityDto, ActivityDto> change)
        {
            var snapshot = new { activityId = a.Id, sheetVersion = version, activity = change(ActivityDto.From(a)) };
            Db.TreatmentEvents.Add(new TreatmentEvent { DossierId = d.Id, Kind = kind, ActorId = Creator, OccurredAt = Now, DossierVersion = version, PayloadJson = JsonSerializer.Serialize(snapshot, Web) });
            await Db.SaveChangesAsync();
        }
    }

    private static async Task<Env> NewEnv()
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var db = pg.Db;
        var n = CreateTestKit.NextId(); var other = CreateTestKit.NextId();
        var dentist = await CreateTestKit.SeedStaffAsync(db, "bs" + n, AccountRoles.Dentist, n);
        var assistant = await CreateTestKit.SeedStaffAsync(db, "tl" + n, AccountRoles.ClinicAssistant, n);
        var creator = await CreateTestKit.SeedStaffAsync(db, "nv" + n, AccountRoles.ClinicOwner, n);
        dentist.FullName = "Bác sĩ Hòa"; assistant.FullName = "Trợ lý Lan"; creator.FullName = "Chủ phòng khám";
        var employees = await db.Employees.Where(x => new[] { dentist.Id, assistant.Id }.Contains(x.AccountId)).ToDictionaryAsync(x => x.AccountId);
        employees[dentist.Id].FullName = "Bác sĩ Hòa"; employees[assistant.Id].FullName = "Trợ lý Lan";
        db.Set<SchedulingBranch>().Add(new SchedulingBranch { Id = other, Name = "Chi nhánh " + other });
        PatientProfile P(string code, string name) => new() { PatientCode = code + n, FullName = name, NormalizedName = name.ToUpperInvariant(), BirthDate = new DateOnly(1990, 1, 1), CreatedAt = Now, UpdatedAt = Now };
        var p1 = P("BN", "Nguyễn Văn An"); var p2 = P("BM", "Lê Thị Bình");
        db.Patients.AddRange(p1, p2);
        var s1 = new SchedulingService { BranchId = n, Name = "Trám răng" }; var s2 = new SchedulingService { BranchId = n, Name = "Nhổ răng" };
        db.Set<SchedulingService>().AddRange(s1, s2);
        await db.SaveChangesAsync();
        return new Env { Pg = pg, N = n, Other = other, Dentist = employees[dentist.Id].Id, DentistAccount = dentist.Id, Assistant = employees[assistant.Id].Id, Creator = creator.Id, P1 = p1.Id, P2 = p2.Id, S1 = s1.Id, S2 = s2.Id, P1Code = p1.PatientCode };
    }

    // ================= Guards =================
    [Fact]
    public async Task List_UT_WST_01_without_permission_is_forbidden()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc(deny: true).ListAsync(env.Creator, null, null, null, null, 1, 20, CancellationToken.None));
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
    }

    [Fact]
    public async Task List_UT_WST_02_branch_outside_scope_is_forbidden()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.List(branch: env.Other));
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
    }

    // Input: paging/search/date arguments -> Expected: MSG04 (400) when invalid, otherwise an (empty) page.
    [Theory]
    [InlineData("UT-WST-03 pageSize 10 không hợp lệ", 1, 10, 0, "", "", false)]
    [InlineData("UT-WST-04 pageSize 0", 1, 0, 0, "", "", false)]
    [InlineData("UT-WST-05 pageSize 101", 1, 101, 0, "", "", false)]
    [InlineData("UT-WST-06 pageSize 20 hợp lệ", 1, 20, 0, "", "", true)]
    [InlineData("UT-WST-07 pageSize 50 hợp lệ", 1, 50, 0, "", "", true)]
    [InlineData("UT-WST-08 pageSize 100 hợp lệ", 1, 100, 0, "", "", true)]
    [InlineData("UT-WST-09 page 0", 0, 20, 0, "", "", false)]
    [InlineData("UT-WST-10 page quá lớn (tràn int)", 107374183, 20, 0, "", "", false)]
    [InlineData("UT-WST-11 page lớn nhất còn hợp lệ", 107374182, 20, 0, "", "", true)]
    [InlineData("UT-WST-12 Từ khóa 161 ký tự", 1, 20, 161, "", "", false)]
    [InlineData("UT-WST-13 Từ khóa 160 ký tự", 1, 20, 160, "", "", true)]
    [InlineData("UT-WST-14 from lớn hơn to", 1, 20, 0, "2030-01-10", "2030-01-09", false)]
    [InlineData("UT-WST-15 from bằng to", 1, 20, 0, "2030-01-10", "2030-01-10", true)]
    public async Task List_validates_arguments(string caseName, int page, int size, int qLength, string from, string to, bool valid)
    {
        await using var env = await NewEnv();
        var call = () => env.List(from == "" ? null : DateOnly.Parse(from), to == "" ? null : DateOnly.Parse(to), q: qLength == 0 ? null : new string('x', qLength), page: page, size: size);
        if (valid) { Assert.Equal(0, (await call()).Total); return; }
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((400, "MSG04"), (error.Status, error.Response.Code));
    }

    // ================= Cost / paid / due =================
    // Input: a plain dentist (not owner/admin) lists -> Expected: only dossiers they treat; the owner still sees all.
    [Fact]
    public async Task List_UT_WST_dentist_only_sees_own_dossiers_while_owner_sees_all()
    {
        await using var env = await NewEnv();
        var mine = await env.Dossier(); await env.Activity(mine, env.S1, 1, 1000);
        var theirs = await env.Dossier(); await env.Activity(theirs, env.S1, 1, 2000);
        var stored = await env.Db.TreatmentDossiers.SingleAsync(x => x.Id == theirs.Id);
        stored.DentistId = env.Assistant; await env.Db.SaveChangesAsync();

        var asDentist = await env.Svc(roles: [AccountRoles.Dentist]).ListAsync(env.DentistAccount, null, null, null, null, 1, 20, CancellationToken.None);
        Assert.Equal([mine.Id], asDentist.Items.Select(x => x.DossierId));
        var asOwner = await env.List();
        Assert.Equal(2, asOwner.Items.Count);
        var asOwnerRole = await env.Svc(roles: [AccountRoles.ClinicOwner]).ListAsync(env.Creator, null, null, null, null, 1, 20, CancellationToken.None);
        Assert.Equal(2, asOwnerRole.Items.Count);
    }

    [Fact]
    public async Task List_UT_WST_16_groups_activities_by_dossier_with_cost_paid_and_due()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(note: "Ghi chú 1");
        var a1 = await env.Activity(d, env.S1, 2, 100000, discount: 50000, assistant: env.Assistant, name: "Trám răng composite");
        var a2 = await env.Activity(d, env.S2, 0.5m, 1001, performed: "2030-01-05T04:00:00Z");
        var a3 = await env.Activity(d, env.S2, 1, 999, performed: "2030-01-05T05:00:00Z");
        await env.SheetEvent(d, "SheetActivityDeleted", a3, 2, x => x with { SheetDeleted = true });
        await env.Ledger(d, "TreatmentCashPayment", new[] { new { activityId = a1.Id, amountVnd = 60000 }, new { activityId = a2.Id, amountVnd = 1000 } });

        var page = await env.List();
        var item = Assert.Single(page.Items);
        Assert.Equal((d.Id, d.Code, new DateOnly(2030, 1, 5), env.P1, env.P1Code, "Nguyễn Văn An", "Bác sĩ Hòa", "Open", "Ghi chú 1", "Chủ phòng khám"),
            (item.DossierId, item.DossierCode, item.StartedOn, item.PatientId, item.PatientCode, item.PatientName, item.DentistName, item.State, item.Note, item.CreatedByName));
        Assert.Equal(["Trợ lý Lan"], item.AssistantNames);
        Assert.Equal(2, item.Lines.Count); // the deleted activity is not counted
        Assert.Equal((a1.Id, "Trám răng composite", 150000L, 60000L, 90000L, 2m, 100000L, 50000L),
            (item.Lines[0].ActivityId, item.Lines[0].ServiceName, item.Lines[0].CostVnd, item.Lines[0].PaidVnd, item.Lines[0].DueVnd, item.Lines[0].Quantity, item.Lines[0].UnitPriceVnd, item.Lines[0].DiscountVnd));
        // 0.5 x 1,001 = 500.5 -> 501 (half away from zero); the 1,000 payment is clamped to the cost.
        Assert.Equal((a2.Id, "Nhổ răng", 501L, 501L, 0L, 0L), (item.Lines[1].ActivityId, item.Lines[1].ServiceName, item.Lines[1].CostVnd, item.Lines[1].PaidVnd, item.Lines[1].DueVnd, item.Lines[1].DiscountVnd));
        Assert.Equal((150501L, 60501L, 90000L), (item.CostVnd, item.PaidVnd, item.DueVnd));
        Assert.Equal((1, 1, 2), (page.Total, page.Page, page.LineCount));
        Assert.Equal((150501L, 60501L, 90000L), (page.Totals.CostVnd, page.Totals.PaidVnd, page.Totals.DueVnd));
    }

    [Fact]
    public async Task List_UT_WST_17_discount_larger_than_gross_is_capped_and_paid_is_never_negative()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier();
        var a = await env.Activity(d, env.S1, 1, 10000, discount: 99999);
        var line = Assert.Single((await env.List()).Items).Lines.Single();
        Assert.Equal((a.Id, 0L, 0L, 0L, 10000L), (line.ActivityId, line.CostVnd, line.PaidVnd, line.DueVnd, line.DiscountVnd));
    }

    [Fact]
    public async Task List_UT_WST_18_latest_sheet_edit_wins_and_edit_can_remove_the_activity()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier();
        var edited = await env.Activity(d, env.S1, 1, 1000, name: "Trám");
        var removed = await env.Activity(d, env.S2, 1, 5000, performed: "2030-01-05T04:00:00Z");
        await env.SheetEvent(d, "SheetActivityEdited", edited, 2, x => x with { Quantity = 2 });
        await env.SheetEvent(d, "SheetActivityEdited", edited, 3, x => x with { Quantity = 3, PriceSnapshotVnd = 2000 });
        await env.SheetEvent(d, "SheetActivityEdited", removed, 4, x => x with { SheetDeleted = true });
        var item = Assert.Single((await env.List()).Items);
        var line = Assert.Single(item.Lines);
        Assert.Equal((edited.Id, 6000L), (line.ActivityId, line.CostVnd)); // 3 x 2,000 from the newest edit
    }

    [Fact]
    public async Task List_UT_WST_19_cancelled_dossier_keeps_only_what_was_collected()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(state: "Cancelled");
        var a = await env.Activity(d, env.S1, 1, 100000, discount: 10000);
        await env.Ledger(d, "TreatmentTransferPayment", new[] { new { activityId = a.Id, amountVnd = 30000 } });
        var item = Assert.Single((await env.List()).Items);
        Assert.Equal((30000L, 30000L, 0L), (item.CostVnd, item.PaidVnd, item.DueVnd));
        Assert.Equal(10000L, item.Lines[0].DiscountVnd); // the discount is still shown
    }

    [Fact]
    public async Task List_UT_WST_20_only_cash_transfer_and_advance_entries_of_the_dossier_count_as_paid()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier();
        var other = await env.Dossier(patient: env.P2);
        var a = await env.Activity(d, env.S1, 1, 1000000);
        var lines = new[] { new { activityId = a.Id, amountVnd = 1000 } };
        await env.Ledger(d, "TreatmentCashPayment", lines);
        await env.Ledger(d, "TreatmentTransferPayment", lines);
        await env.Ledger(d, "AdvanceApplied", lines);
        await env.Ledger(d, "TreatmentRefund", lines);                    // another action: ignored
        await env.Ledger(d, "TreatmentCashPayment", lines, patient: env.P2); // ledger of another patient: ignored
        await env.Ledger(d, "TreatmentCashPayment", lines, target: other.Id.ToString()); // another dossier: ignored
        var item = Assert.Single((await env.List(dossier: d.Id)).Items);
        Assert.Equal(3000L, item.PaidVnd);
    }

    [Fact]
    public async Task List_UT_WST_21_lab_priority_share_is_deducted_from_paid()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier();
        var a = await env.Activity(d, env.S1, 1, 1000000);
        var lab = new LabOrder { ActivityId = a.Id, DentistId = env.Dentist, WorkName = "Mão sứ", OrderedOn = new DateOnly(2030, 1, 5), TotalFeeVnd = 400000 };
        env.Db.Set<LabOrder>().Add(lab);
        await env.Db.SaveChangesAsync();
        await env.Ledger(d, "TreatmentCashPayment", new[] { new { activityId = a.Id, amountVnd = 500000 } }, labPriority: new[] { new { labOrderId = lab.Id, amountVnd = 120000 } });
        var item = Assert.Single((await env.List()).Items);
        Assert.Equal(380000L, item.PaidVnd);
    }

    [Fact]
    public async Task List_UT_WST_22_archived_legacy_payment_lines_count_as_paid()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier();
        var a = await env.Activity(d, env.S1, 1, 100000);
        var paymentId = "pay-" + env.N;
        string Rows(object row) => JsonSerializer.Serialize(new { rows = new[] { row } }, Web);
        async Task Archive(string target, string json) => await env.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AUDIT_LOG" ("StorageId","AuditType","Target","PayloadJson") VALUES ({Guid.NewGuid()}, 'FinanceLegacyArchive', {target}, CAST({json} AS jsonb))
            """);
        await Archive("TREATMENT_DOSSIER_PAYMENT", Rows(new Dictionary<string, object> { ["Id"] = paymentId, ["DossierId"] = d.Id.ToString(), ["PatientId"] = d.PatientId, ["BranchId"] = d.BranchId }));
        await Archive("TREATMENT_DOSSIER_PAYMENT_LINE", Rows(new Dictionary<string, object> { ["PaymentId"] = paymentId, ["ActivityId"] = a.Id.ToString(), ["AmountVnd"] = 25000 }));
        var item = Assert.Single((await env.List()).Items);
        Assert.Equal((25000L, 75000L), (item.PaidVnd, item.DueVnd));
    }

    // ================= Which dossiers are listed =================
    [Fact]
    public async Task List_UT_WST_23_skips_deleted_dossiers_empty_dossiers_and_other_branches()
    {
        await using var env = await NewEnv();
        var visible = await env.Dossier();
        await env.Activity(visible, env.S1, 1, 1000);
        var deleted = await env.Dossier(state: "Deleted"); await env.Activity(deleted, env.S1, 1, 1000);
        await env.Dossier();                                   // no activity at all
        var allDeleted = await env.Dossier(); var gone = await env.Activity(allDeleted, env.S1, 1, 1000);
        await env.SheetEvent(allDeleted, "SheetActivityDeleted", gone, 2, x => x with { SheetDeleted = true });
        var elsewhere = await env.Dossier(branch: env.Other); await env.Activity(elsewhere, env.S1, 1, 1000);
        var page = await env.List();
        Assert.Equal([visible.Id], page.Items.Select(x => x.DossierId));
        Assert.Equal(1, page.Total);
    }

    // Input: search text -> Expected: whether the dossier (patient "Nguyễn Văn An", service "Trám răng") matches.
    [Theory]
    [InlineData("UT-WST-24 Theo mã bệnh nhân (hoa thường, khoảng trắng)", "  bn{n}  ", true)]
    [InlineData("UT-WST-25 Theo tên bệnh nhân", "văn an", true)]
    [InlineData("UT-WST-26 Theo tên dịch vụ", "TRÁM", true)]
    [InlineData("UT-WST-27 Không khớp", "xyz-không-có", false)]
    [InlineData("UT-WST-28 Chuỗi rỗng là không lọc", "   ", true)]
    public async Task List_search_matches_patient_or_service(string caseName, string search, bool matches)
    {
        await using var env = await NewEnv();
        var d = await env.Dossier();
        await env.Activity(d, env.S1, 1, 1000, name: "Trám răng");
        var page = await env.List(q: search.Replace("{n}", env.N.ToString()));
        Assert.Equal(matches ? 1 : 0, page.Total);
    }

    [Fact]
    public async Task List_UT_WST_29_filters_by_date_branch_and_dossier()
    {
        await using var env = await NewEnv();
        var early = await env.Dossier(startedOn: "2030-01-02"); await env.Activity(early, env.S1, 1, 1000);
        var late = await env.Dossier(startedOn: "2030-01-08"); await env.Activity(late, env.S1, 1, 1000);
        var far = await env.Dossier(branch: env.Other, startedOn: "2030-01-08"); await env.Activity(far, env.S1, 1, 1000);
        var wide = new[] { env.N, env.Other };

        Assert.Equal([late.Id, early.Id], (await env.List(scope: [env.N])).Items.Select(x => x.DossierId)); // newest first
        Assert.Equal([late.Id], (await env.List(from: new DateOnly(2030, 1, 3))).Items.Select(x => x.DossierId));
        Assert.Equal([early.Id], (await env.List(to: new DateOnly(2030, 1, 7))).Items.Select(x => x.DossierId));
        Assert.Equal([early.Id], (await env.List(from: new DateOnly(2030, 1, 2), to: new DateOnly(2030, 1, 2))).Items.Select(x => x.DossierId)); // both bounds inclusive
        Assert.Equal([far.Id], (await env.List(branch: env.Other, scope: wide)).Items.Select(x => x.DossierId));
        Assert.Equal(3, (await env.List(scope: wide)).Total);
        Assert.Equal([early.Id], (await env.List(dossier: early.Id)).Items.Select(x => x.DossierId));
        Assert.Empty((await env.List(dossier: Guid.NewGuid())).Items);
        Assert.Empty((await env.List(dossier: far.Id)).Items); // out of scope
    }

    [Fact]
    public async Task List_UT_WST_30_pages_the_result_and_totals_cover_all_pages()
    {
        await using var env = await NewEnv();
        for (var i = 0; i < 21; i++)
        {
            var d = await env.Dossier(startedOn: new DateOnly(2030, 1, 1).AddDays(i % 9).ToString("yyyy-MM-dd"));
            await env.Activity(d, env.S1, 1, 1000);
        }
        var first = await env.List(page: 1, size: 20);
        var second = await env.List(page: 2, size: 20);
        var third = await env.List(page: 3, size: 20);
        Assert.Equal((21, 20, 1, 0), (first.Total, first.Items.Count, second.Items.Count, third.Items.Count));
        Assert.Equal((21000L, 21), (first.Totals.CostVnd, first.LineCount)); // totals are over every page
        Assert.Equal(first.Items.Concat(second.Items).Select(x => x.StartedOn).OrderByDescending(x => x), first.Items.Concat(second.Items).Select(x => x.StartedOn));
    }

    // ================= Other columns =================
    [Fact]
    public async Task List_UT_WST_31_next_appointment_is_the_earliest_future_confirmed_one()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier();
        await env.Activity(d, env.S1, 1, 1000);
        Appointment A(string code, string status, int hours) => new()
        {
            Code = code + env.N, BranchId = env.N, DentistId = env.Dentist, ChairId = 1, AttendeeName = "An", PatientId = env.P1, Status = status,
            StartAt = Now.AddHours(hours), EndAt = Now.AddHours(hours + 1), CreatedAt = Now
        };
        env.Db.Appointments.AddRange(A("A1", "Confirmed", 48), A("A2", "Confirmed", 24), A("A3", "Cancelled", 5), A("A4", "Confirmed", -2));
        await env.Db.SaveChangesAsync();
        Assert.Equal(Now.AddHours(24), Assert.Single((await env.List()).Items).NextAppointmentAt);

        env.Db.Appointments.RemoveRange(env.Db.Appointments.Where(x => x.PatientId == env.P1));
        await env.Db.SaveChangesAsync();
        Assert.Null(Assert.Single((await env.List()).Items).NextAppointmentAt);
    }

    [Fact]
    public async Task List_UT_WST_32_dossier_without_creation_event_has_empty_creator_name()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(created: false);
        await env.Activity(d, env.S1, 1, 1000);
        var item = Assert.Single((await env.List()).Items);
        Assert.Equal(("", Array.Empty<string>()), (item.CreatedByName, item.AssistantNames.ToArray()));
    }
}

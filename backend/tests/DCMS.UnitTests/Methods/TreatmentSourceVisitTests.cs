using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Examinations;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Domain.Features.Treatment;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of the remaining TreatmentDossierService paths: ActivityMaterialsAsync and CreateAsync/QuickCreateAsync from a confirmed
/// examination (SourceVisitId, ExpectedSourceVisitVersion, "ExaminationSourceCaptured" event).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TreatmentSourceVisitTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);

    private sealed class Access(params string[] denied) : ISettingsAccess
    {
        private readonly int[] branches = [];
        public int[] Branches { get; set; } = [];
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(false, false, [], [], Branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) =>
            denied.Contains(permission) ? throw new BusinessException(403, SystemMessages.Get("MSG06")) : ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
        public int[] Unused => branches;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Dentist, Assistant, DentistEmp, PatientId, ChairId;
        public required Guid DossierId, ActivityId;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public TreatmentDossierService Svc(params string[] denied) =>
            new(Db, CreateTestKit.NewRoleUserManager(Db), new TestClock(Now), new UnavailableTreatmentFinancialSummaryPort(), null!, new Access(denied) { Branches = [N] });

        public async Task<Visit> NewVisit(string status = VisitStatuses.Completed, bool examination = true, int? patient = null, int? branch = null, string content = Content)
        {
            var appointment = new Appointment { Code = "LH" + CreateTestKit.NextId(), BranchId = N, DentistId = DentistEmp, ChairId = ChairId, AttendeeName = "Mai", StartAt = Now, EndAt = Now.AddHours(1),
                Status = "CheckedIn", Version = 1, CreatedAt = Now, BookingInfo = new AppointmentBookingInfo { BookerName = "Mai", BookerContact = "0987654321" } };
            Db.Appointments.Add(appointment); await Db.SaveChangesAsync();
            var visit = new Visit { AppointmentId = appointment.Id, BranchId = branch ?? N, PatientId = patient ?? PatientId, DentistId = DentistEmp, ArrivalAt = Now, RecordedBy = Dentist, Status = status, Version = 4 };
            Db.Visits.Add(visit);
            if (examination) Db.Set<VisitExamination>().Add(new VisitExamination { VisitId = visit.Id, ContentJson = content, UpdatedBy = Dentist, UpdatedAt = Now });
            await Db.SaveChangesAsync(); return visit;
        }
        public Task<TreatmentChange> Create(Guid? visit, int? version, int? patient = null, Guid? key = null, string? diagnosis = "Viêm tủy") =>
            Svc().CreateAsync(Dentist, patient ?? PatientId, new CreateDossierRequest(N, DentistEmp, diagnosis, null, null, key ?? Guid.NewGuid(), visit, version), default);
    }

    private const string Content = """{"symptoms":"Đau","medicalHistory":{"state":"Unknown"},"allergies":{"state":"Unknown"},"medications":{"state":"Unknown"},"findings":[{"tooth":16,"condition":"Sâu răng"}],"diagnosis":"Sâu răng 16","notes":null}""";

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
        var dentist = await CreateTestKit.SeedStaffAsync(db, "bs" + n, AccountRoles.Dentist, n);
        var assistant = await CreateTestKit.SeedStaffAsync(db, "tl" + n, AccountRoles.ClinicAssistant, n);
        var patient = new PatientProfile { PatientCode = "BN" + n, FullName = "Trần Thị Mai", NormalizedName = "TRAN THI MAI", BirthDate = new DateOnly(1990, 1, 1), CreatedAt = Now, UpdatedAt = Now };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        (await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == n)).Chairs.Add(new SchedulingChair { Id = n, BranchId = n, Name = "Ghế " + n });
        await db.SaveChangesAsync();
        var emp = (await db.Employees.SingleAsync(x => x.AccountId == dentist.Id)).Id;
        var dossier = new TreatmentDossier { Code = "HS" + n, PatientId = patient.Id, BranchId = n, DentistId = emp, StartedOn = new DateOnly(2030, 1, 1), Diagnosis = "d", State = "Open", Version = 5, CreatedAt = Now, UpdatedAt = Now };
        db.TreatmentDossiers.Add(dossier);
        var activity = new ClinicalActivity { DossierId = dossier.Id, ServiceId = 1, ServiceNameSnapshot = "x", Unit = "lần", Quantity = 1, PriceSnapshotVnd = 1, PerformedAt = Now, PerformerId = emp, Outcome = "ok", CreatedAt = Now };
        db.ClinicalActivities.Add(activity);
        await db.SaveChangesAsync();
        return new Env { Pg = pg, N = n, Dentist = dentist.Id, Assistant = assistant.Id, DentistEmp = emp, PatientId = patient.Id, ChairId = n, DossierId = dossier.Id, ActivityId = activity.Id };
    }

    private static async Task Rejects(Func<Task> call, int status, string code)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (error.Status, error.Response.Code));
    }

    // ================= ActivityMaterialsAsync =================
    [Fact]
    public async Task ActivityMaterials_UT_SRC_01_lists_the_materials_used_by_the_activity()
    {
        await using var env = await NewEnv();
        var category = new MaterialCategory { Name = "Vật liệu " + env.N };
        var m1 = new MaterialCatalogItem { Name = "Composite", Category = category, UsageUnit = "g" };
        var m2 = new MaterialCatalogItem { Name = "Gel", Category = category, UsageUnit = "ml" };
        env.Db.Set<MaterialCatalogItem>().AddRange(m1, m2);
        await env.Db.SaveChangesAsync();
        var other = new ClinicalActivity { DossierId = env.DossierId, ServiceId = 1, ServiceNameSnapshot = "y", Unit = "lần", Quantity = 1, PriceSnapshotVnd = 1, PerformedAt = Now, PerformerId = env.DentistEmp, Outcome = "ok", CreatedAt = Now };
        env.Db.ClinicalActivities.Add(other);
        await env.Db.SaveChangesAsync();
        env.Db.MaterialUsages.AddRange(
            new MaterialUsage { ActivityId = env.ActivityId, MaterialId = m1.Id, Quantity = 2.5m, UsedAt = Now },
            new MaterialUsage { ActivityId = env.ActivityId, MaterialId = m2.Id, Quantity = 1m, UsedAt = Now },
            new MaterialUsage { ActivityId = other.Id, MaterialId = m1.Id, Quantity = 9m, UsedAt = Now });
        await env.Db.SaveChangesAsync();
        var rows = await env.Svc().ActivityMaterialsAsync(env.Dentist, env.DossierId, env.ActivityId, default);
        Assert.Equal(2, rows.Count);
        Assert.Equal(2.5m, rows.Single(x => x.MaterialId == m1.Id).Quantity);
        Assert.Equal(1m, rows.Single(x => x.MaterialId == m2.Id).Quantity);
        Assert.Empty(await env.Svc().ActivityMaterialsAsync(env.Dentist, env.DossierId, (await NewActivity(env)).Id, default));
    }

    private static async Task<ClinicalActivity> NewActivity(Env env)
    {
        var a = new ClinicalActivity { DossierId = env.DossierId, ServiceId = 1, ServiceNameSnapshot = "z", Unit = "lần", Quantity = 1, PriceSnapshotVnd = 1, PerformedAt = Now, PerformerId = env.DentistEmp, Outcome = "ok", CreatedAt = Now };
        env.Db.ClinicalActivities.Add(a); await env.Db.SaveChangesAsync(); return a;
    }

    [Fact]
    public async Task ActivityMaterials_UT_SRC_02_guards()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc("treatment.actuals.update").ActivityMaterialsAsync(env.Dentist, env.DossierId, env.ActivityId, default), 403, "MSG06");
        await Rejects(() => env.Svc().ActivityMaterialsAsync(env.Dentist, Guid.NewGuid(), env.ActivityId, default), 404, "MSG113");
        await Rejects(() => env.Svc().ActivityMaterialsAsync(env.Assistant, env.DossierId, env.ActivityId, default), 403, "MSG06");
        await Rejects(() => env.Svc().ActivityMaterialsAsync(env.Dentist, env.DossierId, Guid.NewGuid(), default), 404, "MSG113");
    }

    // The failing command leaves its half-built dossier tracked in this shared test context; a real request has its own scoped context.
    private static async Task R(Env env, Func<Task> call, int status, string code) { await Rejects(call, status, code); env.Db.ChangeTracker.Clear(); }

    // ================= CreateAsync from a visit =================
    [Fact]
    public async Task Create_UT_SRC_03_copies_the_confirmed_examination_into_an_event()
    {
        await using var env = await NewEnv();
        var visit = await env.NewVisit();
        var result = await env.Create(visit.Id, visit.Version);
        var dossier = await env.Db.TreatmentDossiers.AsNoTracking().SingleAsync(x => x.Id == result.Dossier.Id);
        Assert.Equal(visit.Id, dossier.SourceExaminationVisitId);
        var evt = await env.Db.TreatmentEvents.AsNoTracking().SingleAsync(x => x.DossierId == dossier.Id && x.Kind == "ExaminationSourceCaptured");
        using var json = JsonDocument.Parse(evt.PayloadJson);
        Assert.Equal(visit.Id.ToString(), json.RootElement.GetProperty("sourceVisitId").GetString());
        Assert.Equal(visit.Version, json.RootElement.GetProperty("sourceVisitVersion").GetInt32());
        Assert.Equal("Sâu răng 16", json.RootElement.GetProperty("content").GetProperty("diagnosis").GetString());
        Assert.Contains(env.Db.TreatmentEvents.Where(x => x.DossierId == dossier.Id), e => e.Kind == "DossierCreated");
    }

    [Fact]
    public async Task Create_UT_SRC_04_accepts_an_examination_that_is_only_ExaminationConfirmed()
    {
        await using var env = await NewEnv();
        var visit = await env.NewVisit(VisitStatuses.ExaminationConfirmed);
        Assert.NotNull((await env.Create(visit.Id, visit.Version)).Dossier);
    }

    [Fact]
    public async Task Create_UT_SRC_05_source_visit_rules()
    {
        await using var env = await NewEnv();
        var good = await env.NewVisit();
        await R(env, () => env.Create(Guid.NewGuid(), 4), 404, "MSG113");                                   // unknown visit
        var otherPatient = new PatientProfile { PatientCode = "BX" + env.N, FullName = "Khác", NormalizedName = "KHAC", CreatedAt = Now, UpdatedAt = Now };
        env.Db.Patients.Add(otherPatient); await env.Db.SaveChangesAsync();
        var foreign = await env.NewVisit(patient: otherPatient.Id);
        await R(env, () => env.Create(foreign.Id, foreign.Version), 404, "MSG113");                         // visit of another patient
        await R(env, () => env.Create(good.Id, good.Version + 1), 409, "MSG111");                           // stale visit version
        await R(env, () => env.Create(good.Id, null), 409, "MSG111");                                       // version required
        var queued = await env.NewVisit(VisitStatuses.Queued);
        await R(env, () => env.Create(queued.Id, queued.Version), 409, "MSG240");                           // examination not confirmed yet
        var noExam = await env.NewVisit(examination: false);
        await R(env, () => env.Create(noExam.Id, noExam.Version), 409, "MSG240");                           // no examination row
        await R(env, () => env.Create(null, 4), 400, "MSG04");                                              // version without a visit
        var leaked = await env.Db.TreatmentDossiers.Where(x => x.PatientId == env.PatientId && x.SourceExaminationVisitId != null).Select(x => x.SourceExaminationVisitId).ToListAsync();
        Assert.True(leaked.Count == 0, $"good={good.Id} queued={queued.Id} noExam={noExam.Id} leaked={string.Join(",", leaked)}");
    }

    [Fact]
    public async Task QuickCreate_UT_SRC_06_creates_from_a_visit_without_a_diagnosis()
    {
        await using var env = await NewEnv();
        var visit = await env.NewVisit();
        var result = await env.Svc().QuickCreateAsync(env.Dentist, env.PatientId, new QuickDossierRequest(env.N, Guid.NewGuid(), visit.Id, visit.Version), default);
        Assert.Equal(visit.Id, (await env.Db.TreatmentDossiers.AsNoTracking().SingleAsync(x => x.Id == result.Dossier.Id)).SourceExaminationVisitId);
    }
}

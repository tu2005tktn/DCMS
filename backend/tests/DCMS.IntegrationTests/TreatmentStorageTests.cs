using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class TreatmentStorageTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Previous = "20261112143714_ConsolidateExaminationStorage";
    private static readonly string[] LegacyTables =
    [
        "TREATMENT_STEP", "TREATMENT_SHEET_DRAFT", "TREATMENT_REVISION_DRAFT", "TREATMENT_TEMPLATE_ITEM", "TREATMENT_TEMPLATE_VERSION",
        "CLINICAL_REVIEW", "CLINICAL_CARE_NOTE", "FOLLOW_UP_INSTRUCTION", "TREATMENT_EVENT", "TREATMENT_PROGRESSION_EVENT"
    ];

    // PostgreSQL keeps microseconds, so synthetic timestamps stay on a millisecond boundary.
    private static DateTimeOffset Now() =>
        new(DateTimeOffset.UtcNow.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, TimeSpan.Zero);

    private async Task<(int PatientId, int BranchId, int DentistId, int ServiceId, int ActorId)> Prepare()
    {
        var client = fixture.CreateClient();
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest("assistant.demo", fixture.Password));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await signIn.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        var patientResponse = await client.PostAsJsonAsync("/api/patients", new CreatePatientRequest(
            null, "SYNTHETIC STORAGE " + Guid.NewGuid().ToString("N"), new DateOnly(1990, 5, 6),
            "OTHER", null, null, null, null, null, null, false));
        Assert.Equal(HttpStatusCode.Created, patientResponse.StatusCode);
        var patient = (await patientResponse.Content.ReadFromJsonAsync<PatientChange>())!.Patient;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var dentist = await (from employee in db.Employees join user in db.Users on employee.AccountId equals user.Id
            where user.UserName == "dentist.demo" select new { employee.Id, AccountId = user.Id }).SingleAsync();
        var branch = new SchedulingBranch { Name = "Treatment storage " + Guid.NewGuid().ToString("N") };
        db.Add(branch);
        await db.SaveChangesAsync();
        var service = new SchedulingService { BranchId = branch.Id, Name = "SYNTHETIC storage service", PublicDurationMinutes = 30 };
        await db.AddSchedulingDataAsync(new SchedulingMember { BranchId = branch.Id, EmployeeId = dentist.Id, CanDentist = true }, service);
        await db.SaveChangesAsync();
        return (patient.Id, branch.Id, dentist.Id, service.Id, dentist.AccountId);
    }

    private static (TreatmentDossier Dossier, TreatmentItem Item) NewDossier(
        (int PatientId, int BranchId, int DentistId, int ServiceId, int ActorId) setup, DateTimeOffset now)
    {
        var dossier = new TreatmentDossier
        {
            Code = "ST-" + Guid.NewGuid().ToString("N")[..20], PatientId = setup.PatientId, BranchId = setup.BranchId,
            DentistId = setup.DentistId, StartedOn = DateOnly.FromDateTime(now.UtcDateTime), Diagnosis = "SYNTHETIC diagnosis",
            PlanVersion = 1, PlanState = "Committed", CreatedAt = now, UpdatedAt = now
        };
        var item = new TreatmentItem
        {
            DossierId = dossier.Id, ServiceId = setup.ServiceId, ServiceName = "SYNTHETIC storage service", Unit = "răng",
            Quantity = 1.000m, PlannedOrder = 1, AgreedUnitPriceVnd = 100000, Decision = "Agreed"
        };
        dossier.Items.Add(item);
        return (dossier, item);
    }

    [Fact]
    public async Task FourTablesPreserveCollectionsProcessRecordsAndEventsAcrossRollback()
    {
        var setup = await Prepare();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var now = Now();
        var (dossier, item) = NewDossier(setup, now);
        var first = new TreatmentStep
        {
            DossierId = dossier.Id, PlanId = dossier.PlanId, PlanVersion = 1, ItemId = item.Id, Name = "SYNTHETIC step one",
            EstimatedMinutes = 20, EstimatedMinMinutes = 15, EstimatedMaxMinutes = 40, ReservedMinutes = 30,
            Outcome = "Completed", CompletedAt = now, CompletionEventId = Guid.NewGuid(), Version = 3
        };
        var second = new TreatmentStep
        {
            DossierId = dossier.Id, PlanId = dossier.PlanId, PlanVersion = 1, ItemId = item.Id, Name = "SYNTHETIC step two",
            Region = "16", EstimatedMinutes = 30, ReservedMinutes = 45, PredecessorsJson = JsonSerializer.Serialize(new[] { first.Id }),
            Capability = "SYNTHETIC capability", LabRequirement = "SYNTHETIC lab", EarliestOffsetDays = 3, TargetOffsetDays = 7,
            LatestOffsetDays = 10, HoldReason = "SYNTHETIC hold", HoldEffectiveAt = now, Version = 2
        };
        item.Steps.AddRange([first, second]);
        dossier.SheetDrafts.Add(new TreatmentSheetDraft
        {
            Id = Guid.NewGuid(), DossierId = dossier.Id, VisitId = Guid.NewGuid(), ActorId = setup.ActorId, Kind = "Activity", Version = 4,
            PayloadJson = "{\"rows\":[{\"tooth\":\"16\",\"note\":\"SYNTHETIC\"}]}", LastRequestKey = Guid.NewGuid(),
            LastFingerprint = new string('b', 64), UpdatedAt = now
        });
        dossier.RevisionDrafts.Add(new TreatmentRevisionDraft
        {
            DossierId = dossier.Id, ItemId = item.Id, ActorId = setup.ActorId, BasePlanVersion = 1, Version = 2,
            PayloadJson = "{\"reason\":\"SYNTHETIC revision\"}", UpdatedAt = now
        });
        var template = new TreatmentTemplate
        {
            BranchId = setup.BranchId, Name = "SYNTHETIC template " + Guid.NewGuid().ToString("N"), DefinitionKind = "ItemPlan",
            Version = 5, CreatedBy = setup.ActorId, CreatedAt = now
        };
        template.Items.Add(new TreatmentTemplateItem
        {
            TemplateId = template.Id, Position = 1, Date = new DateOnly(2026, 12, 1), TeethJson = "[\"16\",\"17\"]",
            ServiceId = setup.ServiceId, ServiceName = "SYNTHETIC storage service", EstimatedMinutes = 30, Note = "SYNTHETIC item",
            DentistId = setup.DentistId, DentistName = "SYNTHETIC dentist", CostVnd = 250000
        });
        template.Versions.Add(new TreatmentTemplateVersion
        {
            TemplateId = template.Id, Number = 1, State = "Active", StepsJson = "[{\"name\":\"SYNTHETIC\"}]",
            CreatedBy = setup.ActorId, CreatedAt = now, ActivatedAt = now
        });
        var activity = new ClinicalActivity
        {
            SourceKind = "Dossier", PatientId = setup.PatientId, BranchId = setup.BranchId, DossierId = dossier.Id, ItemId = item.Id,
            StepId = first.Id, PlanVersion = 1, ServiceId = setup.ServiceId, ServiceNameSnapshot = "SYNTHETIC storage service",
            Unit = "răng", Quantity = 1.500m, PerformedAt = now, PerformerId = setup.DentistId, Outcome = "SYNTHETIC outcome",
            ChargingBasis = "Included", PriceSnapshotVnd = 100000m, State = "Posted", Version = 2, CreatedAt = now, PostedAt = now
        };
        // A review and a care note written by the old tables may carry the same id.
        var sharedId = Guid.NewGuid();
        var visitId = Guid.NewGuid();
        var review = new ClinicalReview
        {
            Id = sharedId, VisitId = visitId, PatientId = setup.PatientId, BranchId = setup.BranchId, SourceActivityId = activity.Id,
            SourceItemId = item.Id, Complaint = "SYNTHETIC complaint", Findings = "SYNTHETIC findings", Coverage = "Covered",
            Outcome = "CoveredCare", CoveredComponent = "SYNTHETIC component", Policy = "SYNTHETIC policy", PolicyVersion = "1",
            Basis = "SYNTHETIC basis", NextAction = "SYNTHETIC next", DentistAccountId = setup.ActorId, RecordedAt = now, Version = 3
        };
        var note = new ClinicalCareNote
        {
            Id = sharedId, VisitId = visitId, PatientId = setup.PatientId, BranchId = setup.BranchId, SourceActivityId = activity.Id,
            ReviewId = sharedId, Content = "SYNTHETIC care note", Response = "SYNTHETIC response", ActorId = setup.ActorId, RecordedAt = now
        };
        var instruction = new FollowUpInstruction
        {
            PatientId = setup.PatientId, BranchId = setup.BranchId, DossierId = dossier.Id, ItemId = item.Id, StepId = second.Id,
            PlanVersion = 1, SourceActivityId = activity.Id, SourceCompletionEventId = first.CompletionEventId,
            Reason = "SYNTHETIC follow-up", Guidance = "SYNTHETIC guidance", EarliestOn = new DateOnly(2026, 12, 1),
            TargetOn = new DateOnly(2026, 12, 5), LatestOn = new DateOnly(2026, 12, 9), DurationMinutes = 45, DentistId = setup.DentistId,
            Capability = "SYNTHETIC capability", Status = "NeedsBooking", Version = 2, CreatedBy = setup.ActorId, CreatedAt = now, UpdatedAt = now
        };
        var planEvent = new TreatmentEvent
        {
            DossierId = dossier.Id, Kind = "plan.committed", PayloadJson = "{\"planVersion\":1}", ActorId = setup.ActorId,
            OccurredAt = now, DossierVersion = 1
        };
        // Scanner events are written by the system with actor 0, which is not an account.
        var systemEvent = new TreatmentProgressionEvent
        {
            AggregateId = second.Id, Kind = "step.due", PayloadJson = "{\"planVersion\":1}",
            StableKey = "storage-test:" + second.Id, ActorId = 0, OccurredAt = now
        };
        db.AddRange(dossier, template, activity, review, note, instruction, planEvent, systemEvent);
        await db.SaveChangesAsync();

        var expectedSteps = item.Steps.Select(Snapshot).ToArray();
        var expectedSheetDrafts = dossier.SheetDrafts.Select(Snapshot).ToArray();
        var expectedRevisionDrafts = dossier.RevisionDrafts.Select(Snapshot).ToArray();
        var expectedTemplateItems = template.Items.Select(Snapshot).ToArray();
        var expectedTemplateVersions = template.Versions.Select(Snapshot).ToArray();
        var expectedActivity = Snapshot(activity);
        var expectedReview = Snapshot(review);
        var expectedNote = Snapshot(note);
        var expectedInstruction = Snapshot(instruction);
        var expectedPlanEvent = Snapshot(planEvent);
        var expectedSystemEvent = Snapshot(systemEvent);
        var migrator = db.GetService<IMigrator>();
        db.ChangeTracker.Clear();

        await migrator.MigrateAsync(Previous);
        Assert.Equal(LegacyTables.Length, await TableCount(db));
        Assert.Equal(2, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"TREATMENT_STEP\" WHERE \"ItemId\"={item.Id} AND \"DossierId\"={dossier.Id}"));
        Assert.Equal(1, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"TREATMENT_SHEET_DRAFT\" WHERE \"DossierId\"={dossier.Id}"));
        Assert.Equal(1, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"TREATMENT_REVISION_DRAFT\" WHERE \"DossierId\"={dossier.Id} AND \"ItemId\"={item.Id}"));
        Assert.Equal(1, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"TREATMENT_TEMPLATE_ITEM\" WHERE \"TemplateId\"={template.Id}"));
        Assert.Equal(1, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"TREATMENT_TEMPLATE_VERSION\" WHERE \"TemplateId\"={template.Id} AND \"State\"='Active'"));
        Assert.Equal(1, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"CLINICAL_ACTIVITY\" WHERE \"Id\"={activity.Id}"));
        Assert.Equal(1, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"CLINICAL_REVIEW\" WHERE \"Id\"={sharedId}"));
        Assert.Equal(1, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"CLINICAL_CARE_NOTE\" WHERE \"Id\"={sharedId} AND \"ReviewId\"={sharedId}"));
        Assert.Equal(1, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"FOLLOW_UP_INSTRUCTION\" WHERE \"Id\"={instruction.Id} AND \"StepId\"={second.Id}"));
        Assert.Equal(1, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"TREATMENT_EVENT\" WHERE \"Id\"={planEvent.Id} AND \"ActorId\"={setup.ActorId}"));
        Assert.Equal(1, await Count(db, $"SELECT count(*)::integer AS \"Value\" FROM \"TREATMENT_PROGRESSION_EVENT\" WHERE \"Id\"={systemEvent.Id} AND \"ActorId\"=0"));

        await db.Database.MigrateAsync();
        Assert.Equal(0, await TableCount(db));
        var restoredItem = await db.TreatmentItems.AsNoTracking().SingleAsync(x => x.Id == item.Id);
        AssertSame(expectedSteps, restoredItem.Steps.Select(Snapshot));
        var restoredDossier = await db.TreatmentDossiers.AsNoTracking().SingleAsync(x => x.Id == dossier.Id);
        AssertSame(expectedSheetDrafts, restoredDossier.SheetDrafts.Select(Snapshot));
        AssertSame(expectedRevisionDrafts, restoredDossier.RevisionDrafts.Select(Snapshot));
        var restoredTemplate = await db.TreatmentTemplates.AsNoTracking().SingleAsync(x => x.Id == template.Id);
        AssertSame(expectedTemplateItems, restoredTemplate.Items.Select(Snapshot));
        AssertSame(expectedTemplateVersions, restoredTemplate.Versions.Select(Snapshot));
        var restoredActivity = await db.ClinicalActivities.AsNoTracking().SingleAsync(x => x.Id == activity.Id);
        Assert.Equal(activity.Id, restoredActivity.StorageId);
        AssertSame([expectedActivity], [Snapshot(restoredActivity)]);
        AssertSame([expectedReview], [Snapshot(await db.ClinicalReviews.AsNoTracking().SingleAsync(x => x.Id == sharedId))]);
        AssertSame([expectedNote], [Snapshot(await db.ClinicalCareNotes.AsNoTracking().SingleAsync(x => x.Id == sharedId))]);
        AssertSame([expectedInstruction], [Snapshot(await db.FollowUpInstructions.AsNoTracking().SingleAsync(x => x.Id == instruction.Id))]);
        AssertSame([expectedPlanEvent], [Snapshot(await db.TreatmentEvents.AsNoTracking().SingleAsync(x => x.Id == planEvent.Id))]);
        AssertSame([expectedSystemEvent], [Snapshot(await db.TreatmentProgressionEvents.AsNoTracking().SingleAsync(x => x.Id == systemEvent.Id))]);
        // The JSONB collections stay queryable through the read projections the services use.
        Assert.Equal(2, await db.TreatmentSteps.CountAsync(x => x.DossierId == dossier.Id));
        Assert.True(await db.TreatmentSteps.AnyAsync(x => x.Id == second.Id && x.HoldReason != null));
        Assert.Equal(1, await db.TreatmentRevisionDrafts.CountAsync(x => x.DossierId == dossier.Id && x.ActorId == setup.ActorId));
        Assert.Equal(1, await db.TreatmentTemplateVersions.CountAsync(x => x.TemplateId == template.Id && x.State == "Active"));
    }

    [Fact]
    public async Task StaleParentCannotOverwriteAConcurrentCollectionChange()
    {
        var setup = await Prepare();
        var now = Now();
        var (dossier, item) = NewDossier(setup, now);
        using (var seedScope = fixture.Services.CreateScope())
        {
            var seed = seedScope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            seed.Add(dossier);
            await seed.SaveChangesAsync();
        }
        using var firstScope = fixture.Services.CreateScope();
        using var secondScope = fixture.Services.CreateScope();
        using var thirdScope = fixture.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var third = thirdScope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var staleItem = await second.TreatmentItems.SingleAsync(x => x.Id == item.Id);
        var staleDossier = await third.TreatmentDossiers.SingleAsync(x => x.Id == dossier.Id);
        await first.AddTreatmentDataAsync(
            new TreatmentStep { DossierId = dossier.Id, PlanId = dossier.PlanId, PlanVersion = 1, ItemId = item.Id, Name = "SYNTHETIC current step", EstimatedMinutes = 30, ReservedMinutes = 30 },
            new TreatmentRevisionDraft { DossierId = dossier.Id, ItemId = item.Id, ActorId = setup.ActorId, BasePlanVersion = 1, UpdatedAt = now });
        await first.SaveChangesAsync();

        staleItem.Steps.Add(new TreatmentStep { DossierId = dossier.Id, PlanId = dossier.PlanId, PlanVersion = 1, ItemId = item.Id, Name = "SYNTHETIC stale step", EstimatedMinutes = 30, ReservedMinutes = 30 });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        second.ChangeTracker.Clear();
        // The draft added above did not change the dossier's business version, only its storage stamp.
        staleDossier.Note = "SYNTHETIC stale note";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => third.SaveChangesAsync());

        Assert.Equal("SYNTHETIC current step", Assert.Single((await second.TreatmentItems.AsNoTracking().SingleAsync(x => x.Id == item.Id)).Steps).Name);
        Assert.Single((await second.TreatmentDossiers.AsNoTracking().SingleAsync(x => x.Id == dossier.Id)).RevisionDrafts);
    }

    [Fact]
    public async Task LegacyStepEventWithoutPlanVersionIsSkippedWhileLaterEventsAreStillIngested()
    {
        var setup = await Prepare();
        var now = Now();
        var (dossier, item) = NewDossier(setup, now);
        var step = new TreatmentStep { DossierId = dossier.Id, PlanId = dossier.PlanId, PlanVersion = 1, ItemId = item.Id, Name = "SYNTHETIC blocked step", EstimatedMinutes = 30, ReservedMinutes = 30 };
        item.Steps.Add(step);
        var instruction = new FollowUpInstruction
        {
            PatientId = setup.PatientId, BranchId = setup.BranchId, Reason = "SYNTHETIC due follow-up", EarliestOn = new DateOnly(2026, 12, 1),
            TargetOn = new DateOnly(2026, 12, 5), LatestOn = new DateOnly(2026, 12, 9), DurationMinutes = 30, Status = "NeedsBooking",
            CreatedBy = setup.ActorId, CreatedAt = now, UpdatedAt = now
        };
        var legacyKey = "legacy-step-blocked:" + step.Id;
        var dueKey = $"followup-due:{instruction.Id}:20261201:20261205:20261209";
        var payload = JsonSerializer.Serialize(new { patientId = setup.PatientId });
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        db.AddRange(dossier, instruction,
            new TreatmentProgressionEvent { AggregateId = step.Id, Kind = "step.prerequisite_blocked", PayloadJson = payload, StableKey = legacyKey, ActorId = 0, OccurredAt = now.AddMinutes(-5) },
            new TreatmentProgressionEvent { AggregateId = instruction.Id, Kind = "followup.due", PayloadJson = payload, StableKey = dueKey, ActorId = 0, OccurredAt = now });
        await db.SaveChangesAsync();

        Assert.True(await scope.ServiceProvider.GetRequiredService<CareTaskIngestionService>().IngestAsync(CancellationToken.None) >= 1);

        Assert.False(await db.CareTasks.AnyAsync(x => x.EpisodeKey == legacyKey));
        var task = await db.CareTasks.AsNoTracking().SingleAsync(x => x.EpisodeKey == dueKey);
        Assert.Equal("FollowUpDue", task.Type);
        Assert.Equal(instruction.Id, task.InstructionId);
    }

    private static Task<int> Count(DcmsDbContext db, FormattableString sql) => db.Database.SqlQuery<int>(sql).SingleAsync();

    private static Task<int> TableCount(DcmsDbContext db) => db.Database.SqlQueryRaw<int>(
        "SELECT count(*)::integer AS \"Value\" FROM information_schema.tables WHERE table_schema=current_schema() AND table_name IN ('"
        + string.Join("','", LegacyTables) + "')").SingleAsync();

    // Storage keys and stamps are regenerated by the conversion; JSON text is compared by content.
    private static JsonNode Snapshot<T>(T row) where T : class
    {
        var node = JsonSerializer.SerializeToNode(row, row.GetType())!.AsObject();
        node.Remove(nameof(TreatmentProcessRecord.StorageId));
        node.Remove(nameof(TreatmentProcessRecord.StorageStamp));
        foreach (var name in node.Select(x => x.Key).Where(x => x.EndsWith("Json")).ToArray())
            node[name] = JsonNode.Parse(node[name]!.GetValue<string>());
        return node;
    }

    private static void AssertSame(IEnumerable<JsonNode> expected, IEnumerable<JsonNode> actual)
    {
        static JsonNode[] Sorted(IEnumerable<JsonNode> rows) => rows.OrderBy(x => x["Id"]!.GetValue<Guid>()).ToArray();
        var left = Sorted(expected);
        var right = Sorted(actual);
        Assert.Equal(left.Length, right.Length);
        for (var index = 0; index < left.Length; index++)
            Assert.True(JsonNode.DeepEquals(left[index], right[index]), $"Expected {left[index].ToJsonString()} but found {right[index].ToJsonString()}");
    }
}

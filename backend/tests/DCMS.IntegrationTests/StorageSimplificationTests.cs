using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Prescriptions;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Authentication;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class StorageSimplificationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task PrescriptionApiKeepsSnapshotsReplayVersionAndVerifiedPatientAccess()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var employee = await db.Employees.Join(db.Users, e => e.AccountId, u => u.Id, (e, u) => new { Employee = e, u.UserName })
            .Where(x => x.UserName == "dentist.demo").Select(x => x.Employee).SingleAsync();
        var patient = await db.Patients.FirstAsync();
        var account = await db.Users.SingleAsync(x => x.UserName == "guest.demo");
        var now = DateTimeOffset.UtcNow;
        var branch = new SchedulingBranch { Name = "SYNTHETIC prescription API" };
        var medicine = new CatalogMedicine
        {
            Code = "JSON-API", Name = "SYNTHETIC original medicine", NormalizedName = "SYNTHETIC ORIGINAL MEDICINE",
            BaseUnit = "unit", PrescriptionEligible = true, CreatedBy = employee.AccountId, UpdatedBy = employee.AccountId,
            CreatedAt = now, UpdatedAt = now
        };
        db.AddRange(branch, medicine);
        await db.SaveChangesAsync();
        await db.AddSchedulingDataAsync(new SchedulingMember { BranchId = branch.Id, EmployeeId = employee.Id, CanDentist = true });
        foreach (var link in await db.PatientAccountLinks.Where(x => x.AccountId == account.Id).ToListAsync())
            link.Status = PatientLinkStatus.Revoked;
        await db.SaveChangesAsync();
        db.Add(new PatientAccountLink { AccountId = account.Id, PatientId = patient.Id, Status = PatientLinkStatus.Verified,
            RequestCode = Guid.NewGuid().ToString("N"), DeclaredFullName = patient.FullName, RequestedAt = now });
        await db.SaveChangesAsync();

        async Task<HttpClient> Login(string userName)
        {
            var client = fixture.CreateClient();
            var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(userName, fixture.Password));
            response.EnsureSuccessStatusCode();
            var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            return client;
        }

        using var dentist = await Login("dentist.demo");
        var path = $"/api/prescriptions/patients/{patient.Id}";
        var command = new SavePatientPrescription(Guid.NewGuid(), 0, branch.Id,
            DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime), "SYNTHETIC", null, null,
            [new SavePatientPrescriptionItem(medicine.Id, 2, "SYNTHETIC instructions")]);
        var response = await dentist.PostAsJsonAsync(path, command);
        response.EnsureSuccessStatusCode();
        var saved = (await response.Content.ReadFromJsonAsync<PatientProfilePrescriptionDto>())!;
        var replay = await dentist.PostAsJsonAsync(path, command);
        replay.EnsureSuccessStatusCode();
        Assert.Equal(saved.Id, (await replay.Content.ReadFromJsonAsync<PatientProfilePrescriptionDto>())!.Id);
        medicine.Name = "SYNTHETIC renamed medicine";
        await db.SaveChangesAsync();
        var detail = (await dentist.GetFromJsonAsync<PatientProfilePrescriptionDto>($"{path}/{saved.Id}"))!;
        Assert.Equal("SYNTHETIC original medicine", Assert.Single(detail.Items).MedicineName);
        using var patientClient = await Login("guest.demo");
        var own = await patientClient.GetFromJsonAsync<PatientProfilePrescriptionDto>($"/api/patients/me/prescriptions/{saved.Id}");
        Assert.Equal(saved.Id, own!.Id);
        var edit = command with { RequestKey = Guid.NewGuid(), ExpectedVersion = saved.Version };
        var changed = await dentist.PutAsJsonAsync($"{path}/{saved.Id}", edit);
        changed.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await dentist.PutAsJsonAsync($"{path}/{saved.Id}",
            edit with { RequestKey = Guid.NewGuid() })).StatusCode);
        using var anonymous = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{path}/{saved.Id}")).StatusCode);
    }

    [Fact]
    public async Task UpgradeAndRollbackPreserveCollectionsAuditIdsAndReceiptScopes()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var employee = await db.Employees.FirstAsync();
        var patient = await db.Patients.FirstAsync();
        var actor = employee.AccountId;
        var now = DateTimeOffset.UtcNow;
        var branch = new SchedulingBranch { Name = "SYNTHETIC consolidated branch" };
        var medicine = new CatalogMedicine
        {
            Code = "JSON-TEST", Name = "SYNTHETIC medicine", NormalizedName = "SYNTHETIC MEDICINE",
            BaseUnit = "unit", PrescriptionEligible = true, CreatedBy = actor, UpdatedBy = actor,
            CreatedAt = now, UpdatedAt = now
        };
        db.AddRange(branch, medicine);
        await db.SaveChangesAsync();
        db.Add(new BranchProfile { Id = branch.Id, Code = "JSON-BRANCH", Name = branch.Name, Phone = "0900000000" });
        await db.AddSchedulingDataAsync(new SchedulingMember { BranchId = branch.Id, EmployeeId = employee.Id, CanDentist = true });
        var prescription = new PatientPrescription
        {
            PatientId = patient.Id, BranchId = branch.Id, PrescriberEmployeeId = employee.Id, RequiresExternalPurchase = true,
            PrescribedOn = new DateOnly(2026, 11, 12), CreatedAt = now, UpdatedAt = now, Version = 7,
            Items = [new() { MedicineId = medicine.Id, MedicineName = "SYNTHETIC saved snapshot", MedicineCode = medicine.Code,
                Quantity = 3, Unit = medicine.BaseUnit, Instructions = "SYNTHETIC instructions", Position = 0, IsExternalPurchase = true }]
        };
        var template = new PrescriptionTemplate
        {
            Code = "JSON-TEMPLATE", Name = "SYNTHETIC template", CreatedBy = actor, UpdatedBy = actor,
            CreatedAt = now, UpdatedAt = now,
            Lines = [new() { MedicineId = medicine.Id, MedicineCode = medicine.Code, MedicineName = medicine.Name,
                Unit = medicine.BaseUnit, Quantity = 2, Days = 1 }]
        };
        var week = new WorkingWeek
        {
            BranchId = branch.Id, EmployeeId = employee.Id, WeekStart = new DateOnly(2026, 11, 9),
            PublishedBy = actor, PublishedAt = now,
            Periods = [new() { Day = 0, StartMinute = 480, EndMinute = 540, Kind = "Work" }]
        };
        var group = new AccessGroup
        {
            Code = "JSON-GROUP", NormalizedCode = "JSON-GROUP", Name = "SYNTHETIC group",
            NormalizedName = "SYNTHETIC GROUP", BaseRole = "Dentist",
            Grants = [new() { Permission = "prescriptions.read" }]
        };
        var requestKey = Guid.NewGuid();
        db.AddRange(prescription, template, week, group,
            new AccountAudit { Id = 700, ActorId = actor, TargetId = actor, Action = "Synthetic", Reason = "SYNTHETIC", OccurredAt = now },
            new RecoveryAudit { Id = 700, ActorId = actor, TargetId = actor, Action = "Synthetic", Reason = "SYNTHETIC", ChannelType = "Email", Result = "Sent", OccurredAt = now },
            new ScheduleReceipt { ActorId = actor, RequestKey = requestKey, Fingerprint = "schedule", ResultJson = "{\"version\":7}" },
            new PatientPrescriptionReceipt { ActorId = actor, RequestKey = requestKey, Fingerprint = "prescription", PrescriptionId = prescription.Id });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261109101904_AdvancePayOs20261109");
        await migrator.MigrateAsync();

        var read = await db.Set<PatientPrescription>().SingleAsync(x => x.Id == prescription.Id);
        var item = Assert.Single(read.Items);
        Assert.Equal("SYNTHETIC saved snapshot", item.MedicineName);
        Assert.Equal(prescription.Items[0].Id, item.Id);
        Assert.Equal(3, item.Quantity);
        Assert.True(item.IsExternalPurchase);
        Assert.True(read.RequiresExternalPurchase);
        Assert.Equal(7, read.Version);
        Assert.Equal(2, Assert.Single((await db.Set<PrescriptionTemplate>().SingleAsync(x => x.Id == template.Id)).Lines).Quantity);
        Assert.Equal(480, Assert.Single((await db.Set<WorkingWeek>().SingleAsync(x => x.Id == week.Id)).Periods).StartMinute);
        Assert.Equal("prescriptions.read", Assert.Single((await db.Set<AccessGroup>().SingleAsync(x => x.Id == group.Id)).Grants).Permission);
        Assert.Equal("0900000000", (await db.Set<BranchProfile>().SingleAsync(x => x.Id == branch.Id)).Phone);
        Assert.True(await db.AccountAudits.AnyAsync(x => x.Id == 700 && x.Action == "Synthetic"));
        Assert.True(await db.RecoveryAudits.AnyAsync(x => x.Id == 700 && x.Action == "Synthetic"));
        Assert.Equal("schedule", (await db.Set<ScheduleReceipt>().SingleAsync(x => x.RequestKey == requestKey)).Fingerprint);
        Assert.Equal("prescription", (await db.Set<PatientPrescriptionReceipt>().SingleAsync(x => x.RequestKey == requestKey)).Fingerprint);
        var next = new AccountAudit { ActorId = actor, TargetId = actor, Action = "SyntheticNext", Reason = "SYNTHETIC", OccurredAt = now };
        db.Add(next);
        await db.SaveChangesAsync();
        Assert.True(next.Id > 700);
    }
}

public sealed class LegacyPrescriptionStorageTests(ReceptionFixture fixture) : IClassFixture<ReceptionFixture>
{
    [Fact]
    public async Task VisitApiEditsFinalizesCopiesAndDeletesJsonLinesThroughTheParent()
    {
        var reception = new ReceptionApiTests(fixture);
        var setup = await reception.Prepare();
        using var assistant = await reception.Login();
        var visit = await reception.Check(assistant, setup);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var row = await db.Visits.SingleAsync(x => x.Id == visit.VisitId);
            row.Status = DCMS.Domain.Features.Visits.VisitStatuses.InProgress;
            row.Version++;
            await db.SaveChangesAsync();
        }
        using var dentist = await reception.Login("dentist.demo");
        var path = $"/api/prescriptions/visits/{visit.VisitId}";
        var command = new SavePrescriptionLine(Guid.NewGuid(), 0, null, 1, "Uống", "1", "0", "0", "0",
            "AfterMeal", 1, "SYNTHETIC instructions", "SYNTHETIC custom medicine", "unit");
        var created = await dentist.PostAsJsonAsync($"{path}/lines", command);
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var line = (await created.Content.ReadFromJsonAsync<PrescriptionLineDto>())!;
        var edited = await dentist.PutAsJsonAsync($"{path}/lines/{line.Id}",
            command with { RequestKey = Guid.NewGuid(), ExpectedVersion = line.Version, Quantity = 2 });
        Assert.True(edited.IsSuccessStatusCode, await edited.Content.ReadAsStringAsync());
        var current = (await dentist.GetFromJsonAsync<VisitPrescriptionDto>($"{path}/prescription"))!.Current!;
        Assert.Equal(2, Assert.Single(current.Lines).Quantity);
        var finalizedResponse = await dentist.PostAsJsonAsync($"{path}/finalize", new { requestKey = Guid.NewGuid(), expectedVersion = current.Version });
        Assert.True(finalizedResponse.IsSuccessStatusCode, await finalizedResponse.Content.ReadAsStringAsync());
        var finalized = (await finalizedResponse.Content.ReadFromJsonAsync<PrescriptionDto>())!;
        Assert.Equal("Finalized", Assert.Single(finalized.Lines).Status);
        var correctionResponse = await dentist.PostAsJsonAsync($"{path}/corrections",
            new { requestKey = Guid.NewGuid(), expectedVersion = finalized.Version, reason = "SYNTHETIC correction" });
        Assert.True(correctionResponse.IsSuccessStatusCode, await correctionResponse.Content.ReadAsStringAsync());
        var correction = (await correctionResponse.Content.ReadFromJsonAsync<PrescriptionDto>())!;
        var copied = Assert.Single(correction.Lines);
        Assert.Equal(2, copied.Quantity);
        Assert.NotEqual(line.Id, copied.Id);
        var deleted = await dentist.DeleteAsync($"{path}/lines/{copied.Id}?requestKey={Guid.NewGuid()}&expectedVersion={copied.Version}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var result = (await dentist.GetFromJsonAsync<VisitPrescriptionDto>($"{path}/prescription"))!;
        Assert.Empty(result.Current!.Lines);
        Assert.Equal(2, result.History.Length);
    }
}

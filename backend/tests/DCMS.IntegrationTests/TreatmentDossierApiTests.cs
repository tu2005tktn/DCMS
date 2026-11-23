using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Patients;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class TreatmentDossierApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<HttpClient> Login(string user)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(user, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    private async Task<(int PatientId, int BranchId, int DentistId, int ServiceId)> Prepare()
    {
        using var assistant = await Login("assistant.demo");
        var patientResponse = await assistant.PostAsJsonAsync("/api/patients", new CreatePatientRequest(
            null, "SYNTHETIC DOSSIER " + Guid.NewGuid().ToString("N"), new DateOnly(1990, 5, 6),
            "OTHER", null, null, null, null, null, null, false));
        Assert.Equal(HttpStatusCode.Created, patientResponse.StatusCode);
        var patient = (await patientResponse.Content.ReadFromJsonAsync<PatientChange>())!.Patient;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var dentistId = await (from employee in db.Employees join user in db.Users on employee.AccountId equals user.Id
            where user.UserName == "dentist.demo" select employee.Id).SingleAsync();
        var branch = new SchedulingBranch { Name = "S21 isolated " + Guid.NewGuid().ToString("N") };
        db.Add(branch);
        await db.SaveChangesAsync();
        var member = new SchedulingMember { BranchId = branch.Id, EmployeeId = dentistId, CanDentist = true };
        var service = new SchedulingService { BranchId = branch.Id, Name = "Trám răng tổng hợp", PublicDurationMinutes = 30 };
        await db.AddSchedulingDataAsync(member, service);
        await db.SaveChangesAsync();
        return (patient.Id, branch.Id, dentistId, service.Id);
    }

    private static async Task AssertMessage(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
    }

    private static System.Text.Json.JsonElement SheetRow(int serviceId, int dentistId, long price = 300000, string region = "25") =>
        System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            serviceId, dentistId, regions = new[] { region }, quantity = 1m,
            unitPriceVnd = price, costVnd = price, estimatedMinutes = 30,
            discountType = "VND", discountValue = 0m
        });

    private static async Task<TreatmentDossierDto> AddPlanItem(
        HttpClient client, Guid dossierId, int expectedVersion, int serviceId, int dentistId,
        long unitPrice = 300000, string region = "25")
    {
        var draftId = Guid.NewGuid();
        var draftResponse = await client.PutAsJsonAsync($"/api/dossiers/{dossierId}/sheet-drafts/{draftId}",
            new SaveSheetDraftRequest("Plan", null, SheetRow(serviceId, dentistId, unitPrice, region), 0, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        var confirmResponse = await client.PostAsJsonAsync($"/api/dossiers/{dossierId}/sheet-drafts/{draftId}/confirm-plan",
            new ConfirmSheetPlanRequest(expectedVersion, 1, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        return (await confirmResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
    }

    [Fact]
    public async Task EmptyDossierCanBeDeletedButClinicalPlanCannot()
    {
        var setup = await Prepare();
        using var dentist = await Login("dentist.demo");
        var createdResponse = await dentist.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers",
            new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC delete", null,
                null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var url = $"/api/dossiers/{created.Id}";
        var key = Guid.NewGuid();
        async Task<HttpResponseMessage> Delete(DeleteDossierRequest request)
        {
            using var message = new HttpRequestMessage(HttpMethod.Delete, url)
                { Content = JsonContent.Create(request) };
            return await dentist.SendAsync(message);
        }
        var request = new DeleteDossierRequest(created.Version, key);
        var deletedResponse = await Delete(request);
        Assert.Equal(HttpStatusCode.OK, deletedResponse.StatusCode);
        Assert.Equal("Deleted", (await deletedResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier.State);
        Assert.Equal(HttpStatusCode.OK, (await Delete(request)).StatusCode);
        await AssertMessage(await Delete(new DeleteDossierRequest(created.Version + 1, key)),
            HttpStatusCode.Conflict, "MSG111");
        await AssertMessage(await dentist.GetAsync(url), HttpStatusCode.NotFound, "MSG113");
        var page = await dentist.GetFromJsonAsync<DossierPage>(
            $"/api/patients/{setup.PatientId}/dossiers?page=1&pageSize=20");
        Assert.DoesNotContain(page!.Items, x => x.Id == created.Id);

        var secondResponse = await dentist.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers",
            new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC plan", null,
                null, Guid.NewGuid()));
        var second = (await secondResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var withItem = await AddPlanItem(dentist, second.Id, second.Version, setup.ServiceId, setup.DentistId, 100000);
        using var blocked = new HttpRequestMessage(HttpMethod.Delete, $"/api/dossiers/{second.Id}")
            { Content = JsonContent.Create(new DeleteDossierRequest(withItem.Version, Guid.NewGuid())) };
        await AssertMessage(await dentist.SendAsync(blocked), HttpStatusCode.Conflict, "MSG233");
    }

    [Fact]
    public async Task OwnerCanForfeitDepositAndHideUnperformedNoShowDossier()
    {
        var setup = await Prepare();
        using var dentist = await Login("dentist.demo");
        using var owner = await Login("owner.demo");
        var createdResponse = await dentist.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers",
            new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC no show", null,
                null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var committed = await AddPlanItem(dentist, created.Id, created.Version, setup.ServiceId, setup.DentistId, 200000);
        var ownerId = (await owner.GetFromJsonAsync<CurrentUser>("/api/auth/me"))!.Id;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var payload = System.Text.Json.JsonSerializer.Serialize(new { kind = "AdvanceCash", amountVnd = 100000L,
                labPriority = Array.Empty<object>() });
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AUDIT_LOG" ("StorageId", "AuditType", "Action", "ActorId", "At",
                    "PatientId", "BranchId", "Target", "PayloadJson")
                VALUES ({Guid.NewGuid()}, 'FinanceLedger', 'AdvanceCash', {ownerId}, {DateTimeOffset.UtcNow},
                    {setup.PatientId}, {setup.BranchId}, {created.Id.ToString()}, {payload}::jsonb)
                """);
        }
        var before = await owner.GetFromJsonAsync<DCMS.Api.Features.Finance.FinanceStatisticsResult>(
            $"/api/finance/statistics?branchId={setup.BranchId}");
        Assert.Equal(100000, before!.AdvanceReceivedVnd);
        Assert.Equal(100000, before.ActualReceivedVnd);
        Assert.Contains(before.OutstandingDossiers, x => x.DossierId == created.Id && x.CanForfeitDeposit);
        var request = new DCMS.Api.Features.Finance.ForfeitNoShowDeposit(Guid.NewGuid(), committed.Version,
            "SYNTHETIC patient did not attend");
        var url = $"/api/dossiers/{created.Id}/payments/no-show-forfeit";
        Assert.Equal(HttpStatusCode.Forbidden, (await dentist.PostAsJsonAsync(url, request)).StatusCode);
        var response = await owner.PostAsJsonAsync(url, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync(url, request)).StatusCode);
        var after = await owner.GetFromJsonAsync<DCMS.Api.Features.Finance.FinanceStatisticsResult>(
            $"/api/finance/statistics?branchId={setup.BranchId}");
        Assert.Equal(0, after!.AdvanceReceivedVnd);
        Assert.Equal(100000, after.ActualReceivedVnd);
        Assert.Equal(0, after.RemainingReceivableVnd);
        Assert.DoesNotContain(after.OutstandingDossiers, x => x.DossierId == created.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await dentist.GetAsync($"/api/dossiers/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task CloseAndReopenDossierPreservesHistoryAndBlocksClosingEmptyPlan()
    {
        var setup = await Prepare();
        using var dentist = await Login("dentist.demo");
        var create = new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC diagnosis", null,
            null, Guid.NewGuid());
        var url = $"/api/patients/{setup.PatientId}/dossiers";
        var createdResponse = await dentist.PostAsJsonAsync(url, create);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var retry = await dentist.PostAsJsonAsync(url, create);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(created.Id, (await retry.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier.Id);
        await AssertMessage(await dentist.PostAsJsonAsync(url, create with { Diagnosis = "Different" }),
            HttpStatusCode.Conflict, "MSG111");

        await AssertMessage(await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/close",
            new CloseDossierRequest(created.Version, "CompletedAsAgreed", null, Guid.NewGuid())),
            HttpStatusCode.Conflict, "MSG243");

        var withItem = await AddPlanItem(dentist, created.Id, created.Version, setup.ServiceId, setup.DentistId);
        Assert.Equal(1, withItem.PlanVersion);

        var stoppedResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/close",
            new CloseDossierRequest(withItem.Version, "StoppedIncomplete", "Dừng phần còn lại", Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, stoppedResponse.StatusCode);
        var stopped = (await stoppedResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal("Stopped", stopped.State);
        Assert.Single(stopped.Timeline, e => e.Kind == "DossierClosed" && e.PayloadJson.Contains("StoppedIncomplete"));

        var reopenedResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/reopen",
            new ReopenDossierRequest(stopped.Version, "Tiếp tục điều trị", Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, reopenedResponse.StatusCode);
        var reopened = (await reopenedResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal("Open", reopened.State);
        using var verify = fixture.Services.CreateScope();
        var readDb = verify.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(0, await readDb.Appointments.CountAsync(x => x.PatientId == setup.PatientId));
    }

    [Fact]
    public async Task NonDentistCannotCreateAndUnlinkedPublicCannotReadOwnSummary()
    {
        var setup = await Prepare();
        var create = new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC", null, null, Guid.NewGuid());
        using var assistant = await Login("assistant.demo");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await assistant.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers", create)).StatusCode);
        using var publicClient = await Login("guest.demo");
        var summary = await publicClient.GetFromJsonAsync<OwnTreatmentSummaryItem[]>("/api/patients/me/treatment-summary");
        Assert.DoesNotContain(summary!, x => x.Diagnosis == "SYNTHETIC");
    }

    [Fact]
    public async Task OwnerCashPaymentMovesFullyPaidServiceOutOfOutstandingList()
    {
        var setup = await Prepare();
        using var dentist = await Login("dentist.demo");
        var createdResponse = await dentist.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers",
            new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC payment tabs", null,
                null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var dossierId = (await createdResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier.Id;
        Guid activityId, labOrderId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var activity = new ClinicalActivity
            {
                DossierId = dossierId,
                ServiceId = setup.ServiceId, ServiceNameSnapshot = "SYNTHETIC paid service",
                Unit = "lần", Quantity = 1, PriceSnapshotVnd = 120000,
                PerformerId = setup.DentistId, PerformedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow};
            db.ClinicalActivities.Add(activity);
            await db.SaveChangesAsync();
            activityId = activity.Id;
            var labOrder = new LabOrder { ActivityId = activity.Id,
                DentistId = setup.DentistId,
                WorkName = "SYNTHETIC lab", OrderedOn = DateOnly.FromDateTime(DateTime.UtcNow),
                TotalFeeVnd = 80000, PaidVnd = 10000 };
            db.LabOrders.Add(labOrder);
            await db.SaveChangesAsync(); labOrderId = labOrder.Id;
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var category = new DCMS.Domain.Features.SystemSettings.MaterialCategory
            {
                Name = "SYNTHETIC finance category " + suffix
            };
            var material = new DCMS.Domain.Features.SystemSettings.MaterialCatalogItem
            {
                Name = "SYNTHETIC finance material " + suffix,
                Category = category, UsageUnit = "unit"
            };
            db.MaterialCatalogItems.Add(material);
            db.MaterialPurchases.Add(new DCMS.Domain.Features.SystemSettings.MaterialPurchase
            {
                Material = material,
                EnteredOn = DateOnly.FromDateTime(DateTime.UtcNow), Quantity = 2, Unit = "unit",
                UsageQuantity = 2, UnitPriceVnd = 10000, TotalVnd = 20000,
                CreatedAt = DateTimeOffset.UtcNow
            });
            db.MaterialUsages.Add(new DCMS.Domain.Features.SystemSettings.MaterialUsage
            {
                ActivityId = activity.Id, MaterialId = material.Id,
                Quantity = 1, UsedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
        using var owner = await Login("owner.demo");
        var before = await owner.GetFromJsonAsync<DCMS.Api.Features.Finance.TreatmentPaymentContext>(
            $"/api/dossiers/{dossierId}/payments/context");
        Assert.Equal(120000, Assert.Single(before!.Lines).DueVnd);
        Assert.Equal(24000, Assert.Single(before.LabFees).Minimum30PercentVnd);
        Assert.Equal(24000, Assert.Single(before.LabFees).PriorityRemainingVnd);
        Assert.Equal(200000, before.TotalCostVnd);
        var labBefore = Assert.Single((await owner.GetFromJsonAsync<LabWorkPage>(
            "/api/lab-orders?pageSize=100"))!.Items, x => x.Id == labOrderId);
        Assert.Equal("Unpaid", labBefore.PatientPaymentStatus);
        Assert.Equal(0, labBefore.PatientPaidVnd);
        Assert.Equal(10000, labBefore.PaidVnd); // Clinic payment to the lab provider is separate.
        var key = Guid.NewGuid();
        var request = new DCMS.Api.Features.Finance.RecordTreatmentCashPayment(key,
            DateOnly.FromDateTime(DateTime.UtcNow), [new(activityId, 120000)], [new(labOrderId, 24000)]);
        var response = await owner.PostAsJsonAsync($"/api/dossiers/{dossierId}/payments", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = (await response.Content.ReadFromJsonAsync<DCMS.Api.Features.Finance.TreatmentPaymentContext>())!;
        Assert.Equal(0, Assert.Single(after.Lines).DueVnd);
        Assert.Equal(120000, Assert.Single(after.Lines).PaidVnd);
        Assert.Equal(24000, Assert.Single(after.LabFees).PaidVnd);
        Assert.Equal(56000, Assert.Single(after.LabFees).DueVnd);
        Assert.Equal(0, Assert.Single(after.LabFees).PriorityRemainingVnd);
        var labPartial = Assert.Single((await owner.GetFromJsonAsync<LabWorkPage>(
            "/api/lab-orders?pageSize=100"))!.Items, x => x.Id == labOrderId);
        Assert.Equal("Partial", labPartial.PatientPaymentStatus);
        Assert.Equal(24000, labPartial.PatientPaidVnd);
        var partialStatistics = await owner.GetFromJsonAsync<DCMS.Api.Features.Finance.FinanceStatisticsResult>(
            $"/api/finance/statistics?branchId={setup.BranchId}");
        Assert.Equal(200000, partialStatistics!.ServiceAmountVnd);
        Assert.Equal(20000, partialStatistics.MaterialPurchaseVnd);
        Assert.Equal(144000, partialStatistics.ActualReceivedVnd);
        Assert.Equal(56000, partialStatistics.RemainingReceivableVnd);
        Assert.Contains(partialStatistics.OutstandingDossiers, x => x.DossierId == dossierId && x.DueVnd == 56000);
        Assert.Equal(44000, partialStatistics.ProfitVnd);
        Assert.Equal(1, partialStatistics.TreatmentSessionCount);
        Assert.Contains(partialStatistics.ProfitEntries, x => x.DossierId == dossierId
            && x.RevenueVnd == 144000 && x.CostVnd == 80000);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsJsonAsync($"/api/dossiers/{dossierId}/payments", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsJsonAsync($"/api/dossiers/{dossierId}/payments", request with
            { RequestKey = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await dentist.GetAsync($"/api/dossiers/{dossierId}/payments/context")).StatusCode);
        var advance = new DCMS.Api.Features.Finance.CreateTreatmentPayOsOrder(Guid.NewGuid(), null, 75000, null);
        var advanceResponse = await owner.PostAsJsonAsync($"/api/dossiers/{dossierId}/payments/advances", advance);
        Assert.Equal(HttpStatusCode.OK, advanceResponse.StatusCode);
        var afterAdvance = await owner.GetFromJsonAsync<DCMS.Api.Features.Finance.TreatmentPaymentContext>(
            $"/api/dossiers/{dossierId}/payments/context");
        Assert.Equal(75000, afterAdvance!.AdvanceReceivedVnd);
        Assert.Equal(24000, Assert.Single(afterAdvance.LabFees).PaidVnd);
        Assert.Equal(0, Assert.Single(afterAdvance.Lines).DueVnd);
        var statistics = await owner.GetFromJsonAsync<DCMS.Api.Features.Finance.FinanceStatisticsResult>(
            $"/api/finance/statistics?branchId={setup.BranchId}");
        Assert.Equal(200000, statistics!.ServiceAmountVnd);
        Assert.Equal(219000, statistics.ActualReceivedVnd);
        Assert.Equal(75000, statistics.AdvanceReceivedVnd);
        Assert.Equal(219000, statistics.ActualReceivedVnd);
        Assert.Equal(56000, statistics.RemainingReceivableVnd);
        Assert.Equal(75000, statistics.AdvanceReceivedVnd);
        Assert.Equal(44000, statistics.ProfitVnd);
        Assert.Equal(1, statistics.TreatmentSessionCount); // Unallocated deposit does not increase session profit.
        Guid additionalActivityId;
        var nextTreatmentAt = DateTimeOffset.UtcNow.AddDays(2);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var additional = new ClinicalActivity
            {
                DossierId = dossierId,
                ServiceId = setup.ServiceId, ServiceNameSnapshot = "SYNTHETIC service paid from deposit",
                Unit = "lần", Quantity = 1, PriceSnapshotVnd = 19000,
                PerformerId = setup.DentistId, PerformedAt = nextTreatmentAt,
                CreatedAt = DateTimeOffset.UtcNow};
            db.ClinicalActivities.Add(additional);
            await db.SaveChangesAsync();
            additionalActivityId = additional.Id;
        }
        var useDeposit = new DCMS.Api.Features.Finance.ApplyTreatmentAdvance(Guid.NewGuid(),
            [new(additionalActivityId, 19000)], [new(labOrderId, 56000)]);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsJsonAsync($"/api/dossiers/{dossierId}/payments/advance-allocations", useDeposit)).StatusCode);
        var afterAllocation = await owner.GetFromJsonAsync<DCMS.Api.Features.Finance.TreatmentPaymentContext>(
            $"/api/dossiers/{dossierId}/payments/context");
        Assert.Equal(0, afterAllocation!.AdvanceReceivedVnd);
        Assert.Equal(0, Assert.Single(afterAllocation.Lines, x => x.ActivityId == additionalActivityId).DueVnd);
        Assert.Equal(0, Assert.Single(afterAllocation.LabFees).DueVnd);
        var afterAllocationStatistics = await owner.GetFromJsonAsync<DCMS.Api.Features.Finance.FinanceStatisticsResult>(
            $"/api/finance/statistics?branchId={setup.BranchId}");
        Assert.Equal(219000, afterAllocationStatistics!.ServiceAmountVnd);
        Assert.Equal(219000, afterAllocationStatistics.ActualReceivedVnd);
        Assert.Equal(0, afterAllocationStatistics.AdvanceReceivedVnd);
        Assert.Equal(0, afterAllocationStatistics.RemainingReceivableVnd);
        Assert.DoesNotContain(afterAllocationStatistics.OutstandingDossiers, x => x.DossierId == dossierId);
        Assert.Equal(20000, afterAllocationStatistics.MaterialPurchaseVnd);
        Assert.Equal(119000, afterAllocationStatistics.ProfitVnd);
        Assert.Equal(2, afterAllocationStatistics.TreatmentSessionCount);
        Assert.Equal(2, afterAllocationStatistics.ProfitEntries.Count(x => x.DossierId == dossierId));
        var nextTreatmentDate = DateOnly.FromDateTime(nextTreatmentAt.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var nextSessionOnly = await owner.GetFromJsonAsync<DCMS.Api.Features.Finance.FinanceStatisticsResult>(
            $"/api/finance/statistics?branchId={setup.BranchId}&from={nextTreatmentDate:yyyy-MM-dd}&to={nextTreatmentDate:yyyy-MM-dd}");
        Assert.Equal(19000, nextSessionOnly!.ProfitVnd);
        Assert.Equal(1, nextSessionOnly.TreatmentSessionCount);
        Assert.Equal(19000, Assert.Single(nextSessionOnly.ProfitEntries).RevenueVnd);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsJsonAsync($"/api/dossiers/{dossierId}/payments/advance-allocations", useDeposit)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsJsonAsync($"/api/dossiers/{dossierId}/payments/advance-allocations",
                useDeposit with { RequestKey = Guid.NewGuid() })).StatusCode);
        var labPaid = Assert.Single((await owner.GetFromJsonAsync<LabWorkPage>(
            "/api/lab-orders?pageSize=100"))!.Items, x => x.Id == labOrderId);
        Assert.Equal("Paid", labPaid.PatientPaymentStatus);
        Assert.Equal(80000, labPaid.PatientPaidVnd);
        Assert.Equal("NotSent", labPaid.Status);
        Assert.Equal(10000, labPaid.PaidVnd);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsJsonAsync($"/api/dossiers/{dossierId}/payments/advances", advance)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await dentist.PostAsJsonAsync($"/api/dossiers/{dossierId}/payments/advances", advance with
            { RequestKey = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task LabMinimumBlocksOtherServiceUntilAdvanceCoversIt()
    {
        var setup = await Prepare();
        using var dentist = await Login("dentist.demo");
        var created = await dentist.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers",
            new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC lab priority", null, null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dossierId = (await created.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier.Id;
        Guid otherId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var labActivity = new ClinicalActivity { DossierId = dossierId, ServiceId = setup.ServiceId, ServiceNameSnapshot = "SYNTHETIC lab service",
                Unit = "lần", Quantity = 1, PriceSnapshotVnd = 100000, PerformerId = setup.DentistId,
                PerformedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow};
            var other = new ClinicalActivity { DossierId = dossierId, ServiceId = setup.ServiceId, ServiceNameSnapshot = "SYNTHETIC other service",
                Unit = "lần", Quantity = 1, PriceSnapshotVnd = 100000, PerformerId = setup.DentistId,
                PerformedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow};
            db.ClinicalActivities.AddRange(labActivity, other);
            await db.SaveChangesAsync(); otherId = other.Id;
            db.LabOrders.Add(new LabOrder { ActivityId = labActivity.Id,
                DentistId = setup.DentistId,
                WorkName = "SYNTHETIC lab", OrderedOn = DateOnly.FromDateTime(DateTime.UtcNow),
                TotalFeeVnd = 100000, PaidVnd = 0 });
            await db.SaveChangesAsync();
        }
        using var owner = await Login("owner.demo");
        var payment = new DCMS.Api.Features.Finance.RecordTreatmentCashPayment(Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow), [new(otherId, 50000)], null);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsJsonAsync($"/api/dossiers/{dossierId}/payments", payment)).StatusCode);
        var deposit = new DCMS.Api.Features.Finance.CreateTreatmentPayOsOrder(Guid.NewGuid(), null, 30000, null);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsJsonAsync($"/api/dossiers/{dossierId}/payments/advances", deposit)).StatusCode);
        var afterDeposit = await owner.GetFromJsonAsync<DCMS.Api.Features.Finance.TreatmentPaymentContext>(
            $"/api/dossiers/{dossierId}/payments/context");
        Assert.Equal(30000, Assert.Single(afterDeposit!.LabFees).PaidVnd);
        Assert.Equal(0, afterDeposit.AdvanceReceivedVnd);
        Assert.Equal(0, Assert.Single(afterDeposit.LabFees).PriorityRemainingVnd);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsJsonAsync($"/api/dossiers/{dossierId}/payments", payment)).StatusCode);
    }

    [Fact]
    public async Task OwnSummaryExposesOnlyCommittedPlanForVerifiedSelfLink()
    {
        var setup = await Prepare();
        using var dentist = await Login("dentist.demo");
        var createdResponse = await dentist.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers",
            new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC own summary", null,
                null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        using var publicClient = await Login("guest.demo");
        var before = await publicClient.GetFromJsonAsync<OwnTreatmentSummaryItem[]>("/api/patients/me/treatment-summary");
        Assert.DoesNotContain(before!, x => x.DossierId == created.Id);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var accountId = await db.Users.Where(x => x.UserName == "guest.demo").Select(x => x.Id).SingleAsync();
            db.PatientAccountLinks.Add(new PatientAccountLink
            {
                AccountId = accountId, PatientId = setup.PatientId, Status = PatientLinkStatus.Verified,
                RequestCode = "SYNTHETIC-" + Guid.NewGuid().ToString("N")[..16],
                DeclaredFullName = "SYNTHETIC self", RequestedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
        var uncommitted = await publicClient.GetFromJsonAsync<OwnTreatmentSummaryItem[]>("/api/patients/me/treatment-summary");
        Assert.DoesNotContain(uncommitted!, x => x.DossierId == created.Id);
        var draftId = Guid.NewGuid();
        var draftPut = await dentist.PutAsJsonAsync($"/api/dossiers/{created.Id}/sheet-drafts/{draftId}",
            new SaveSheetDraftRequest("Plan", null, SheetRow(setup.ServiceId, setup.DentistId), 0, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, draftPut.StatusCode);
        var stillUncommitted = await publicClient.GetFromJsonAsync<OwnTreatmentSummaryItem[]>("/api/patients/me/treatment-summary");
        Assert.DoesNotContain(stillUncommitted!, x => x.DossierId == created.Id);
        var committedResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/sheet-drafts/{draftId}/confirm-plan",
            new ConfirmSheetPlanRequest(created.Version, 1, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, committedResponse.StatusCode);
        var official = await publicClient.GetFromJsonAsync<OwnTreatmentSummaryItem[]>("/api/patients/me/treatment-summary");
        var item = Assert.Single(official!, x => x.DossierId == created.Id);
        Assert.Equal(1, item.PlanVersion);
        Assert.Single(item.AgreedItems);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var activity = new ClinicalActivity
            {
                DossierId = created.Id,
                ServiceId = setup.ServiceId, ServiceNameSnapshot = "SYNTHETIC service performed",
                Region = "25", Unit = "răng", Quantity = 1, PerformerId = setup.DentistId,
                PerformedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow};
            db.ClinicalActivities.Add(activity);
            await db.SaveChangesAsync();
            var paymentId = Guid.NewGuid();
            var archivedPayment = System.Text.Json.JsonSerializer.Serialize(new
            {
                table = "TREATMENT_DOSSIER_PAYMENT", rowCount = 1,
                rows = new[] { new { Id = paymentId, DossierId = created.Id, PatientId = setup.PatientId,
                    PaidOn = DateOnly.FromDateTime(DateTime.UtcNow), Method = "Cash", TotalVnd = 50000L } }
            });
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AUDIT_LOG\" (\"StorageId\", \"AuditType\", \"Target\", \"PayloadJson\", \"At\") VALUES ({Guid.NewGuid()}, 'FinanceLegacyArchive', 'TREATMENT_DOSSIER_PAYMENT', {archivedPayment}::jsonb, {DateTimeOffset.UtcNow})");
            var paymentLines = System.Text.Json.JsonSerializer.Serialize(new
            {
                table = "TREATMENT_DOSSIER_PAYMENT_LINE", rowCount = 1,
                rows = new[] { new { PaymentId = paymentId, ActivityId = activity.Id, AmountVnd = 50000L } }
            });
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AUDIT_LOG\" (\"StorageId\", \"AuditType\", \"Target\", \"PayloadJson\", \"At\") VALUES ({Guid.NewGuid()}, 'FinanceLegacyArchive', 'TREATMENT_DOSSIER_PAYMENT_LINE', {paymentLines}::jsonb, {DateTimeOffset.UtcNow})");
            var advanceReceipts = System.Text.Json.JsonSerializer.Serialize(new
            {
                table = "PAYMENT_RECEIPT", rowCount = 2,
                rows = new[] {
                    new { Id = Guid.NewGuid(), DossierId = created.Id, PatientId = setup.PatientId, BranchId = setup.BranchId,
                        State = "Posted", Purpose = "Advance", Method = "Cash", AmountVnd = 100000L,
                        CreatedAt = DateTimeOffset.UtcNow, ConfirmedAt = DateTimeOffset.UtcNow },
                    new { Id = Guid.NewGuid(), DossierId = created.Id, PatientId = setup.PatientId, BranchId = setup.BranchId,
                        State = "Pending", Purpose = "Advance", Method = "BankTransfer", AmountVnd = 900000L,
                        CreatedAt = DateTimeOffset.UtcNow, ConfirmedAt = DateTimeOffset.UtcNow }
                }
            });
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AUDIT_LOG\" (\"StorageId\", \"AuditType\", \"Target\", \"PayloadJson\", \"At\") VALUES ({Guid.NewGuid()}, 'FinanceLegacyArchive', 'PAYMENT_RECEIPT', {advanceReceipts}::jsonb, {DateTimeOffset.UtcNow})");
        }
        var afterPost = await publicClient.GetFromJsonAsync<OwnTreatmentSummaryItem[]>("/api/patients/me/treatment-summary");
        Assert.Equal("SYNTHETIC service performed", Assert.Single(Assert.Single(afterPost!, x => x.DossierId == created.Id).PerformedItems).ServiceName);
        var recordedPayments = Assert.Single(afterPost!, x => x.DossierId == created.Id).Payments;
        Assert.Equal(2, recordedPayments.Count);
        Assert.Contains(recordedPayments, x => x.TotalVnd == 50000 && x.Source == "DossierPayment");
        using var owner = await Login("owner.demo");
        var ownerProfile = await owner.GetFromJsonAsync<CurrentUser>("/api/auth/me");
        Assert.Contains("ClinicOwner", ownerProfile!.EffectiveRoles);
        // Scoped to this test's own branch: the unscoped total also counts money recorded by the other tests of this class.
        var statisticsResponse = await owner.GetAsync($"/api/finance/statistics?branchId={setup.BranchId}");
        Assert.True(statisticsResponse.IsSuccessStatusCode, await statisticsResponse.Content.ReadAsStringAsync());
        var statistics = (await statisticsResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>());
        Assert.Equal(150000, statistics.GetProperty("actualReceivedVnd").GetInt64());
        Assert.Equal(100000, statistics.GetProperty("advanceReceivedVnd").GetInt64());
        Assert.Equal(HttpStatusCode.Forbidden, (await dentist.GetAsync("/api/finance/statistics")).StatusCode);
        var otherBranch = await owner.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/finance/statistics?branchId=9999");
        Assert.Equal(0, otherBranch.GetProperty("actualReceivedVnd").GetInt64());
        Assert.Equal(0, otherBranch.GetProperty("advanceReceivedVnd").GetInt64());
        Assert.Equal(0, otherBranch.GetProperty("serviceAmountVnd").GetInt64());
    }

    [Fact]
    public async Task CancelledDossierClearsRemainingReceivable()
    {
        var setup = await Prepare();
        using var dentist = await Login("dentist.demo");
        using var owner = await Login("owner.demo");
        var createdResponse = await dentist.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers",
            new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC cancel receivable", null,
                null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var committed = await AddPlanItem(dentist, created.Id, created.Version, setup.ServiceId, setup.DentistId, 300000);
        var before = await owner.GetFromJsonAsync<DCMS.Api.Features.Finance.FinanceStatisticsResult>(
            $"/api/finance/statistics?branchId={setup.BranchId}");
        Assert.Equal(300000, before!.RemainingReceivableVnd);
        Assert.Contains(before.OutstandingDossiers, x => x.DossierId == created.Id && x.DueVnd == 300000);
        var cancelResponse = await owner.PostAsJsonAsync($"/api/dossiers/{created.Id}/payments/cancel-dossier",
            new DCMS.Api.Features.Finance.CancelTreatmentDossier(Guid.NewGuid(), committed.Version, "Hủy phiếu"));
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var after = await owner.GetFromJsonAsync<DCMS.Api.Features.Finance.FinanceStatisticsResult>(
            $"/api/finance/statistics?branchId={setup.BranchId}");
        Assert.Equal(0, after!.RemainingReceivableVnd);
        Assert.DoesNotContain(after.OutstandingDossiers, x => x.DossierId == created.Id);
        var read = await owner.GetFromJsonAsync<TreatmentDossierDto>($"/api/dossiers/{created.Id}");
        Assert.Equal("Cancelled", read!.State);
    }
}

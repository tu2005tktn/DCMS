using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Api.Features.Finance;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Patients;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class FakeTreatmentPayOsHandler : HttpMessageHandler
{
    public bool Paid { get; set; }
    private long code;
    private long amount;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Method == HttpMethod.Post)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            code = body.RootElement.GetProperty("orderCode").GetInt64();
            amount = body.RootElement.GetProperty("amount").GetInt64();
            return Json(new { code = "00", data = new { orderCode = code, amount, paymentLinkId = "fake-link",
                qrCode = "000201-fake-test-qr", bin = "970422", accountNumber = "12345678", accountName = "TEST" } });
        }
        return Json(new { code = "00", data = new { orderCode = code, id = "fake-link", amount,
            amountPaid = Paid ? amount : 0, status = Paid ? "PAID" : "PENDING" } });
    }
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    { Content = JsonContent.Create(value) };
}

public sealed class TreatmentPayOsFixture : ApiFixture
{
    public FakeTreatmentPayOsHandler PayOs { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, settings) => settings.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PayOs:ClientId"] = "test-client", ["PayOs:ApiKey"] = "test-api-key",
            ["PayOs:ChecksumKey"] = "test-checksum"
        }));
        builder.ConfigureServices(services => services.AddHttpClient("treatment-payos")
            .ConfigurePrimaryHttpMessageHandler(() => PayOs));
    }
}

public sealed class TreatmentPayOsApiTests(TreatmentPayOsFixture fixture) : IClassFixture<TreatmentPayOsFixture>
{
    private async Task<HttpClient> Login(string name)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await response.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        return client;
    }

    [Fact]
    public async Task TransferAndAdvanceOnlyPostAfterPayOsConfirmsExactAmount()
    {
        using var assistant = await Login("assistant.demo");
        var patientResponse = await assistant.PostAsJsonAsync("/api/patients", new CreatePatientRequest(null,
            "SYNTHETIC PAYOS " + Guid.NewGuid().ToString("N"), new DateOnly(1990, 1, 1), "OTHER",
            null, null, null, null, null, null, false));
        Assert.Equal(HttpStatusCode.Created, patientResponse.StatusCode);
        var patientId = (await patientResponse.Content.ReadFromJsonAsync<PatientChange>())!.Patient.Id;
        int dentistId, branchId, serviceId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            dentistId = await (from employee in db.Employees join user in db.Users on employee.AccountId equals user.Id
                where user.UserName == "dentist.demo" select employee.Id).SingleAsync();
            var branch = new SchedulingBranch { Name = "SYNTHETIC PAYOS " + Guid.NewGuid().ToString("N") };
            db.Add(branch); await db.SaveChangesAsync(); branchId = branch.Id;
            var service = new SchedulingService { BranchId = branchId, Name = "Synthetic treatment", PublicDurationMinutes = 30 };
            await db.AddSchedulingDataAsync(new SchedulingMember { BranchId = branchId, EmployeeId = dentistId, CanDentist = true }, service);
            await db.SaveChangesAsync(); serviceId = service.Id;
        }
        using var dentist = await Login("dentist.demo");
        var created = await dentist.PostAsJsonAsync($"/api/patients/{patientId}/dossiers",
            new CreateDossierRequest(branchId, dentistId, "SYNTHETIC payos", null, null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dossierId = (await created.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier.Id;
        Guid activityId, labOrderId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var activity = new ClinicalActivity { DossierId = dossierId, PatientId = patientId, BranchId = branchId,
                ServiceId = serviceId, ServiceNameSnapshot = "Synthetic treatment", Unit = "lần", Quantity = 1,
                PriceSnapshotVnd = 100000, PerformerId = dentistId, PerformedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow, State = "Posted" };
            db.ClinicalActivities.Add(activity); await db.SaveChangesAsync(); activityId = activity.Id;
            var labOrder = new LabOrder { ActivityId = activityId, DossierId = dossierId,
                PatientId = patientId, BranchId = branchId, DentistId = dentistId,
                WorkName = "Synthetic lab", OrderedOn = DateOnly.FromDateTime(DateTime.UtcNow),
                TotalFeeVnd = 50000, PaidVnd = 0, CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow, CreatedBy = dentistId, UpdatedBy = dentistId };
            db.LabOrders.Add(labOrder);
            await db.SaveChangesAsync(); labOrderId = labOrder.Id;
        }
        using var owner = await Login("owner.demo");
        var basePath = $"/api/dossiers/{dossierId}/payments";
        var request = new CreateTreatmentPayOsOrder(Guid.NewGuid(), [new(activityId, 100000)], null, [new(labOrderId, 15000)]);
        var createdOrder = await owner.PostAsJsonAsync($"{basePath}/payos-orders", request);
        Assert.Equal(HttpStatusCode.OK, createdOrder.StatusCode);
        var state = (await createdOrder.Content.ReadFromJsonAsync<TreatmentPayOsOrderState>())!;
        Assert.NotEmpty(state.Order.QrCode);
        Assert.Equal(100000, Assert.Single((await owner.GetFromJsonAsync<TreatmentPaymentContext>($"{basePath}/context"))!.Lines).DueVnd);
        var pending = await owner.GetFromJsonAsync<TreatmentPayOsOrderState>($"{basePath}/payos-orders/{state.Order.Id}");
        Assert.Equal("PENDING", pending!.Status);
        fixture.PayOs.Paid = true;
        var paid = await owner.GetFromJsonAsync<TreatmentPayOsOrderState>($"{basePath}/payos-orders/{state.Order.Id}");
        Assert.Equal("PAID", paid!.Status);
        Assert.Equal(0, Assert.Single(paid.Context!.Lines).DueVnd);
        Assert.Equal(15000, Assert.Single(paid.Context.LabFees).PaidVnd);
        Assert.Equal(35000, Assert.Single(paid.Context.LabFees).DueVnd);
        Assert.Equal("PAID", (await owner.GetFromJsonAsync<TreatmentPayOsOrderState>($"{basePath}/payos-orders/{state.Order.Id}"))!.Status);
        fixture.PayOs.Paid = false;
        var deposit = await owner.PostAsJsonAsync($"{basePath}/advance-payos-orders",
            new CreateTreatmentPayOsOrder(Guid.NewGuid(), null, 50000, null));
        Assert.Equal(HttpStatusCode.OK, deposit.StatusCode);
        var depositOrder = (await deposit.Content.ReadFromJsonAsync<TreatmentPayOsOrderState>())!;
        Assert.Equal(0, depositOrder.Context!.AdvanceReceivedVnd);
        fixture.PayOs.Paid = true;
        using var anonymous = fixture.CreateClient();
        var webhookData = new { orderCode = depositOrder.Order.OrderCode, amount = 50000 };
        var signed = $"amount=50000&orderCode={depositOrder.Order.OrderCode}";
        var signature = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes("test-checksum"),
            Encoding.UTF8.GetBytes(signed)));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await anonymous.PostAsJsonAsync("/api/payments/payos/webhook", new { data = webhookData, signature = "invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await anonymous.PostAsJsonAsync("/api/payments/payos/webhook", new { data = webhookData, signature })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await anonymous.PostAsJsonAsync("/api/payments/payos/webhook", new { data = webhookData, signature })).StatusCode);
        var depositPaid = await owner.GetFromJsonAsync<TreatmentPayOsOrderState>($"{basePath}/advance-payos-orders/{depositOrder.Order.Id}");
        Assert.Equal(50000, depositPaid!.Context!.AdvanceReceivedVnd);
        Assert.Equal(15000, Assert.Single(depositPaid.Context.LabFees).PaidVnd);
        Assert.Equal(0, Assert.Single(depositPaid.Context.Lines).DueVnd);
        var totals = await owner.GetFromJsonAsync<FinanceStatisticsResult>($"/api/finance/statistics?branchId={branchId}");
        Assert.Equal(100000, totals!.ActualReceivedVnd);
        Assert.Equal(50000, totals.AdvanceReceivedVnd);
    }
}

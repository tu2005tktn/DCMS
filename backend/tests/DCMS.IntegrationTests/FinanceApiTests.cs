using System.Net;
using System.Net.Http.Json;
using DCMS.Application.Features.Finance;
using DCMS.Application.Features.Examinations;
using DCMS.Application.Features.Reception;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Finance;
using DCMS.Domain.Features.Settings;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace DCMS.IntegrationTests;

public sealed class FinanceApiTests(ReceptionFixture fixture) : IClassFixture<ReceptionFixture>
{
    readonly ReceptionApiTests reception = new(fixture);
    static async Task<T> Ok<T>(HttpResponseMessage r) { Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync()); return (await r.Content.ReadFromJsonAsync<T>())!; }
    static string Orders(Guid v) => $"/api/visits/{v}/investigations";
    static string Billing(Guid v, Guid o) => $"{Orders(v)}/{o}/billing";
    static string Bill(Guid v) => $"/api/finance/visits/{v}";
    async Task<(VisitResult V, InvestigationDto O, HttpClient Dentist, InvestigationServiceDto Service)> Prepare()
    {
        var s = await reception.Prepare(); using var a = await reception.Login(); var v = await reception.Check(a, s); var d = await reception.Login("dentist.demo"); v = (await Ok<ExaminationWorkspace>(await d.PostAsJsonAsync($"/api/visits/{v.VisitId}/start", new StartExamination(v.PatientId, v.VisitVersion, Guid.NewGuid())))).Visit;
        var p = await Ok<InvestigationPage>(await d.PostAsJsonAsync(Orders(v.VisitId), new CreateInvestigation(v.PatientId, v.VisitVersion, Guid.NewGuid(), "Lab", "SYNTHETIC công thức máu", null, "SYNTHETIC chỉ định", false)));
        var service = await Ok<InvestigationServiceDto>(await d.PostAsJsonAsync("/api/finance/services", new SaveInvestigationService(Guid.NewGuid(), v.BranchId, "SYNTHETIC công thức máu", "Lab", "Lần", 200000)));
        return (v with { VisitVersion = p.VisitVersion }, Assert.Single(p.Items), d, service);
    }
    QuoteInvestigation Quote(VisitResult v, InvestigationDto o, InvestigationServiceDto s) => new(v.PatientId, v.VisitVersion, o.Version, Guid.NewGuid(), "Clinic", s.Id, s.Version);
    async Task<InvestigationBillingDto> Perform(HttpClient d, VisitResult v, Guid id, InvestigationBillingDto b) => await Ok<InvestigationBillingDto>(await d.PostAsJsonAsync(Billing(v.VisitId, id) + "/perform", new PerformInvestigation(v.PatientId, b.VisitVersion, b.OrderVersion, Guid.NewGuid())));
    async Task<(VisitResult V, Guid Id, HttpClient Dentist, InvestigationBillingDto B)> Performed() { var (v, o, d, s) = await Prepare(); var b = await Ok<InvestigationBillingDto>(await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", Quote(v, o, s))); b = await Perform(d, v, o.Id, b); return (v, o.Id, d, b); }
    [Fact]
    public async Task QuoteThenPerformThenCashPartialAndTransferConfirmationAreDistinctAtomicSources()
    {
        var (v, o, d, s) = await Prepare(); using (d) using (var a = await reception.Login())
        {
            var quote = Quote(v, o, s); var b = await Ok<InvestigationBillingDto>(await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", quote)); Assert.Empty((await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!.Charges);
            var perform = new PerformInvestigation(v.PatientId, b.VisitVersion, b.OrderVersion, Guid.NewGuid()); b = await Ok<InvestigationBillingDto>(await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/perform", perform)); Assert.NotNull(b.PerformedAt);
            await Ok<InvestigationBillingDto>(await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/perform", perform));
            var bill = (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!; var charge = Assert.Single(bill.Charges); Assert.Equal(200000, bill.RemainingVnd); Assert.Equal("Ordered", Assert.Single((await d.GetFromJsonAsync<InvestigationPage>(Orders(v.VisitId)))!.Items).State);
            var cash = new CollectPayment(v.PatientId, bill.AccountVersion, Guid.NewGuid(), "Cash", "SYNTHETIC người nộp", [new(charge.Id, 50000)]);
            var p = await Ok<PaymentDto>(await a.PostAsJsonAsync(Bill(v.VisitId) + "/payments", cash)); Assert.Equal("Posted", p.State); Assert.NotNull(p.Number); Assert.Equal(p.Id, (await Ok<PaymentDto>(await a.PostAsJsonAsync(Bill(v.VisitId) + "/payments", cash))).Id);
            bill = (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!; Assert.Equal(50000, bill.PaidVnd); Assert.Equal(150000, bill.RemainingVnd);
            var transfer = await Ok<PaymentDto>(await a.PostAsJsonAsync(Bill(v.VisitId) + "/payments", new CollectPayment(v.PatientId, bill.AccountVersion, Guid.NewGuid(), "Transfer", "SYNTHETIC nộp", [new(charge.Id, 150000)], DeclaredReference: "BANK-ONE"))); Assert.Null(transfer.Number);
            bill = (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!; Assert.Equal(50000, bill.PaidVnd);
            var confirmed = await Ok<PaymentDto>(await a.PostAsJsonAsync($"/api/finance/payments/{transfer.Id}/confirm", new ConfirmPayment(transfer.Version, bill.AccountVersion, Guid.NewGuid(), "bank-one", "SYNTHETIC đã đối chiếu"))); Assert.Equal("Posted", confirmed.State); Assert.NotEqual(p.Number, confirmed.Number);
            bill = (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!; Assert.Equal(0, bill.RemainingVnd); Assert.Equal(200000, bill.PaidVnd);
            using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); Assert.Equal(1, await db.Set<FinanceCharge>().CountAsync(x => x.VisitId == v.VisitId)); Assert.Equal(2, await db.Set<PaymentReceipt>().CountAsync(x => x.VisitId == v.VisitId && x.State == "Posted"));
        }
    }
    [Theory]
    [InlineData("ExternalSelfPay")]
    [InlineData("ExistingResult")]
    public async Task NoClinicCollectionModesNeverCreateCharge(string mode) { var (v, o, d, s) = await Prepare(); using (d) { var q = Quote(v, o, s) with { Mode = mode, ServiceId = null, ExpectedPriceVersion = null }; var b = await Ok<InvestigationBillingDto>(await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", q)); Assert.Null(b.AmountVnd); Assert.DoesNotContain("perform", b.AllowedActions); Assert.Equal(HttpStatusCode.Conflict, (await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/perform", new PerformInvestigation(v.PatientId, b.VisitVersion, b.OrderVersion, Guid.NewGuid()))).StatusCode); Assert.Empty((await d.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!.Charges); } }
    [Fact]
    public async Task OutsourcingRecordsSeparateProviderPayableAndHidesCostsFromDentistAssistant()
    {
        var (v, o, d, s) = await Prepare(); using (d) using (var owner = await reception.Login("owner.demo")) using (var a = await reception.Login())
        {
            var provider = await Ok<ProviderDto>(await owner.PostAsJsonAsync("/api/finance/providers", new CreateProvider(Guid.NewGuid(), "P" + Guid.NewGuid().ToString("N"), "SYNTHETIC provider " + Guid.NewGuid())));
            var q = Quote(v, o, s) with { Mode = "OutsourcedClinic", ProviderId = provider.Id, ProviderVersion = provider.Version, ProviderUnitCostVnd = 70000 }; Assert.Equal(HttpStatusCode.Forbidden, (await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", q)).StatusCode);
            var b = await Ok<InvestigationBillingDto>(await owner.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", q)); Assert.Equal(70000, b.ProviderCostVnd); var hidden = (await d.GetFromJsonAsync<InvestigationBillingDto>(Billing(v.VisitId, o.Id)))!; Assert.Null(hidden.ProviderCostVnd); Assert.Null(hidden.ProviderName);
            await Perform(a, v, o.Id, b); var bill = (await owner.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!; Assert.Equal(70000, Assert.Single(bill.ProviderPayables!).AmountVnd); Assert.Equal(200000, bill.RemainingVnd); Assert.Null((await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!.ProviderPayables);
        }
    }
    [Theory][InlineData(0)][InlineData(101)] public async Task QuantityMustBeWithinServiceLimits(int quantity) { var (v, o, d, s) = await Prepare(); using (d) { Assert.Equal(HttpStatusCode.BadRequest, (await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", Quote(v, o, s) with { Quantity = quantity })).StatusCode); } }
    [Fact]
    public async Task StaleCatalogAndFreeOverrideRequireExplicitResolutionAndSnapshotNeverChanges()
    {
        var (v, o, d, s) = await Prepare(); using (d)
        {
            var changed = await Ok<InvestigationServiceDto>(await d.PutAsJsonAsync($"/api/finance/services/{s.Id}", new SaveInvestigationService(Guid.NewGuid(), v.BranchId, s.Name, "Lab", "Lần", 300000, s.Version)));
            Assert.Equal(HttpStatusCode.Conflict, (await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", Quote(v, o, s))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", Quote(v, o, changed) with { AgreedUnitPriceVnd = 0 })).StatusCode);
            var b = await Ok<InvestigationBillingDto>(await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", Quote(v, o, changed) with { AgreedUnitPriceVnd = 0, Evidence = "SYNTHETIC miễn phí đã duyệt" })); await Perform(d, v, o.Id, b);
            await Ok<InvestigationServiceDto>(await d.PutAsJsonAsync($"/api/finance/services/{s.Id}", new SaveInvestigationService(Guid.NewGuid(), v.BranchId, "SYNTHETIC đổi tên", "Lab", "Mẫu", 400000, changed.Version)));
            var charge = Assert.Single((await d.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!.Charges); Assert.Equal(0, charge.AmountVnd); Assert.Equal(s.Name, charge.Name); Assert.Equal("Lần", charge.Unit);
        }
    }
    [Fact] public async Task NullCatalogPriceIsNotFreeAndExplicitPriceNeedsEvidence() { var (v, o, d, s) = await Prepare(); using (d) { s = await Ok<InvestigationServiceDto>(await d.PutAsJsonAsync($"/api/finance/services/{s.Id}", new SaveInvestigationService(Guid.NewGuid(), v.BranchId, s.Name, "Lab", "Lần", null, s.Version))); Assert.Equal(HttpStatusCode.BadRequest, (await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", Quote(v, o, s))).StatusCode); Assert.Equal(HttpStatusCode.BadRequest, (await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", Quote(v, o, s) with { AgreedUnitPriceVnd = 100000 })).StatusCode); await Ok<InvestigationBillingDto>(await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", Quote(v, o, s) with { AgreedUnitPriceVnd = 100000, Evidence = "SYNTHETIC thỏa thuận" })); } }
    [Theory][InlineData(false)][InlineData(true)] public async Task ClinicalCancellationDoesNotCreateOrReverseCharges(bool performed) { var (v, o, d, s) = await Prepare(); using (d) { var b = await Ok<InvestigationBillingDto>(await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", Quote(v, o, s))); if (performed) b = await Perform(d, v, o.Id, b); await Ok<InvestigationPage>(await d.PostAsJsonAsync($"{Orders(v.VisitId)}/{o.Id}/cancel", new CancelInvestigation(v.PatientId, b.VisitVersion, b.OrderVersion, Guid.NewGuid(), "SYNTHETIC hủy"))); Assert.Equal(performed ? 200000 : 0, (await d.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!.TotalVnd); } }
    [Fact] public async Task ReadDoesNotCreateAccountSequenceOrCharge() { var (v, _, d, _) = await Prepare(); using (d) { for (var i = 0; i < 2; i++) Assert.Equal(0, (await d.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!.AccountVersion); using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); Assert.False(await db.Set<FinancialAccount>().AnyAsync(x => x.PatientId == v.PatientId)); Assert.False(await db.Set<BranchReceiptSequence>().AnyAsync(x => x.BranchId == v.BranchId)); } }
    [Fact]
    public async Task TwoPerformanceRequestsCannotDoubleChargeAndChangedKeyPayloadIsRejected()
    {
        var (v, o, d, s) = await Prepare(); using (d) using (var a = await reception.Login())
        {
            var q = Quote(v, o, s); var b = await Ok<InvestigationBillingDto>(await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", q)); Assert.Equal(HttpStatusCode.Conflict, (await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", q with { Quantity = 2 })).StatusCode);
            var p = new PerformInvestigation(v.PatientId, b.VisitVersion, b.OrderVersion, Guid.NewGuid()); var responses = await Task.WhenAll(d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/perform", p), a.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/perform", p with { RequestKey = Guid.NewGuid() })); Assert.Equal(1, responses.Count(x => x.IsSuccessStatusCode)); Assert.Contains(responses, x => x.StatusCode == HttpStatusCode.Conflict); Assert.Single((await d.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!.Charges);
        }
    }
    [Fact]
    public async Task PendingDoesNotReserveBalanceOverpaymentIsRejectedAndPostedReceiptCannotCancel()
    {
        var (v, _, d, _) = await Performed(); using (d) using (var a = await reception.Login())
        {
            var b = (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!; var charge = Assert.Single(b.Charges); var transfer = await Ok<PaymentDto>(await a.PostAsJsonAsync(Bill(v.VisitId) + "/payments", new CollectPayment(v.PatientId, b.AccountVersion, Guid.NewGuid(), "Transfer", "SYNTHETIC payer", [new(charge.Id, 200000)])));
            b = (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!; var cash = await Ok<PaymentDto>(await a.PostAsJsonAsync(Bill(v.VisitId) + "/payments", new CollectPayment(v.PatientId, b.AccountVersion, Guid.NewGuid(), "Cash", "SYNTHETIC payer", [new(charge.Id, 200000)])));
            b = (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!; Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync($"/api/finance/payments/{transfer.Id}/confirm", new ConfirmPayment(1, b.AccountVersion, Guid.NewGuid(), "SYNTHETIC-ref", "SYNTHETIC đối chiếu"))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync($"/api/finance/payments/{cash.Id}/cancel", new CancelPayment(1, Guid.NewGuid(), "SYNTHETIC"))).StatusCode);
            await Ok<PaymentDto>(await a.PostAsJsonAsync($"/api/finance/payments/{transfer.Id}/cancel", new CancelPayment(1, Guid.NewGuid(), "SYNTHETIC bị trùng, tiền mặt đã thu"))); Assert.Equal(200000, (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!.PaidVnd);
        }
    }
    [Fact]
    public async Task DuplicateBankReferenceAndStaleFinancialVersionDoNotDoubleCollect()
    {
        var (v, _, d, _) = await Performed(); using (d) using (var a = await reception.Login())
        {
            var b = (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!; var charge = Assert.Single(b.Charges); var c = new CollectPayment(v.PatientId, b.AccountVersion, Guid.NewGuid(), "Transfer", "SYNTHETIC", [new(charge.Id, 50000)]); var p1 = await Ok<PaymentDto>(await a.PostAsJsonAsync(Bill(v.VisitId) + "/payments", c)); Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync(Bill(v.VisitId) + "/payments", c with { RequestKey = Guid.NewGuid() })).StatusCode);
            b = (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!; var p2 = await Ok<PaymentDto>(await a.PostAsJsonAsync(Bill(v.VisitId) + "/payments", c with { ExpectedAccountVersion = b.AccountVersion, RequestKey = Guid.NewGuid() })); b = (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!;
            var confirm = new ConfirmPayment(1, b.AccountVersion, Guid.NewGuid(), "REF-" + Guid.NewGuid(), "SYNTHETIC đối chiếu"); await Ok<PaymentDto>(await a.PostAsJsonAsync($"/api/finance/payments/{p1.Id}/confirm", confirm)); b = (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!;
            Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync($"/api/finance/payments/{p2.Id}/confirm", confirm with { ExpectedAccountVersion = b.AccountVersion, RequestKey = Guid.NewGuid() })).StatusCode); Assert.Equal(50000, (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!.PaidVnd);
        }
    }
    [Fact]
    public async Task PermissionsBranchAndAssignedGroupAreEnforcedBeforeReplay()
    {
        var (v, o, d, s) = await Prepare(); using (d) using (var a = await reception.Login()) using (var admin = await reception.Login("admin.demo"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(Bill(v.VisitId))).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await a.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", Quote(v, o, s))).StatusCode);
            var q = Quote(v, o, s); await Ok<InvestigationBillingDto>(await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", q));
            using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var account = await db.Users.Where(x => x.UserName == "dentist.demo").Select(x => x.Id).SingleAsync(); var g = new AccessGroup { Code = "SYN-" + Guid.NewGuid().ToString("N"), Name = "SYNTHETIC", BaseRole = "Dentist" }; g.NormalizedCode=g.Code;g.NormalizedName=g.Code;db.Add(g); db.Add(new StaffAccessAssignment { AccountId = account, GroupId = g.Id, BranchIds = [v.BranchId] }); await db.SaveChangesAsync(); }
            try { Assert.Equal(HttpStatusCode.Forbidden, (await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", q)).StatusCode); } finally { using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); await db.Set<StaffAccessAssignment>().ExecuteDeleteAsync(); }
        }
    }
    [Fact] public async Task ClinicalActualChargeUsesSameLedgerAndRejectsUncommittedOutOfTransaction() { var (v, _, d, s) = await Prepare(); using (d) { using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var employee = await db.Employees.Join(db.Users, x => x.AccountId, x => x.Id, (e, u) => new { e, u }).Where(x => x.u.UserName == "dentist.demo").Select(x => x.e.Id).SingleAsync(); var row = new ClinicalActivity { VisitId = v.VisitId, PatientId = v.PatientId, BranchId = v.BranchId, ServiceId = s.Id, PerformerId = employee, Unit = "Lần", Quantity = 1, ChargingBasis = "Separate", PriceSnapshotVnd = 75000, PerformedAt = fixture.Clock.Now, CreatedAt = fixture.Clock.Now }; db.Add(row); await db.SaveChangesAsync(); var actor = await db.Employees.Where(x => x.Id == employee).Select(x => x.AccountId).SingleAsync(); var port = scope.ServiceProvider.GetRequiredService<IClinicalChargePort>(); Assert.False(await port.PostInTransactionAsync(v.PatientId, v.BranchId, row.Id, 1, 75000, actor, default)); await using (var tx = await db.Database.BeginTransactionAsync()) { await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(90403)"); Assert.True(await port.PostInTransactionAsync(v.PatientId, v.BranchId, row.Id, 1, 75000, actor, default)); row.State = "Posted"; await db.SaveChangesAsync(); await tx.CommitAsync(); } Assert.Equal(75000, Assert.Single((await d.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!.Charges).AmountVnd); } }
    [Fact]
    public async Task ReviewedQuotedServiceBlocksClosingUntilPerformedButUnpaidBillDoesNotBlockClosing()
    {
        var (v, o, d, s) = await Prepare(); using (d)
        {
            var b = await Ok<InvestigationBillingDto>(await d.PostAsJsonAsync(Billing(v.VisitId, o.Id) + "/quote", Quote(v, o, s)));
            var result = await Ok<InvestigationPage>(await d.PostAsJsonAsync($"{Orders(v.VisitId)}/{o.Id}/result", new ReceiveInvestigationResult(v.PatientId, b.VisitVersion, b.OrderVersion, Guid.NewGuid(), "SYNTHETIC kết quả"))); var ro = Assert.Single(result.Items);
            var reviewed = await Ok<InvestigationPage>(await d.PostAsJsonAsync($"{Orders(v.VisitId)}/{o.Id}/review", new ReviewInvestigation(v.PatientId, result.VisitVersion, ro.Version, Guid.NewGuid())));
            var content = new ExaminationContent("SYNTHETIC kiểm tra", new(), new(), new(), [], null, null, Advice:"SYNTHETIC chăm sóc và theo dõi",Disposition: "ConsultationOnly", Diagnoses: ["Chưa phát hiện bất thường"]);
            var exam = await Ok<ExaminationWorkspace>(await d.PutAsJsonAsync($"/api/visits/{v.VisitId}/examination", new SaveExamination(v.PatientId, reviewed.VisitVersion, Guid.NewGuid(), content, true)));
            Assert.Equal(HttpStatusCode.Conflict, (await d.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete", new CompleteExaminationVisit(v.PatientId, exam.Visit.VisitVersion, Guid.NewGuid(), true))).StatusCode);
            b = (await d.GetFromJsonAsync<InvestigationBillingDto>(Billing(v.VisitId, o.Id)))!; b = await Perform(d, v, o.Id, b);
            var closed = await Ok<ExaminationWorkspace>(await d.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete", new CompleteExaminationVisit(v.PatientId, b.VisitVersion, Guid.NewGuid(), true))); Assert.Equal("Completed", closed.Visit.Status); Assert.Equal(200000, (await d.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!.RemainingVnd);
        }
    }
    [Theory]
    [InlineData("Duplicate")]
    [InlineData("Negative")]
    [InlineData("TooMuch")]
    [InlineData("UnknownCharge")]
    [InlineData("WrongPatient")]
    public async Task InvalidAllocationsDoNotCreateReceiptOrChangeFinancialVersion(string kind)
    {
        var (v, _, d, _) = await Performed(); using (d) using (var a = await reception.Login())
        {
            var b = (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!; var id = Assert.Single(b.Charges).Id; IReadOnlyList<AllocationInput> allocations = kind switch { "Duplicate" => [new(id, 1), new(id, 1)], "Negative" => [new(id, -1)], "TooMuch" => [new(id, 200001)], "UnknownCharge" => [new(Guid.NewGuid(), 1)], _ => [new(id, 1)] };
            var response = await a.PostAsJsonAsync(Bill(v.VisitId) + "/payments", new CollectPayment(kind == "WrongPatient" ? v.PatientId + 999 : v.PatientId, b.AccountVersion, Guid.NewGuid(), "Cash", "SYNTHETIC", allocations)); Assert.False(response.IsSuccessStatusCode); var after = (await a.GetFromJsonAsync<BillDto>(Bill(v.VisitId)))!; Assert.Equal(b.AccountVersion, after.AccountVersion); Assert.Empty(after.Payments); Assert.Equal(200000, after.RemainingVnd);
        }
    }
    [Fact]
    public async Task GrantedReadCannotEscapeAssignedBranchScope()
    {
        var (v, _, d, _) = await Prepare(); using (d)
        {
            using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var account = await db.Users.Where(x => x.UserName == "dentist.demo").Select(x => x.Id).SingleAsync(); var g = new AccessGroup { Code = "S-" + Guid.NewGuid().ToString("N"), Name = "SYNTHETIC scoped", BaseRole = "Dentist" }; g.NormalizedCode=g.Code;g.NormalizedName=g.Code;g.Grants.Add(new() { GroupId = g.Id, Permission = "finance.read" }); g.NormalizedCode=g.Code;g.NormalizedName=g.Code;db.Add(g); db.Add(new StaffAccessAssignment { AccountId = account, GroupId = g.Id, BranchIds = [] }); await db.SaveChangesAsync(); }
            try { Assert.Equal(HttpStatusCode.Forbidden, (await d.GetAsync(Bill(v.VisitId))).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await d.GetAsync($"/api/finance/queue?branchId={v.BranchId}")).StatusCode); } finally { using var scope = fixture.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<DcmsDbContext>().Set<StaffAccessAssignment>().ExecuteDeleteAsync(); }
        }
    }

}

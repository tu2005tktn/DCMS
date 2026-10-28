using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Finance;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Finance;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Examinations;
using DCMS.Infrastructure.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.Settings.SettingsAccess;

namespace DCMS.Infrastructure.Features.Finance;

public sealed class FinanceService(DcmsDbContext db, ISettingsAccess access, TimeProvider clock) : IFinanceService
{
    public const long MaxAmount = 1_000_000_000_000;
    static readonly string[] Kinds = ["XRay", "Imaging", "Lab", "Pathology", "Other"];
    static BusinessException Invalid(string message, int status = 400) => new(status, new SystemMessage("FINANCE_VALIDATION", message));
    static string Text(string? s, int max) { if (string.IsNullOrWhiteSpace(s) || s.Trim().Length > max) throw Error("MSG04"); return s.Trim(); }
    static void Money(long n) { if (n < 0 || n > MaxAmount) throw Invalid("Số tiền vượt giới hạn cho phép."); }
    async Task<Visit> Visit(int actor, Guid id, string grant, CancellationToken ct) { var v = await db.Set<Visit>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404); await access.OperationalAsync(actor, grant, v.BranchId, ct); return v; }
    async Task Own(int actor, Visit v, CancellationToken ct) { var s = await access.ResolveAsync(actor, ct); if (!s.Owner && s.Roles.Contains("Dentist") && !await db.Employees.AnyAsync(x => x.Id == v.DentistId && x.AccountId == actor, ct)) throw Error("MSG06", 403); }
    async Task<T> Run<T>(int actor, Guid key, string action, string target, object payload, int branch, Guid? visit, Func<Task<T>> mutate, CancellationToken ct)
    {
        if (key == Guid.Empty) throw Error("MSG04");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})", ct);
        var hash = ExaminationService.Hash(new { action, target, payload });
        var receipt = await db.Set<FinanceCommandReceipt>().SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == key, ct);
        if (receipt != null) { if (receipt.Fingerprint != hash) throw Error("MSG111", 409); await tx.CommitAsync(ct); return JsonSerializer.Deserialize<T>(receipt.ResultJson, ExaminationService.Json)!; }
        var result = await mutate();
        db.Add(new FinanceAudit { ActorId = actor, BranchId = branch, VisitId = visit, Action = action, Target = target, PayloadJson = JsonSerializer.Serialize(payload, ExaminationService.Json), At = clock.GetUtcNow() });
        db.Add(new FinanceCommandReceipt { ActorId = actor, RequestKey = key, Fingerprint = hash, ResultJson = JsonSerializer.Serialize(result, ExaminationService.Json) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    }
    public async Task<FinanceCapabilities> CapabilitiesAsync(int actor, CancellationToken ct) { var s = await access.ResolveAsync(actor, ct); return new(s.Permissions, await db.Set<SchedulingBranch>().Where(x => s.BranchIds.Contains(x.Id) && x.Active).Select(x => new ScopeBranch(x.Id, x.Name)).ToArrayAsync(ct)); }
    static InvestigationServiceDto ServiceDto(SchedulingService x) => new(x.Id, x.BranchId, x.Name, x.InvestigationKind!, x.BillingUnit, x.ReferenceUnitPriceVnd, x.PriceVersion, x.Active);
    public async Task<InvestigationServiceDto[]> ServicesAsync(int actor, int branchId, bool includeInactive, CancellationToken ct) { var s = await access.ResolveAsync(actor, ct); if (!s.BranchIds.Contains(branchId) || !s.Permissions.Any(x => x is "services.read" or "finance.read")) throw Error("MSG06", 403); return (await db.Set<SchedulingService>().Where(x => x.BranchId == branchId && x.IsInvestigationService && (includeInactive || x.Active)).OrderBy(x => x.Name).ToArrayAsync(ct)).Select(ServiceDto).ToArray(); }
    public async Task<InvestigationServiceDto> SaveServiceAsync(int actor, int? id, SaveInvestigationService c, CancellationToken ct)
    {
        await access.OperationalAsync(actor, "services.manage", c.BranchId, ct);
        return await Run(actor, c.RequestKey, "SaveService", id?.ToString() ?? "new", c, c.BranchId, null, async () =>
        {
            await access.OperationalAsync(actor, "services.manage", c.BranchId, ct);
            var name = Text(c.Name, 160); var unit = Text(c.Unit, 40); if (!Kinds.Contains(c.Kind)) throw Error("MSG04"); if (c.PriceVnd.HasValue) Money(c.PriceVnd.Value);
            SchedulingService row;
            if (id.HasValue) { await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({(long)id.Value + 905000L})", ct); row = await db.Set<SchedulingService>().SingleOrDefaultAsync(x => x.Id == id && x.BranchId == c.BranchId && x.IsInvestigationService, ct) ?? throw Error("MSG113", 404); if (row.PriceVersion != c.ExpectedVersion) throw Error("MSG111", 409); row.PriceVersion++; }
            else { if (c.ExpectedVersion != 0) throw Error("MSG111", 409); row = new() { BranchId = c.BranchId, IsInvestigationService = true, PublicBookable = false }; db.Add(row); }
            if (await db.Set<SchedulingService>().AnyAsync(x => x.BranchId == c.BranchId && x.Id != row.Id && x.Name.ToUpper() == name.ToUpper(), ct)) throw Invalid("Tên dịch vụ đã tồn tại.", 409);
            row.Name = name; row.InvestigationKind = c.Kind; row.BillingUnit = unit; row.ReferenceUnitPriceVnd = c.PriceVnd; row.Active = c.Active;
            await db.SaveChangesAsync(ct); return ServiceDto(row);
        }, ct);
    }
    public async Task<ProviderDto[]> ProvidersAsync(int actor, CancellationToken ct) { await access.RequireAsync(actor, "finance.costs.read", ct); return await db.Set<Provider>().Where(x => x.Active && x.Kind == "ClinicalFacility").OrderBy(x => x.Name).Select(x => new ProviderDto(x.Id, x.Code, x.Name, x.Version)).ToArrayAsync(ct); }
    public async Task<ProviderDto> CreateProviderAsync(int actor, CreateProvider c, CancellationToken ct) { await access.RequireAsync(actor, "finance.costs.manage", ct); return await Run(actor, c.RequestKey, "CreateProvider", "new", c, 0, null, async () => { await access.RequireAsync(actor, "finance.costs.manage", ct); var code = Text(c.Code, 40); var name = Text(c.Name, 160); if (await db.Set<Provider>().AnyAsync(x => x.NormalizedCode == code.ToUpper() || x.NormalizedName == name.ToUpper(), ct)) throw Invalid("Đơn vị thực hiện đã tồn tại.", 409); var row = new Provider { Code = code, Name = name, NormalizedCode = code.ToUpperInvariant(), NormalizedName = name.ToUpperInvariant() }; db.Add(row); return new ProviderDto(row.Id, row.Code, row.Name, row.Version); }, ct); }
    async Task<InvestigationOrder> Order(Guid visit, Guid id, CancellationToken ct) => await db.Set<InvestigationOrder>().SingleOrDefaultAsync(x => x.Id == id && x.VisitId == visit, ct) ?? throw Error("MSG113", 404);
    async Task<InvestigationBillingDto> BillingDto(int actor, Visit v, InvestigationOrder o, CancellationToken ct)
    {
        var s = await access.ResolveAsync(actor, ct); var cost = s.Permissions.Contains("finance.costs.read"); var own = s.Owner || !s.Roles.Contains("Dentist") || await db.Employees.AnyAsync(x => x.Id == v.DentistId && x.AccountId == actor, ct);
        var actions = new List<string>(); if (v.Status == "InProgress" && o.State != "Cancelled" && o.PerformedAt == null && own) { if (s.Permissions.Contains("finance.quotes.manage")) actions.Add("quote"); if (s.Permissions.Contains("investigations.perform") && o.BillingMode is "Clinic" or "OutsourcedClinic") actions.Add("perform"); }
        var charge = await db.Set<FinanceCharge>().Where(x => x.InvestigationId == o.Id).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        return new(o.Id, v.Id, v.Version, o.Version, o.BillingMode, o.ServiceId, o.ServiceNameSnapshot, o.UnitSnapshot, o.Quantity, o.UnitPriceSnapshotVnd, o.UnitPriceSnapshotVnd * o.Quantity, o.QuoteEvidence, o.PerformedAt, charge, cost ? o.ProviderId : null, cost ? o.ProviderNameSnapshot : null, cost ? o.ProviderUnitCostSnapshotVnd * o.Quantity : null, actions.ToArray());
    }
    public async Task<InvestigationBillingDto> BillingAsync(int actor, Guid visitId, Guid id, CancellationToken ct) { var v = await Visit(actor, visitId, "finance.read", ct); return await BillingDto(actor, v, await Order(visitId, id, ct), ct); }
    public async Task<InvestigationBillingDto> QuoteAsync(int actor, Guid visitId, Guid id, QuoteInvestigation c, CancellationToken ct)
    {
        var v = await Visit(actor, visitId, "finance.quotes.manage", ct); await Own(actor, v, ct); if (c.Mode == "OutsourcedClinic") await access.RequireAsync(actor, "finance.costs.manage", ct);
        // Return a fresh, permission-filtered projection on replay as well.
        await Run(actor, c.RequestKey, "Quote", id.ToString(), c, v.BranchId, v.Id, async () =>
        {
            await access.OperationalAsync(actor, "finance.quotes.manage", v.BranchId, ct); await db.Entry(v).ReloadAsync(ct); await Own(actor, v, ct); var o = await Order(visitId, id, ct);
            ValidateOrder(v, o, c.PatientId, c.ExpectedVisitVersion, c.ExpectedVersion);
            if (!new[] { "Clinic", "OutsourcedClinic", "ExternalSelfPay", "ExistingResult" }.Contains(c.Mode)) throw Error("MSG04");
            o.BillingMode = c.Mode; o.ServiceId = null; o.ServiceNameSnapshot = null; o.UnitSnapshot = null; o.UnitPriceSnapshotVnd = null; o.PriceVersionSnapshot = null; o.ProviderId = null; o.ProviderNameSnapshot = null; o.ProviderUnitCostSnapshotVnd = null; o.QuoteEvidence = null; o.Quantity = 1;
            if (c.Mode is "Clinic" or "OutsourcedClinic")
            {
                if (c.ServiceId == null || c.Quantity < 1 || c.Quantity > 100) throw Error("MSG04");
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({(long)c.ServiceId.Value + 905000L})", ct);
                var service = await db.Set<SchedulingService>().SingleOrDefaultAsync(x => x.Id == c.ServiceId && x.BranchId == v.BranchId && x.Active && x.IsInvestigationService, ct) ?? throw Invalid("Chọn dịch vụ cận lâm sàng đang hoạt động.");
                if (service.InvestigationKind != o.Kind) throw Invalid("Nhóm dịch vụ không khớp chỉ định."); if (service.PriceVersion != c.ExpectedPriceVersion) throw Error("MSG111", 409);
                var price = c.AgreedUnitPriceVnd ?? service.ReferenceUnitPriceVnd ?? throw Invalid("Dịch vụ chưa có giá. Hãy ghi giá thỏa thuận và căn cứ."); Money(price); Money(checked(price * c.Quantity));
                if (price == 0 || service.ReferenceUnitPriceVnd != price) o.QuoteEvidence = Text(c.Evidence, 1000); else if (c.Evidence != null) o.QuoteEvidence = Text(c.Evidence, 1000);
                o.ServiceId = service.Id; o.ServiceNameSnapshot = service.Name; o.UnitSnapshot = service.BillingUnit; o.Quantity = c.Quantity; o.UnitPriceSnapshotVnd = price; o.PriceVersionSnapshot = service.PriceVersion;
                if (c.Mode == "OutsourcedClinic")
                {
                    await access.RequireAsync(actor, "finance.costs.manage", ct);
                    var p = await db.Set<Provider>().SingleOrDefaultAsync(x => x.Id == c.ProviderId && x.Active && x.Kind == "ClinicalFacility", ct) ?? throw Invalid("Chọn đơn vị thực hiện.");
                    if (p.Version != c.ProviderVersion) throw Error("MSG111", 409); if (c.ProviderUnitCostVnd == null) throw Invalid("Nhập chi phí phải trả đơn vị thực hiện."); Money(c.ProviderUnitCostVnd.Value); Money(checked(c.ProviderUnitCostVnd.Value * c.Quantity)); o.ProviderId = p.Id; o.ProviderNameSnapshot = p.Name; o.ProviderUnitCostSnapshotVnd = c.ProviderUnitCostVnd;
                }
            }
            else if (c.ServiceId != null || c.AgreedUnitPriceVnd != null || c.ProviderId != null || c.ProviderUnitCostVnd != null) throw Invalid("Trường hợp không thu tại phòng khám không được kèm khoản phí.");
            o.QuotedBy = actor; o.QuotedAt = clock.GetUtcNow(); o.Version++; v.Version++; return true;
        }, ct); return await BillingAsync(actor, visitId, id, ct);
    }
    static void ValidateOrder(Visit v, InvestigationOrder o, int patient, int visitVersion, int orderVersion) { if (v.PatientId != patient) throw Error("MSG113", 404); if (v.Version != visitVersion || o.Version != orderVersion) throw Error("MSG111", 409); if (v.Status != "InProgress" || o.State == "Cancelled" || o.PerformedAt != null) throw Error("MSG42", 409); }
    public async Task<InvestigationBillingDto> PerformAsync(int actor, Guid visitId, Guid id, PerformInvestigation c, CancellationToken ct)
    {
        var v = await Visit(actor, visitId, "investigations.perform", ct); await Own(actor, v, ct);
        await Run(actor, c.RequestKey, "Perform", id.ToString(), c, v.BranchId, v.Id, async () =>
        {
            await access.OperationalAsync(actor, "investigations.perform", v.BranchId, ct); await db.Entry(v).ReloadAsync(ct); await Own(actor, v, ct); var o = await Order(visitId, id, ct); ValidateOrder(v, o, c.PatientId, c.ExpectedVisitVersion, c.ExpectedVersion);
            if (o.BillingMode is not ("Clinic" or "OutsourcedClinic") || o.ServiceId == null || o.UnitPriceSnapshotVnd == null) throw Invalid("Lập phí dịch vụ trước khi xác nhận thực hiện.", 409);
            var charge = new FinanceCharge { VisitId = v.Id, PatientId = v.PatientId, BranchId = v.BranchId, SourceKind = "Investigation", SourceId = o.Id, InvestigationId = o.Id, ServiceId = o.ServiceId.Value, Name = o.ServiceNameSnapshot!, Unit = o.UnitSnapshot!, Quantity = o.Quantity, UnitPriceVnd = o.UnitPriceSnapshotVnd.Value, AmountVnd = checked(o.UnitPriceSnapshotVnd.Value * o.Quantity), PriceVersion = o.PriceVersionSnapshot!.Value, PostedBy = actor, PostedAt = clock.GetUtcNow() };
            await AddCharge(charge, ct);
            if (o.BillingMode == "OutsourcedClinic") db.Add(new ProviderPayable { ChargeId = charge.Id, ProviderId = o.ProviderId!.Value, ProviderName = o.ProviderNameSnapshot!, BranchId = v.BranchId, AmountVnd = checked(o.ProviderUnitCostSnapshotVnd!.Value * o.Quantity), RecordedAt = clock.GetUtcNow() });
            o.PerformedBy = actor; o.PerformedAt = clock.GetUtcNow(); o.Version++; v.Version++; return true;
        }, ct); return await BillingAsync(actor, visitId, id, ct);
    }
    internal async Task AddCharge(FinanceCharge charge, CancellationToken ct) { Money(charge.AmountVnd); var total = await db.Set<FinanceCharge>().Where(x => x.PatientId == charge.PatientId && x.BranchId == charge.BranchId).SumAsync(x => x.AmountVnd, ct); if (total > 9_000_000_000_000 - charge.AmountVnd) throw Invalid("Tổng phí vượt giới hạn.", 409); db.Add(charge); await Touch(charge.PatientId, charge.BranchId, ct); }
    async Task<FinancialAccount> Account(int patient, int branch, CancellationToken ct) { var a = await db.Set<FinancialAccount>().SingleOrDefaultAsync(x => x.PatientId == patient && x.BranchId == branch, ct); if (a != null) return a; a = new() { PatientId = patient, BranchId = branch, Version = 0 }; db.Add(a); return a; }
    async Task Touch(int patient, int branch, CancellationToken ct) { var a = await Account(patient, branch, ct); a.Version++; }
    async Task<int> Version(int patient, int branch, CancellationToken ct) => await db.Set<FinancialAccount>().Where(x => x.PatientId == patient && x.BranchId == branch).Select(x => x.Version).SingleOrDefaultAsync(ct);
    async Task<ChargeDto[]> Charges(Guid visit, CancellationToken ct) { var rows = await db.Set<FinanceCharge>().Where(x => x.VisitId == visit).OrderBy(x => x.PostedAt).ToArrayAsync(ct); var paid = await (from a in db.Set<PaymentAllocation>() join p in db.Set<PaymentReceipt>() on a.PaymentId equals p.Id where p.VisitId == visit && p.State == "Posted" group a by a.ChargeId into g select new { Id = g.Key, Amount = g.Sum(x => x.AmountVnd) }).ToDictionaryAsync(x => x.Id, x => x.Amount, ct); return rows.Select(x => new ChargeDto(x.Id, x.VisitId, x.SourceKind, x.SourceId, x.Name, x.Unit, x.Quantity, x.UnitPriceVnd, x.AmountVnd, paid.GetValueOrDefault(x.Id), x.AmountVnd - paid.GetValueOrDefault(x.Id), x.PostedAt)).ToArray(); }
    async Task<PaymentDto> Payment(PaymentReceipt p,CancellationToken ct)
    {
        var names=await db.Users.Where(x=>x.Id==p.CreatedBy||x.Id==p.ConfirmedBy).Select(x=>new{x.Id,x.FullName}).ToDictionaryAsync(x=>x.Id,x=>x.FullName,ct);
        return new(p.Id,p.Number,p.Method,p.State,p.AmountVnd,p.Payer,p.Note,p.BankReference,p.Version,p.CreatedBy,p.CreatedAt,p.ConfirmedBy,p.ConfirmedAt,p.Allocations.Select(x=>new AllocationInput(x.ChargeId,x.AmountVnd)).ToArray(),names.GetValueOrDefault(p.CreatedBy,""),p.ConfirmedBy.HasValue?names.GetValueOrDefault(p.ConfirmedBy.Value):null,p.DeclaredReference,p.ConfirmationNote,p.CancellationReason);
    }
    async Task<PaymentDto[]> MapPayments(PaymentReceipt[] rows,CancellationToken ct){var result=new List<PaymentDto>();foreach(var p in rows)result.Add(await Payment(p,ct));return result.ToArray();}
    public async Task<BillDto> BillAsync(int actor, Guid visitId, CancellationToken ct)
    {
        var v = await Visit(actor, visitId, "finance.read", ct); var caps = await access.ResolveAsync(actor, ct); var charges = await Charges(visitId, ct); var payments = await db.Set<PaymentReceipt>().Include(x => x.Allocations).Where(x => x.VisitId == visitId).OrderByDescending(x => x.CreatedAt).ToArrayAsync(ct); var patient = await db.Set<PatientProfile>().SingleAsync(x => x.Id == v.PatientId, ct); var branch = await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == v.BranchId, ct); var json = await db.Set<ClinicSettingsVersion>().OrderByDescending(x => x.Version).Select(x => x.ContentJson).FirstOrDefaultAsync(ct); var settings = json == null ? DCMS.Application.Features.SystemSettings.GeneralSettings.Default : JsonSerializer.Deserialize<DCMS.Application.Features.SystemSettings.GeneralSettings>(json, DCMS.Infrastructure.Features.SystemSettings.ClinicSettingsStore.Json)!;
        var ids = charges.Select(x => x.Id).ToArray(); var costs = caps.Permissions.Contains("finance.costs.read") ? await db.Set<ProviderPayable>().Where(x => ids.Contains(x.ChargeId)).Select(x => new PayableDto(x.Id, x.ChargeId, x.ProviderName, x.AmountVnd)).ToArrayAsync(ct) : null;
        return new(v.Id, v.PatientId, patient.FullName, v.BranchId, branch.Name, await Version(v.PatientId, v.BranchId, ct), charges.Sum(x => x.AmountVnd), charges.Sum(x => x.PaidVnd), charges.Sum(x => x.RemainingVnd), charges, (await MapPayments(payments,ct)), costs, settings.ReceiptPrint.DefaultPayerToPatient ? patient.FullName : null, caps.Permissions.Where(x => x.StartsWith("finance.payments.")).ToArray(), await db.Set<InvestigationOrder>().Where(x => x.VisitId == visitId).OrderBy(x => x.CreatedAt).Select(x => new BillInvestigation(x.Id, x.Name, x.Kind, x.State, x.BillingMode, x.PerformedAt)).ToArrayAsync(ct));
    }
    public async Task<QueueBill[]> QueueAsync(int actor,int branchId,CancellationToken ct)
    {
        await access.OperationalAsync(actor,"finance.read",branchId,ct);
        var pendingOrders=from o in db.Set<InvestigationOrder>() join v in db.Set<Visit>() on o.VisitId equals v.Id
            where v.BranchId==branchId && v.Status=="InProgress" && o.State!="Cancelled" && o.PerformedAt==null
                && (o.BillingMode==null || o.BillingMode=="Clinic" || o.BillingMode=="OutsourcedClinic") select o.VisitId;
        var feeVisits=db.Set<ExaminationFee>().Where(x=>x.State=="Pending").Join(db.Visits.Where(x=>x.BranchId==branchId),x=>x.VisitId,x=>x.Id,(f,v)=>v.Id);
        var visits=await db.Set<FinanceCharge>().Where(x=>x.BranchId==branchId).Select(x=>x.VisitId).Union(pendingOrders).Union(feeVisits).ToArrayAsync(ct);
        var list=new List<QueueBill>();
        foreach(var id in visits){
            var b=await BillAsync(actor,id,ct);var pending=b.Payments.Count(x=>x.State=="Pending");
            var services=await pendingOrders.CountAsync(x=>x==id,ct);
            var pendingFee=await feeVisits.CountAsync(x=>x==id,ct);
            if(b.RemainingVnd>0||pending>0||services>0||pendingFee>0)list.Add(new(id,b.PatientId,b.PatientName,b.BranchId,b.TotalVnd,b.RemainingVnd,pending,services,pendingFee));
        }
        return list.ToArray();
    }
    async Task ValidateAllocations(Guid visit, IReadOnlyList<AllocationInput> items, CancellationToken ct) { if (items == null || items.Count == 0 || items.Count > 100 || items.Select(x => x.ChargeId).Distinct().Count() != items.Count) throw Error("MSG04"); var charges = await Charges(visit, ct); foreach (var i in items) { Money(i.AmountVnd); if (i.AmountVnd == 0 || !charges.Any(x => x.Id == i.ChargeId && x.RemainingVnd >= i.AmountVnd)) throw Invalid("Khoản phân bổ vượt số còn phải thu hoặc không thuộc lượt này.", 409); } Money(items.Sum(x => x.AmountVnd)); }
    async Task Post(PaymentReceipt p, int actor, CancellationToken ct) { var seq = await db.Set<BranchReceiptSequence>().SingleOrDefaultAsync(x => x.BranchId == p.BranchId, ct); if (seq == null) { seq = new() { BranchId = p.BranchId }; db.Add(seq); } p.Number = $"PT-{p.BranchId}-{seq.NextNumber++:D8}"; p.State = "Posted"; p.ConfirmedBy = actor; p.ConfirmedAt = clock.GetUtcNow(); }
    public async Task<PaymentDto> CollectAsync(int actor, Guid visitId, CollectPayment c, CancellationToken ct)
    {
        var v = await Visit(actor, visitId, "finance.payments.create", ct); return await Run(actor, c.RequestKey, "Collect", visitId.ToString(), c, v.BranchId, v.Id, async () =>
        {
            await access.OperationalAsync(actor, "finance.payments.create", v.BranchId, ct); if (v.PatientId != c.PatientId) throw Error("MSG113", 404); if (await Version(v.PatientId, v.BranchId, ct) != c.ExpectedAccountVersion) throw Error("MSG111", 409); if (c.Method is not ("Cash" or "Transfer")) throw Error("MSG04"); if (c.Note?.Length > 1000 || c.DeclaredReference?.Length > 100) throw Error("MSG04"); await ValidateAllocations(v.Id, c.Allocations, ct);
            var p = new PaymentReceipt { VisitId = v.Id, PatientId = v.PatientId, BranchId = v.BranchId, PatientName = await db.Set<PatientProfile>().Where(x => x.Id == v.PatientId).Select(x => x.FullName).SingleAsync(ct), BranchName = await db.Set<SchedulingBranch>().Where(x => x.Id == v.BranchId).Select(x => x.Name).SingleAsync(ct), Method = c.Method, Payer = Text(c.Payer, 160), Note = c.Note?.Trim(), DeclaredReference = string.IsNullOrWhiteSpace(c.DeclaredReference) ? null : c.DeclaredReference.Trim(), AmountVnd = c.Allocations.Sum(x => x.AmountVnd), CreatedBy = actor, CreatedAt = clock.GetUtcNow() };
            p.Allocations = c.Allocations.Select(x => new PaymentAllocation { PaymentId = p.Id, ChargeId = x.ChargeId, AmountVnd = x.AmountVnd }).ToList(); db.Add(p); if (c.Method == "Cash") await Post(p, actor, ct); await Touch(v.PatientId, v.BranchId, ct); return await Payment(p,ct);
        }, ct);
    }
    public async Task<PaymentDto> ConfirmAsync(int actor, Guid id, ConfirmPayment c, CancellationToken ct)
    {
        var p = await db.Set<PaymentReceipt>().Include(x => x.Allocations).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404); await access.OperationalAsync(actor, "finance.payments.confirm", p.BranchId, ct); return await Run(actor, c.RequestKey, "ConfirmPayment", id.ToString(), c, p.BranchId, p.VisitId, async () =>
        {
            await access.OperationalAsync(actor, "finance.payments.confirm", p.BranchId, ct); await db.Entry(p).ReloadAsync(ct); if (p.Version != c.ExpectedVersion || await Version(p.PatientId, p.BranchId, ct) != c.ExpectedAccountVersion) throw Error("MSG111", 409); if (p.State != "Pending" || p.Method != "Transfer") throw Error("MSG42", 409);
            var reference = Text(c.BankReference, 100); var normalized = reference.ToUpperInvariant(); if (p.DeclaredReference != null && p.DeclaredReference.ToUpperInvariant() != normalized) throw Invalid("Mã giao dịch không khớp thông tin chuyển khoản đã khai báo.", 409); if (await db.Set<PaymentReceipt>().AnyAsync(x => x.BranchId == p.BranchId && x.NormalizedBankReference == normalized, ct)) throw Invalid("Mã giao dịch đã được thu tiền.", 409);
            p.ConfirmationNote = Text(c.Note, 1000); await ValidateAllocations(p.VisitId, p.Allocations.Select(x => new AllocationInput(x.ChargeId, x.AmountVnd)).ToArray(), ct); p.BankReference = reference; p.NormalizedBankReference = normalized; await Post(p, actor, ct); p.Version++; await Touch(p.PatientId, p.BranchId, ct); return await Payment(p,ct);
        }, ct);
    }
    public async Task<PaymentDto> CancelAsync(int actor, Guid id, CancelPayment c, CancellationToken ct) { var p = await db.Set<PaymentReceipt>().Include(x => x.Allocations).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404); await access.OperationalAsync(actor, "finance.payments.create", p.BranchId, ct); return await Run(actor, c.RequestKey, "CancelPayment", id.ToString(), c, p.BranchId, p.VisitId, async () => { await access.OperationalAsync(actor, "finance.payments.create", p.BranchId, ct); await db.Entry(p).ReloadAsync(ct); if (p.Version != c.ExpectedVersion) throw Error("MSG111", 409); if (p.State != "Pending") throw Error("MSG42", 409); p.CancellationReason = Text(c.Reason, 1000); p.State = "Cancelled"; p.Version++; await Touch(p.PatientId, p.BranchId, ct); return await Payment(p,ct); }, ct); }

    async Task<(int Version, DCMS.Application.Features.SystemSettings.ReceiptPrintSettings Settings)> PrintSettings(CancellationToken ct)
    {
        var row = await db.Set<ClinicSettingsVersion>().OrderByDescending(x => x.Version)
            .Select(x => new { x.Version, x.ContentJson }).FirstOrDefaultAsync(ct);
        var settings = row == null ? DCMS.Application.Features.SystemSettings.GeneralSettings.Default : JsonSerializer.Deserialize<DCMS.Application.Features.SystemSettings.GeneralSettings>(row.ContentJson, DCMS.Infrastructure.Features.SystemSettings.ClinicSettingsStore.Json)!;
        return (row?.Version ?? 0, settings.ReceiptPrint);
    }
    async Task<ReceiptPrintDto> PrintDto(PaymentReceipt p, bool preview, CancellationToken ct)
    {
        var (settingsVersion, settings) = await PrintSettings(ct);
        if (preview) return new(null, true, settings.ShowReceiptNumber ? "PT-MẪU-00000001" : null,
            "NGUYỄN VĂN A (DỮ LIỆU MẪU)", p.BranchName, "Cash", 500_000, 200_000, 300_000,
            "NGUYỄN VĂN A", "NHÂN VIÊN THU NGÂN", "Bản xem trước không phát hành phiếu", clock.GetUtcNow(), settingsVersion, settings);
        if (p.State != "Posted" || p.ConfirmedAt == null || p.Number == null) throw Error("MSG42", 409);
        var issuedAt = p.ConfirmedAt.Value;
        var total = await db.Set<FinanceCharge>().Where(x => x.VisitId == p.VisitId && x.PostedAt <= issuedAt).SumAsync(x => x.AmountVnd, ct);
        var paidBefore = await db.Set<PaymentReceipt>().Where(x => x.VisitId == p.VisitId && x.State == "Posted" && x.ConfirmedAt < issuedAt).SumAsync(x => x.AmountVnd, ct);
        var collector = await db.Users.Where(x => x.Id == p.ConfirmedBy).Select(x => x.FullName).SingleAsync(ct);
        return new(p.Id, false, settings.ShowReceiptNumber ? p.Number : null, p.PatientName, p.BranchName, p.Method,
            p.AmountVnd, paidBefore, Math.Max(0, total - paidBefore - p.AmountVnd), p.Payer, collector, p.Note,
            issuedAt, settingsVersion, settings);
    }
    public async Task<ReceiptPrintDto> PreviewReceiptAsync(int actor, int branchId, CancellationToken ct)
    {
        await access.OperationalAsync(actor, "finance.read", branchId, ct);
        var branch = await db.Set<SchedulingBranch>().SingleOrDefaultAsync(x => x.Id == branchId, ct) ?? throw Error("MSG113", 404);
        return await PrintDto(new PaymentReceipt { BranchName = branch.Name }, true, ct);
    }
    public async Task<ReceiptPrintDto> ReceiptPrintAsync(int actor, Guid id, CancellationToken ct)
    {
        var p = await db.Set<PaymentReceipt>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);
        await access.OperationalAsync(actor, "finance.read", p.BranchId, ct);
        return await PrintDto(p, false, ct);
    }
    public async Task<ReceiptPrintDto> RecordPrintAsync(int actor, Guid id, RecordReceiptPrint c, CancellationToken ct)
    {
        var p = await db.Set<PaymentReceipt>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);
        await access.OperationalAsync(actor, "finance.read", p.BranchId, ct);
        if (c.RequestKey == Guid.Empty) throw Error("MSG04");
        var dto = await PrintDto(p, false, ct);
        if (!await db.Set<ReceiptPrintEvent>().AnyAsync(x => x.ReceiptId == id && x.ActorId == actor && x.RequestKey == c.RequestKey, ct))
        {
            db.Add(new ReceiptPrintEvent { ReceiptId = id, ActorId = actor, RequestKey = c.RequestKey, SettingsVersion = dto.SettingsVersion, PrintedAt = clock.GetUtcNow() });
            await db.SaveChangesAsync(ct);
        }
        return dto;
    }
    public async Task<ReceiptSequenceDto> ReceiptSequenceAsync(int actor, int branchId, CancellationToken ct)
    {
        await access.OperationalAsync(actor, "settings.general.manage", branchId, ct);
        var row = await db.Set<BranchReceiptSequence>().SingleOrDefaultAsync(x => x.BranchId == branchId, ct);
        return new(branchId, row?.NextNumber ?? 1, Math.Max(0, (row?.NextNumber ?? 1) - 1), row?.Version ?? 0);
    }
    public async Task<ReceiptSequenceDto> UpdateReceiptSequenceAsync(int actor, int branchId, UpdateReceiptSequence c, CancellationToken ct)
    {
        await access.OperationalAsync(actor, "settings.general.manage", branchId, ct);
        if (c.NextNumber < 1 || string.IsNullOrWhiteSpace(c.Reason) || c.Reason.Length > 500) throw Error("MSG04");
        return await Run(actor, c.RequestKey, "UpdateReceiptSequence", branchId.ToString(), c, branchId, null, async () =>
        {
            await access.OperationalAsync(actor, "settings.general.manage", branchId, ct);
            var row = await db.Set<BranchReceiptSequence>().SingleOrDefaultAsync(x => x.BranchId == branchId, ct);
            if (row == null) { if (c.ExpectedVersion != 0) throw Error("MSG111", 409); row = new() { BranchId = branchId, NextNumber = 1, Version = 0 }; db.Add(row); }
            if (row.Version != c.ExpectedVersion) throw Error("MSG111", 409);
            if (c.NextNumber <= row.NextNumber - 1 || c.NextNumber < row.NextNumber) throw Invalid("Mốc số mới phải lớn hơn mọi số phiếu đã cấp và không được giảm hoặc tái sử dụng.", 409);
            row.NextNumber = c.NextNumber; row.Version++;
            return new ReceiptSequenceDto(branchId, row.NextNumber, row.NextNumber - 1, row.Version);
        }, ct);
    }
}

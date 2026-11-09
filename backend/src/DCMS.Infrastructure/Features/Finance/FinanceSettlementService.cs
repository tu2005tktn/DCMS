using System.Text.Json;
using DCMS.Application.Features.Finance;
using DCMS.Domain.Features.Finance;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Features.Examinations;
using DCMS.Infrastructure.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.Settings.SettingsAccess;
using DCMS.Application.Features.Settings;

namespace DCMS.Infrastructure.Features.Finance;

public sealed class FinanceSettlementService(DcmsDbContext db, ISettingsAccess access,
    IFinanceService finance, TimeProvider clock) : IFinanceSettlementService
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone).DateTime);

    private async Task<T> Run<T>(int actor, Guid key, string action, string target, object payload,
        int branchId, Func<Task<T>> mutate, CancellationToken ct)
    {
        if (key == Guid.Empty) throw Error("MSG04");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})", ct);
        var hash = ExaminationService.Hash(new { action, target, payload });
        var old = await db.Set<FinanceCommandReceipt>().SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == key, ct);
        if (old != null)
        {
            if (old.Fingerprint != hash) throw Error("MSG111", 409);
            await tx.CommitAsync(ct);
            return JsonSerializer.Deserialize<T>(old.ResultJson, ExaminationService.Json)!;
        }
        var result = await mutate();
        db.Add(new FinanceAudit { ActorId = actor, BranchId = branchId, Action = action, Target = target,
            PayloadJson = JsonSerializer.Serialize(payload, ExaminationService.Json), At = clock.GetUtcNow() });
        db.Add(new FinanceCommandReceipt { ActorId = actor, RequestKey = key, Fingerprint = hash,
            ResultJson = JsonSerializer.Serialize(result, ExaminationService.Json) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<int[]> Scope(int actor, string permission, int? branchId, CancellationToken ct)
    {
        var scope = await access.RequireAsync(actor, permission, ct);
        if (branchId.HasValue && !scope.BranchIds.Contains(branchId.Value)) throw Error("MSG06", 403);
        return branchId.HasValue ? [branchId.Value] : scope.BranchIds;
    }

    public async Task<PaymentMethodDto[]> PaymentMethodsAsync(int actor, int branchId, CancellationToken ct)
    {
        await access.OperationalAsync(actor, "finance.read", branchId, ct);
        var customs = await db.Set<FinancePaymentMethod>().AsNoTracking().Where(x => x.BranchId == branchId)
            .OrderBy(x => x.Name).Select(x => new PaymentMethodDto(x.BranchId, x.Code, x.Name, x.Active, x.Version))
            .ToArrayAsync(ct);
        return FinancePaymentMethods.Builtin.Select(x => new PaymentMethodDto(branchId, x.Key, x.Value, true, 0))
            .Concat(customs).ToArray();
    }

    public async Task<PaymentMethodDto> SavePaymentMethodAsync(int actor, SavePaymentMethod c, CancellationToken ct)
    {
        await access.OperationalAsync(actor, "settings.general.manage", c.BranchId, ct);
        var code = c.Code?.Trim().ToUpperInvariant() ?? "";
        var name = c.Name?.Trim() ?? "";
        if (code.Length is < 2 or > 32 || code.Any(x => !char.IsAsciiLetterOrDigit(x) && x != '_') ||
            name.Length is < 1 or > 100 || FinancePaymentMethods.Builtin.Keys.Any(x =>
                x.Equals(code, StringComparison.OrdinalIgnoreCase))) throw Error("MSG04");
        return await Run(actor, c.RequestKey, "SavePaymentMethod", code, c, c.BranchId, async () =>
        {
            var row = await db.Set<FinancePaymentMethod>().SingleOrDefaultAsync(x => x.BranchId == c.BranchId &&
                x.Code == code, ct);
            if (row == null)
            {
                if (c.ExpectedVersion != 0) throw Error("MSG111", 409);
                row = new FinancePaymentMethod { BranchId = c.BranchId, Code = code, Name = name, Active = c.Active };
                db.Add(row);
            }
            else
            {
                if (row.Version != c.ExpectedVersion) throw Error("MSG111", 409);
                row.Name = name; row.Active = c.Active; row.Version++;
            }
            return new PaymentMethodDto(row.BranchId, row.Code, row.Name, row.Active, row.Version);
        }, ct);
    }

    private async Task<InvoiceDto> InvoiceCore(Guid id, CancellationToken ct)
    {
        var invoice = await db.Set<FinanceInvoice>().AsNoTracking().Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);
        var chargeIds = invoice.Lines.Select(x => x.ChargeId).ToArray();
        var charges = await db.Set<FinanceCharge>().AsNoTracking().Where(x => chargeIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.ActivityId }).ToArrayAsync(ct);
        var names = charges.ToDictionary(x => x.Id, x => x.Name);
        var activities = charges.Where(x => x.ActivityId.HasValue).ToDictionary(x => x.ActivityId!.Value, x => x.Id);
        var receiptPaid = await (from a in db.Set<PaymentAllocation>().AsNoTracking()
            join p in db.Set<PaymentReceipt>().AsNoTracking() on a.PaymentId equals p.Id
            where chargeIds.Contains(a.ChargeId) && p.State == "Posted"
            group a by a.ChargeId into g select new { ChargeId = g.Key, Amount = g.Sum(x => x.AmountVnd) })
            .ToDictionaryAsync(x => x.ChargeId, x => x.Amount, ct);
        var activityIds = activities.Keys.ToArray();
        var dossierPaid = await db.Set<DossierPaymentLine>().AsNoTracking()
            .Where(x => activityIds.Contains(x.ActivityId))
            .GroupBy(x => x.ActivityId).Select(g => new { ActivityId = g.Key, Amount = g.Sum(x => x.AmountVnd) })
            .ToDictionaryAsync(x => x.ActivityId, x => x.Amount, ct);
        var lineIds = invoice.Lines.Select(x => x.Id).ToArray();
        var advances = await db.Set<FinanceAdvanceAllocation>().AsNoTracking()
            .Where(x => lineIds.Contains(x.InvoiceLineId))
            .GroupBy(x => x.InvoiceLineId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.AmountVnd) })
            .ToDictionaryAsync(x => x.Id, x => x.Amount, ct);
        var refundsByCharge = await db.Set<FinanceRefund>().AsNoTracking()
            .Where(x => x.InvoiceId == id && x.ChargeId.HasValue && x.State == "Completed")
            .GroupBy(x => x.ChargeId!.Value).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.AmountVnd) })
            .ToDictionaryAsync(x => x.Id, x => x.Amount, ct);
        var lines = invoice.Lines.OrderBy(x => x.ServiceDate).Select(x =>
        {
            var activityId = charges.FirstOrDefault(c => c.Id == x.ChargeId)?.ActivityId;
            var paid = receiptPaid.GetValueOrDefault(x.ChargeId) +
                (activityId.HasValue ? dossierPaid.GetValueOrDefault(activityId.Value) : 0) +
                advances.GetValueOrDefault(x.Id) - refundsByCharge.GetValueOrDefault(x.ChargeId);
            return new InvoiceLineDto(x.Id, x.ChargeId, names.GetValueOrDefault(x.ChargeId) ?? "—",
                x.ServiceDate, x.GrossVnd, x.DiscountVnd, x.NetVnd, paid);
        }).ToArray();
        var refunded = await db.Set<FinanceRefund>().AsNoTracking()
            .Where(x => x.InvoiceId == id && x.State == "Completed").SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;
        var gross = lines.Sum(x => x.GrossVnd);
        var discount = lines.Sum(x => x.DiscountVnd);
        var net = lines.Sum(x => x.NetVnd);
        var paid = lines.Sum(x => x.PaidVnd);
        var outstanding = Math.Max(0, net - paid);
        var state = refunded >= net && net > 0 ? "Refunded" : refunded > 0 ? "PartiallyRefunded"
            : paid <= 0 ? "Unpaid" : outstanding > 0 ? "PartiallyPaid" : "Paid";
        var patientName = await db.Set<PatientProfile>().AsNoTracking().Where(x => x.Id == invoice.PatientId)
            .Select(x => x.FullName).SingleAsync(ct);
        var accountVersion = await db.Set<FinancialAccount>().AsNoTracking()
            .Where(x => x.PatientId == invoice.PatientId && x.BranchId == invoice.BranchId)
            .Select(x => x.Version).SingleOrDefaultAsync(ct);
        var receiptHistory = await (from a in db.Set<PaymentAllocation>().AsNoTracking()
            join p in db.Set<PaymentReceipt>().AsNoTracking() on a.PaymentId equals p.Id
            where chargeIds.Contains(a.ChargeId)
            select new InvoicePaymentDto(p.Id, "Receipt", p.Method, p.State, a.AmountVnd,
                p.ConfirmedAt ?? p.CreatedAt, a.ChargeId, p.Version)).ToArrayAsync(ct);
        var dossierHistory = await (from a in db.Set<DossierPaymentLine>().AsNoTracking()
            join p in db.Set<DossierPayment>().AsNoTracking() on a.PaymentId equals p.Id
            join charge in db.Set<FinanceCharge>().AsNoTracking() on a.ActivityId equals charge.ActivityId
            where chargeIds.Contains(charge.Id)
            select new InvoicePaymentDto(p.Id, "Treatment", p.Method, "Posted", a.AmountVnd,
                p.CreatedAt, charge.Id, 0)).ToArrayAsync(ct);
        var advanceHistory = await (from a in db.Set<FinanceAdvanceAllocation>().AsNoTracking()
            join line in db.Set<FinanceInvoiceLine>().AsNoTracking() on a.InvoiceLineId equals line.Id
            where lineIds.Contains(line.Id)
            select new InvoicePaymentDto(a.Id, "Advance", "Advance", "Posted", a.AmountVnd,
                a.AllocatedAt, line.ChargeId, 0)).ToArrayAsync(ct);
        return new(invoice.Id, invoice.Number, invoice.PatientId, patientName, invoice.BranchId,
            invoice.VisitId, invoice.DossierId, invoice.InvoiceDate, invoice.DueDate, invoice.CreatedAt,
            gross, discount, net, paid, outstanding, state, invoice.Version, accountVersion, lines,
            receiptHistory.Concat(dossierHistory).Concat(advanceHistory).OrderByDescending(x => x.ReceivedAt).ToArray());
    }

    public async Task<InvoiceDto> InvoiceAsync(int actor, Guid id, CancellationToken ct)
    {
        var branch = await db.Set<FinanceInvoice>().AsNoTracking().Where(x => x.Id == id)
            .Select(x => (int?)x.BranchId).SingleOrDefaultAsync(ct) ?? throw Error("MSG113", 404);
        await access.OperationalAsync(actor, "finance.read", branch, ct);
        return await InvoiceCore(id, ct);
    }

    public async Task<InvoicePage> InvoicesAsync(int actor, int? branchId, int? patientId,
        int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw Error("MSG04");
        var branches = await Scope(actor, "finance.read", branchId, ct);
        var query = db.Set<FinanceInvoice>().AsNoTracking().Where(x => branches.Contains(x.BranchId));
        if (patientId.HasValue) query = query.Where(x => x.PatientId == patientId.Value);
        var count = await query.CountAsync(ct);
        var ids = await query.OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.Id).ToArrayAsync(ct);
        var items = new List<InvoiceDto>(ids.Length);
        foreach (var id in ids) items.Add(await InvoiceCore(id, ct));
        return new(count, items);
    }

    public async Task<UnbilledChargeDto[]> UnbilledChargesAsync(int actor, int branchId, CancellationToken ct)
    {
        await access.OperationalAsync(actor, "finance.read", branchId, ct);
        var charges = await db.Set<FinanceCharge>().AsNoTracking()
            .Where(x => x.BranchId == branchId && !db.Set<FinanceInvoiceLine>().Any(l => l.ChargeId == x.Id))
            .OrderByDescending(x => x.PostedAt).ToArrayAsync(ct);
        var patientIds = charges.Select(x => x.PatientId).Distinct().ToArray();
        var patientNames = await db.Set<PatientProfile>().AsNoTracking().Where(x => patientIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var activityIds = charges.Where(x => x.ActivityId.HasValue).Select(x => x.ActivityId!.Value).ToArray();
        var dates = await db.Set<ClinicalActivity>().AsNoTracking().Where(x => activityIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.PerformedAt, ct);
        return charges.Select(x => new UnbilledChargeDto(x.Id, x.PatientId,
            patientNames.GetValueOrDefault(x.PatientId) ?? "—", x.BranchId, x.VisitId, x.DossierId,
            x.Name, DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                dates.GetValueOrDefault(x.ActivityId ?? Guid.Empty, x.PostedAt), Zone).DateTime), x.AmountVnd)).ToArray();
    }

    public async Task<InvoiceDto> IssueInvoiceAsync(int actor, IssueInvoice c, CancellationToken ct)
    {
        await access.OperationalAsync(actor, "finance.payments.create", c.BranchId, ct);
        if (c.PatientId < 1 || c.ChargeIds is not { Count: > 0 and <= 100 } ||
            c.ChargeIds.Distinct().Count() != c.ChargeIds.Count || c.InvoiceDate > Today || c.DueDate < c.InvoiceDate)
            throw Error("MSG04");
        return await Run(actor, c.RequestKey, "IssueInvoice", c.PatientId.ToString(), c, c.BranchId, async () =>
        {
            var charges = await db.Set<FinanceCharge>().Where(x => c.ChargeIds.Contains(x.Id)).ToArrayAsync(ct);
            if (charges.Length != c.ChargeIds.Count || charges.Any(x => x.PatientId != c.PatientId || x.BranchId != c.BranchId))
                throw Error("MSG113", 404);
            if (charges.Select(x => x.VisitId).Distinct().Count() != 1 ||
                charges.Select(x => x.DossierId).Distinct().Count() != 1) throw Error("MSG04");
            if (await db.Set<FinanceInvoiceLine>().AnyAsync(x => c.ChargeIds.Contains(x.ChargeId), ct)) throw Error("MSG111", 409);
            var activityIds = charges.Where(x => x.ActivityId.HasValue).Select(x => x.ActivityId!.Value).ToArray();
            var activities = await db.Set<ClinicalActivity>().AsNoTracking().Where(x => activityIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
            var invoice = new FinanceInvoice { PatientId = c.PatientId, BranchId = c.BranchId,
                VisitId = charges[0].VisitId, DossierId = charges[0].DossierId,
                Number = $"HD-{c.BranchId}-{clock.GetUtcNow():yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..36],
                InvoiceDate = c.InvoiceDate, DueDate = c.DueDate, CreatedAt = clock.GetUtcNow(), CreatedBy = actor };
            foreach (var charge in charges)
            {
                activities.TryGetValue(charge.ActivityId ?? Guid.Empty, out var activity);
                var gross = activity == null ? charge.AmountVnd :
                    checked((long)Math.Round(activity.Quantity * activity.PriceSnapshotVnd, MidpointRounding.AwayFromZero));
                var discount = gross - charge.AmountVnd;
                if (discount < 0 || gross < 0) throw Error("MSG111", 409);
                invoice.Lines.Add(new FinanceInvoiceLine { InvoiceId = invoice.Id, ChargeId = charge.Id,
                    ServiceDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(activity?.PerformedAt ?? charge.PostedAt, Zone).DateTime),
                    GrossVnd = gross, DiscountVnd = discount, NetVnd = charge.AmountVnd });
            }
            db.Add(invoice);
            await db.SaveChangesAsync(ct);
            return await InvoiceCore(invoice.Id, ct);
        }, ct);
    }

    public async Task<PaymentDto> CollectInvoiceAsync(int actor, Guid id, CollectPayment command, CancellationToken ct)
    {
        var invoice = await InvoiceAsync(actor, id, ct);
        var allowed = invoice.Lines.Select(x => x.ChargeId).ToHashSet();
        if (command.PatientId != invoice.PatientId || command.Allocations == null ||
            command.Allocations.Any(x => !allowed.Contains(x.ChargeId))) throw Error("MSG04");
        if (invoice.VisitId.HasValue) return await finance.CollectAsync(actor, invoice.VisitId.Value, command, ct);
        return await finance.CollectDossierAsync(actor, invoice.DossierId!.Value, command, ct);
    }

    private async Task<AdvanceDto> AdvanceCore(FinanceAdvance a, CancellationToken ct)
    {
        var payment = await db.Set<PaymentReceipt>().AsNoTracking().SingleAsync(x => x.Id == a.PaymentId, ct);
        var allocated = await db.Set<FinanceAdvanceAllocation>().AsNoTracking()
            .Where(x => x.AdvanceId == a.Id).SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;
        var refunded = await db.Set<FinanceRefund>().AsNoTracking()
            .Where(x => x.AdvanceId == a.Id && x.State == "Completed").SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;
        var accountVersion = await db.Set<FinancialAccount>().AsNoTracking()
            .Where(x => x.PatientId == a.PatientId && x.BranchId == a.BranchId)
            .Select(x => x.Version).SingleOrDefaultAsync(ct);
        return new(a.Id, a.PatientId, a.BranchId, a.DossierId, a.Kind, a.PaymentId,
            payment.State, payment.Method, payment.AmountVnd, allocated, refunded,
            payment.State == "Posted" ? Math.Max(0, payment.AmountVnd - allocated - refunded) : 0,
            a.CreatedAt, a.Version, payment.Version, accountVersion);
    }

    public async Task<AdvanceDto[]> AdvancesAsync(int actor, int? branchId, int? patientId, CancellationToken ct)
    {
        var branches = await Scope(actor, "finance.read", branchId, ct);
        var query = db.Set<FinanceAdvance>().AsNoTracking().Where(x => branches.Contains(x.BranchId));
        if (patientId.HasValue) query = query.Where(x => x.PatientId == patientId.Value);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ToArrayAsync(ct);
        var result = new List<AdvanceDto>(rows.Length);
        foreach (var row in rows) result.Add(await AdvanceCore(row, ct));
        return result.ToArray();
    }

    public async Task<AdvanceDto> CreateAdvanceAsync(int actor, CreateAdvance c, CancellationToken ct)
    {
        await access.OperationalAsync(actor, "finance.payments.create", c.BranchId, ct);
        if (c.PatientId < 1 || c.AmountVnd is < 1 or > FinanceService.MaxAmount ||
            c.Kind is not ("Deposit" or "ServiceAdvance") ||
            string.IsNullOrWhiteSpace(c.Payer) || c.Payer.Length > 160 || c.Note?.Length > 1000) throw Error("MSG04");
        return await Run(actor, c.RequestKey, "CreateAdvance", c.PatientId.ToString(), c, c.BranchId, async () =>
        {
            if (!await FinancePaymentMethods.IsAllowedAsync(db, c.BranchId, c.Method, ct)) throw Error("MSG04");
            var patient = await db.Set<PatientProfile>().Where(x => x.Id == c.PatientId).SingleOrDefaultAsync(ct)
                ?? throw Error("MSG113", 404);
            var branch = await db.Set<SchedulingBranch>().Where(x => x.Id == c.BranchId).SingleOrDefaultAsync(ct)
                ?? throw Error("MSG113", 404);
            if (c.DossierId.HasValue && !await db.TreatmentDossiers.AnyAsync(x => x.Id == c.DossierId &&
                x.PatientId == c.PatientId && x.BranchId == c.BranchId && x.State != "Deleted", ct)) throw Error("MSG113", 404);
            var payment = new PaymentReceipt { PatientId = c.PatientId, BranchId = c.BranchId,
                PatientName = patient.FullName, BranchName = branch.Name, Purpose = "Advance", Method = c.Method,
                AmountVnd = c.AmountVnd, Payer = c.Payer.Trim(), Note = c.Note?.Trim(), CreatedBy = actor,
                CreatedAt = clock.GetUtcNow() };
            if (c.Method == "Cash")
            {
                var seq = await db.Set<BranchReceiptSequence>().SingleOrDefaultAsync(x => x.BranchId == c.BranchId, ct);
                if (seq == null) { seq = new BranchReceiptSequence { BranchId = c.BranchId }; db.Add(seq); }
                payment.Number = $"PT-{c.BranchId}-{seq.NextNumber++:D8}";
                payment.State = "Posted"; payment.ConfirmedAt = clock.GetUtcNow(); payment.ConfirmedBy = actor;
            }
            db.Add(payment);
            var advance = new FinanceAdvance { PatientId = c.PatientId, BranchId = c.BranchId,
                DossierId = c.DossierId, PaymentId = payment.Id, Kind = c.Kind,
                CreatedAt = clock.GetUtcNow(), CreatedBy = actor };
            db.Add(advance);
            var account = await db.Set<FinancialAccount>().SingleOrDefaultAsync(x => x.PatientId == c.PatientId && x.BranchId == c.BranchId, ct);
            if (account == null) db.Add(new FinancialAccount { PatientId = c.PatientId, BranchId = c.BranchId });
            else account.Version++;
            await db.SaveChangesAsync(ct);
            return await AdvanceCore(advance, ct);
        }, ct);
    }

    public async Task<AdvanceDto> ApplyAdvanceAsync(int actor, Guid id, ApplyAdvance c, CancellationToken ct)
    {
        var advance = await db.Set<FinanceAdvance>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);
        await access.OperationalAsync(actor, "finance.payments.create", advance.BranchId, ct);
        if (c.AmountVnd <= 0) throw Error("MSG04");
        return await Run(actor, c.RequestKey, "ApplyAdvance", id.ToString(), c, advance.BranchId, async () =>
        {
            await db.Entry(advance).ReloadAsync(ct);
            if (advance.Version != c.ExpectedVersion) throw Error("MSG111", 409);
            var balance = await AdvanceCore(advance, ct);
            var reserved = await db.Set<FinanceRefund>().Where(x => x.AdvanceId == id &&
                (x.State == "Requested" || x.State == "Approved")).SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;
            if (balance.PaymentState != "Posted" || balance.AvailableVnd - reserved < c.AmountVnd) throw Error("MSG111", 409);
            var line = await db.Set<FinanceInvoiceLine>().SingleOrDefaultAsync(x => x.Id == c.InvoiceLineId, ct)
                ?? throw Error("MSG113", 404);
            var invoice = await db.Set<FinanceInvoice>().SingleAsync(x => x.Id == line.InvoiceId, ct);
            if (invoice.PatientId != advance.PatientId || invoice.BranchId != advance.BranchId ||
                (advance.DossierId.HasValue && invoice.DossierId != advance.DossierId)) throw Error("MSG06", 403);
            var current = await InvoiceCore(invoice.Id, ct);
            var currentLine = current.Lines.Single(x => x.Id == line.Id);
            if (currentLine.NetVnd - currentLine.PaidVnd < c.AmountVnd) throw Error("MSG111", 409);
            db.Add(new FinanceAdvanceAllocation { AdvanceId = id, InvoiceLineId = line.Id,
                AmountVnd = c.AmountVnd, AllocatedAt = clock.GetUtcNow(), AllocatedBy = actor });
            advance.Version++;
            var account = await db.Set<FinancialAccount>().SingleAsync(x => x.PatientId == advance.PatientId &&
                x.BranchId == advance.BranchId, ct);
            account.Version++;
            await db.SaveChangesAsync(ct);
            return await AdvanceCore(advance, ct);
        }, ct);
    }

    public async Task<ReceivableDto[]> ReceivablesAsync(int actor, int? branchId, CancellationToken ct)
    {
        var branches = await Scope(actor, "finance.read", branchId, ct);
        var invoices = await InvoicesAsync(actor, branchId, null, 1, 100, ct);
        var all = new List<InvoiceDto>(invoices.Items);
        for (var page = 2; all.Count < invoices.Total; page++)
            all.AddRange((await InvoicesAsync(actor, branchId, null, page, 100, ct)).Items);
        var summary = all.Where(x => x.OutstandingVnd > 0).GroupBy(x => new { x.PatientId, x.PatientName, x.BranchId })
            .Select(g => new ReceivableDto(g.Key.PatientId, g.Key.PatientName, g.Key.BranchId,
                g.Sum(x => x.OutstandingVnd), g.Min(x => x.InvoiceDate), g.Min(x => x.DueDate), g.Count(), 0))
            .ToDictionary(x => (x.PatientId, x.BranchId));
        var unbilled = await db.Set<FinanceCharge>().AsNoTracking().Where(x => branches.Contains(x.BranchId) &&
            !db.Set<FinanceInvoiceLine>().Any(line => line.ChargeId == x.Id)).ToArrayAsync(ct);
        var ids = unbilled.Select(x => x.Id).ToArray();
        var activityIds = unbilled.Where(x => x.ActivityId.HasValue).Select(x => x.ActivityId!.Value).ToArray();
        var receiptPaid = await (from allocation in db.Set<PaymentAllocation>().AsNoTracking()
            join receipt in db.Set<PaymentReceipt>().AsNoTracking() on allocation.PaymentId equals receipt.Id
            where ids.Contains(allocation.ChargeId) && receipt.State == "Posted"
            group allocation by allocation.ChargeId into g select new { Id = g.Key, Amount = g.Sum(x => x.AmountVnd) })
            .ToDictionaryAsync(x => x.Id, x => x.Amount, ct);
        var dossierPaid = await db.Set<DossierPaymentLine>().AsNoTracking().Where(x => activityIds.Contains(x.ActivityId))
            .GroupBy(x => x.ActivityId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.AmountVnd) })
            .ToDictionaryAsync(x => x.Id, x => x.Amount, ct);
        var patientIds = unbilled.Select(x => x.PatientId).Distinct().ToArray();
        var names = await db.Set<PatientProfile>().AsNoTracking().Where(x => patientIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        foreach (var group in unbilled.GroupBy(x => (x.PatientId, x.BranchId)))
        {
            var due = group.Sum(x => Math.Max(0, x.AmountVnd - receiptPaid.GetValueOrDefault(x.Id) -
                (x.ActivityId.HasValue ? dossierPaid.GetValueOrDefault(x.ActivityId.Value) : 0)));
            if (due <= 0) continue;
            var first = group.Min(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x.PostedAt, Zone).DateTime));
            if (summary.TryGetValue(group.Key, out var existing))
                summary[group.Key] = existing with { OutstandingVnd = existing.OutstandingVnd + due,
                    UnbilledVnd = due, OldestInvoiceDate = first < existing.OldestInvoiceDate ? first : existing.OldestInvoiceDate };
            else summary[group.Key] = new(group.Key.PatientId, names.GetValueOrDefault(group.Key.PatientId) ?? "—",
                group.Key.BranchId, due, first, null, 0, due);
        }
        return summary.Values.OrderByDescending(x => x.OutstandingVnd).ToArray();
    }

    public async Task<DebtHistoryEntryDto[]> DebtHistoryAsync(int actor, int patientId, int branchId, CancellationToken ct)
    {
        await access.OperationalAsync(actor, "finance.read", branchId, ct);
        var charges = await db.Set<FinanceCharge>().AsNoTracking().Where(x => x.PatientId == patientId &&
            x.BranchId == branchId).ToArrayAsync(ct);
        var ids = charges.Select(x => x.Id).ToArray();
        var activityIds = charges.Where(x => x.ActivityId.HasValue).Select(x => x.ActivityId!.Value).ToArray();
        var receiptMovements = await (from allocation in db.Set<PaymentAllocation>().AsNoTracking()
            join payment in db.Set<PaymentReceipt>().AsNoTracking() on allocation.PaymentId equals payment.Id
            where ids.Contains(allocation.ChargeId) && payment.State == "Posted"
            select new { payment.Id, At = payment.ConfirmedAt!.Value, allocation.AmountVnd }).ToArrayAsync(ct);
        var dossierMovements = await (from allocation in db.Set<DossierPaymentLine>().AsNoTracking()
            join payment in db.Set<DossierPayment>().AsNoTracking() on allocation.PaymentId equals payment.Id
            where activityIds.Contains(allocation.ActivityId)
            select new { payment.Id, At = payment.CreatedAt, allocation.AmountVnd }).ToArrayAsync(ct);
        var advanceMovements = await (from allocation in db.Set<FinanceAdvanceAllocation>().AsNoTracking()
            join line in db.Set<FinanceInvoiceLine>().AsNoTracking() on allocation.InvoiceLineId equals line.Id
            where ids.Contains(line.ChargeId)
            select new { allocation.Id, At = allocation.AllocatedAt, allocation.AmountVnd }).ToArrayAsync(ct);
        var refunds = await db.Set<FinanceRefund>().AsNoTracking().Where(x => x.ChargeId.HasValue &&
            ids.Contains(x.ChargeId.Value) && x.State == "Completed").ToArrayAsync(ct);
        var events = charges.Select(x => new DebtHistoryEntryDto(x.PostedAt, "Charge", x.Id, x.Name,
                x.AmountVnd, 0))
            .Concat(receiptMovements.Select(x => new DebtHistoryEntryDto(x.At, "Payment", x.Id,
                "Thanh toán", -x.AmountVnd, 0)))
            .Concat(dossierMovements.Select(x => new DebtHistoryEntryDto(x.At, "TreatmentPayment", x.Id,
                "Thanh toán phiếu điều trị", -x.AmountVnd, 0)))
            .Concat(advanceMovements.Select(x => new DebtHistoryEntryDto(x.At, "AdvanceAllocation", x.Id,
                "Phân bổ tiền nhận trước", -x.AmountVnd, 0)))
            .Concat(refunds.Select(x => new DebtHistoryEntryDto(x.CompletedAt!.Value, "Refund", x.Id,
                x.Reason, x.AmountVnd, 0)))
            .OrderBy(x => x.At).ThenBy(x => x.Kind == "Charge" ? 0 : 1).ThenBy(x => x.SourceId).ToArray();
        long balance = 0;
        return events.Select(x => x with { BalanceVnd = balance = checked(balance + x.ChangeVnd) }).ToArray();
    }

    public async Task<PlanSettlementDto> PlanSettlementAsync(int actor, Guid dossierId, CancellationToken ct)
    {
        var dossier = await db.TreatmentDossiers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == dossierId &&
            x.State != "Deleted", ct) ?? throw Error("MSG113", 404);
        await access.OperationalAsync(actor, "finance.read", dossier.BranchId, ct);
        if (dossier.PlanState != "Committed") throw Error("MSG111", 409);
        var items = await db.TreatmentItems.AsNoTracking().Where(x => x.DossierId == dossierId &&
            !x.SheetDeleted && !x.HiddenFromSheet && (x.Decision == "Agreed" || x.EntrySource == "ClinicianEntry"))
            .OrderBy(x => x.PlannedOrder).ToArrayAsync(ct);
        var itemIds = items.Select(x => x.Id).ToArray();
        var performed = await (from charge in db.Set<FinanceCharge>().AsNoTracking()
            join activity in db.Set<ClinicalActivity>().AsNoTracking() on charge.ActivityId equals activity.Id
            where charge.DossierId == dossierId && activity.ItemId.HasValue && itemIds.Contains(activity.ItemId.Value)
            group charge by activity.ItemId!.Value into g
            select new { ItemId = g.Key, Amount = g.Sum(x => x.AmountVnd) })
            .ToDictionaryAsync(x => x.ItemId, x => x.Amount, ct);
        var stages = items.Select(item =>
        {
            var gross = TreatmentPlanRules.Gross(item.Quantity, item.AgreedUnitPriceVnd);
            var discount = item.DiscountStatus == "Approved" && item.DiscountValue.HasValue
                ? TreatmentPlanRules.Discount(gross, item.DiscountType!, item.DiscountValue.Value) : 0;
            var net = gross - discount;
            var done = performed.GetValueOrDefault(item.Id);
            return new PlanStageSettlementDto(item.Id, item.ServiceName, item.PlannedOrder,
                gross, discount, net, done, Math.Max(0, net - done));
        }).ToArray();
        var linked = await db.Set<FinanceAdvance>().AsNoTracking().Where(x => x.DossierId == dossierId &&
            x.Kind == "ServiceAdvance").ToArrayAsync(ct);
        long available = 0;
        foreach (var advance in linked) available = checked(available + (await AdvanceCore(advance, ct)).AvailableVnd);
        var unperformed = stages.Sum(x => x.UnperformedNetVnd);
        return new(dossierId, dossier.PatientId, dossier.BranchId, dossier.PlanVersion,
            stages.Sum(x => x.PlannedNetVnd), stages.Sum(x => x.PerformedNetVnd), unperformed,
            available, Math.Min(available, unperformed), stages);
    }

    private static RefundDto Refund(FinanceRefund x) => new(x.Id, x.Number, x.PatientId, x.BranchId,
        x.InvoiceId, x.ChargeId, x.AdvanceId, x.PaymentId, x.DossierPaymentId, x.AmountVnd, x.Method, x.Reason,
        x.State, x.RequestedBy, x.ApprovedBy, x.CompletedBy, x.RequestedAt, x.ApprovedAt, x.CompletedAt, x.Version);

    public async Task<RefundDto[]> RefundsAsync(int actor, int? branchId, CancellationToken ct)
    {
        var branches = await Scope(actor, "finance.read", branchId, ct);
        return await db.Set<FinanceRefund>().AsNoTracking().Where(x => branches.Contains(x.BranchId))
            .OrderByDescending(x => x.RequestedAt).Select(x => Refund(x)).ToArrayAsync(ct);
    }

    public async Task<RefundDto> RequestRefundAsync(int actor, RequestRefund c, CancellationToken ct)
    {
        if (c.AmountVnd <= 0 || c.AmountVnd > FinanceService.MaxAmount ||
            string.IsNullOrWhiteSpace(c.Reason) || c.Reason.Length > 1000 ||
            ((c.PaymentId.HasValue ? 1 : 0) + (c.DossierPaymentId.HasValue ? 1 : 0)) != 1) throw Error("MSG04");
        var payment = c.PaymentId.HasValue ? await db.Set<PaymentReceipt>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == c.PaymentId, ct) : null;
        var dossierPayment = c.DossierPaymentId.HasValue ? await db.Set<DossierPayment>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == c.DossierPaymentId, ct) : null;
        if (payment == null && dossierPayment == null) throw Error("MSG113", 404);
        var patient = payment?.PatientId ?? dossierPayment!.PatientId;
        var branch = payment?.BranchId ?? dossierPayment!.BranchId;
        await access.OperationalAsync(actor, "finance.payments.create", branch, ct);
        if (!await FinancePaymentMethods.IsAllowedAsync(db, branch, c.Method, ct)) throw Error("MSG04");
        return await Run(actor, c.RequestKey, "RequestRefund", (c.PaymentId ?? c.DossierPaymentId)!.Value.ToString(), c, branch, async () =>
        {
            if (payment != null && !await db.Set<PaymentReceipt>().AnyAsync(x => x.Id == payment.Id && x.State == "Posted", ct))
                throw Error("MSG111", 409);
            if (c.AdvanceId.HasValue)
            {
                var advance = await db.Set<FinanceAdvance>().SingleOrDefaultAsync(x => x.Id == c.AdvanceId &&
                    x.PaymentId == c.PaymentId, ct) ?? throw Error("MSG113", 404);
                var balance = await AdvanceCore(advance, ct);
                var reserved = await db.Set<FinanceRefund>().Where(x => x.AdvanceId == c.AdvanceId &&
                    (x.State == "Requested" || x.State == "Approved")).SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;
                if (c.InvoiceId.HasValue || c.ChargeId.HasValue || c.AmountVnd > balance.AvailableVnd - reserved) throw Error("MSG111", 409);
            }
            else if (c.InvoiceId.HasValue)
            {
                var invoice = await InvoiceCore(c.InvoiceId.Value, ct);
                if (invoice.PatientId != patient || invoice.BranchId != branch) throw Error("MSG113", 404);
                var chargeIds = invoice.Lines.Select(x => x.ChargeId).ToArray();
                if (!c.ChargeId.HasValue || !chargeIds.Contains(c.ChargeId.Value)) throw Error("MSG04");
                var activityIds = await db.Set<FinanceCharge>().Where(x => chargeIds.Contains(x.Id) && x.ActivityId.HasValue)
                    .Select(x => x.ActivityId!.Value).ToArrayAsync(ct);
                var eligible = payment != null
                    ? await db.Set<PaymentAllocation>().Where(x => x.PaymentId == payment.Id && x.ChargeId == c.ChargeId)
                        .SumAsync(x => (long?)x.AmountVnd, ct) ?? 0
                    : await db.Set<DossierPaymentLine>().Where(x => x.PaymentId == dossierPayment!.Id && activityIds.Contains(x.ActivityId) &&
                        db.Set<FinanceCharge>().Any(ch => ch.Id == c.ChargeId && ch.ActivityId == x.ActivityId))
                        .SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;
                var reserved = await db.Set<FinanceRefund>().Where(x => x.InvoiceId == c.InvoiceId &&
                    x.ChargeId == c.ChargeId &&
                    (payment != null ? x.PaymentId == c.PaymentId : x.DossierPaymentId == c.DossierPaymentId) &&
                    x.State != "Rejected").SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;
                if (c.AmountVnd > eligible - reserved) throw Error("MSG111", 409);
            }
            else throw Error("MSG04");
            var refund = new FinanceRefund { Number = $"HT-{branch}-{Guid.NewGuid():N}"[..36],
                PatientId = patient, BranchId = branch, InvoiceId = c.InvoiceId, ChargeId = c.ChargeId, AdvanceId = c.AdvanceId,
                PaymentId = c.PaymentId, DossierPaymentId = c.DossierPaymentId, AmountVnd = c.AmountVnd,
                Method = c.Method, Reason = c.Reason.Trim(), RequestedBy = actor, RequestedAt = clock.GetUtcNow() };
            db.Add(refund);
            return Refund(refund);
        }, ct);
    }

    private async Task<RefundDto> Decide(int actor, Guid id, DecideRefund c, string action, CancellationToken ct)
    {
        var refund = await db.Set<FinanceRefund>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);
        await access.OperationalAsync(actor, "finance.payments.confirm", refund.BranchId, ct);
        return await Run(actor, c.RequestKey, action, id.ToString(), c, refund.BranchId, async () =>
        {
            await db.Entry(refund).ReloadAsync(ct);
            if (refund.Version != c.ExpectedVersion) throw Error("MSG111", 409);
            if (action == "ApproveRefund")
            {
                if (refund.State != "Requested" || refund.RequestedBy == actor) throw Error("MSG111", 409);
                refund.State = "Approved"; refund.ApprovedBy = actor; refund.ApprovedAt = clock.GetUtcNow();
            }
            else if (action == "CompleteRefund")
            {
                if (refund.State != "Approved") throw Error("MSG111", 409);
                refund.State = "Completed"; refund.CompletedBy = actor; refund.CompletedAt = clock.GetUtcNow();
                var account = await db.Set<FinancialAccount>().SingleOrDefaultAsync(x => x.PatientId == refund.PatientId &&
                    x.BranchId == refund.BranchId, ct);
                if (account != null) account.Version++;
            }
            else
            {
                if (refund.State is not ("Requested" or "Approved") || string.IsNullOrWhiteSpace(c.Reason)) throw Error("MSG04");
                refund.State = "Rejected";
            }
            refund.Version++;
            return Refund(refund);
        }, ct);
    }
    public Task<RefundDto> ApproveRefundAsync(int actor, Guid id, DecideRefund c, CancellationToken ct) => Decide(actor, id, c, "ApproveRefund", ct);
    public Task<RefundDto> CompleteRefundAsync(int actor, Guid id, DecideRefund c, CancellationToken ct) => Decide(actor, id, c, "CompleteRefund", ct);
    public Task<RefundDto> RejectRefundAsync(int actor, Guid id, DecideRefund c, CancellationToken ct) => Decide(actor, id, c, "RejectRefund", ct);

    public async Task<SettlementOverview> OverviewAsync(int actor, DateOnly? from, DateOnly? to, int? branchId, CancellationToken ct)
    {
        var branches = await Scope(actor, "finance.read", branchId, ct);
        var end = to ?? Today;
        var start = from ?? new DateOnly(end.Year, end.Month, 1);
        if (start > end || end.DayNumber - start.DayNumber > 365) throw Error("MSG04");
        var lower = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
        var upper = new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
        var charges = await db.Set<FinanceCharge>().AsNoTracking().Where(x => branches.Contains(x.BranchId) &&
            (x.ActivityId.HasValue
                ? db.Set<ClinicalActivity>().Any(a => a.Id == x.ActivityId && a.PerformedAt >= lower && a.PerformedAt < upper)
                : x.PostedAt >= lower && x.PostedAt < upper)).ToArrayAsync(ct);
        var activityIds = charges.Where(x => x.ActivityId.HasValue).Select(x => x.ActivityId!.Value).ToArray();
        var activities = await db.Set<ClinicalActivity>().AsNoTracking().Where(x => activityIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        long gross = 0, discount = 0;
        foreach (var charge in charges)
        {
            var itemGross = activities.TryGetValue(charge.ActivityId ?? Guid.Empty, out var activity)
                ? checked((long)Math.Round(activity.Quantity * activity.PriceSnapshotVnd, MidpointRounding.AwayFromZero))
                : charge.AmountVnd;
            gross = checked(gross + itemGross);
            discount = checked(discount + Math.Max(0, itemGross - charge.AmountVnd));
        }
        var posted = await db.Set<PaymentReceipt>().AsNoTracking().Where(x => branches.Contains(x.BranchId) &&
            x.State == "Posted" && x.ConfirmedAt >= lower && x.ConfirmedAt < upper)
            .SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;
        var dossierPaid = await db.Set<DossierPayment>().AsNoTracking().Where(x => branches.Contains(x.BranchId) &&
            (x.Method == "Cash" ? x.PaidOn >= start && x.PaidOn <= end : x.CreatedAt >= lower && x.CreatedAt < upper))
            .SumAsync(x => (long?)x.TotalVnd, ct) ?? 0;
        var refunded = await db.Set<FinanceRefund>().AsNoTracking().Where(x => branches.Contains(x.BranchId) &&
            x.State == "Completed" && x.CompletedAt >= lower && x.CompletedAt < upper)
            .SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;
        var advances = await AdvancesAsync(actor, branchId, null, ct);
        var allInvoices = await InvoicesAsync(actor, branchId, null, 1, 100, ct);
        var invoices = new List<InvoiceDto>(allInvoices.Items);
        for (var page = 2; invoices.Count < allInvoices.Total; page++)
            invoices.AddRange((await InvoicesAsync(actor, branchId, null, page, 100, ct)).Items);
        var unallocated = advances.Sum(x => x.AvailableVnd);
        long? advanceForUnperformed = 0;
        var linkedDossiers = advances.Where(x => x.Kind == "ServiceAdvance" && x.DossierId.HasValue &&
            x.AvailableVnd > 0).Select(x => x.DossierId!.Value).Distinct().ToArray();
        if (advances.Any(x => x.Kind == "ServiceAdvance" && !x.DossierId.HasValue && x.AvailableVnd > 0))
            advanceForUnperformed = null;
        foreach (var dossierId in linkedDossiers)
        {
            if (!advanceForUnperformed.HasValue) break;
            if (!await db.TreatmentDossiers.AsNoTracking().AnyAsync(x => x.Id == dossierId && x.PlanState == "Committed", ct))
            { advanceForUnperformed = null; break; }
            advanceForUnperformed += (await PlanSettlementAsync(actor, dossierId, ct)).AdvanceForUnperformedServicesVnd;
        }
        var allCharges = await db.Set<FinanceCharge>().AsNoTracking().Where(x => branches.Contains(x.BranchId))
            .Select(x => new { x.Id, x.ActivityId, x.AmountVnd }).ToArrayAsync(ct);
        var allIds = allCharges.Select(x => x.Id).ToArray();
        var allActivities = allCharges.Where(x => x.ActivityId.HasValue).Select(x => x.ActivityId!.Value).ToArray();
        var receiptAllocations = await (from a in db.Set<PaymentAllocation>()
            join p in db.Set<PaymentReceipt>() on a.PaymentId equals p.Id
            where allIds.Contains(a.ChargeId) && p.State == "Posted"
            group a by a.ChargeId into g select new { Id = g.Key, Total = g.Sum(x => x.AmountVnd) })
            .ToDictionaryAsync(x => x.Id, x => x.Total, ct);
        var dossierAllocations = await db.Set<DossierPaymentLine>().Where(x => allActivities.Contains(x.ActivityId))
            .GroupBy(x => x.ActivityId).Select(g => new { Id = g.Key, Total = g.Sum(x => x.AmountVnd) })
            .ToDictionaryAsync(x => x.Id, x => x.Total, ct);
        var advanceAllocations = await (from a in db.Set<FinanceAdvanceAllocation>()
            join line in db.Set<FinanceInvoiceLine>() on a.InvoiceLineId equals line.Id
            where allIds.Contains(line.ChargeId) group a by line.ChargeId into g
            select new { Id = g.Key, Total = g.Sum(x => x.AmountVnd) })
            .ToDictionaryAsync(x => x.Id, x => x.Total, ct);
        var refundAllocations = await db.Set<FinanceRefund>().Where(x => x.ChargeId.HasValue &&
            allIds.Contains(x.ChargeId.Value) && x.State == "Completed")
            .GroupBy(x => x.ChargeId!.Value).Select(g => new { Id = g.Key, Total = g.Sum(x => x.AmountVnd) })
            .ToDictionaryAsync(x => x.Id, x => x.Total, ct);
        var outstanding = allCharges.Sum(x => Math.Max(0, x.AmountVnd - receiptAllocations.GetValueOrDefault(x.Id)
            - (x.ActivityId.HasValue ? dossierAllocations.GetValueOrDefault(x.ActivityId.Value) : 0)
            - advanceAllocations.GetValueOrDefault(x.Id) + refundAllocations.GetValueOrDefault(x.Id)));
        var issues = allCharges.Count(x => receiptAllocations.GetValueOrDefault(x.Id) +
            (x.ActivityId.HasValue ? dossierAllocations.GetValueOrDefault(x.ActivityId.Value) : 0) +
            advanceAllocations.GetValueOrDefault(x.Id) - refundAllocations.GetValueOrDefault(x.Id) > x.AmountVnd);
        return new(start, end, clock.GetUtcNow(),
            new(gross, discount, gross - discount, posted + dossierPaid, refunded,
                posted + dossierPaid - refunded, outstanding, unallocated, advanceForUnperformed), issues);
    }
}

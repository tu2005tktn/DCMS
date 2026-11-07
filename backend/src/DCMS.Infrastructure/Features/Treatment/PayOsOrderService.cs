using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed class PayOsOrderService(DcmsDbContext db, ITreatmentDossierService dossiers,
    PayOsGateway gateway, TimeProvider clock, DCMS.Application.Features.Settings.ISettingsAccess access) : IPayOsOrderService
{
    private static BusinessException Bad() => new(400, SystemMessages.Get("MSG04"));
    private static BusinessException Conflict() => new(409, SystemMessages.Get("MSG111"));
    private static BusinessException Missing() => new(404, SystemMessages.Get("MSG113"));
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static PayOsOrderDto Dto(DossierPayOsOrder x) => new(x.Id, x.OrderCode, x.State, x.AmountVnd,
        x.PaidOn, x.QrCode, x.BankBin, x.AccountNumber, x.AccountName, x.ExpiresAt, x.PaymentId,
        x.Lines.Select(l => new DossierPaymentLineInput(l.ActivityId, l.AmountVnd)).ToArray());

    public async Task<PayOsOrderDto?> ActiveAsync(int actorId, Guid dossierId, CancellationToken ct)
    {
        await dossiers.GetAsync(actorId, dossierId, ct);
        var id = await db.DossierPayOsOrders.AsNoTracking().Where(x => x.DossierId == dossierId
            && (x.State == "Creating" || x.State == "Pending" || x.State == "Review"))
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (!id.HasValue) return null;
        var order = await GetAsync(actorId, dossierId, id.Value, ct);
        return order.State is "Creating" or "Pending" or "Review" ? order : null;
    }

    public async Task<PayOsOrderDto> CreateAsync(int actorId, Guid dossierId, CreatePayOsOrderRequest request, CancellationToken ct)
    {
        await access.RequireAsync(actorId, "finance.payos.create", ct);
        if (request.RequestKey == Guid.Empty || request.Lines is not { Count: > 0 and <= 500 }
            || request.CollectorId <= 0 || request.Note?.Length > 1000
            || request.Lines.Any(x => x.AmountVnd <= 0 || x.AmountVnd > 2_000_000_000)
            || request.Lines.Select(x => x.ActivityId).Distinct().Count() != request.Lines.Count)
            throw Bad();
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime);
        if (request.PaidOn > today || request.PaidOn.Year < 2000) throw Bad();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new { request.PaidOn, request.CollectorId, request.Note, request.Lines }, Json))));
        var pending = await db.DossierPayOsOrders.AsNoTracking().Where(x => x.DossierId == dossierId
            && x.State == "Pending").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (pending.HasValue) await GetAsync(actorId, dossierId, pending.Value, ct);
        // The same lock as dossier payment writes makes pending-order creation and balance checks serial.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({DCMS.Infrastructure.Features.Scheduling.WorkingScheduleService.TransactionLock})", ct);
        var dossier = await dossiers.GetAsync(actorId, dossierId, ct);
        var old = await db.DossierPayOsOrders.AsNoTracking().Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, ct);
        if (old != null)
        {
            if (old.CreatedBy != actorId || old.DossierId != dossierId || old.Fingerprint != fingerprint) throw Conflict();
            if (old.State == "LinkError") throw new BusinessException(503,
                new SystemMessage("PAYOS_LINK_FAILED", "Không tạo được QR PayOS. Vui lòng thử lại."));
            return Dto(old);
        }
        var active = await db.DossierPayOsOrders.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.DossierId == dossierId
            && (x.State == "Creating" || x.State == "Pending" || x.State == "Review"), ct);
        if (active != null)
        {
            if (active.CreatedBy == actorId && active.Fingerprint == fingerprint) return Dto(active);
            throw Conflict();
        }
        var context = await dossiers.PaymentContextAsync(actorId, dossierId, ct);
        if (!context.Collectors.Any(x => x.Id == request.CollectorId)) throw Bad();
        var byActivity = context.Lines.ToDictionary(x => x.ActivityId);
        foreach (var line in request.Lines)
        {
            if (!byActivity.TryGetValue(line.ActivityId, out var source)) throw Bad();
            if (line.AmountVnd > source.DueVnd) throw Conflict();
        }
        var total = checked(request.Lines.Sum(x => x.AmountVnd));
        if (total > 2_000_000_000) throw Bad();
        var order = new DossierPayOsOrder
        {
            DossierId = dossierId, PatientId = dossier.PatientId, BranchId = dossier.BranchId,
            RequestKey = request.RequestKey, Fingerprint = fingerprint,
            OrderCode = now.ToUnixTimeMilliseconds() * 1000 + RandomNumberGenerator.GetInt32(1000),
            AmountVnd = total, PaidOn = request.PaidOn, CollectorEmployeeId = request.CollectorId,
            CreatedBy = actorId, Note = request.Note?.Trim(), CreatedAt = now, UpdatedAt = now,
            ExpiresAt = now.AddMinutes(30),
            Lines = request.Lines.Select(x => new DossierPayOsOrderLine
                { ActivityId = x.ActivityId, AmountVnd = x.AmountVnd }).ToList()
        };
        db.DossierPayOsOrders.Add(order);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        // payOS description is limited to 9 characters on some linked banks.
        var description = $"D{order.OrderCode % 100_000_000:00000000}";
        try
        {
            var link = await gateway.CreateAsync(order.OrderCode, total, description, order.PatientId, dossierId, order.ExpiresAt, ct);
            order.PaymentLinkId = link.PaymentLinkId; order.QrCode = link.QrCode;
            order.BankBin = link.BankBin; order.AccountNumber = link.AccountNumber; order.AccountName = link.AccountName;
            order.State = "Pending"; order.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return Dto(order);
        }
        catch
        {
            order.State = "LinkError"; order.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task<PayOsOrderDto> GetAsync(int actorId, Guid dossierId, Guid orderId, CancellationToken ct)
    {
        await dossiers.GetAsync(actorId, dossierId, ct);
        var order = await db.DossierPayOsOrders.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == orderId && x.DossierId == dossierId, ct)
            ?? throw Missing();
        if (order.State == "Creating" && clock.GetUtcNow() - order.CreatedAt > TimeSpan.FromMinutes(3))
        {
            await CloseAsync(order.Id, "LinkError", ct);
            await db.Entry(order).ReloadAsync(ct);
        }
        if (order.State == "Pending")
        {
            try
            {
                var remote = await gateway.StatusAsync(order.OrderCode, ct);
                if (remote.OrderCode == order.OrderCode)
                {
                    if (remote.PaymentLinkId != order.PaymentLinkId || remote.Amount != order.AmountVnd)
                        await CloseAsync(order.Id, "Review", ct);
                    else if (remote.Status == "PAID")
                    {
                        if (remote.AmountPaid == order.AmountVnd) await FinalizeAsync(order.Id, null, ct);
                        else await CloseAsync(order.Id, "Review", ct);
                    }
                    else if (remote.Status is "CANCELLED" or "EXPIRED")
                        await CloseAsync(order.Id, remote.Status == "EXPIRED" ? "Expired" : "Cancelled", ct);
                }
            }
            catch (BusinessException) { /* Keep the last known state; the next poll or webhook retries. */ }
            await db.Entry(order).ReloadAsync(ct);
        }
        return Dto(order);
    }

    public async Task ReceiveWebhookAsync(JsonElement payload, CancellationToken ct)
    {
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty("data", out var data)
            || !payload.TryGetProperty("signature", out var signature)
            || !gateway.VerifyWebhook(data, signature.GetString())) throw Bad();
        // payOS sends a signed sample webhook when registering a URL. Unknown orders are acknowledged without mutation.
        if (!data.TryGetProperty("orderCode", out var code) || !code.TryGetInt64(out var orderCode)) throw Bad();
        var order = await db.DossierPayOsOrders.AsNoTracking().SingleOrDefaultAsync(x => x.OrderCode == orderCode, ct);
        if (order == null) return;
        if (!payload.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True
            || !payload.TryGetProperty("code", out var result) || result.GetString() != "00") return;
        if (data.TryGetProperty("code", out var dataCode) && dataCode.GetString() != "00") return;
        if (!data.TryGetProperty("amount", out var amount) || !amount.TryGetInt64(out var value)
            || !data.TryGetProperty("paymentLinkId", out var link)
            || !data.TryGetProperty("currency", out var currency)
            || link.GetString() != order.PaymentLinkId || currency.GetString() != "VND"
            || value != order.AmountVnd)
        {
            await CloseAsync(order.Id, "Review", ct);
            return;
        }
        var reference = data.TryGetProperty("reference", out var bank) ? bank.GetString() : null;
        await FinalizeAsync(order.Id, reference, ct);
    }

    private async Task FinalizeAsync(Guid orderId, string? bankReference, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({DCMS.Infrastructure.Features.Scheduling.WorkingScheduleService.TransactionLock})", ct);
        var order = await db.DossierPayOsOrders.Include(x => x.Lines).SingleAsync(x => x.Id == orderId, ct);
        await db.Entry(order).ReloadAsync(ct);
        if (order.State == "Paid") return;
        if (order.State != "Pending") return;
        DossierPaymentContext context;
        try { context = await dossiers.PaymentContextAsync(order.CreatedBy, order.DossierId, ct); }
        catch (BusinessException) { order.State = "Review"; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return; }
        var due = context.Lines.ToDictionary(x => x.ActivityId, x => x.DueVnd);
        if (order.Lines.Any(x => !due.TryGetValue(x.ActivityId, out var remaining) || x.AmountVnd > remaining))
        {
            order.State = "Review"; order.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return;
        }
        var now = clock.GetUtcNow();
        var payment = new DossierPayment
        {
            DossierId = order.DossierId, PatientId = order.PatientId, BranchId = order.BranchId,
            PaidOn = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime), Method = "Transfer",
            CollectorEmployeeId = order.CollectorEmployeeId, CreatedBy = order.CreatedBy,
            Note = order.Note, TotalVnd = order.AmountVnd, CreatedAt = now,
            Lines = order.Lines.Select(x => new DossierPaymentLine
                { ActivityId = x.ActivityId, AmountVnd = x.AmountVnd }).ToList()
        };
        db.DossierPayments.Add(payment);
        order.PaymentId = payment.Id; order.BankReference = bankReference; order.State = "Paid"; order.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task CloseAsync(Guid orderId, string state, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({DCMS.Infrastructure.Features.Scheduling.WorkingScheduleService.TransactionLock})", ct);
        var order = await db.DossierPayOsOrders.SingleAsync(x => x.Id == orderId, ct);
        await db.Entry(order).ReloadAsync(ct);
        if (order.State == "Pending" || order.State == "Creating" && state == "LinkError")
        {
            order.State = state; order.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
    }
}

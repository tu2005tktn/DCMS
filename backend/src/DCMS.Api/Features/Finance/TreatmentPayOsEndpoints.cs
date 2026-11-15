using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Api.Features.Finance;

public sealed record CreateTreatmentPayOsOrder(Guid RequestKey, IReadOnlyList<TreatmentPaymentInput>? Lines, long? AmountVnd, IReadOnlyList<TreatmentLabAllocation>? LabLines);
public sealed record TreatmentPayOsOrder(Guid Id, string Purpose, int ActorId, long OrderCode, long AmountVnd,
    string PaymentLinkId, string QrCode, string BankBin, string AccountNumber, string AccountName,
    DateTimeOffset ExpiresAt, IReadOnlyList<TreatmentPaymentInput> Lines,
    IReadOnlyList<TreatmentLabAllocation> LabLines);
public sealed record TreatmentPayOsOrderState(TreatmentPayOsOrder Order, string Status, TreatmentPaymentContext? Context);

public static class TreatmentPayOsEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static bool Owner(HttpContext context) => context.Items["CurrentUser"] is CurrentUser user
        && user.EffectiveRoles.Contains(AccountRoles.ClinicOwner);

    public static void MapWebhook(IEndpointRouteBuilder endpoints) => endpoints.MapPost("/api/payments/payos/webhook",
        async (JsonElement body, DcmsDbContext db, TreatmentPayOsGateway gateway, CancellationToken ct) =>
        {
            if (!body.TryGetProperty("data", out var data)
                || !body.TryGetProperty("signature", out var signature)
                || !gateway.VerifyWebhook(data, signature.GetString())) return Results.BadRequest();
            if (!data.TryGetProperty("orderCode", out var code) || !code.TryGetInt64(out var orderCode))
                return Results.BadRequest();
            var order = await FindByCode(db, orderCode, ct);
            if (order is null) return Results.Ok(); // payOS sends a signed sample while configuring the webhook.
            return await Settle(order, await OrderDossier(db, order.Id, ct), db, gateway, ct);
        }).AllowAnonymous();

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/payos-orders", (HttpContext http, Guid dossierId, CreateTreatmentPayOsOrder request,
            DcmsDbContext db, TreatmentPayOsGateway gateway, CancellationToken ct) =>
            Create(http, dossierId, request, "Treatment", db, gateway, ct));
        group.MapPost("/advance-payos-orders", (HttpContext http, Guid dossierId, CreateTreatmentPayOsOrder request,
            DcmsDbContext db, TreatmentPayOsGateway gateway, CancellationToken ct) =>
            Create(http, dossierId, request, "Advance", db, gateway, ct));
        group.MapGet("/payos-orders/active", (HttpContext http, Guid dossierId, DcmsDbContext db, TreatmentPayOsGateway gateway,
            CancellationToken ct) => Active(http, dossierId, "Treatment", db, gateway, ct));
        group.MapGet("/advance-payos-orders/active", (HttpContext http, Guid dossierId, DcmsDbContext db, TreatmentPayOsGateway gateway,
            CancellationToken ct) => Active(http, dossierId, "Advance", db, gateway, ct));
        group.MapGet("/payos-orders/{orderId:guid}", (HttpContext http, Guid dossierId, Guid orderId,
            DcmsDbContext db, TreatmentPayOsGateway gateway, CancellationToken ct) =>
            Refresh(http, dossierId, orderId, "Treatment", db, gateway, ct));
        group.MapGet("/advance-payos-orders/{orderId:guid}", (HttpContext http, Guid dossierId, Guid orderId,
            DcmsDbContext db, TreatmentPayOsGateway gateway, CancellationToken ct) =>
            Refresh(http, dossierId, orderId, "Advance", db, gateway, ct));
        group.MapPost("/advances", async (HttpContext http, Guid dossierId, CreateTreatmentPayOsOrder request,
            DcmsDbContext db, CancellationToken ct) =>
        {
            if (!Owner(http)) return Results.Forbid();
            if (request.RequestKey == Guid.Empty || request.AmountVnd is null or <= 0 or > 1_000_000_000_000L)
                return Results.BadRequest("Số tiền cọc không hợp lệ.");
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await LockDossier(db, dossierId, ct);
            var context = await TreatmentPaymentEndpoints.ContextAsync(db, dossierId, ct);
            if (context is null) return Results.NotFound();
            var old = await LedgerPayload(db, request.RequestKey, ct);
            var labLines = TreatmentPaymentEndpoints.AllocateAdvanceToLab(context, request.AmountVnd.Value);
            var payload = JsonSerializer.Serialize(new { kind = "AdvanceCash", method = "Cash",
                amountVnd = request.AmountVnd, labLines }, JsonOptions);
            if (old is not null)
            {
                using var previous = JsonDocument.Parse(old);
                if (previous.RootElement.GetProperty("kind").GetString() != "AdvanceCash"
                    || previous.RootElement.GetProperty("amountVnd").GetInt64() != request.AmountVnd)
                    return Results.Conflict("Mã yêu cầu đã được dùng.");
                await transaction.CommitAsync(ct);
                return Results.Ok(context);
            }
            await InsertLedger(db, request.RequestKey, "AdvanceCash", ((CurrentUser)http.Items["CurrentUser"]!).Id,
                context, payload, ct);
            var updated = await TreatmentPaymentEndpoints.ContextAsync(db, dossierId, ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(updated);
        });
    }

    private static async Task<IResult> Create(HttpContext http, Guid dossierId, CreateTreatmentPayOsOrder request,
        string purpose, DcmsDbContext db, TreatmentPayOsGateway gateway, CancellationToken ct)
    {
        if (!Owner(http)) return Results.Forbid();
        if (!gateway.Configured) return Results.Json(new { code = "PAYOS_NOT_CONFIGURED",
            message = "Chưa cấu hình khóa chuyển khoản trên máy chủ." }, statusCode: 503);
        if (request.RequestKey == Guid.Empty) return Results.BadRequest("Mã yêu cầu không hợp lệ.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockDossier(db, dossierId, ct);
        var context = await TreatmentPaymentEndpoints.ContextAsync(db, dossierId, ct);
        if (context is null) return Results.NotFound();
        var old = await ReadOrder(db, request.RequestKey, dossierId, ct);
        if (old is not null)
        {
            if (old.Purpose != purpose || old.AmountVnd != (purpose == "Advance" ? request.AmountVnd : (request.Lines?.Sum(x => x.AmountVnd) ?? 0) + (request.LabLines?.Sum(x => x.AmountVnd) ?? 0))
                || !SameLines(old.Lines, request.Lines ?? [])
                || (purpose == "Treatment" && !SameLabLines(old.LabLines ?? [], request.LabLines ?? []))) return Results.Conflict("Mã yêu cầu đã được dùng.");
            await transaction.CommitAsync(ct);
            return Results.Ok(new TreatmentPayOsOrderState(old, "PENDING", context));
        }
        var active = await LatestOrder(db, dossierId, purpose, ct);
        if (active is not null && active.ExpiresAt > DateTimeOffset.UtcNow
            && !await Settled(db, active.Id, ct))
            return Results.Conflict("Đã có mã QR chưa hết hạn. Hãy mở lại mã QR hiện tại.");
        var lines = purpose == "Treatment" ? request.Lines?.ToArray() ?? [] : [];
        var labLines = purpose == "Treatment" ? request.LabLines?.ToArray() ?? [] :
            TreatmentPaymentEndpoints.AllocateAdvanceToLab(context, request.AmountVnd ?? 0);
        if (purpose == "Treatment" && (lines.Length == 0 && labLines.Length == 0 || lines.Any(x => x.AmountVnd <= 0)
            || lines.Select(x => x.ActivityId).Distinct().Count() != lines.Length
            || lines.Any(x => context.Lines.All(y => y.ActivityId != x.ActivityId || y.DueVnd < x.AmountVnd))))
            return Results.Conflict("Dịch vụ hoặc số tiền cần thu đã thay đổi.");
        if (purpose == "Treatment" && !TreatmentPaymentEndpoints.ValidateLabPayment(context, lines, labLines))
            return Results.Conflict("Cần ưu tiên đủ 30% phí labo trước khi thu cho dịch vụ khác.");
        long amount;
        try { amount = purpose == "Advance" ? request.AmountVnd ?? 0 : checked(lines.Sum(x => x.AmountVnd) + labLines.Sum(x => x.AmountVnd)); }
        catch (OverflowException) { return Results.BadRequest("Số tiền không hợp lệ."); }
        if (amount <= 0 || amount > 1_000_000_000_000L) return Results.BadRequest("Số tiền không hợp lệ.");
        // Keep orderCode below JavaScript's safe integer limit used by payOS clients.
        var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000L + RandomNumberGenerator.GetInt32(1_000);
        TreatmentPayOsLink link;
        try { link = await gateway.CreateAsync(orderCode, amount, ct); }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
        { return Results.Json(new { code = "PAYOS_UNAVAILABLE",
            message = "Không tạo được mã QR chuyển khoản. Kiểm tra kết nối PayOS của máy chủ rồi thử lại." }, statusCode: 503); }
        var order = new TreatmentPayOsOrder(request.RequestKey, purpose, ((CurrentUser)http.Items["CurrentUser"]!).Id,
            orderCode, amount, link.PaymentLinkId,
            link.QrCode, link.BankBin, link.AccountNumber, link.AccountName,
            DateTimeOffset.UtcNow.AddMinutes(30), lines, labLines);
        var payload = JsonSerializer.Serialize(order, JsonOptions);
        var count = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AUDIT_LOG" ("StorageId", "AuditType", "Action", "ActorId", "At", "PatientId", "BranchId", "Target", "PayloadJson")
            VALUES ({order.Id}, 'FinancePayOsOrder', {purpose}, {((CurrentUser)http.Items["CurrentUser"]!).Id},
                {DateTimeOffset.UtcNow}, {context.PatientId}, {context.BranchId}, {dossierId.ToString()}, {payload}::jsonb)
            ON CONFLICT ("StorageId") DO NOTHING
            """, ct);
        if (count != 1) return Results.Conflict("Mã yêu cầu đã được dùng.");
        await transaction.CommitAsync(ct);
        return Results.Ok(new TreatmentPayOsOrderState(order, "PENDING", context));
    }

    private static async Task<IResult> Active(HttpContext http, Guid dossierId, string purpose,
        DcmsDbContext db, TreatmentPayOsGateway gateway, CancellationToken ct)
    {
        if (!Owner(http)) return Results.Forbid();
        var order = await LatestOrder(db, dossierId, purpose, ct);
        if (order is null || order.ExpiresAt <= DateTimeOffset.UtcNow || await Settled(db, order.Id, ct))
            return Results.Ok((TreatmentPayOsOrderState?)null);
        return await Refresh(http, dossierId, order.Id, purpose, db, gateway, ct);
    }

    private static async Task<IResult> Refresh(HttpContext http, Guid dossierId, Guid orderId, string purpose,
        DcmsDbContext db, TreatmentPayOsGateway gateway, CancellationToken ct)
    {
        if (!Owner(http)) return Results.Forbid();
        var order = await ReadOrder(db, orderId, dossierId, ct);
        if (order is null || order.Purpose != purpose) return Results.NotFound();
        return await Settle(order, dossierId, db, gateway, ct);
    }

    internal static async Task<IResult> Settle(TreatmentPayOsOrder order, Guid dossierId,
        DcmsDbContext db, TreatmentPayOsGateway gateway, CancellationToken ct)
    {
        var orderId = order.Id;
        var purpose = order.Purpose;
        var context = await TreatmentPaymentEndpoints.ContextAsync(db, dossierId, ct);
        if (context is null) return Results.NotFound();
        if (await Settled(db, orderId, ct)) return Results.Ok(new TreatmentPayOsOrderState(order, "PAID", context));
        TreatmentPayOsStatus remote;
        try { remote = await gateway.GetAsync(order.OrderCode, ct); }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException)
        { return Results.Json(new { code = "PAYOS_STATUS_UNAVAILABLE",
            message = "Không tra cứu được chuyển khoản. Vui lòng thử lại." }, statusCode: 503); }
        if (remote.OrderCode != order.OrderCode || remote.PaymentLinkId != order.PaymentLinkId
            || remote.Amount != order.AmountVnd) return Results.Ok(new TreatmentPayOsOrderState(order, "REVIEW", context));
        if (remote.Status != "PAID") return Results.Ok(new TreatmentPayOsOrderState(order, remote.Status, context));
        if (remote.AmountPaid != order.AmountVnd) return Results.Ok(new TreatmentPayOsOrderState(order, "REVIEW", context));
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockDossier(db, dossierId, ct);
        context = await TreatmentPaymentEndpoints.ContextAsync(db, dossierId, ct);
        if (context is null) return Results.NotFound();
        if (!await Settled(db, orderId, ct))
        {
            if (purpose == "Treatment" && order.Lines.Any(x => context.Lines.All(y => y.ActivityId != x.ActivityId || y.DueVnd < x.AmountVnd)))
                return Results.Ok(new TreatmentPayOsOrderState(order, "REVIEW", context));
            var labLines = order.LabLines ?? [];
            if (purpose == "Treatment" && !TreatmentPaymentEndpoints.ValidateLabPayment(context, order.Lines, labLines)) return Results.Ok(new TreatmentPayOsOrderState(order, "REVIEW", context));
            if (purpose == "Advance" && labLines.Any(x => context.LabFees.All(lab =>
                lab.LabOrderId != x.LabOrderId || x.AmountVnd > lab.DueVnd)))
                return Results.Ok(new TreatmentPayOsOrderState(order, "REVIEW", context));
            var action = purpose == "Treatment" ? "TreatmentTransferPayment" : "AdvanceTransfer";
            var payload = purpose == "Treatment"
                ? JsonSerializer.Serialize(new { kind = action, method = "Transfer", orderId, lines = order.Lines, labLines }, JsonOptions)
                : JsonSerializer.Serialize(new { kind = action, method = "Transfer", orderId, amountVnd = order.AmountVnd, labLines }, JsonOptions);
            await InsertLedger(db, Guid.NewGuid(), action, order.ActorId, context, payload, ct);
            context = await TreatmentPaymentEndpoints.ContextAsync(db, dossierId, ct);
        }
        await transaction.CommitAsync(ct);
        return Results.Ok(new TreatmentPayOsOrderState(order, "PAID", context));
    }

    private static bool SameLabLines(IReadOnlyList<TreatmentLabAllocation> a, IReadOnlyList<TreatmentLabAllocation> b) =>
        a.OrderBy(x => x.LabOrderId).SequenceEqual(b.OrderBy(x => x.LabOrderId));

    private static bool SameLines(IReadOnlyList<TreatmentPaymentInput> a, IReadOnlyList<TreatmentPaymentInput> b) =>
        a.OrderBy(x => x.ActivityId).SequenceEqual(b.OrderBy(x => x.ActivityId));

    private static async Task LockDossier(DcmsDbContext db, Guid dossierId, CancellationToken ct) =>
        _ = await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"TREATMENT_DOSSIER\" WHERE \"Id\" = {dossierId} FOR UPDATE", ct);

    private static async Task<string?> LedgerPayload(DcmsDbContext db, Guid key, CancellationToken ct) =>
        await db.Database.SqlQuery<string>($"SELECT \"PayloadJson\"::text AS \"Value\" FROM \"AUDIT_LOG\" WHERE \"StorageId\" = {key} AND \"AuditType\" = 'FinanceLedger'")
            .SingleOrDefaultAsync(ct);

    private static async Task<bool> Settled(DcmsDbContext db, Guid orderId, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM "AUDIT_LOG" WHERE "AuditType" = 'FinanceLedger'
              AND "Action" IN ('TreatmentTransferPayment', 'AdvanceTransfer')
              AND "PayloadJson"->>'orderId' = {orderId.ToString()} LIMIT 1
            """).AnyAsync(ct);

    private static async Task<TreatmentPayOsOrder?> ReadOrder(DcmsDbContext db, Guid orderId, Guid dossierId, CancellationToken ct)
    {
        var json = await db.Database.SqlQuery<string>($"""
            SELECT "PayloadJson"::text AS "Value" FROM "AUDIT_LOG" WHERE "StorageId" = {orderId}
              AND "AuditType" = 'FinancePayOsOrder' AND "Target" = {dossierId.ToString()}
            """).SingleOrDefaultAsync(ct);
        return json is null ? null : JsonSerializer.Deserialize<TreatmentPayOsOrder>(json, JsonOptions);
    }

    private static async Task<TreatmentPayOsOrder?> LatestOrder(DcmsDbContext db, Guid dossierId, string purpose, CancellationToken ct)
    {
        var json = await db.Database.SqlQuery<string>($"""
            SELECT "PayloadJson"::text AS "Value" FROM "AUDIT_LOG"
            WHERE "AuditType" = 'FinancePayOsOrder' AND "Action" = {purpose}
              AND "Target" = {dossierId.ToString()} ORDER BY "At" DESC LIMIT 1
            """).SingleOrDefaultAsync(ct);
        return json is null ? null : JsonSerializer.Deserialize<TreatmentPayOsOrder>(json, JsonOptions);
    }

    internal static async Task<TreatmentPayOsOrder?> FindByCode(DcmsDbContext db, long code, CancellationToken ct)
    {
        var json = await db.Database.SqlQuery<string>($"""
            SELECT "PayloadJson"::text AS "Value" FROM "AUDIT_LOG"
            WHERE "AuditType" = 'FinancePayOsOrder' AND "PayloadJson"->>'orderCode' = {code.ToString()}
            LIMIT 1
            """).SingleOrDefaultAsync(ct);
        return json is null ? null : JsonSerializer.Deserialize<TreatmentPayOsOrder>(json, JsonOptions);
    }

    private static async Task<Guid> OrderDossier(DcmsDbContext db, Guid orderId, CancellationToken ct)
    {
        var target = await db.Database.SqlQuery<string>($"""
            SELECT "Target" AS "Value" FROM "AUDIT_LOG" WHERE "StorageId" = {orderId}
            """).SingleAsync(ct);
        return Guid.Parse(target);
    }

    private static Task<int> InsertLedger(DcmsDbContext db, Guid key, string action, int actor,
        TreatmentPaymentContext context, string payload, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AUDIT_LOG" ("StorageId", "AuditType", "Action", "ActorId", "At", "PatientId", "BranchId", "Target", "PayloadJson")
            VALUES ({key}, 'FinanceLedger', {action}, {actor}, {DateTimeOffset.UtcNow},
                {context.PatientId}, {context.BranchId}, {context.DossierId.ToString()}, {payload}::jsonb)
            """, ct);
}

using System.Data;
using System.Text.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Api.Features.Finance;

public sealed record TreatmentPaymentLine(Guid ActivityId, DateTimeOffset PerformedAt, string ServiceName,
    long PriceVnd, long PaidVnd, long DueVnd);
public sealed record TreatmentLabFee(Guid LabOrderId, Guid ActivityId, string WorkName,
    long TotalFeeVnd, long PaidVnd, long DueVnd, long ProviderPaidVnd,
    long Minimum30PercentVnd, long PriorityRemainingVnd);
public sealed record TreatmentLabAllocation(Guid LabOrderId, long AmountVnd);
public sealed record TreatmentPaymentContext(Guid DossierId, int PatientId, int BranchId,
    IReadOnlyList<TreatmentPaymentLine> Lines, long AdvanceReceivedVnd, IReadOnlyList<TreatmentLabFee> LabFees,
    long TotalCostVnd, long LabReceivedVnd);
public sealed record TreatmentPaymentInput(Guid ActivityId, long AmountVnd);
public sealed record RecordTreatmentCashPayment(Guid RequestKey, DateOnly PaidOn,
    IReadOnlyList<TreatmentPaymentInput> Lines, IReadOnlyList<TreatmentLabAllocation>? LabLines);
public sealed record ApplyTreatmentAdvance(Guid RequestKey, IReadOnlyList<TreatmentPaymentInput> Lines,
    IReadOnlyList<TreatmentLabAllocation>? LabLines);
public sealed record ForfeitNoShowDeposit(Guid RequestKey, int ExpectedVersion, string Reason);
public sealed record CancelTreatmentDossier(Guid RequestKey, int ExpectedVersion, string? Reason = null);
public sealed class TreatmentPaidRow
{
    public Guid ActivityId { get; set; }
    public long PaidVnd { get; set; }
}

public static class TreatmentPaymentEndpoints
{
    private static bool IsOwner(HttpContext context) => context.Items["CurrentUser"] is CurrentUser user
        && user.EffectiveRoles.Contains(AccountRoles.ClinicOwner);

    public static void MapTreatmentPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dossiers/{dossierId:guid}/payments")
            .RequireAuthorization().WithTags("Treatment payments");
        group.MapGet("/context", async (HttpContext context, Guid dossierId, DcmsDbContext db, CancellationToken ct) =>
        {
            if (!IsOwner(context)) return Results.Forbid();
            var result = await ContextAsync(db, dossierId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapPost("", async (HttpContext context, Guid dossierId, RecordTreatmentCashPayment request,
            DcmsDbContext db, CancellationToken ct) =>
        {
            if (!IsOwner(context)) return Results.Forbid();
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                DateTimeOffset.UtcNow, "Asia/Ho_Chi_Minh").DateTime);
            if (request.RequestKey == Guid.Empty || request.PaidOn > today || (request.Lines.Count == 0 && (request.LabLines?.Count ?? 0) == 0)
                || request.Lines.Any(x => x.AmountVnd <= 0)
                || (request.LabLines?.Any(x => x.AmountVnd <= 0) ?? false)
                || (request.LabLines?.Select(x => x.LabOrderId).Distinct().Count() != request.LabLines?.Count) ||
                request.Lines.Select(x => x.ActivityId).Distinct().Count() != request.Lines.Count)
                return Results.BadRequest("Thông tin thu tiền không hợp lệ.");
            long total;
            try { total = checked(request.Lines.Sum(x => x.AmountVnd) + (request.LabLines?.Sum(x => x.AmountVnd) ?? 0)); }
            catch (OverflowException) { return Results.BadRequest("Số tiền không hợp lệ."); }
            if (total > 1_000_000_000_000L) return Results.BadRequest("Số tiền vượt giới hạn.");
            var actor = ((CurrentUser)context.Items["CurrentUser"]!).Id;
            var normalized = request.Lines.OrderBy(x => x.ActivityId).Select(x => new
            {
                activityId = x.ActivityId, amountVnd = x.AmountVnd
            }).ToArray();
            var labLines = request.LabLines?.OrderBy(x => x.LabOrderId).ToArray() ?? [];
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var locked = await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"TREATMENT_DOSSIER\" WHERE \"Id\" = {dossierId} FOR UPDATE", ct);
            _ = locked;
            var current = await ContextAsync(db, dossierId, ct);
            if (current is null) return Results.NotFound();
            var existing = await db.Database.SqlQuery<string>($"""
                SELECT "PayloadJson"::text AS "Value" FROM "AUDIT_LOG"
                WHERE "StorageId" = {request.RequestKey} AND "AuditType" = 'FinanceLedger'
                  AND "Target" = {dossierId.ToString()}
                """).SingleOrDefaultAsync(ct);
            if (existing is not null)
            {
                using var previous = JsonDocument.Parse(existing);
                using var proposed = JsonDocument.Parse(JsonSerializer.Serialize(normalized));
                using var proposedLabs = JsonDocument.Parse(JsonSerializer.Serialize(labLines, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                if (previous.RootElement.GetProperty("paidOn").GetString() != request.PaidOn.ToString("yyyy-MM-dd")
                    || !JsonElement.DeepEquals(previous.RootElement.GetProperty("lines"), proposed.RootElement)
                    || (previous.RootElement.TryGetProperty("labLines", out var oldLabLines)
                        ? !JsonElement.DeepEquals(oldLabLines, proposedLabs.RootElement)
                        : labLines.Length > 0))
                    return Results.Conflict("Mã yêu cầu đã được dùng cho một khoản thu khác.");
                await transaction.CommitAsync(ct);
                return Results.Ok(current);
            }
            foreach (var line in request.Lines)
            {
                var service = current.Lines.SingleOrDefault(x => x.ActivityId == line.ActivityId);
                if (service is null || line.AmountVnd > service.DueVnd)
                    return Results.Conflict("Dịch vụ hoặc số còn phải thu đã thay đổi. Vui lòng tải lại.");
            }
            var labPriority = ValidateLabPayment(current, request.Lines, labLines);
            if (!labPriority) return Results.Conflict("Cần ưu tiên đủ 30% phí labo trước khi thu cho dịch vụ khác.");
            var payload = JsonSerializer.Serialize(new { kind = "TreatmentCashPayment", paidOn = request.PaidOn,
                method = "Cash", lines = normalized, labLines },
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AUDIT_LOG" ("StorageId", "AuditType", "Action", "ActorId", "At",
                    "PatientId", "BranchId", "Target", "PayloadJson")
                VALUES ({request.RequestKey}, 'FinanceLedger', 'TreatmentCashPayment', {actor}, {DateTimeOffset.UtcNow},
                    {current.PatientId}, {current.BranchId}, {dossierId.ToString()}, {payload}::jsonb)
                ON CONFLICT ("StorageId") DO NOTHING
                """, ct);
            if (inserted != 1) return Results.Conflict("Mã yêu cầu đã được dùng.");
            var updated = await ContextAsync(db, dossierId, ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(updated);
        });
        group.MapPost("/advance-allocations", async (HttpContext context, Guid dossierId,
            ApplyTreatmentAdvance request, DcmsDbContext db, CancellationToken ct) =>
        {
            if (!IsOwner(context)) return Results.Forbid();
            var labLines = request.LabLines?.OrderBy(x => x.LabOrderId).ToArray() ?? [];
            var lines = request.Lines.OrderBy(x => x.ActivityId).ToArray();
            if (request.RequestKey == Guid.Empty || (lines.Length == 0 && labLines.Length == 0)
                || lines.Any(x => x.AmountVnd <= 0) || labLines.Any(x => x.AmountVnd <= 0)
                || lines.Select(x => x.ActivityId).Distinct().Count() != lines.Length
                || labLines.Select(x => x.LabOrderId).Distinct().Count() != labLines.Length)
                return Results.BadRequest("Khoản phân bổ cọc không hợp lệ.");
            long total;
            try { total = checked(lines.Sum(x => x.AmountVnd) + labLines.Sum(x => x.AmountVnd)); }
            catch (OverflowException) { return Results.BadRequest("Số tiền không hợp lệ."); }
            if (total > 1_000_000_000_000L) return Results.BadRequest("Số tiền vượt giới hạn.");

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"TREATMENT_DOSSIER\" WHERE \"Id\" = {dossierId} FOR UPDATE", ct);
            var current = await ContextAsync(db, dossierId, ct);
            if (current is null) return Results.NotFound();
            var old = await db.Database.SqlQuery<string>($"""
                SELECT "PayloadJson"::text AS "Value" FROM "AUDIT_LOG"
                WHERE "StorageId" = {request.RequestKey} AND "AuditType" = 'FinanceLedger'
                  AND "Target" = {dossierId.ToString()}
                """).SingleOrDefaultAsync(ct);
            if (old is not null)
            {
                using var previous = JsonDocument.Parse(old);
                using var proposed = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    kind = "AdvanceApplied", lines, labLines
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                if (!JsonElement.DeepEquals(previous.RootElement, proposed.RootElement))
                    return Results.Conflict("Mã yêu cầu đã được dùng cho một khoản phân bổ khác.");
                await transaction.CommitAsync(ct);
                return Results.Ok(current);
            }
            if (total > current.AdvanceReceivedVnd)
                return Results.Conflict("Số dư cọc không đủ. Vui lòng tải lại.");
            if (lines.Any(line => current.Lines.All(x => x.ActivityId != line.ActivityId || line.AmountVnd > x.DueVnd)))
                return Results.Conflict("Dịch vụ hoặc số còn phải thu đã thay đổi. Vui lòng tải lại.");
            if (!ValidateLabPayment(current, lines, labLines))
                return Results.Conflict("Cần ưu tiên đủ 30% phí labo trước khi phân bổ cọc cho dịch vụ khác.");
            var payload = JsonSerializer.Serialize(new { kind = "AdvanceApplied", lines, labLines },
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AUDIT_LOG" ("StorageId", "AuditType", "Action", "ActorId", "At",
                    "PatientId", "BranchId", "Target", "PayloadJson")
                VALUES ({request.RequestKey}, 'FinanceLedger', 'AdvanceApplied',
                    {((CurrentUser)context.Items["CurrentUser"]!).Id}, {DateTimeOffset.UtcNow},
                    {current.PatientId}, {current.BranchId}, {dossierId.ToString()}, {payload}::jsonb)
                ON CONFLICT ("StorageId") DO NOTHING
                """, ct);
            if (inserted != 1) return Results.Conflict("Mã yêu cầu đã được dùng.");
            var updated = await ContextAsync(db, dossierId, ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(updated);
        });
        group.MapPost("/no-show-forfeit", async (HttpContext context, Guid dossierId,
            ForfeitNoShowDeposit request, DcmsDbContext db, CancellationToken ct) =>
        {
            if (!IsOwner(context)) return Results.Forbid();
            var reason = request.Reason?.Trim();
            if (request.RequestKey == Guid.Empty || request.ExpectedVersion < 1 ||
                string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
                return Results.BadRequest("Cần nhập lý do khách không đến.");
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"TREATMENT_DOSSIER\" WHERE \"Id\" = {dossierId} FOR UPDATE", ct);
            var previous = await db.Database.SqlQuery<string>($"""
                SELECT "PayloadJson"::text AS "Value" FROM "AUDIT_LOG"
                WHERE "StorageId" = {request.RequestKey} AND "AuditType" = 'FinanceLedger'
                    AND "Target" = {dossierId.ToString()} AND "Action" = 'AdvanceForfeited'
                """).SingleOrDefaultAsync(ct);
            if (previous is not null)
            {
                using var document = JsonDocument.Parse(previous);
                if (document.RootElement.GetProperty("reason").GetString() != reason ||
                    document.RootElement.GetProperty("expectedVersion").GetInt32() != request.ExpectedVersion)
                    return Results.Conflict("Mã yêu cầu đã được dùng cho thao tác khác.");
                await transaction.CommitAsync(ct);
                return Results.Ok(new { forfeitedVnd = document.RootElement.GetProperty("amountVnd").GetInt64() });
            }
            var dossier = await db.TreatmentDossiers.SingleOrDefaultAsync(x => x.Id == dossierId, ct);
            if (dossier is null) return Results.NotFound();
            if (dossier.State != "Open" || dossier.Version != request.ExpectedVersion)
                return Results.Conflict("Phiếu đã thay đổi. Vui lòng tải lại.");
            if (await db.ClinicalActivities.AnyAsync(x => x.DossierId == dossierId && x.State == "Posted", ct)
                || await db.LabOrders.AnyAsync(x => x.DossierId == dossierId, ct))
                return Results.Conflict("Phiếu đã có điều trị hoặc labo, không thể xóa do khách không đến.");
            var current = await ContextAsync(db, dossierId, ct);
            if (current is null || current.AdvanceReceivedVnd <= 0)
                return Results.Conflict("Phiếu không có số dư cọc để ghi nhận bỏ cọc.");
            var hasOtherMoney = await db.Database.SqlQuery<int>($"""
                SELECT 1 AS "Value" FROM "AUDIT_LOG" WHERE "AuditType" = 'FinanceLedger'
                    AND "Target" = {dossierId.ToString()}
                    AND "Action" NOT IN ('AdvanceCash', 'AdvanceTransfer') LIMIT 1
                """).AnyAsync(ct);
            if (hasOtherMoney) return Results.Conflict("Phiếu đã sử dụng cọc hoặc có giao dịch dịch vụ.");
            var hasUnsettledOrder = await db.Database.SqlQuery<int>($"""
                SELECT 1 AS "Value" FROM "AUDIT_LOG" order_log
                WHERE order_log."AuditType" = 'FinancePayOsOrder'
                    AND order_log."Target" = {dossierId.ToString()}
                    AND NOT EXISTS (SELECT 1 FROM "AUDIT_LOG" payment
                        WHERE payment."AuditType" = 'FinanceLedger'
                          AND payment."Action" IN ('TreatmentTransferPayment', 'AdvanceTransfer')
                          AND payment."PayloadJson"->>'orderId' = order_log."StorageId"::text)
                LIMIT 1
                """).AnyAsync(ct);
            if (hasUnsettledOrder) return Results.Conflict("Phiếu còn QR chuyển khoản chưa đối soát.");
            var payload = JsonSerializer.Serialize(new { kind = "AdvanceForfeited", amountVnd = current.AdvanceReceivedVnd,
                expectedVersion = request.ExpectedVersion, reason }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AUDIT_LOG" ("StorageId", "AuditType", "Action", "ActorId", "At",
                    "PatientId", "BranchId", "Target", "PayloadJson")
                VALUES ({request.RequestKey}, 'FinanceLedger', 'AdvanceForfeited',
                    {((CurrentUser)context.Items["CurrentUser"]!).Id}, {DateTimeOffset.UtcNow},
                    {current.PatientId}, {current.BranchId}, {dossierId.ToString()}, {payload}::jsonb)
                ON CONFLICT ("StorageId") DO NOTHING
                """, ct);
            if (inserted != 1) return Results.Conflict("Mã yêu cầu đã được dùng.");
            dossier.State = "Deleted";
            dossier.Version++;
            dossier.UpdatedAt = DateTimeOffset.UtcNow;
            db.TreatmentEvents.Add(new DCMS.Domain.Features.Treatment.TreatmentEvent
            {
                DossierId = dossierId, ActorId = ((CurrentUser)context.Items["CurrentUser"]!).Id,
                Kind = "DossierNoShowDeleted", DossierVersion = dossier.Version,
                OccurredAt = DateTimeOffset.UtcNow, PayloadJson = payload
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { forfeitedVnd = current.AdvanceReceivedVnd });
        });
        group.MapPost("/cancel-dossier", async (HttpContext context, Guid dossierId,
            CancelTreatmentDossier request, DcmsDbContext db,
            DCMS.Application.Features.Settings.ISettingsAccess access, CancellationToken ct) =>
        {
            if (!IsOwner(context) && (context.Items["CurrentUser"] is not CurrentUser currentUser
                || !currentUser.Permissions.Contains("treatment.dossiers.update"))) return Results.Forbid();
            // Non-owners reach this endpoint, so the dossier must belong to a branch the caller is assigned to.
            var dossierBranchId = await db.TreatmentDossiers.AsNoTracking().Where(x => x.Id == dossierId && x.State != "Deleted")
                .Select(x => (int?)x.BranchId).SingleOrDefaultAsync(ct);
            if (dossierBranchId is null) return Results.NotFound();
            var scope = await access.ResolveAsync(((CurrentUser)context.Items["CurrentUser"]!).Id, ct);
            if (!scope.BranchIds.Contains(dossierBranchId.Value)) return Results.Forbid();
            var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Hủy phiếu" : request.Reason.Trim();
            if (request.RequestKey == Guid.Empty || request.ExpectedVersion < 1 || reason.Length > 1000)
                return Results.BadRequest("Cần nhập lý do hủy phiếu.");
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"TREATMENT_DOSSIER\" WHERE \"Id\" = {dossierId} FOR UPDATE", ct);
            var previous = await db.Database.SqlQuery<string>($"""
                SELECT "PayloadJson"::text AS "Value" FROM "AUDIT_LOG"
                WHERE "StorageId" = {request.RequestKey} AND "AuditType" = 'FinanceLedger'
                    AND "Target" = {dossierId.ToString()} AND "Action" = 'DossierCancelled'
                """).SingleOrDefaultAsync(ct);
            if (previous is not null)
            {
                using var document = JsonDocument.Parse(previous);
                if (document.RootElement.GetProperty("reason").GetString() != reason ||
                    document.RootElement.GetProperty("expectedVersion").GetInt32() != request.ExpectedVersion)
                    return Results.Conflict("Mã yêu cầu đã được dùng cho thao tác khác.");
                await transaction.CommitAsync(ct);
                return Results.Ok(new { status = "Cancelled" });
            }
            var dossier = await db.TreatmentDossiers.SingleOrDefaultAsync(x => x.Id == dossierId, ct);
            if (dossier is null || dossier.State == "Deleted") return Results.NotFound();
            if (dossier.State == "Cancelled" || dossier.Version != request.ExpectedVersion)
                return Results.Conflict("Phiếu đã thay đổi. Vui lòng tải lại.");
            var hasUnsettledOrder = await db.Database.SqlQuery<int>($"""
                SELECT 1 AS "Value" FROM "AUDIT_LOG" order_log
                WHERE order_log."AuditType" = 'FinancePayOsOrder'
                    AND order_log."Target" = {dossierId.ToString()}
                    AND NOT EXISTS (SELECT 1 FROM "AUDIT_LOG" payment
                        WHERE payment."AuditType" = 'FinanceLedger'
                          AND payment."Action" IN ('TreatmentTransferPayment', 'AdvanceTransfer')
                          AND payment."PayloadJson"->>'orderId' = order_log."StorageId"::text)
                LIMIT 1
                """).AnyAsync(ct);
            if (hasUnsettledOrder) return Results.Conflict("Phiếu còn QR chuyển khoản chưa đối soát.");
            var actor = ((CurrentUser)context.Items["CurrentUser"]!).Id;
            var now = DateTimeOffset.UtcNow;
            var payload = JsonSerializer.Serialize(new { kind = "DossierCancelled", reason,
                expectedVersion = request.ExpectedVersion }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AUDIT_LOG" ("StorageId", "AuditType", "Action", "ActorId", "At",
                    "PatientId", "BranchId", "Target", "PayloadJson")
                VALUES ({request.RequestKey}, 'FinanceLedger', 'DossierCancelled', {actor}, {now},
                    {dossier.PatientId}, {dossier.BranchId}, {dossierId.ToString()}, {payload}::jsonb)
                ON CONFLICT ("StorageId") DO NOTHING
                """, ct);
            if (inserted != 1) return Results.Conflict("Mã yêu cầu đã được dùng.");
            dossier.State = "Cancelled";
            dossier.ClosureOutcome = "Cancelled";
            dossier.ClosureReason = reason;
            dossier.ClosedAt = now;
            dossier.ClosedBy = actor;
            dossier.Version++;
            dossier.UpdatedAt = now;
            db.TreatmentEvents.Add(new DCMS.Domain.Features.Treatment.TreatmentEvent
            {
                DossierId = dossierId, ActorId = actor, Kind = "DossierCancelled",
                DossierVersion = dossier.Version, OccurredAt = now, PayloadJson = payload
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { status = "Cancelled" });
        });
        TreatmentPayOsEndpoints.Map(group);
    }

    internal static async Task<TreatmentPaymentContext?> ContextAsync(DcmsDbContext db, Guid dossierId, CancellationToken ct)
    {
        var dossier = await db.TreatmentDossiers.AsNoTracking()
            .Where(x => x.Id == dossierId && x.State != "Deleted" && x.State != "Cancelled")
            .Select(x => new { x.Id, x.PatientId, x.BranchId }).SingleOrDefaultAsync(ct);
        if (dossier is null) return null;
        var activities = await db.ClinicalActivities.AsNoTracking()
            .Where(x => x.DossierId == dossierId && x.PatientId == dossier.PatientId &&
                x.BranchId == dossier.BranchId && x.State == "Posted")
            .OrderBy(x => x.PerformedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        var paid = await db.Database.SqlQuery<TreatmentPaidRow>($"""
            WITH archive AS (
                SELECT a."Target" AS source, item.row
                FROM "AUDIT_LOG" a CROSS JOIN LATERAL jsonb_array_elements(a."PayloadJson"->'rows') item(row)
                WHERE a."AuditType" = 'FinanceLegacyArchive'
                  AND a."Target" IN ('TREATMENT_DOSSIER_PAYMENT', 'TREATMENT_DOSSIER_PAYMENT_LINE',
                      'PAYMENT_RECEIPT', 'PAYMENT_ALLOCATION', 'FINANCE_CHARGE',
                      'FINANCE_ADVANCE_ALLOCATION', 'FINANCE_INVOICE_LINE')
            ), sources AS (
                SELECT (l.row->>'ActivityId')::uuid AS activity_id, (l.row->>'AmountVnd')::bigint AS amount
                FROM archive l JOIN archive p ON p.source = 'TREATMENT_DOSSIER_PAYMENT'
                    AND p.row->>'Id' = l.row->>'PaymentId'
                WHERE l.source = 'TREATMENT_DOSSIER_PAYMENT_LINE'
                  AND p.row->>'DossierId' = {dossierId.ToString()}
                  AND (p.row->>'PatientId')::integer = {dossier.PatientId}
                  AND (p.row->>'BranchId')::integer = {dossier.BranchId}
                UNION ALL
                SELECT (c.row->>'ActivityId')::uuid, (a.row->>'AmountVnd')::bigint
                FROM archive a JOIN archive c ON c.source = 'FINANCE_CHARGE' AND c.row->>'Id' = a.row->>'ChargeId'
                JOIN archive r ON r.source = 'PAYMENT_RECEIPT' AND r.row->>'Id' = a.row->>'PaymentId'
                WHERE a.source = 'PAYMENT_ALLOCATION' AND r.row->>'State' = 'Posted'
                  AND c.row->>'DossierId' = {dossierId.ToString()}
                  AND (r.row->>'PatientId')::integer = {dossier.PatientId}
                UNION ALL
                SELECT (c.row->>'ActivityId')::uuid, (a.row->>'AmountVnd')::bigint
                FROM archive a JOIN archive i ON i.source = 'FINANCE_INVOICE_LINE' AND i.row->>'Id' = a.row->>'InvoiceLineId'
                JOIN archive c ON c.source = 'FINANCE_CHARGE' AND c.row->>'Id' = i.row->>'ChargeId'
                WHERE a.source = 'FINANCE_ADVANCE_ALLOCATION'
                  AND c.row->>'DossierId' = {dossierId.ToString()}
                UNION ALL
                SELECT (line.row->>'activityId')::uuid, (line.row->>'amountVnd')::bigint
                  - COALESCE((SELECT sum((priority.row->>'amountVnd')::bigint)
                      FROM jsonb_array_elements(COALESCE(entry."PayloadJson"->'labPriority', '[]'::jsonb)) priority(row)
                      JOIN "LAB_ORDER" lab ON lab."Id" = (priority.row->>'labOrderId')::uuid
                      WHERE lab."ActivityId" = (line.row->>'activityId')::uuid), 0)
                FROM "AUDIT_LOG" entry
                CROSS JOIN LATERAL jsonb_array_elements(entry."PayloadJson"->'lines') line(row)
                WHERE entry."AuditType" = 'FinanceLedger'
                  AND entry."Action" IN ('TreatmentCashPayment', 'TreatmentTransferPayment', 'AdvanceApplied')
                  AND entry."Target" = {dossierId.ToString()}
                  AND entry."PatientId" = {dossier.PatientId}
                  AND entry."BranchId" = {dossier.BranchId}
            )
            SELECT activity_id AS "ActivityId", sum(amount)::bigint AS "PaidVnd"
            FROM sources WHERE activity_id IS NOT NULL GROUP BY activity_id
            """).ToArrayAsync(ct);
        var paidByActivity = paid.ToDictionary(x => x.ActivityId, x => x.PaidVnd);
        var lines = activities.Select(activity =>
        {
            var gross = checked((long)Math.Round(activity.Quantity * activity.PriceSnapshotVnd, MidpointRounding.AwayFromZero));
            var price = Math.Max(0, gross - activity.DiscountSnapshotVnd);
            var paidVnd = Math.Clamp(paidByActivity.GetValueOrDefault(activity.Id), 0, price);
            return new TreatmentPaymentLine(activity.Id, activity.PerformedAt,
                activity.ServiceNameSnapshot ?? "Dịch vụ điều trị", price, paidVnd, price - paidVnd);
        }).Where(x => x.PriceVnd > 0).ToArray();
        var activityIds = activities.Select(x => x.Id).ToArray();
        var labs = await db.LabOrders.AsNoTracking().Where(x => x.DossierId == dossierId
                && x.PatientId == dossier.PatientId && x.BranchId == dossier.BranchId
                && activityIds.Contains(x.ActivityId) && x.TotalFeeVnd > 0)
            .OrderBy(x => x.OrderedOn).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.ActivityId, x.WorkName, x.TotalFeeVnd, x.PaidVnd }).ToArrayAsync(ct);
        var reserved = await db.Database.SqlQuery<TreatmentPaidRow>($"""
            SELECT (item.row->>'labOrderId')::uuid AS "ActivityId", sum((item.row->>'amountVnd')::bigint)::bigint AS "PaidVnd"
            FROM "AUDIT_LOG" ledger
            CROSS JOIN LATERAL jsonb_array_elements(COALESCE(ledger."PayloadJson"->'labPriority', '[]'::jsonb)
                || COALESCE(ledger."PayloadJson"->'labLines', '[]'::jsonb)) item(row)
            WHERE ledger."AuditType" = 'FinanceLedger' AND ledger."Target" = {dossierId.ToString()}
            GROUP BY (item.row->>'labOrderId')::uuid
            """).ToDictionaryAsync(x => x.ActivityId, x => x.PaidVnd, ct);
        var labFees = labs.Select(x => {
            var target = checked((x.TotalFeeVnd * 3 + 9) / 10);
            var allocated = reserved.GetValueOrDefault(x.Id);
            var patientPaid = Math.Clamp(allocated, 0, x.TotalFeeVnd);
            return new TreatmentLabFee(x.Id, x.ActivityId, x.WorkName, x.TotalFeeVnd, patientPaid,
                x.TotalFeeVnd - patientPaid, x.PaidVnd, target, Math.Max(0, target - patientPaid));
        }).ToArray();
        var advance = await db.Database.SqlQuery<long>($"""
            SELECT COALESCE((SELECT sum((item.row->>'AmountVnd')::bigint)
                FROM "AUDIT_LOG" a CROSS JOIN LATERAL jsonb_array_elements(a."PayloadJson"->'rows') item(row)
                WHERE a."AuditType" = 'FinanceLegacyArchive' AND a."Target" = 'PAYMENT_RECEIPT'
                  AND item.row->>'Purpose' = 'Advance' AND item.row->>'State' = 'Posted'
                  AND item.row->>'DossierId' = {dossierId.ToString()}), 0)
              - COALESCE((SELECT sum((allocation.row->>'AmountVnd')::bigint)
                  FROM "AUDIT_LOG" a CROSS JOIN LATERAL jsonb_array_elements(a."PayloadJson"->'rows') allocation(row)
                  JOIN "AUDIT_LOG" i ON i."AuditType" = 'FinanceLegacyArchive'
                      AND i."Target" = 'FINANCE_INVOICE_LINE'
                  CROSS JOIN LATERAL jsonb_array_elements(i."PayloadJson"->'rows') invoice(row)
                  JOIN "AUDIT_LOG" c ON c."AuditType" = 'FinanceLegacyArchive'
                      AND c."Target" = 'FINANCE_CHARGE'
                  CROSS JOIN LATERAL jsonb_array_elements(c."PayloadJson"->'rows') charge(row)
                  WHERE a."AuditType" = 'FinanceLegacyArchive' AND a."Target" = 'FINANCE_ADVANCE_ALLOCATION'
                    AND invoice.row->>'Id' = allocation.row->>'InvoiceLineId'
                    AND charge.row->>'Id' = invoice.row->>'ChargeId'
                    AND charge.row->>'DossierId' = {dossierId.ToString()}), 0)
              + COALESCE((SELECT sum((entry."PayloadJson"->>'amountVnd')::bigint
                  - COALESCE((SELECT sum((priority.row->>'amountVnd')::bigint)
                      FROM jsonb_array_elements(COALESCE(entry."PayloadJson"->'labPriority', '[]'::jsonb)
                          || COALESCE(entry."PayloadJson"->'labLines', '[]'::jsonb)) priority(row)), 0))
                FROM "AUDIT_LOG" entry WHERE entry."AuditType" = 'FinanceLedger'
                  AND entry."Action" IN ('AdvanceCash', 'AdvanceTransfer')
                  AND entry."Target" = {dossierId.ToString()}), 0)
              - COALESCE((SELECT sum(COALESCE((
                    SELECT sum((line.row->>'amountVnd')::bigint)
                    FROM jsonb_array_elements(COALESCE(entry."PayloadJson"->'lines', '[]'::jsonb)
                        || COALESCE(entry."PayloadJson"->'labLines', '[]'::jsonb)) line(row)), 0))
                  FROM "AUDIT_LOG" entry
                  WHERE entry."AuditType" = 'FinanceLedger'
                    AND entry."Action" = 'AdvanceApplied'
                    AND entry."Target" = {dossierId.ToString()}), 0) AS "Value"
            """).SingleAsync(ct);
        return new(dossier.Id, dossier.PatientId, dossier.BranchId, lines, advance, labFees,
            checked(lines.Sum(x => x.PriceVnd) + labFees.Sum(x => x.TotalFeeVnd)), labFees.Sum(x => x.PaidVnd));
    }

    internal static bool ValidateLabPayment(TreatmentPaymentContext context,
        IReadOnlyList<TreatmentPaymentInput> lines, IReadOnlyList<TreatmentLabAllocation> labLines)
    {
        if (labLines.Any(x => x.AmountVnd <= 0 ||
            context.LabFees.All(lab => lab.LabOrderId != x.LabOrderId || x.AmountVnd > lab.DueVnd))) return false;
        if (labLines.Select(x => x.LabOrderId).Distinct().Count() != labLines.Count) return false;
        return lines.Count == 0 || context.LabFees.All(lab =>
            lab.PaidVnd + labLines.Where(x => x.LabOrderId == lab.LabOrderId).Sum(x => x.AmountVnd)
                >= lab.Minimum30PercentVnd);
    }

    internal static TreatmentLabAllocation[] AllocateAdvanceToLab(TreatmentPaymentContext context, long amount)
    {
        var result = new List<TreatmentLabAllocation>();
        foreach (var lab in context.LabFees)
        {
            var applied = Math.Min(amount, lab.PriorityRemainingVnd);
            if (applied > 0) result.Add(new(lab.LabOrderId, applied));
            amount -= applied;
        }
        return result.ToArray();
    }
}

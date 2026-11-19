using System.Text.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Api.Features.Finance;

public sealed class FinanceStatisticsResult
{
    public long ServiceAmountVnd { get; set; }
    public long ActualReceivedVnd { get; set; }
    public long AdvanceReceivedVnd { get; set; }
    public long RemainingReceivableVnd { get; set; }
    public long MaterialPurchaseVnd { get; set; }
    public long ProfitVnd { get; set; }
    public int ProfitableDossierCount { get; set; }
    public List<FinanceOutstandingDossier> OutstandingDossiers { get; set; } = [];
    public List<FinanceServiceDossier> ServiceDossiers { get; set; } = [];
    public List<FinancePaymentEntry> ActualPayments { get; set; } = [];
    public List<FinancePaymentEntry> AdvanceEntries { get; set; } = [];
    public List<FinanceMaterialPurchaseEntry> MaterialPurchases { get; set; } = [];
    public List<FinanceProfitEntry> ProfitEntries { get; set; } = [];
}

public sealed record FinanceOutstandingDossier(Guid DossierId, string DossierCode, int PatientId,
    string PatientName, string PatientCode, DateOnly StartedOn, long TotalVnd, long PaidVnd, long DueVnd,
    int Version, bool CanForfeitDeposit);

public sealed record FinanceServiceDossier(Guid DossierId, string DossierCode, int PatientId,
    string PatientName, string PatientCode, DateOnly StartedOn, long TotalVnd, long PaidVnd, long DueVnd,
    string State);

public sealed record FinancePaymentEntry(string Id, Guid? DossierId, string DossierCode, int PatientId,
    string PatientName, string PatientCode, DateTimeOffset PaidAt, string Method, string Note,
    long AmountVnd, bool CanOpenDossier);

public sealed record FinanceMaterialPurchaseEntry(Guid Id, DateOnly EnteredOn, string MaterialName,
    string CategoryName, string? SupplierName, decimal Quantity, string Unit, long UnitPriceVnd,
    long TotalVnd, long PaidVnd);

public sealed record FinanceProfitEntry(string Id, Guid? DossierId, string DossierCode, int PatientId,
    string PatientName, string PatientCode, DateOnly Date, long RevenueVnd, long CostVnd,
    long ProfitVnd, bool CanOpenDossier);

public sealed class FinanceDossierReceiptRow
{
    public Guid DossierId { get; set; }
    public long TreatmentVnd { get; set; }
    public long AdvanceVnd { get; set; }
    public long AllocatedVnd { get; set; }
}

public sealed class FinanceLedgerAuditRow
{
    public Guid Id { get; set; }
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public int PatientId { get; set; }
    public DateTimeOffset At { get; set; }
    public string PayloadJson { get; set; } = "{}";
}

public sealed class FinanceLegacyEntryRow
{
    public string Id { get; set; } = "";
    public Guid DossierId { get; set; }
    public string Kind { get; set; } = "";
    public string Method { get; set; } = "";
    public DateTimeOffset PaidAt { get; set; }
    public long AmountVnd { get; set; }
}

public static class FinanceStatisticsEndpoints
{
    public static void MapFinanceStatisticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/finance/statistics", async (HttpContext context, DateOnly? from, DateOnly? to, int? branchId,
            DcmsDbContext db, CancellationToken ct) =>
        {
            // finance.statistics.read defaults to the owner only; an access group may grant it to anyone.
            if (context.Items["CurrentUser"] is not CurrentUser user ||
                !user.Permissions.Contains("finance.statistics.read")) return Results.Forbid();
            if (from > to || branchId is < 1) return Results.BadRequest("Khoảng ngày hoặc chi nhánh không hợp lệ.");

            // Select a cohort by dossier opening date, then use lifetime figures for each dossier.
            // This keeps billed, received and remaining amounts on the same basis.
            var dossiers = await db.TreatmentDossiers.AsNoTracking()
                .Where(x => x.State != "Deleted" && (!branchId.HasValue || x.BranchId == branchId)
                    && (!from.HasValue || x.StartedOn >= from) && (!to.HasValue || x.StartedOn <= to))
                .Select(x => new { x.Id, x.Code, x.PatientId, x.StartedOn, x.State, x.Version }).ToArrayAsync(ct);
            var deletedDossiers = await db.TreatmentDossiers.AsNoTracking()
                .Where(x => x.State == "Deleted" && (!branchId.HasValue || x.BranchId == branchId)
                    && (!from.HasValue || x.StartedOn >= from) && (!to.HasValue || x.StartedOn <= to))
                .Select(x => new { x.Id, x.Code, x.PatientId, x.StartedOn }).ToArrayAsync(ct);
            var deletedIds = deletedDossiers.Select(x => x.Id.ToString()).ToArray();
            var forfeited = deletedIds.Length == 0 ? 0 : await db.Database.SqlQuery<long>($"""
                SELECT COALESCE(sum(("PayloadJson"->>'amountVnd')::bigint), 0)::bigint AS "Value"
                FROM "AUDIT_LOG" WHERE "AuditType" = 'FinanceLedger'
                    AND "Action" = 'AdvanceForfeited' AND "Target" = ANY({deletedIds})
                """).SingleAsync(ct);
            // Purchases are clinic-wide: MATERIAL_PURCHASE has no branch or dossier link.
            // They follow the entered-on date filter, but cannot be attributed to one branch.
            var purchaseRows = await db.MaterialPurchases.AsNoTracking()
                .Where(x => !x.IsDeleted && (!from.HasValue || x.EnteredOn >= from)
                    && (!to.HasValue || x.EnteredOn <= to))
                .OrderByDescending(x => x.EnteredOn).ThenByDescending(x => x.CreatedAt)
                .Select(x => new FinanceMaterialPurchaseEntry(x.Id, x.EnteredOn, x.Material.Name,
                    x.Material.Category.Name, x.Supplier != null ? x.Supplier.Name : null, x.Quantity, x.Unit, x.UnitPriceVnd, x.TotalVnd, x.PaidVnd))
                .ToListAsync(ct);
            var purchaseTotal = purchaseRows.Sum(x => x.TotalVnd);

            var allPatientIds = dossiers.Select(x => x.PatientId)
                .Concat(deletedDossiers.Select(x => x.PatientId)).Distinct().ToArray();
            var patients = allPatientIds.Length == 0
                ? new Dictionary<int, (string FullName, string PatientCode)>()
                : (await db.Patients.AsNoTracking().Where(x => allPatientIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.FullName, x.PatientCode }).ToArrayAsync(ct))
                    .ToDictionary(x => x.Id, x => (x.FullName, x.PatientCode));

            var result = new FinanceStatisticsResult
            {
                MaterialPurchaseVnd = purchaseTotal,
                ActualReceivedVnd = forfeited,
                MaterialPurchases = purchaseRows
            };

            if (deletedIds.Length > 0)
            {
                var forfeitedRows = await db.Database.SqlQuery<FinanceLedgerAuditRow>($"""
                    SELECT "StorageId" AS "Id", "Action", "Target", COALESCE("PatientId", 0) AS "PatientId",
                        "At", "PayloadJson"::text AS "PayloadJson"
                    FROM "AUDIT_LOG" WHERE "AuditType" = 'FinanceLedger'
                        AND "Action" = 'AdvanceForfeited' AND "Target" = ANY({deletedIds})
                    ORDER BY "At" DESC
                    """).ToArrayAsync(ct);
                var deletedById = deletedDossiers.ToDictionary(x => x.Id.ToString());
                foreach (var row in forfeitedRows)
                {
                    using var doc = JsonDocument.Parse(row.PayloadJson);
                    var amount = doc.RootElement.TryGetProperty("amountVnd", out var a) ? a.GetInt64() : 0;
                    var reason = doc.RootElement.TryGetProperty("reason", out var r) ? r.GetString() : null;
                    deletedById.TryGetValue(row.Target, out var del);
                    var pid = del?.PatientId ?? row.PatientId;
                    patients.TryGetValue(pid, out var pt);
                    result.ActualPayments.Add(new FinancePaymentEntry(row.Id.ToString(), del?.Id,
                        del?.Code ?? "Đã xóa", pid, pt.FullName ?? "", pt.PatientCode ?? "",
                        row.At, "Khách bỏ cọc", string.IsNullOrWhiteSpace(reason) ? "Khách không đến, bỏ cọc" : reason,
                        amount, false));
                }
            }

            if (dossiers.Length == 0)
            {
                if (purchaseTotal > 0)
                {
                    result.ProfitEntries.Add(new FinanceProfitEntry("material-purchases", null, "Nhập dụng cụ vật liệu",
                        0, "Toàn phòng khám", "", to ?? from ?? DateOnly.FromDateTime(DateTime.UtcNow),
                        0, purchaseTotal, -purchaseTotal, false));
                }
                result.ProfitVnd = checked(-purchaseTotal);
                return Results.Ok(result);
            }
            var ids = dossiers.Select(x => x.Id).ToArray();
            var idStrings = ids.Select(x => x.ToString()).ToArray();

            // The legacy finance rows were archived in AUDIT_LOG by the 33-table migration.
            // A PayOS transfer appears in FinanceLedger only after it is confirmed.
            var receipts = await db.Database.SqlQuery<FinanceDossierReceiptRow>($"""
                WITH legacy AS (
                    SELECT a."Target" AS source, item.row
                    FROM "AUDIT_LOG" a
                    CROSS JOIN LATERAL jsonb_array_elements(a."PayloadJson"->'rows') item(row)
                    WHERE a."AuditType" = 'FinanceLegacyArchive'
                      AND a."Target" IN ('TREATMENT_DOSSIER_PAYMENT', 'TREATMENT_DOSSIER_PAYMENT_LINE',
                          'PAYMENT_RECEIPT', 'PAYMENT_ALLOCATION', 'FINANCE_CHARGE',
                          'FINANCE_ADVANCE_ALLOCATION', 'FINANCE_INVOICE_LINE')
                ), money AS (
                    SELECT (p.row->>'DossierId')::uuid AS dossier_id,
                        (l.row->>'AmountVnd')::bigint AS treatment, 0::bigint AS advance,
                        (l.row->>'AmountVnd')::bigint AS allocated
                    FROM legacy l JOIN legacy p ON p.source = 'TREATMENT_DOSSIER_PAYMENT'
                        AND p.row->>'Id' = l.row->>'PaymentId'
                    WHERE l.source = 'TREATMENT_DOSSIER_PAYMENT_LINE'
                    UNION ALL
                    SELECT (c.row->>'DossierId')::uuid, (a.row->>'AmountVnd')::bigint, 0::bigint,
                        (a.row->>'AmountVnd')::bigint
                    FROM legacy a JOIN legacy r ON r.source = 'PAYMENT_RECEIPT'
                        AND r.row->>'Id' = a.row->>'PaymentId'
                    JOIN legacy c ON c.source = 'FINANCE_CHARGE' AND c.row->>'Id' = a.row->>'ChargeId'
                    WHERE a.source = 'PAYMENT_ALLOCATION' AND r.row->>'State' = 'Posted'
                      AND r.row->>'Purpose' <> 'Advance'
                    UNION ALL
                    SELECT (c.row->>'DossierId')::uuid, 0::bigint,
                        -(a.row->>'AmountVnd')::bigint,
                        (a.row->>'AmountVnd')::bigint
                    FROM legacy a JOIN legacy i ON i.source = 'FINANCE_INVOICE_LINE'
                        AND i.row->>'Id' = a.row->>'InvoiceLineId'
                    JOIN legacy c ON c.source = 'FINANCE_CHARGE' AND c.row->>'Id' = i.row->>'ChargeId'
                    WHERE a.source = 'FINANCE_ADVANCE_ALLOCATION'
                    UNION ALL
                    SELECT (r.row->>'DossierId')::uuid, (r.row->>'AmountVnd')::bigint,
                        (r.row->>'AmountVnd')::bigint, 0::bigint
                    FROM legacy r WHERE r.source = 'PAYMENT_RECEIPT'
                      AND r.row->>'State' = 'Posted' AND r.row->>'Purpose' = 'Advance'
                    UNION ALL
                    SELECT ledger."Target"::uuid,
                        COALESCE((SELECT sum((line.row->>'amountVnd')::bigint)
                            FROM jsonb_array_elements(COALESCE(ledger."PayloadJson"->'lines', '[]'::jsonb)
                                || COALESCE(ledger."PayloadJson"->'labLines', '[]'::jsonb)) line(row)), 0),
                        0::bigint,
                        COALESCE((SELECT sum((line.row->>'amountVnd')::bigint)
                            FROM jsonb_array_elements(COALESCE(ledger."PayloadJson"->'lines', '[]'::jsonb)
                                || COALESCE(ledger."PayloadJson"->'labLines', '[]'::jsonb)) line(row)), 0)
                    FROM "AUDIT_LOG" ledger WHERE ledger."AuditType" = 'FinanceLedger'
                      AND ledger."Action" IN ('TreatmentCashPayment', 'TreatmentTransferPayment')
                    UNION ALL
                    SELECT ledger."Target"::uuid,
                        (ledger."PayloadJson"->>'amountVnd')::bigint,
                        (ledger."PayloadJson"->>'amountVnd')::bigint -
                        COALESCE((SELECT sum((line.row->>'amountVnd')::bigint)
                            FROM jsonb_array_elements(COALESCE(ledger."PayloadJson"->'labPriority', '[]'::jsonb)
                                || COALESCE(ledger."PayloadJson"->'labLines', '[]'::jsonb)) line(row)), 0),
                        COALESCE((SELECT sum((line.row->>'amountVnd')::bigint)
                            FROM jsonb_array_elements(COALESCE(ledger."PayloadJson"->'labPriority', '[]'::jsonb)
                                || COALESCE(ledger."PayloadJson"->'labLines', '[]'::jsonb)) line(row)), 0)
                    FROM "AUDIT_LOG" ledger WHERE ledger."AuditType" = 'FinanceLedger'
                      AND ledger."Action" IN ('AdvanceCash', 'AdvanceTransfer')
                    UNION ALL
                    SELECT ledger."Target"::uuid, 0::bigint,
                        -COALESCE((SELECT sum((line.row->>'amountVnd')::bigint)
                            FROM jsonb_array_elements(COALESCE(ledger."PayloadJson"->'lines', '[]'::jsonb)
                                || COALESCE(ledger."PayloadJson"->'labLines', '[]'::jsonb)) line(row)), 0),
                        COALESCE((SELECT sum((line.row->>'amountVnd')::bigint)
                            FROM jsonb_array_elements(COALESCE(ledger."PayloadJson"->'lines', '[]'::jsonb)
                                || COALESCE(ledger."PayloadJson"->'labLines', '[]'::jsonb)) line(row)), 0)
                    FROM "AUDIT_LOG" ledger WHERE ledger."AuditType" = 'FinanceLedger'
                      AND ledger."Action" = 'AdvanceApplied'
                )
                SELECT money.dossier_id AS "DossierId", sum(money.treatment)::bigint AS "TreatmentVnd",
                    sum(money.advance)::bigint AS "AdvanceVnd",
                    sum(money.allocated)::bigint AS "AllocatedVnd"
                FROM money JOIN "TREATMENT_DOSSIER" dossier ON dossier."Id" = money.dossier_id
                WHERE dossier."Id" = ANY({ids})
                GROUP BY money.dossier_id
                """).ToArrayAsync(ct);
            var received = receipts.ToDictionary(x => x.DossierId);

            var activities = await db.ClinicalActivities.AsNoTracking()
                .Where(x => ids.Contains(x.DossierId))
                .Select(x => new { x.Id, x.DossierId, x.ItemId, x.ServiceNameSnapshot, x.Quantity, x.PriceSnapshotVnd,
                    x.DiscountSnapshotVnd }).ToArrayAsync(ct);
            var postedIds = activities.Select(x => x.Id).ToArray();
            var completedQuantity = activities
                .Where(x => x.ItemId.HasValue)
                .GroupBy(x => x.ItemId!.Value).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
            var items = await db.TreatmentItems.AsNoTracking()
                .Where(x => ids.Contains(x.DossierId))
                .ToArrayAsync(ct);
            var labs = await (
                from x in db.LabOrders.AsNoTracking()
                join a in db.ClinicalActivities.AsNoTracking() on x.ActivityId equals a.Id
                where ids.Contains(a.DossierId) && postedIds.Contains(x.ActivityId)
                select new { x.Id, a.DossierId, x.WorkName, x.TotalFeeVnd }
            ).ToArrayAsync(ct);

            var activityNames = activities.ToDictionary(x => x.Id, x => x.ServiceNameSnapshot ?? "Dịch vụ điều trị");
            var labNames = labs.ToDictionary(x => x.Id, x => $"Labo: {x.WorkName}");
            var dossierById = dossiers.ToDictionary(x => x.Id);

            var ledgerEntries = await db.Database.SqlQuery<FinanceLedgerAuditRow>($"""
                SELECT "StorageId" AS "Id", "Action", "Target", COALESCE("PatientId", 0) AS "PatientId",
                    "At", "PayloadJson"::text AS "PayloadJson"
                FROM "AUDIT_LOG"
                WHERE "AuditType" = 'FinanceLedger'
                  AND "Action" IN ('TreatmentCashPayment', 'TreatmentTransferPayment', 'AdvanceCash', 'AdvanceTransfer', 'AdvanceApplied')
                  AND "Target" = ANY({idStrings})
                ORDER BY "At" DESC, "StorageId" DESC
                """).ToArrayAsync(ct);

            foreach (var entry in ledgerEntries)
            {
                if (!Guid.TryParse(entry.Target, out var dossierId) || !dossierById.TryGetValue(dossierId, out var d))
                    continue;
                patients.TryGetValue(d.PatientId, out var pt);
                using var doc = JsonDocument.Parse(entry.PayloadJson);
                var root = doc.RootElement;
                var lineNames = new List<string>();
                long linesTotal = 0;
                if (root.TryGetProperty("lines", out var linesEl) && linesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var line in linesEl.EnumerateArray())
                    {
                        var amt = line.TryGetProperty("amountVnd", out var a) ? a.GetInt64() : 0;
                        linesTotal += amt;
                        if (line.TryGetProperty("activityId", out var actEl) && Guid.TryParse(actEl.GetString(), out var actId)
                            && activityNames.TryGetValue(actId, out var sName))
                            lineNames.Add(sName);
                    }
                }
                long labTotal = 0;
                foreach (var prop in new[] { "labLines", "labPriority" })
                {
                    if (root.TryGetProperty(prop, out var labsEl) && labsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var lab in labsEl.EnumerateArray())
                        {
                            var amt = lab.TryGetProperty("amountVnd", out var a) ? a.GetInt64() : 0;
                            labTotal += amt;
                            if (lab.TryGetProperty("labOrderId", out var labIdEl) && Guid.TryParse(labIdEl.GetString(), out var labId)
                                && labNames.TryGetValue(labId, out var lName))
                                lineNames.Add(lName);
                        }
                    }
                }
                var detailText = lineNames.Count > 0 ? string.Join(", ", lineNames.Distinct()) : "Thanh toán điều trị";
                if (entry.Action is "TreatmentCashPayment" or "TreatmentTransferPayment")
                {
                    var amount = linesTotal + labTotal;
                    if (amount > 0)
                    {
                        result.ActualPayments.Add(new FinancePaymentEntry(entry.Id.ToString(), d.Id, d.Code,
                            d.PatientId, pt.FullName ?? "", pt.PatientCode ?? "", entry.At,
                            entry.Action == "TreatmentCashPayment" ? "Tiền mặt" : "Chuyển khoản",
                            detailText, amount, true));
                    }
                }
                else if (entry.Action is "AdvanceCash" or "AdvanceTransfer")
                {
                    var amount = root.TryGetProperty("amountVnd", out var a) ? a.GetInt64() : 0;
                    var methodLabel = entry.Action == "AdvanceCash" ? "Tiền cọc (tiền mặt)" : "Tiền cọc (chuyển khoản)";
                    if (amount > 0)
                    {
                        result.ActualPayments.Add(new FinancePaymentEntry(entry.Id.ToString(), d.Id, d.Code,
                            d.PatientId, pt.FullName ?? "", pt.PatientCode ?? "", entry.At, methodLabel,
                            labTotal > 0 ? $"Nhận tiền cọc (ưu tiên labo: {detailText})" : "Nhận tiền cọc",
                            amount, true));
                        result.AdvanceEntries.Add(new FinancePaymentEntry(entry.Id.ToString(), d.Id, d.Code,
                            d.PatientId, pt.FullName ?? "", pt.PatientCode ?? "", entry.At, methodLabel,
                            "Nhận tiền cọc", amount, true));
                    }
                    if (labTotal > 0)
                    {
                        result.AdvanceEntries.Add(new FinancePaymentEntry($"{entry.Id}-lab", d.Id, d.Code,
                            d.PatientId, pt.FullName ?? "", pt.PatientCode ?? "", entry.At, "Khấu trừ cọc cho Labo",
                            detailText, -labTotal, true));
                    }
                }
                else if (entry.Action == "AdvanceApplied")
                {
                    var amount = linesTotal + labTotal;
                    if (amount > 0)
                    {
                        result.AdvanceEntries.Add(new FinancePaymentEntry(entry.Id.ToString(), d.Id, d.Code,
                            d.PatientId, pt.FullName ?? "", pt.PatientCode ?? "", entry.At,
                            "Dùng cọc thanh toán", detailText, -amount, true));
                    }
                }
            }

            var legacyEntries = await db.Database.SqlQuery<FinanceLegacyEntryRow>($"""
                WITH legacy AS (
                    SELECT a."Target" AS source, a."At" AS archived_at, item.row
                    FROM "AUDIT_LOG" a
                    CROSS JOIN LATERAL jsonb_array_elements(a."PayloadJson"->'rows') item(row)
                    WHERE a."AuditType" = 'FinanceLegacyArchive'
                      AND a."Target" IN ('TREATMENT_DOSSIER_PAYMENT', 'TREATMENT_DOSSIER_PAYMENT_LINE',
                          'PAYMENT_RECEIPT', 'PAYMENT_ALLOCATION', 'FINANCE_CHARGE',
                          'FINANCE_ADVANCE_ALLOCATION', 'FINANCE_INVOICE_LINE')
                ), entries AS (
                    SELECT COALESCE(p.row->>'Id', gen_random_uuid()::text) AS "Id",
                        (p.row->>'DossierId')::uuid AS "DossierId",
                        'Treatment' AS "Kind",
                        COALESCE(p.row->>'Method', 'Cash') AS "Method",
                        COALESCE((p.row->>'PaidOn')::timestamptz, p.archived_at) AS "PaidAt",
                        sum((l.row->>'AmountVnd')::bigint)::bigint AS "AmountVnd"
                    FROM legacy l JOIN legacy p ON p.source = 'TREATMENT_DOSSIER_PAYMENT'
                        AND p.row->>'Id' = l.row->>'PaymentId'
                    WHERE l.source = 'TREATMENT_DOSSIER_PAYMENT_LINE'
                    GROUP BY p.row->>'Id', p.row->>'DossierId', p.row->>'Method', p.row->>'PaidOn', p.archived_at
                    UNION ALL
                    SELECT COALESCE(r.row->>'Id', gen_random_uuid()::text),
                        (c.row->>'DossierId')::uuid,
                        'Treatment',
                        COALESCE(r.row->>'Method', 'Cash'),
                        COALESCE((r.row->>'ConfirmedAt')::timestamptz, (r.row->>'CreatedAt')::timestamptz, r.archived_at),
                        sum((a.row->>'AmountVnd')::bigint)::bigint
                    FROM legacy a JOIN legacy r ON r.source = 'PAYMENT_RECEIPT'
                        AND r.row->>'Id' = a.row->>'PaymentId'
                    JOIN legacy c ON c.source = 'FINANCE_CHARGE' AND c.row->>'Id' = a.row->>'ChargeId'
                    WHERE a.source = 'PAYMENT_ALLOCATION' AND r.row->>'State' = 'Posted'
                      AND r.row->>'Purpose' <> 'Advance'
                    GROUP BY r.row->>'Id', c.row->>'DossierId', r.row->>'Method', r.row->>'ConfirmedAt', r.row->>'CreatedAt', r.archived_at
                    UNION ALL
                    SELECT COALESCE(r.row->>'Id', gen_random_uuid()::text),
                        (r.row->>'DossierId')::uuid,
                        'AdvanceReceipt',
                        COALESCE(r.row->>'Method', 'Cash'),
                        COALESCE((r.row->>'ConfirmedAt')::timestamptz, (r.row->>'CreatedAt')::timestamptz, r.archived_at),
                        (r.row->>'AmountVnd')::bigint
                    FROM legacy r WHERE r.source = 'PAYMENT_RECEIPT'
                      AND r.row->>'State' = 'Posted' AND r.row->>'Purpose' = 'Advance'
                    UNION ALL
                    SELECT COALESCE(a.row->>'Id', gen_random_uuid()::text),
                        (c.row->>'DossierId')::uuid,
                        'AdvanceApplied',
                        'Advance',
                        COALESCE((a.row->>'CreatedAt')::timestamptz, a.archived_at),
                        -(a.row->>'AmountVnd')::bigint
                    FROM legacy a JOIN legacy i ON i.source = 'FINANCE_INVOICE_LINE'
                        AND i.row->>'Id' = a.row->>'InvoiceLineId'
                    JOIN legacy c ON c.source = 'FINANCE_CHARGE' AND c.row->>'Id' = i.row->>'ChargeId'
                    WHERE a.source = 'FINANCE_ADVANCE_ALLOCATION'
                )
                SELECT "Id", "DossierId", "Kind", "Method", "PaidAt", "AmountVnd"
                FROM entries WHERE "DossierId" = ANY({ids})
                ORDER BY "PaidAt" DESC
                """).ToArrayAsync(ct);

            foreach (var leg in legacyEntries)
            {
                if (!dossierById.TryGetValue(leg.DossierId, out var d)) continue;
                patients.TryGetValue(d.PatientId, out var pt);
                var methodText = leg.Method == "BankTransfer" || leg.Method == "Transfer" ? "Chuyển khoản" : "Tiền mặt";
                if (leg.Kind == "Treatment")
                {
                    result.ActualPayments.Add(new FinancePaymentEntry($"legacy-{leg.Id}", d.Id, d.Code,
                        d.PatientId, pt.FullName ?? "", pt.PatientCode ?? "", leg.PaidAt,
                        methodText, "Thanh toán dịch vụ điều trị", leg.AmountVnd, true));
                }
                else if (leg.Kind == "AdvanceReceipt")
                {
                    result.ActualPayments.Add(new FinancePaymentEntry($"legacy-adv-{leg.Id}", d.Id, d.Code,
                        d.PatientId, pt.FullName ?? "", pt.PatientCode ?? "", leg.PaidAt,
                        $"Tiền cọc ({methodText.ToLowerInvariant()})", "Nhận tiền cọc", leg.AmountVnd, true));
                    result.AdvanceEntries.Add(new FinancePaymentEntry($"legacy-adv-{leg.Id}", d.Id, d.Code,
                        d.PatientId, pt.FullName ?? "", pt.PatientCode ?? "", leg.PaidAt,
                        $"Tiền cọc ({methodText.ToLowerInvariant()})", "Nhận tiền cọc", leg.AmountVnd, true));
                }
                else if (leg.Kind == "AdvanceApplied")
                {
                    result.AdvanceEntries.Add(new FinancePaymentEntry($"legacy-app-{leg.Id}", d.Id, d.Code,
                        d.PatientId, pt.FullName ?? "", pt.PatientCode ?? "", leg.PaidAt,
                        "Dùng cọc thanh toán", "Phân bổ tiền cọc", leg.AmountVnd, true));
                }
            }

            foreach (var dossier in dossiers)
            {
                var posted = activities.Where(x => x.DossierId == dossier.Id).Sum(x =>
                    Math.Max(0, TreatmentPlanRules.Gross(x.Quantity,
                        checked((long)x.PriceSnapshotVnd)) - x.DiscountSnapshotVnd));
                var dossierItems = items.Where(x => x.DossierId == dossier.Id).ToArray();
                var planned = dossierItems.Sum(item =>
                {
                    var remaining = Math.Max(0, item.Quantity - completedQuantity.GetValueOrDefault(item.Id));
                    if (remaining == 0) return 0L;
                    var gross = TreatmentPlanRules.Gross(remaining, item.AgreedUnitPriceVnd);
                    if (item.DiscountType is not ("Fixed" or "Percent") || item.DiscountValue is not > 0)
                        return gross;
                    var fullGross = TreatmentPlanRules.Gross(item.Quantity, item.AgreedUnitPriceVnd);
                    var fullDiscount = TreatmentPlanRules.Discount(fullGross, item.DiscountType, item.DiscountValue.Value);
                    var remainingDiscount = (long)decimal.Round(fullDiscount * remaining / item.Quantity, 0,
                        MidpointRounding.AwayFromZero);
                    return Math.Max(0, gross - remainingDiscount);
                });
                var labCost = labs.Where(x => x.DossierId == dossier.Id).Sum(x => x.TotalFeeVnd);
                var originalBilled = checked(posted + planned + labCost);
                var payment = received.GetValueOrDefault(dossier.Id);
                var advanceReceived = payment?.AdvanceVnd ?? 0;
                var treatmentReceived = payment?.TreatmentVnd ?? 0;
                var allocated = payment?.AllocatedVnd ?? 0;
                // A cancelled sheet keeps its collected payments, but the unpaid part is written off.
                var billed = dossier.State == "Cancelled" ? Math.Min(originalBilled, Math.Max(0, allocated)) : originalBilled;
                result.ServiceAmountVnd = checked(result.ServiceAmountVnd + billed);
                result.ActualReceivedVnd = checked(result.ActualReceivedVnd + treatmentReceived);
                result.AdvanceReceivedVnd = checked(result.AdvanceReceivedVnd + advanceReceived);
                var due = dossier.State == "Cancelled" ? 0 : Math.Max(0, billed - allocated);
                result.RemainingReceivableVnd = checked(result.RemainingReceivableVnd + due);
                patients.TryGetValue(dossier.PatientId, out var patient);
                if (billed > 0)
                {
                    result.ServiceDossiers.Add(new FinanceServiceDossier(dossier.Id, dossier.Code,
                        dossier.PatientId, patient.FullName ?? "", patient.PatientCode ?? "",
                        dossier.StartedOn, billed, Math.Min(billed, allocated), due, dossier.State));
                }
                if (due > 0)
                {
                    result.OutstandingDossiers.Add(new FinanceOutstandingDossier(dossier.Id, dossier.Code,
                        dossier.PatientId, patient.FullName ?? "", patient.PatientCode ?? "",
                        dossier.StartedOn, billed, Math.Min(billed, allocated), due, dossier.Version,
                        dossier.State == "Open" && posted == 0 && labCost == 0 && allocated == 0
                            && treatmentReceived == advanceReceived && advanceReceived > 0));
                }
                if (dossier.State == "Cancelled" || billed == 0 || allocated < billed) continue;

                var dossierRevenue = Math.Min(allocated, billed);
                var dossierProfit = checked(dossierRevenue - labCost);
                result.ProfitVnd = checked(result.ProfitVnd + dossierProfit);
                result.ProfitableDossierCount++;
                result.ProfitEntries.Add(new FinanceProfitEntry(dossier.Id.ToString(), dossier.Id, dossier.Code,
                    dossier.PatientId, patient.FullName ?? "", patient.PatientCode ?? "",
                    dossier.StartedOn, dossierRevenue, labCost, dossierProfit, true));
            }
            // The material-purchase total includes stock already used by dossiers, so deduct it
            // once here rather than also deducting estimated usage cost from each dossier.
            result.ProfitVnd = checked(result.ProfitVnd - result.MaterialPurchaseVnd);
            if (purchaseTotal > 0)
            {
                result.ProfitEntries.Add(new FinanceProfitEntry("material-purchases", null, "Nhập dụng cụ vật liệu",
                    0, "Toàn phòng khám", "", to ?? from ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    0, purchaseTotal, -purchaseTotal, false));
            }
            result.ServiceDossiers = result.ServiceDossiers.OrderByDescending(x => x.StartedOn)
                .ThenBy(x => x.DossierCode).ToList();
            result.ActualPayments = result.ActualPayments.OrderByDescending(x => x.PaidAt).ToList();
            result.AdvanceEntries = result.AdvanceEntries.OrderByDescending(x => x.PaidAt).ToList();
            result.OutstandingDossiers = result.OutstandingDossiers.OrderByDescending(x => x.StartedOn)
                .ThenBy(x => x.DossierCode).ToList();
            return Results.Ok(result);
        }).RequireAuthorization().WithTags("Finance statistics");
    }
}

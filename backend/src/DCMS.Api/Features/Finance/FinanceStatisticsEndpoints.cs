using DCMS.Domain.Features.Accounts;
using DCMS.Application.Features.Authentication;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Api.Features.Finance;

public sealed class FinanceStatisticsResult
{
    public long ActualReceivedVnd { get; set; }
    public long AdvanceReceivedVnd { get; set; }
}

public static class FinanceStatisticsEndpoints
{
    public static void MapFinanceStatisticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/finance/statistics", async (HttpContext context, DateOnly? from, DateOnly? to, int? branchId,
            DcmsDbContext db, CancellationToken ct) =>
        {
            if (context.Items["CurrentUser"] is not CurrentUser user ||
                !user.EffectiveRoles.Contains(AccountRoles.ClinicOwner)) return Results.Forbid();
            if (from > to || branchId is < 1) return Results.BadRequest("Khoảng ngày hoặc chi nhánh không hợp lệ.");
            var fromText = from?.ToString("yyyy-MM-dd") ?? "";
            var toText = to?.ToString("yyyy-MM-dd") ?? "";
            var branch = branchId ?? 0;
            // Archive rows remain in AUDIT_LOG after the 33-table migration. Only money assigned to
            // performed work is counted as actual receipts. Posted advances are kept separate.
            var result = await db.Database.SqlQuery<FinanceStatisticsResult>($"""
                WITH legacy AS (
                    SELECT a."Target" AS source, item.row
                    FROM "AUDIT_LOG" a
                    CROSS JOIN LATERAL jsonb_array_elements(a."PayloadJson"->'rows') AS item(row)
                    WHERE a."AuditType" = 'FinanceLegacyArchive'
                      AND a."Target" IN ('TREATMENT_DOSSIER_PAYMENT', 'TREATMENT_DOSSIER_PAYMENT_LINE',
                          'PAYMENT_RECEIPT', 'PAYMENT_ALLOCATION', 'FINANCE_CHARGE')
                ), dossier_payments AS (
                    SELECT row FROM legacy WHERE source = 'TREATMENT_DOSSIER_PAYMENT'
                ), dossier_lines AS (
                    SELECT row FROM legacy WHERE source = 'TREATMENT_DOSSIER_PAYMENT_LINE'
                ), receipts AS (
                    SELECT row FROM legacy WHERE source = 'PAYMENT_RECEIPT'
                ), allocations AS (
                    SELECT row FROM legacy WHERE source = 'PAYMENT_ALLOCATION'
                ), charges AS (
                    SELECT row FROM legacy WHERE source = 'FINANCE_CHARGE'
                ), direct_dossier AS (
                    SELECT COALESCE(sum((l.row->>'AmountVnd')::bigint), 0) AS amount
                    FROM dossier_lines l
                    JOIN dossier_payments p ON p.row->>'Id' = l.row->>'PaymentId'
                    JOIN "CLINICAL_ACTIVITY" activity ON activity."Id" = (l.row->>'ActivityId')::uuid
                    WHERE activity."State" = 'Posted'
                      AND ({branch} = 0 OR (p.row->>'BranchId')::integer = {branch})
                      AND ({fromText} = '' OR (p.row->>'PaidOn')::date >= {fromText}::date)
                      AND ({toText} = '' OR (p.row->>'PaidOn')::date <= {toText}::date)
                ), direct_receipt AS (
                    SELECT COALESCE(sum((a.row->>'AmountVnd')::bigint), 0) AS amount
                    FROM allocations a
                    JOIN receipts r ON r.row->>'Id' = a.row->>'PaymentId'
                    JOIN charges c ON c.row->>'Id' = a.row->>'ChargeId'
                    WHERE r.row->>'State' = 'Posted' AND r.row->>'Purpose' <> 'Advance'
                      AND ({branch} = 0 OR (r.row->>'BranchId')::integer = {branch})
                      AND ({fromText} = '' OR ((r.row->>'ConfirmedAt')::timestamptz AT TIME ZONE 'Asia/Ho_Chi_Minh')::date >= {fromText}::date)
                      AND ({toText} = '' OR ((r.row->>'ConfirmedAt')::timestamptz AT TIME ZONE 'Asia/Ho_Chi_Minh')::date <= {toText}::date)
                ), advances AS (
                    SELECT COALESCE(sum((r.row->>'AmountVnd')::bigint), 0) AS amount
                    FROM receipts r
                    WHERE r.row->>'State' = 'Posted' AND r.row->>'Purpose' = 'Advance'
                      AND ({branch} = 0 OR (r.row->>'BranchId')::integer = {branch})
                      AND ({fromText} = '' OR ((r.row->>'ConfirmedAt')::timestamptz AT TIME ZONE 'Asia/Ho_Chi_Minh')::date >= {fromText}::date)
                      AND ({toText} = '' OR ((r.row->>'ConfirmedAt')::timestamptz AT TIME ZONE 'Asia/Ho_Chi_Minh')::date <= {toText}::date)
                )
                SELECT (d.amount + r.amount + COALESCE((SELECT sum((line.row->>'amountVnd')::bigint
                        - COALESCE((SELECT sum((priority.row->>'amountVnd')::bigint)
                            FROM jsonb_array_elements(COALESCE(entry."PayloadJson"->'labPriority', '[]'::jsonb)) priority(row)
                            JOIN "LAB_ORDER" lab ON lab."Id" = (priority.row->>'labOrderId')::uuid
                            WHERE lab."ActivityId" = (line.row->>'activityId')::uuid), 0))
                    FROM "AUDIT_LOG" entry CROSS JOIN LATERAL jsonb_array_elements(entry."PayloadJson"->'lines') line(row)
                    WHERE entry."AuditType" = 'FinanceLedger'
                      AND entry."Action" IN ('TreatmentCashPayment', 'TreatmentTransferPayment')
                      AND ({branch} = 0 OR entry."BranchId" = {branch})
                      AND ({fromText} = '' OR (entry."At" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date >= {fromText}::date)
                      AND ({toText} = '' OR (entry."At" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date <= {toText}::date)), 0))::bigint AS "ActualReceivedVnd",
                       (a.amount + COALESCE((SELECT sum((entry."PayloadJson"->>'amountVnd')::bigint
                        - COALESCE((SELECT sum((priority.row->>'amountVnd')::bigint)
                            FROM jsonb_array_elements(COALESCE(entry."PayloadJson"->'labPriority', '[]'::jsonb)
                                || COALESCE(entry."PayloadJson"->'labLines', '[]'::jsonb)) priority(row)), 0))
                    FROM "AUDIT_LOG" entry WHERE entry."AuditType" = 'FinanceLedger'
                      AND entry."Action" IN ('AdvanceCash', 'AdvanceTransfer')
                      AND ({branch} = 0 OR entry."BranchId" = {branch})
                      AND ({fromText} = '' OR (entry."At" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date >= {fromText}::date)
                      AND ({toText} = '' OR (entry."At" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date <= {toText}::date)), 0))::bigint AS "AdvanceReceivedVnd"
                FROM direct_dossier d CROSS JOIN direct_receipt r CROSS JOIN advances a
                """).SingleAsync(ct);
            return Results.Ok(result);
        }).RequireAuthorization()
            .WithTags("Finance statistics");
    }
}

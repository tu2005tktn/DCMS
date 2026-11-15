$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'import-development-env.ps1')

# Run after ConsolidateCareAndLabStorage and before RemoveFinanceStorage.
# Both the archive and its row-count verification commit atomically.
$sql = @'
BEGIN;
SELECT pg_advisory_xact_lock(hashtext('dcms-finance-legacy-archive'));
DO $archive$
DECLARE
    source_name text;
    rows_json jsonb;
    archive_id uuid;
    existing_json jsonb;
BEGIN
    FOREACH source_name IN ARRAY ARRAY[
        'BRANCH_RECEIPT_SEQUENCE', 'EXAMINATION_FEE', 'FINANCE_ADVANCE_ALLOCATION',
        'FINANCE_PAYMENT_METHOD', 'FINANCE_REFUND', 'FINANCIAL_ACCOUNT',
        'PAYMENT_ALLOCATION', 'PROVIDER_PAYABLE', 'RECEIPT_PRINT_EVENT',
        'TREATMENT_DOSSIER_PAYMENT_LINE', 'TREATMENT_PAYOS_ORDER',
        'FINANCE_INVOICE_LINE', 'PROVIDER', 'FINANCE_ADVANCE',
        'TREATMENT_DOSSIER_PAYMENT', 'FINANCE_CHARGE', 'FINANCE_INVOICE',
        'PAYMENT_RECEIPT', 'FinanceAudit', 'FinanceCommandReceipt'
    ] LOOP
        IF source_name = 'FinanceAudit' THEN
            SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text), '[]'::jsonb)
              INTO rows_json FROM "AUDIT_LOG" t WHERE t."AuditType" = 'FinanceAudit';
        ELSIF source_name = 'FinanceCommandReceipt' THEN
            SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text), '[]'::jsonb)
              INTO rows_json FROM "COMMAND_RECEIPT" t WHERE t."ReceiptType" = 'FinanceCommandReceipt';
        ELSE
            IF to_regclass(format('%I.%I', 'public', source_name)) IS NULL THEN
                RAISE EXCEPTION 'Missing finance source table: %', source_name;
            END IF;
            EXECUTE format('SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text), ''[]''::jsonb) FROM %I t', source_name)
               INTO rows_json;
        END IF;
        archive_id := md5('dcms-finance-legacy-archive:' || source_name)::uuid;
        SELECT "PayloadJson" INTO existing_json FROM "AUDIT_LOG" WHERE "StorageId" = archive_id;
        IF existing_json IS NOT NULL AND existing_json->'rows' IS DISTINCT FROM rows_json THEN
            RAISE EXCEPTION 'Existing finance archive differs from source: %', source_name;
        END IF;
        INSERT INTO "AUDIT_LOG" ("StorageId", "AuditType", "Target", "PayloadJson", "At")
        VALUES (archive_id, 'FinanceLegacyArchive', source_name,
            jsonb_build_object('table', source_name, 'rowCount', jsonb_array_length(rows_json), 'rows', rows_json), now())
        ON CONFLICT ("StorageId") DO NOTHING;
        IF (SELECT ("PayloadJson"->>'rowCount')::int FROM "AUDIT_LOG" WHERE "StorageId" = archive_id)
           <> jsonb_array_length(rows_json) THEN
            RAISE EXCEPTION 'Finance archive row count mismatch: %', source_name;
        END IF;
    END LOOP;
END $archive$;
COMMIT;
SELECT "Target", "PayloadJson"->>'rowCount' AS "Rows"
FROM "AUDIT_LOG" WHERE "AuditType" = 'FinanceLegacyArchive' ORDER BY "Target";
'@

$path = Join-Path $repositoryRoot '.artifacts\finance-archive.sql'
New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
[System.IO.File]::WriteAllText($path, $sql, [System.Text.UTF8Encoding]::new($false))
& docker cp $path 'dcms-postgres-1:/tmp/finance-archive.sql'
if ($LASTEXITCODE -ne 0) { throw 'Could not copy finance archive SQL into PostgreSQL.' }
& docker exec dcms-postgres-1 psql -U $settings['POSTGRES_USER'] -d $settings['POSTGRES_DB'] -v ON_ERROR_STOP=1 -At -f /tmp/finance-archive.sql
if ($LASTEXITCODE -ne 0) { throw 'Finance archive failed; transaction was rolled back.' }

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations;

// docs/specs/SETTINGS-CATALOG-STORAGE.vi.md
public partial class ConsolidateSettingsStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET LOCAL TimeZone = 'UTC';

            ALTER TABLE "COMMAND_RECEIPT"
                ALTER COLUMN "Command" TYPE character varying(48),
                ADD COLUMN "ResourceId" uuid,
                ADD COLUMN "BeforeJson" jsonb,
                ADD COLUMN "OccurredAt" timestamp with time zone;
            INSERT INTO "COMMAND_RECEIPT" ("StorageId", "ReceiptType", "RequestKey", "ActorId", "Command", "ResourceId", "Fingerprint", "BeforeJson", "ResultJson", "OccurredAt")
            SELECT gen_random_uuid(), 'SystemCatalogChange', s."RequestKey", s."ActorId", s."Command", s."ResourceId", s."Fingerprint", s."BeforeJson", s."ResultJson", s."OccurredAt" FROM "SYSTEM_CATALOG_CHANGE" s;
            CREATE INDEX "IX_COMMAND_RECEIPT_ResourceId_OccurredAt" ON "COMMAND_RECEIPT" ("ResourceId", "OccurredAt");

            DO $archive$
            DECLARE
                source_table regclass := to_regclass('"SETTINGS_VERSION"');
                source_rows jsonb;
            BEGIN
                IF source_table IS NULL THEN RETURN; END IF;
                EXECUTE 'SELECT COALESCE(jsonb_agg(to_jsonb(s)), ''[]''::jsonb) FROM "SETTINGS_VERSION" s' INTO source_rows;
                INSERT INTO "AUDIT_LOG" ("StorageId", "AuditType", "BeforeJson") VALUES ('6f1d2c3a-8b4e-4c5d-9e7f-a1b2c3d4e501', 'SettingsLegacyArchive', jsonb_build_object(
                    'table', 'SETTINGS_VERSION',
                    'columns', (SELECT jsonb_agg(jsonb_build_object('name', a.attname, 'type', format_type(a.atttypid, a.atttypmod),
                        'required', a.attnotnull, 'default', pg_get_expr(d.adbin, d.adrelid)) ORDER BY a.attnum)
                        FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
                        WHERE a.attrelid = source_table AND a.attnum > 0 AND NOT a.attisdropped),
                    'constraints', COALESCE((SELECT jsonb_agg(jsonb_build_object('name', c.conname, 'sql', pg_get_constraintdef(c.oid))
                        ORDER BY (c.contype = 'f'), c.conname) FROM pg_constraint c WHERE c.conrelid = source_table), '[]'::jsonb),
                    'indexes', COALESCE((SELECT jsonb_agg(pg_get_indexdef(i.indexrelid) ORDER BY i.indexrelid)
                        FROM pg_index i WHERE i.indrelid = source_table AND NOT EXISTS
                        (SELECT 1 FROM pg_constraint c WHERE c.conindid = i.indexrelid)), '[]'::jsonb),
                    'rows', source_rows));
                IF jsonb_array_length(source_rows) <> (SELECT jsonb_array_length("BeforeJson"->'rows') FROM "AUDIT_LOG" WHERE "StorageId" = '6f1d2c3a-8b4e-4c5d-9e7f-a1b2c3d4e501') THEN
                    RAISE EXCEPTION 'SETTINGS_VERSION snapshot row count does not match';
                END IF;
                DROP TABLE "SETTINGS_VERSION";
            END $archive$;
            DO $archive$
            DECLARE
                source_table regclass := to_regclass('"OPERATIONAL_REFERENCE_ENTRY"');
                source_rows jsonb;
            BEGIN
                IF source_table IS NULL THEN RETURN; END IF;
                EXECUTE 'SELECT COALESCE(jsonb_agg(to_jsonb(s)), ''[]''::jsonb) FROM "OPERATIONAL_REFERENCE_ENTRY" s' INTO source_rows;
                INSERT INTO "AUDIT_LOG" ("StorageId", "AuditType", "BeforeJson") VALUES ('6f1d2c3a-8b4e-4c5d-9e7f-a1b2c3d4e502', 'SettingsLegacyArchive', jsonb_build_object(
                    'table', 'OPERATIONAL_REFERENCE_ENTRY',
                    'columns', (SELECT jsonb_agg(jsonb_build_object('name', a.attname, 'type', format_type(a.atttypid, a.atttypmod),
                        'required', a.attnotnull, 'default', pg_get_expr(d.adbin, d.adrelid)) ORDER BY a.attnum)
                        FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
                        WHERE a.attrelid = source_table AND a.attnum > 0 AND NOT a.attisdropped),
                    'constraints', COALESCE((SELECT jsonb_agg(jsonb_build_object('name', c.conname, 'sql', pg_get_constraintdef(c.oid))
                        ORDER BY (c.contype = 'f'), c.conname) FROM pg_constraint c WHERE c.conrelid = source_table), '[]'::jsonb),
                    'indexes', COALESCE((SELECT jsonb_agg(pg_get_indexdef(i.indexrelid) ORDER BY i.indexrelid)
                        FROM pg_index i WHERE i.indrelid = source_table AND NOT EXISTS
                        (SELECT 1 FROM pg_constraint c WHERE c.conindid = i.indexrelid)), '[]'::jsonb),
                    'rows', source_rows));
                IF jsonb_array_length(source_rows) <> (SELECT jsonb_array_length("BeforeJson"->'rows') FROM "AUDIT_LOG" WHERE "StorageId" = '6f1d2c3a-8b4e-4c5d-9e7f-a1b2c3d4e502') THEN
                    RAISE EXCEPTION 'OPERATIONAL_REFERENCE_ENTRY snapshot row count does not match';
                END IF;
                DROP TABLE "OPERATIONAL_REFERENCE_ENTRY";
            END $archive$;

            DO $verify$
            BEGIN
                IF (SELECT count(*) FROM "SYSTEM_CATALOG_CHANGE") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'SystemCatalogChange') THEN
                    RAISE EXCEPTION 'System catalog change conversion row counts do not match';
                END IF;
            END $verify$;
            DROP TABLE "SYSTEM_CATALOG_CHANGE";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET LOCAL TimeZone = 'UTC';

            CREATE TABLE "SYSTEM_CATALOG_CHANGE" (
                "RequestKey" uuid NOT NULL,
                "ActorId" integer NOT NULL,
                "BeforeJson" jsonb,
                "Command" character varying(48) NOT NULL,
                "Fingerprint" character varying(64) NOT NULL,
                "OccurredAt" timestamp with time zone NOT NULL,
                "ResourceId" uuid NOT NULL,
                "ResultJson" jsonb NOT NULL,
                CONSTRAINT "PK_SYSTEM_CATALOG_CHANGE" PRIMARY KEY ("RequestKey")
            );
            CREATE INDEX "IX_SYSTEM_CATALOG_CHANGE_ResourceId_OccurredAt" ON "SYSTEM_CATALOG_CHANGE" ("ResourceId", "OccurredAt");
            INSERT INTO "SYSTEM_CATALOG_CHANGE" ("RequestKey", "ActorId", "Command", "ResourceId", "Fingerprint", "BeforeJson", "ResultJson", "OccurredAt")
            SELECT "RequestKey", "ActorId", "Command", "ResourceId", "Fingerprint", "BeforeJson", "ResultJson", "OccurredAt" FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'SystemCatalogChange';
            DELETE FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'SystemCatalogChange';
            DROP INDEX "IX_COMMAND_RECEIPT_ResourceId_OccurredAt";
            ALTER TABLE "COMMAND_RECEIPT"
                DROP COLUMN "ResourceId",
                DROP COLUMN "BeforeJson",
                DROP COLUMN "OccurredAt",
                ALTER COLUMN "Command" TYPE character varying(40);

            DO $restore$
            DECLARE
                snapshot jsonb;
                columns_sql text;
                constraint_row jsonb;
                index_sql text;
            BEGIN
                SELECT "BeforeJson" INTO snapshot FROM "AUDIT_LOG" WHERE "StorageId" = '6f1d2c3a-8b4e-4c5d-9e7f-a1b2c3d4e501' AND "AuditType" = 'SettingsLegacyArchive';
                IF snapshot IS NULL THEN RETURN; END IF;
                SELECT string_agg(format('%I %s%s%s', c->>'name', c->>'type',
                    CASE WHEN c->>'default' IS NULL THEN '' ELSE ' DEFAULT ' || (c->>'default') END,
                    CASE WHEN (c->>'required')::boolean THEN ' NOT NULL' ELSE '' END), ', ' ORDER BY ordinal)
                    INTO columns_sql FROM jsonb_array_elements(snapshot->'columns') WITH ORDINALITY source(c, ordinal);
                EXECUTE 'CREATE TABLE "SETTINGS_VERSION" (' || columns_sql || ')';
                EXECUTE 'INSERT INTO "SETTINGS_VERSION" SELECT * FROM jsonb_populate_recordset(NULL::"SETTINGS_VERSION", $1)' USING snapshot->'rows';
                FOR constraint_row IN SELECT value FROM jsonb_array_elements(snapshot->'constraints') LOOP
                    EXECUTE format('ALTER TABLE "SETTINGS_VERSION" ADD CONSTRAINT %I %s', constraint_row->>'name', constraint_row->>'sql');
                END LOOP;
                FOR index_sql IN SELECT jsonb_array_elements_text(snapshot->'indexes') LOOP
                    EXECUTE index_sql;
                END LOOP;
                DELETE FROM "AUDIT_LOG" WHERE "StorageId" = '6f1d2c3a-8b4e-4c5d-9e7f-a1b2c3d4e501' AND "AuditType" = 'SettingsLegacyArchive';
            END $restore$;
            DO $restore$
            DECLARE
                snapshot jsonb;
                columns_sql text;
                constraint_row jsonb;
                index_sql text;
            BEGIN
                SELECT "BeforeJson" INTO snapshot FROM "AUDIT_LOG" WHERE "StorageId" = '6f1d2c3a-8b4e-4c5d-9e7f-a1b2c3d4e502' AND "AuditType" = 'SettingsLegacyArchive';
                IF snapshot IS NULL THEN RETURN; END IF;
                SELECT string_agg(format('%I %s%s%s', c->>'name', c->>'type',
                    CASE WHEN c->>'default' IS NULL THEN '' ELSE ' DEFAULT ' || (c->>'default') END,
                    CASE WHEN (c->>'required')::boolean THEN ' NOT NULL' ELSE '' END), ', ' ORDER BY ordinal)
                    INTO columns_sql FROM jsonb_array_elements(snapshot->'columns') WITH ORDINALITY source(c, ordinal);
                EXECUTE 'CREATE TABLE "OPERATIONAL_REFERENCE_ENTRY" (' || columns_sql || ')';
                EXECUTE 'INSERT INTO "OPERATIONAL_REFERENCE_ENTRY" SELECT * FROM jsonb_populate_recordset(NULL::"OPERATIONAL_REFERENCE_ENTRY", $1)' USING snapshot->'rows';
                FOR constraint_row IN SELECT value FROM jsonb_array_elements(snapshot->'constraints') LOOP
                    EXECUTE format('ALTER TABLE "OPERATIONAL_REFERENCE_ENTRY" ADD CONSTRAINT %I %s', constraint_row->>'name', constraint_row->>'sql');
                END LOOP;
                FOR index_sql IN SELECT jsonb_array_elements_text(snapshot->'indexes') LOOP
                    EXECUTE index_sql;
                END LOOP;
                DELETE FROM "AUDIT_LOG" WHERE "StorageId" = '6f1d2c3a-8b4e-4c5d-9e7f-a1b2c3d4e502' AND "AuditType" = 'SettingsLegacyArchive';
            END $restore$;
            """);
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations;

public partial class ConsolidateExaminationStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "VISIT"
                ADD COLUMN "ExaminationContentJson" jsonb,
                ADD COLUMN "ExaminationConfirmed" boolean,
                ADD COLUMN "ExaminationUpdatedBy" integer,
                ADD COLUMN "ExaminationUpdatedAt" timestamp with time zone,
                ADD COLUMN "Investigations" jsonb NOT NULL DEFAULT '[]'::jsonb,
                ADD COLUMN "StorageStamp" uuid NOT NULL DEFAULT gen_random_uuid();
            ALTER TABLE "CLINICAL_DOCUMENT" ADD COLUMN "Reviews" jsonb NOT NULL DEFAULT '[]'::jsonb;
            
            UPDATE "VISIT" v SET "ExaminationContentJson"=e."ContentJson", "ExaminationConfirmed"=e."Confirmed",
                "ExaminationUpdatedBy"=e."UpdatedBy", "ExaminationUpdatedAt"=e."UpdatedAt"
            FROM "VISIT_EXAMINATION" e WHERE e."VisitId"=v."Id";
            UPDATE "VISIT" v SET "Investigations"=COALESCE((SELECT jsonb_agg(
                to_jsonb(o) || jsonb_build_object('ResultDocumentIdsJson',o."ResultDocumentIdsJson"::text)
                ORDER BY o."CreatedAt",o."Id") FROM "INVESTIGATION_ORDER" o WHERE o."VisitId"=v."Id"),'[]'::jsonb);
            UPDATE "CLINICAL_DOCUMENT" d SET "Reviews"=COALESCE((SELECT jsonb_agg(to_jsonb(r) ORDER BY r."VisitId")
                FROM "DOCUMENT_REVIEW" r WHERE r."DocumentId"=d."Id"),'[]'::jsonb);
            
            DO $verify$
            BEGIN
                IF (SELECT count(*) FROM "VISIT_EXAMINATION") <> (SELECT count(*) FROM "VISIT" WHERE "ExaminationContentJson" IS NOT NULL)
                    OR (SELECT count(*) FROM "INVESTIGATION_ORDER") <> (SELECT COALESCE(sum(jsonb_array_length("Investigations")),0) FROM "VISIT")
                    OR (SELECT count(*) FROM "DOCUMENT_REVIEW") <> (SELECT COALESCE(sum(jsonb_array_length("Reviews")),0) FROM "CLINICAL_DOCUMENT") THEN
                    RAISE EXCEPTION 'Examination storage conversion row counts do not match';
                END IF;
            END $verify$;
            
            ALTER TABLE "CLINICAL_DOCUMENT" DROP CONSTRAINT "FK_CLINICAL_DOCUMENT_INVESTIGATION_ORDER_InvestigationId";
            ALTER TABLE "FINANCE_CHARGE" DROP CONSTRAINT "FK_FINANCE_CHARGE_INVESTIGATION_ORDER_InvestigationId";
            ALTER TABLE "VISIT" ADD CONSTRAINT "FK_VISIT_USER_ACCOUNT_ExaminationUpdatedBy"
                FOREIGN KEY ("ExaminationUpdatedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT;
            CREATE INDEX "IX_VISIT_ExaminationUpdatedBy" ON "VISIT" ("ExaminationUpdatedBy");
            DROP TABLE "DOCUMENT_REVIEW";
            DROP TABLE "INVESTIGATION_ORDER";
            DROP TABLE "VISIT_EXAMINATION";
            
            DO $archive$
            DECLARE
                source_table regclass := to_regclass('"CLINICAL_SOURCE_REFERENCE"');
                snapshot jsonb;
                source_rows jsonb;
            BEGIN
                IF source_table IS NULL THEN RETURN; END IF;
                EXECUTE 'SELECT COALESCE(jsonb_agg(to_jsonb(s)),''[]''::jsonb) FROM "CLINICAL_SOURCE_REFERENCE" s' INTO source_rows;
                snapshot := jsonb_build_object(
                    'columns',(SELECT jsonb_agg(jsonb_build_object('name',a.attname,'type',format_type(a.atttypid,a.atttypmod),
                        'required',a.attnotnull,'default',pg_get_expr(d.adbin,d.adrelid)) ORDER BY a.attnum)
                        FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum
                        WHERE a.attrelid=source_table AND a.attnum>0 AND NOT a.attisdropped),
                    'constraints',COALESCE((SELECT jsonb_agg(jsonb_build_object('name',c.conname,'sql',pg_get_constraintdef(c.oid))
                        ORDER BY (c.contype='f'),c.conname) FROM pg_constraint c WHERE c.conrelid=source_table),'[]'::jsonb),
                    'indexes',COALESCE((SELECT jsonb_agg(pg_get_indexdef(i.indexrelid) ORDER BY i.indexrelid)
                        FROM pg_index i WHERE i.indrelid=source_table AND NOT EXISTS
                        (SELECT 1 FROM pg_constraint c WHERE c.conindid=i.indexrelid)),'[]'::jsonb),
                    'rows',source_rows);
                INSERT INTO "AUDIT_LOG" ("StorageId","AuditType","BeforeJson") VALUES
                    ('59ad3592-2919-4d74-ad7d-21dc65eb1223','ClinicalSourceArchive',snapshot);
                DROP TABLE "CLINICAL_SOURCE_REFERENCE";
            END $archive$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "VISIT_EXAMINATION" (
                "VisitId" uuid NOT NULL,
                "Confirmed" boolean NOT NULL,
                "ContentJson" jsonb NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "UpdatedBy" integer NOT NULL,
                CONSTRAINT "PK_VISIT_EXAMINATION" PRIMARY KEY ("VisitId"),
                CONSTRAINT "FK_VISIT_EXAMINATION_USER_ACCOUNT_UpdatedBy" FOREIGN KEY ("UpdatedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_VISIT_EXAMINATION_VISIT_VisitId" FOREIGN KEY ("VisitId") REFERENCES "VISIT" ("Id") ON DELETE RESTRICT
            );
            CREATE TABLE "INVESTIGATION_ORDER" (
                "Id" uuid NOT NULL,
                "BillingMode" character varying(24),
                "CancellationReason" character varying(1000),
                "CancelledAt" timestamp with time zone,
                "CancelledBy" integer,
                "CreateRequestKey" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" integer NOT NULL,
                "Interpretation" character varying(4000),
                "Kind" character varying(16) NOT NULL,
                "Name" character varying(160) NOT NULL,
                "PerformedAt" timestamp with time zone,
                "PerformedBy" integer,
                "PriceVersionSnapshot" integer,
                "ProviderId" uuid,
                "ProviderNameSnapshot" character varying(160),
                "ProviderUnitCostSnapshotVnd" bigint,
                "Quantity" integer NOT NULL,
                "QuoteEvidence" character varying(1000),
                "QuotedAt" timestamp with time zone,
                "QuotedBy" integer,
                "Reason" character varying(1000) NOT NULL,
                "Region" character varying(200),
                "RequiredForDiagnosis" boolean NOT NULL,
                "ResultAt" timestamp with time zone,
                "ResultBy" integer,
                "ResultDocumentIdsJson" jsonb NOT NULL,
                "ResultText" character varying(4000),
                "ReviewedAt" timestamp with time zone,
                "ReviewedBy" integer,
                "ServiceId" integer,
                "ServiceNameSnapshot" character varying(160),
                "State" character varying(24) NOT NULL,
                "UnitPriceSnapshotVnd" bigint,
                "UnitSnapshot" character varying(40),
                "Version" integer NOT NULL,
                "VisitId" uuid NOT NULL,
                CONSTRAINT "PK_INVESTIGATION_ORDER" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_INVESTIGATION_ORDER_STATE" CHECK ("State" IN ('Ordered','ResultAvailable','Reviewed','Cancelled')),
                CONSTRAINT "FK_INVESTIGATION_ORDER_PROVIDER_ProviderId" FOREIGN KEY ("ProviderId") REFERENCES "PROVIDER" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_INVESTIGATION_ORDER_SCHEDULING_SERVICE_ServiceId" FOREIGN KEY ("ServiceId") REFERENCES "SCHEDULING_SERVICE" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_INVESTIGATION_ORDER_USER_ACCOUNT_CancelledBy" FOREIGN KEY ("CancelledBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_INVESTIGATION_ORDER_USER_ACCOUNT_CreatedBy" FOREIGN KEY ("CreatedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_INVESTIGATION_ORDER_USER_ACCOUNT_PerformedBy" FOREIGN KEY ("PerformedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_INVESTIGATION_ORDER_USER_ACCOUNT_QuotedBy" FOREIGN KEY ("QuotedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_INVESTIGATION_ORDER_USER_ACCOUNT_ResultBy" FOREIGN KEY ("ResultBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_INVESTIGATION_ORDER_USER_ACCOUNT_ReviewedBy" FOREIGN KEY ("ReviewedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_INVESTIGATION_ORDER_VISIT_VisitId" FOREIGN KEY ("VisitId") REFERENCES "VISIT" ("Id") ON DELETE RESTRICT
            );
            CREATE TABLE "DOCUMENT_REVIEW" (
                "DocumentId" uuid NOT NULL,
                "VisitId" uuid NOT NULL,
                "ReviewedAt" timestamp with time zone NOT NULL,
                "ReviewedBy" integer NOT NULL,
                CONSTRAINT "PK_DOCUMENT_REVIEW" PRIMARY KEY ("DocumentId", "VisitId"),
                CONSTRAINT "FK_DOCUMENT_REVIEW_CLINICAL_DOCUMENT_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "CLINICAL_DOCUMENT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_DOCUMENT_REVIEW_USER_ACCOUNT_ReviewedBy" FOREIGN KEY ("ReviewedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_DOCUMENT_REVIEW_VISIT_VisitId" FOREIGN KEY ("VisitId") REFERENCES "VISIT" ("Id") ON DELETE RESTRICT
            );
            
            INSERT INTO "VISIT_EXAMINATION" ("VisitId","ContentJson","Confirmed","UpdatedBy","UpdatedAt")
            SELECT "Id","ExaminationContentJson","ExaminationConfirmed","ExaminationUpdatedBy","ExaminationUpdatedAt"
            FROM "VISIT" WHERE "ExaminationContentJson" IS NOT NULL;
            INSERT INTO "INVESTIGATION_ORDER"
            SELECT restored.* FROM "VISIT" v CROSS JOIN LATERAL jsonb_array_elements(v."Investigations") item
            CROSS JOIN LATERAL jsonb_populate_record(NULL::"INVESTIGATION_ORDER",
                jsonb_set(item,'{ResultDocumentIdsJson}',(item->>'ResultDocumentIdsJson')::jsonb)) restored;
            INSERT INTO "DOCUMENT_REVIEW"
            SELECT restored.* FROM "CLINICAL_DOCUMENT" d
            CROSS JOIN LATERAL jsonb_populate_recordset(NULL::"DOCUMENT_REVIEW",d."Reviews") restored;
            ALTER TABLE "CLINICAL_DOCUMENT" ADD CONSTRAINT "FK_CLINICAL_DOCUMENT_INVESTIGATION_ORDER_InvestigationId"
                FOREIGN KEY ("InvestigationId") REFERENCES "INVESTIGATION_ORDER" ("Id") ON DELETE RESTRICT;
            ALTER TABLE "FINANCE_CHARGE" ADD CONSTRAINT "FK_FINANCE_CHARGE_INVESTIGATION_ORDER_InvestigationId"
                FOREIGN KEY ("InvestigationId") REFERENCES "INVESTIGATION_ORDER" ("Id") ON DELETE RESTRICT;
            ALTER TABLE "VISIT" DROP CONSTRAINT "FK_VISIT_USER_ACCOUNT_ExaminationUpdatedBy";
            DROP INDEX "IX_VISIT_ExaminationUpdatedBy";
            ALTER TABLE "VISIT" DROP COLUMN "ExaminationContentJson", DROP COLUMN "ExaminationConfirmed",
                DROP COLUMN "ExaminationUpdatedBy", DROP COLUMN "ExaminationUpdatedAt", DROP COLUMN "Investigations", DROP COLUMN "StorageStamp";
            ALTER TABLE "CLINICAL_DOCUMENT" DROP COLUMN "Reviews";
            CREATE INDEX "IX_DOCUMENT_REVIEW_ReviewedBy" ON "DOCUMENT_REVIEW" ("ReviewedBy");
            CREATE INDEX "IX_DOCUMENT_REVIEW_VisitId" ON "DOCUMENT_REVIEW" ("VisitId");
            CREATE INDEX "IX_INVESTIGATION_ORDER_CancelledBy" ON "INVESTIGATION_ORDER" ("CancelledBy");
            CREATE UNIQUE INDEX "IX_INVESTIGATION_ORDER_CreatedBy_CreateRequestKey" ON "INVESTIGATION_ORDER" ("CreatedBy", "CreateRequestKey");
            CREATE INDEX "IX_INVESTIGATION_ORDER_PerformedBy" ON "INVESTIGATION_ORDER" ("PerformedBy");
            CREATE INDEX "IX_INVESTIGATION_ORDER_ProviderId" ON "INVESTIGATION_ORDER" ("ProviderId");
            CREATE INDEX "IX_INVESTIGATION_ORDER_QuotedBy" ON "INVESTIGATION_ORDER" ("QuotedBy");
            CREATE INDEX "IX_INVESTIGATION_ORDER_ResultBy" ON "INVESTIGATION_ORDER" ("ResultBy");
            CREATE INDEX "IX_INVESTIGATION_ORDER_ReviewedBy" ON "INVESTIGATION_ORDER" ("ReviewedBy");
            CREATE INDEX "IX_INVESTIGATION_ORDER_ServiceId" ON "INVESTIGATION_ORDER" ("ServiceId");
            CREATE INDEX "IX_INVESTIGATION_ORDER_VisitId_State" ON "INVESTIGATION_ORDER" ("VisitId", "State");
            CREATE INDEX "IX_VISIT_EXAMINATION_UpdatedBy" ON "VISIT_EXAMINATION" ("UpdatedBy");
            
            DO $restore$
            DECLARE
                snapshot jsonb;
                columns_sql text;
                constraint_row jsonb;
                index_sql text;
            BEGIN
                SELECT "BeforeJson" INTO snapshot FROM "AUDIT_LOG"
                    WHERE "StorageId"='59ad3592-2919-4d74-ad7d-21dc65eb1223' AND "AuditType"='ClinicalSourceArchive';
                IF snapshot IS NULL THEN RETURN; END IF;
                SELECT string_agg(format('%I %s%s%s',c->>'name',c->>'type',
                    CASE WHEN c->>'default' IS NULL THEN '' ELSE ' DEFAULT ' || (c->>'default') END,
                    CASE WHEN (c->>'required')::boolean THEN ' NOT NULL' ELSE '' END),', ' ORDER BY ordinal)
                    INTO columns_sql FROM jsonb_array_elements(snapshot->'columns') WITH ORDINALITY source(c,ordinal);
                EXECUTE 'CREATE TABLE "CLINICAL_SOURCE_REFERENCE" (' || columns_sql || ')';
                EXECUTE 'INSERT INTO "CLINICAL_SOURCE_REFERENCE" SELECT * FROM jsonb_populate_recordset(NULL::"CLINICAL_SOURCE_REFERENCE",$1)'
                    USING snapshot->'rows';
                FOR constraint_row IN SELECT value FROM jsonb_array_elements(snapshot->'constraints') LOOP
                    EXECUTE format('ALTER TABLE "CLINICAL_SOURCE_REFERENCE" ADD CONSTRAINT %I %s',constraint_row->>'name',constraint_row->>'sql');
                END LOOP;
                FOR index_sql IN SELECT jsonb_array_elements_text(snapshot->'indexes') LOOP
                    EXECUTE index_sql;
                END LOOP;
                DELETE FROM "AUDIT_LOG" WHERE "StorageId"='59ad3592-2919-4d74-ad7d-21dc65eb1223' AND "AuditType"='ClinicalSourceArchive';
            END $restore$;
            """);
    }
}

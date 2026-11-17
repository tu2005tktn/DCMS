using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Simplifies VISIT from 32 to 16 columns: the examination confirmation becomes the ExaminationConfirmed status,
    /// Version is the only concurrency token, the reception source and RecordedAt are dropped, 11 orphan columns are dropped,
    /// and derived diagnosis fields are no longer stored in ExaminationContentJson.
    /// See docs/specs/VISIT-TABLE-SIMPLIFICATION.vi.md.
    /// </summary>
    public partial class SimplifyVisitTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "VISIT" DROP CONSTRAINT IF EXISTS "CK_VISIT_Status";
                ALTER TABLE "VISIT" DROP CONSTRAINT IF EXISTS "CK_VISIT_Arrival";

                -- A confirmed examination on an open visit becomes the ExaminationConfirmed status.
                UPDATE "VISIT" SET "Status" = 'ExaminationConfirmed'
                WHERE "Status" = 'InProgress' AND "ExaminationConfirmed" IS TRUE;

                -- diagnoses / diagnosis / conclusion are derived from findings + unscopedDiagnoses.
                -- Only rows that store unscopedDiagnoses as an array are trimmed; a free-text value that
                -- differs from the derived text is kept.
                UPDATE "VISIT" AS v SET "ExaminationContentJson" = (
                    SELECT CASE WHEN c->>'diagnosis' IS NULL OR c->>'diagnosis' = d.text THEN x - 'diagnosis' ELSE x END
                           - CASE WHEN c->>'conclusion' IS NULL OR c->>'conclusion' = d.text OR c->>'conclusion' = c->>'diagnosis' THEN 'conclusion' ELSE '' END
                    FROM (SELECT v."ExaminationContentJson" AS c, v."ExaminationContentJson" - 'diagnoses' AS x) AS s,
                         LATERAL (SELECT string_agg(e.value, '; ' ORDER BY e.ordinality) AS text
                                  FROM jsonb_array_elements_text(CASE WHEN jsonb_typeof(s.c->'diagnoses') = 'array' THEN s.c->'diagnoses' ELSE '[]'::jsonb END)
                                       WITH ORDINALITY AS e(value, ordinality)) AS d)
                WHERE jsonb_typeof(v."ExaminationContentJson"->'unscopedDiagnoses') = 'array';

                ALTER TABLE "VISIT" DROP CONSTRAINT IF EXISTS "FK_VISIT_USER_ACCOUNT_ExaminationUpdatedBy";
                ALTER TABLE "VISIT" RENAME COLUMN "ExaminationUpdatedBy" TO "UpdatedBy";
                ALTER TABLE "VISIT" RENAME COLUMN "ExaminationUpdatedAt" TO "UpdatedAt";
                ALTER INDEX IF EXISTS "IX_VISIT_ExaminationUpdatedBy" RENAME TO "IX_VISIT_UpdatedBy";

                -- The last 11 columns exist only in databases upgraded from the pre-migration reception schema;
                -- they are not part of the migration chain, so Down does not recreate them.
                -- Dropping a column also drops its FK (PriorityDentistId) and indexes (Code, BranchId+QueueOrder, PriorityDentistId).
                ALTER TABLE "VISIT"
                    DROP COLUMN IF EXISTS "ExaminationConfirmed",
                    DROP COLUMN IF EXISTS "RecordedAt",
                    DROP COLUMN IF EXISTS "RequiresClinicalSourceReview",
                    DROP COLUMN IF EXISTS "SourceJson",
                    DROP COLUMN IF EXISTS "StorageStamp",
                    DROP COLUMN IF EXISTS "AdditionalConcern",
                    DROP COLUMN IF EXISTS "ClinicalPriority",
                    DROP COLUMN IF EXISTS "ReviewReason",
                    DROP COLUMN IF EXISTS "PriorityDentistId",
                    DROP COLUMN IF EXISTS "IdentityNote",
                    DROP COLUMN IF EXISTS "QueueOrder",
                    DROP COLUMN IF EXISTS "Code",
                    DROP COLUMN IF EXISTS "ActivityId",
                    DROP COLUMN IF EXISTS "DossierId",
                    DROP COLUMN IF EXISTS "ItemId",
                    DROP COLUMN IF EXISTS "StepId";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_VISIT_Status",
                table: "VISIT",
                sql: "\"Status\" IN ('Queued','InProgress','ExaminationConfirmed','Completed','Cancelled')");

            migrationBuilder.AddForeignKey(
                name: "FK_VISIT_USER_ACCOUNT_UpdatedBy",
                table: "VISIT",
                column: "UpdatedBy",
                principalTable: "USER_ACCOUNT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "VISIT" DROP CONSTRAINT IF EXISTS "FK_VISIT_USER_ACCOUNT_UpdatedBy";
                ALTER TABLE "VISIT" DROP CONSTRAINT IF EXISTS "CK_VISIT_Status";
                ALTER TABLE "VISIT" RENAME COLUMN "UpdatedBy" TO "ExaminationUpdatedBy";
                ALTER TABLE "VISIT" RENAME COLUMN "UpdatedAt" TO "ExaminationUpdatedAt";
                ALTER INDEX IF EXISTS "IX_VISIT_UpdatedBy" RENAME TO "IX_VISIT_ExaminationUpdatedBy";

                ALTER TABLE "VISIT"
                    ADD COLUMN "ExaminationConfirmed" boolean,
                    ADD COLUMN "RecordedAt" timestamp with time zone,
                    ADD COLUMN "RequiresClinicalSourceReview" boolean NOT NULL DEFAULT false,
                    ADD COLUMN "SourceJson" jsonb,
                    ADD COLUMN "StorageStamp" uuid NOT NULL DEFAULT gen_random_uuid();

                UPDATE "VISIT" SET "RecordedAt" = "ArrivalAt",
                    "RequiresClinicalSourceReview" = "Purpose" <> 'InitialExamination',
                    "ExaminationConfirmed" = CASE WHEN "ExaminationContentJson" IS NULL THEN NULL
                                                  ELSE "Status" IN ('ExaminationConfirmed', 'Completed') END;
                UPDATE "VISIT" SET "Status" = 'InProgress' WHERE "Status" = 'ExaminationConfirmed';
                ALTER TABLE "VISIT" ALTER COLUMN "RecordedAt" SET NOT NULL;
                ALTER TABLE "VISIT" ALTER COLUMN "StorageStamp" DROP DEFAULT;

                -- Restore the stored derived diagnosis fields expected by the previous code.
                UPDATE "VISIT" AS v SET "ExaminationContentJson" = v."ExaminationContentJson" || (
                    SELECT jsonb_build_object('diagnoses', COALESCE(jsonb_agg(u.value ORDER BY u.ordinality), '[]'::jsonb),
                               'diagnosis', string_agg(u.value, '; ' ORDER BY u.ordinality),
                               'conclusion', COALESCE(v."ExaminationContentJson"->>'conclusion', string_agg(u.value, '; ' ORDER BY u.ordinality)))
                    FROM (SELECT DISTINCT ON (lower(t.value)) t.value, t.ordinality
                          FROM (SELECT e.value, e.ordinality FROM jsonb_array_elements_text(
                                    CASE WHEN jsonb_typeof(v."ExaminationContentJson"->'unscopedDiagnoses') = 'array'
                                         THEN v."ExaminationContentJson"->'unscopedDiagnoses' ELSE '[]'::jsonb END) WITH ORDINALITY AS e(value, ordinality)
                                UNION ALL
                                SELECT fd.value, 1000 + f.ordinality * 100 + fd.ordinality
                                FROM jsonb_array_elements(CASE WHEN jsonb_typeof(v."ExaminationContentJson"->'findings') = 'array'
                                                               THEN v."ExaminationContentJson"->'findings' ELSE '[]'::jsonb END) WITH ORDINALITY AS f(item, ordinality),
                                     jsonb_array_elements_text(CASE WHEN jsonb_typeof(f.item->'diagnoses') = 'array' THEN f.item->'diagnoses' ELSE '[]'::jsonb END)
                                         WITH ORDINALITY AS fd(value, ordinality)) AS t
                          ORDER BY lower(t.value), t.ordinality) AS u)
                WHERE v."ExaminationContentJson" ? 'unscopedDiagnoses' AND NOT v."ExaminationContentJson" ? 'diagnoses';

                ALTER TABLE "VISIT" ADD CONSTRAINT "CK_VISIT_Arrival" CHECK ("ArrivalAt" <= "RecordedAt");
                ALTER TABLE "VISIT" ADD CONSTRAINT "CK_VISIT_Status" CHECK ("Status" IN ('Queued','InProgress','Completed','Cancelled'));
                ALTER TABLE "VISIT" ADD CONSTRAINT "FK_VISIT_USER_ACCOUNT_ExaminationUpdatedBy"
                    FOREIGN KEY ("ExaminationUpdatedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT;
                """);
        }
    }
}

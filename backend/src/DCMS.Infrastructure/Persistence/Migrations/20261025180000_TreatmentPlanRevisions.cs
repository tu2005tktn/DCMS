using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DCMS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DcmsDbContext))]
[Migration("20261025180000_TreatmentPlanRevisions")]
public sealed class TreatmentPlanRevisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "TREATMENT_PLAN_ITEM" ADD COLUMN "Material" character varying(160);
            ALTER TABLE "TREATMENT_PLAN_ITEM" ADD COLUMN "DecisionPlanVersion" integer;
            CREATE TABLE "TREATMENT_REVISION_DRAFT" (
              "Id" uuid NOT NULL PRIMARY KEY,
              "DossierId" uuid NOT NULL,
              "ItemId" uuid NOT NULL,
              "ActorId" integer NOT NULL,
              "BasePlanVersion" integer NOT NULL,
              "Version" integer NOT NULL,
              "PayloadJson" jsonb NOT NULL,
              "UpdatedAt" timestamp with time zone NOT NULL,
              CONSTRAINT "FK_TREATMENT_REVISION_DRAFT_TREATMENT_DOSSIER_DossierId"
                FOREIGN KEY ("DossierId") REFERENCES "TREATMENT_DOSSIER" ("Id") ON DELETE RESTRICT,
              CONSTRAINT "FK_TREATMENT_REVISION_DRAFT_TREATMENT_PLAN_ITEM_ItemId"
                FOREIGN KEY ("ItemId") REFERENCES "TREATMENT_PLAN_ITEM" ("Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX "IX_TREATMENT_REVISION_DRAFT_DossierId_ItemId_ActorId"
              ON "TREATMENT_REVISION_DRAFT" ("DossierId", "ItemId", "ActorId");
            CREATE INDEX "IX_TREATMENT_REVISION_DRAFT_ItemId" ON "TREATMENT_REVISION_DRAFT" ("ItemId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE "TREATMENT_REVISION_DRAFT";
            ALTER TABLE "TREATMENT_PLAN_ITEM" DROP COLUMN "Material";
            ALTER TABLE "TREATMENT_PLAN_ITEM" DROP COLUMN "DecisionPlanVersion";
            """);
    }
}

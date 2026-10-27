using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DCMS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DcmsDbContext))]
[Migration("20261025200000_TreatmentPlanV23")]
public sealed class TreatmentPlanV23 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "SCHEDULING_SERVICE"
              ADD COLUMN "ReferenceUnitPriceVnd" bigint NULL,
              ADD COLUMN "PriceVersion" integer NOT NULL DEFAULT 1;
            ALTER TABLE "SCHEDULING_SERVICE" ADD CONSTRAINT "CK_SERVICE_REFERENCE_PRICE"
              CHECK ("ReferenceUnitPriceVnd" IS NULL OR "ReferenceUnitPriceVnd" >= 0);
            ALTER TABLE "TREATMENT_DOSSIER"
              ADD COLUMN "DepositExceptionReason" varchar(1000) NULL,
              ADD COLUMN "DepositExceptionBy" integer NULL,
              ADD COLUMN "DepositExceptionAt" timestamptz NULL;
            ALTER TABLE "TREATMENT_PLAN_ITEM"
              ADD COLUMN "EstimatedMinMinutes" integer NULL,
              ADD COLUMN "EstimatedMaxMinutes" integer NULL,
              ADD COLUMN "ReservedMinutes" integer NULL,
              ADD COLUMN "ShortReservationAcknowledgedBy" integer NULL,
              ADD COLUMN "ShortReservationAcknowledgedAt" timestamptz NULL,
              ADD COLUMN "EstimatedSessions" integer NULL,
              ADD COLUMN "ReturnEarliestOn" date NULL,
              ADD COLUMN "ReturnTargetOn" date NULL,
              ADD COLUMN "ReturnLatestOn" date NULL,
              ADD COLUMN "ReferenceUnitPriceVnd" bigint NULL,
              ADD COLUMN "ReferencePriceVersion" integer NULL,
              ADD COLUMN "DiscountType" varchar(16) NULL,
              ADD COLUMN "DiscountValue" numeric(18,3) NULL,
              ADD COLUMN "DiscountReason" varchar(1000) NULL,
              ADD COLUMN "DiscountQualifyingItemId" uuid NULL,
              ADD COLUMN "DiscountStatus" varchar(16) NULL,
              ADD COLUMN "DiscountBasisHash" varchar(64) NULL,
              ADD COLUMN "DiscountDecidedBy" integer NULL,
              ADD COLUMN "DiscountDecidedAt" timestamptz NULL,
              ADD COLUMN "WarrantyComponent" varchar(500) NULL,
              ADD COLUMN "WarrantyExclusions" varchar(1000) NULL,
              ADD COLUMN "WarrantyActivatedOn" date NULL,
              ADD COLUMN "WarrantyExplanation" varchar(2000) NULL,
              ADD COLUMN "ExpectedServiceLifeMonths" integer NULL;
            ALTER TABLE "TREATMENT_PLAN_ITEM" ADD CONSTRAINT "CK_TREATMENT_ITEM_DURATION_V23"
              CHECK (("EstimatedMinMinutes" IS NULL AND "EstimatedMaxMinutes" IS NULL OR
                "EstimatedMinMinutes" > 0 AND "EstimatedMinMinutes" <= "EstimatedMinutes" AND
                "EstimatedMinutes" <= "EstimatedMaxMinutes" AND "EstimatedMaxMinutes" <= 480) AND
                ("ReservedMinutes" IS NULL OR "ReservedMinutes" BETWEEN 15 AND 480 AND
                "ReservedMinutes" % 15 = 0 AND "ReservedMinutes" >= "EstimatedMinutes"));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "TREATMENT_PLAN_ITEM" DROP CONSTRAINT "CK_TREATMENT_ITEM_DURATION_V23";
            ALTER TABLE "TREATMENT_PLAN_ITEM"
              DROP COLUMN "EstimatedMinMinutes", DROP COLUMN "EstimatedMaxMinutes",
              DROP COLUMN "ReservedMinutes", DROP COLUMN "ShortReservationAcknowledgedBy",
              DROP COLUMN "ShortReservationAcknowledgedAt", DROP COLUMN "EstimatedSessions",
              DROP COLUMN "ReturnEarliestOn", DROP COLUMN "ReturnTargetOn", DROP COLUMN "ReturnLatestOn",
              DROP COLUMN "ReferenceUnitPriceVnd", DROP COLUMN "ReferencePriceVersion",
              DROP COLUMN "DiscountType", DROP COLUMN "DiscountValue", DROP COLUMN "DiscountReason",
              DROP COLUMN "DiscountQualifyingItemId", DROP COLUMN "DiscountStatus", DROP COLUMN "DiscountBasisHash",
              DROP COLUMN "DiscountDecidedBy", DROP COLUMN "DiscountDecidedAt",
              DROP COLUMN "WarrantyComponent", DROP COLUMN "WarrantyExclusions",
              DROP COLUMN "WarrantyActivatedOn", DROP COLUMN "WarrantyExplanation",
              DROP COLUMN "ExpectedServiceLifeMonths";
            ALTER TABLE "TREATMENT_DOSSIER"
              DROP COLUMN "DepositExceptionReason", DROP COLUMN "DepositExceptionBy",
              DROP COLUMN "DepositExceptionAt";
            ALTER TABLE "SCHEDULING_SERVICE" DROP CONSTRAINT "CK_SERVICE_REFERENCE_PRICE";
            ALTER TABLE "SCHEDULING_SERVICE"
              DROP COLUMN "ReferenceUnitPriceVnd", DROP COLUMN "PriceVersion";
            """);
    }
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DCMS.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DcmsDbContext))]
[Migration("20261025120000_TreatmentStepDurationRange")]
public sealed class TreatmentStepDurationRange : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "TREATMENT_STEP" ADD COLUMN "EstimatedMinMinutes" integer;
            ALTER TABLE "TREATMENT_STEP" ADD COLUMN "EstimatedMaxMinutes" integer;
            ALTER TABLE "TREATMENT_STEP" ADD COLUMN "ShortReservationAcknowledgedBy" integer;
            ALTER TABLE "TREATMENT_STEP" ADD COLUMN "ShortReservationAcknowledgedAt" timestamptz;
            ALTER TABLE "TREATMENT_STEP" ADD CONSTRAINT "CK_STEP_ESTIMATE_RANGE"
              CHECK (("EstimatedMinMinutes" IS NULL AND "EstimatedMaxMinutes" IS NULL)
                OR ("EstimatedMinMinutes" IS NOT NULL AND "EstimatedMaxMinutes" IS NOT NULL
                AND "EstimatedMinMinutes" > 0 AND "EstimatedMinMinutes" <= "EstimatedMinutes"
                AND "EstimatedMinutes" <= "EstimatedMaxMinutes" AND "EstimatedMaxMinutes" <= 480));
            CREATE UNIQUE INDEX "IX_FOLLOW_UP_INSTRUCTION_ActiveStep"
              ON "FOLLOW_UP_INSTRUCTION" ("StepId")
              WHERE "StepId" IS NOT NULL AND "Status" IN ('NeedsBooking','Booked');
            CREATE UNIQUE INDEX "IX_FOLLOW_UP_INSTRUCTION_ActiveItemOnly"
              ON "FOLLOW_UP_INSTRUCTION" ("DossierId", "ItemId")
              WHERE "DossierId" IS NOT NULL AND "ItemId" IS NOT NULL AND "StepId" IS NULL
                AND "Status" IN ('NeedsBooking','Booked');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX "IX_FOLLOW_UP_INSTRUCTION_ActiveItemOnly";
            DROP INDEX "IX_FOLLOW_UP_INSTRUCTION_ActiveStep";
            ALTER TABLE "TREATMENT_STEP" DROP CONSTRAINT "CK_STEP_ESTIMATE_RANGE";
            ALTER TABLE "TREATMENT_STEP" DROP COLUMN "ShortReservationAcknowledgedAt";
            ALTER TABLE "TREATMENT_STEP" DROP COLUMN "ShortReservationAcknowledgedBy";
            ALTER TABLE "TREATMENT_STEP" DROP COLUMN "EstimatedMaxMinutes";
            ALTER TABLE "TREATMENT_STEP" DROP COLUMN "EstimatedMinMinutes";
            """);
    }
}

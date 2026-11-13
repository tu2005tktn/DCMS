using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PatientNotificationRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // APPOINTMENT_REMINDER moved into the appointment's JSONB in ConsolidateSchedulingStorage. On databases that
            // applied that migration first, the per-recipient rule is enforced by code (docs/specs/SCHEDULING-STORAGE-REVIEW.vi.md).
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('"APPOINTMENT_REMINDER"') IS NOT NULL THEN
                        DROP INDEX IF EXISTS "IX_APPOINTMENT_REMINDER_AppointmentId_AppointmentVersion_LeadM~";
                        CREATE UNIQUE INDEX "IX_APPOINTMENT_REMINDER_AppointmentId_AppointmentVersion_LeadM~"
                            ON "APPOINTMENT_REMINDER" ("AppointmentId", "AppointmentVersion", "LeadMinutes", "AccountId");
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // APPOINTMENT_REMINDER moved into the appointment's JSONB in ConsolidateSchedulingStorage. On databases that
            // applied that migration first, the per-recipient rule is enforced by code (docs/specs/SCHEDULING-STORAGE-REVIEW.vi.md).
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('"APPOINTMENT_REMINDER"') IS NOT NULL THEN
                        DROP INDEX IF EXISTS "IX_APPOINTMENT_REMINDER_AppointmentId_AppointmentVersion_LeadM~";
                        CREATE UNIQUE INDEX "IX_APPOINTMENT_REMINDER_AppointmentId_AppointmentVersion_LeadM~"
                            ON "APPOINTMENT_REMINDER" ("AppointmentId", "AppointmentVersion", "LeadMinutes");
                    END IF;
                END $$;
                """);
        }
    }
}

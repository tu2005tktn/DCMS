using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations;

// docs/specs/NOTIFICATION-STORAGE.vi.md
public partial class ConsolidateNotificationStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET LOCAL TimeZone = 'UTC';

            ALTER TABLE "IN_APP_NOTIFICATION"
                ADD COLUMN "NotificationId" uuid,
                ADD COLUMN "Channel" character varying(16),
                ADD COLUMN "Status" character varying(16),
                ADD COLUMN "AttemptCount" integer,
                ADD COLUMN "LastAttemptAt" timestamp with time zone,
                ADD COLUMN "NextRetryAt" timestamp with time zone,
                ADD COLUMN "LastError" character varying(1000),
                ADD COLUMN "Error" character varying(1000);
            INSERT INTO "IN_APP_NOTIFICATION" ("NotificationRecordKind", "Id", "AttemptCount", "Channel", "LastAttemptAt", "LastError", "NextRetryAt", "NotificationId", "Status")
            SELECT 'NotificationDelivery', s."Id", s."AttemptCount", s."Channel", s."LastAttemptAt", s."LastError", s."NextRetryAt", s."NotificationId", s."Status" FROM "NOTIFICATION_DELIVERY" s;
            INSERT INTO "IN_APP_NOTIFICATION" ("NotificationRecordKind", "Id", "AppointmentId", "AttemptCount", "BranchId", "CreatedAt", "Error", "Kind", "NotificationId", "Status")
            SELECT 'StaffHandlingItem', s."Id", s."AppointmentId", s."AttemptCount", s."BranchId", s."CreatedAt", s."Error", s."Kind", s."NotificationId", s."Status" FROM "STAFF_HANDLING_ITEM" s;

            DO $verify$
            BEGIN
                IF (SELECT count(*) FROM "NOTIFICATION_DELIVERY") <> (SELECT count(*) FROM "IN_APP_NOTIFICATION" WHERE "NotificationRecordKind" = 'NotificationDelivery')
                    OR (SELECT count(*) FROM "STAFF_HANDLING_ITEM") <> (SELECT count(*) FROM "IN_APP_NOTIFICATION" WHERE "NotificationRecordKind" = 'StaffHandlingItem') THEN
                    RAISE EXCEPTION 'Notification storage conversion row counts do not match';
                END IF;
            END $verify$;

            DROP TABLE "NOTIFICATION_DELIVERY";
            DROP TABLE "STAFF_HANDLING_ITEM";
            ALTER TABLE "IN_APP_NOTIFICATION" ADD CONSTRAINT "FK_IN_APP_NOTIFICATION_IN_APP_NOTIFICATION_NotificationId" FOREIGN KEY ("NotificationId") REFERENCES "IN_APP_NOTIFICATION" ("Id") ON DELETE RESTRICT;
            CREATE INDEX "IX_IN_APP_NOTIFICATION_NotificationId" ON "IN_APP_NOTIFICATION" ("NotificationId");
            CREATE UNIQUE INDEX "IX_IN_APP_NOTIFICATION_NotificationId_Channel" ON "IN_APP_NOTIFICATION" ("NotificationId", "Channel") WHERE "NotificationRecordKind" = 'NotificationDelivery';
            CREATE INDEX "IX_IN_APP_NOTIFICATION_Status_CreatedAt" ON "IN_APP_NOTIFICATION" ("Status", "CreatedAt") WHERE "NotificationRecordKind" = 'StaffHandlingItem';
            CREATE INDEX "IX_IN_APP_NOTIFICATION_Status_NextRetryAt" ON "IN_APP_NOTIFICATION" ("Status", "NextRetryAt") WHERE "NotificationRecordKind" = 'NotificationDelivery';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET LOCAL TimeZone = 'UTC';

            CREATE TABLE "NOTIFICATION_DELIVERY" (
                "Id" uuid NOT NULL,
                "AttemptCount" integer NOT NULL,
                "Channel" character varying(16) NOT NULL,
                "LastAttemptAt" timestamp with time zone,
                "LastError" character varying(1000),
                "NextRetryAt" timestamp with time zone,
                "NotificationId" uuid NOT NULL,
                "Status" character varying(16) NOT NULL,
                CONSTRAINT "PK_NOTIFICATION_DELIVERY" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_NOTIFICATION_DELIVERY_IN_APP_NOTIFICATION_NotificationId" FOREIGN KEY ("NotificationId") REFERENCES "IN_APP_NOTIFICATION" ("Id") ON DELETE CASCADE
            );
            CREATE TABLE "STAFF_HANDLING_ITEM" (
                "Id" uuid NOT NULL,
                "AppointmentId" uuid,
                "AttemptCount" integer NOT NULL,
                "BranchId" integer,
                "CreatedAt" timestamp with time zone NOT NULL,
                "Error" character varying(1000) NOT NULL,
                "Kind" character varying(40) NOT NULL,
                "NotificationId" uuid,
                "Status" character varying(16) NOT NULL,
                CONSTRAINT "PK_STAFF_HANDLING_ITEM" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_STAFF_HANDLING_ITEM_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("AppointmentId") ON DELETE RESTRICT,
                CONSTRAINT "FK_STAFF_HANDLING_ITEM_IN_APP_NOTIFICATION_NotificationId" FOREIGN KEY ("NotificationId") REFERENCES "IN_APP_NOTIFICATION" ("Id") ON DELETE RESTRICT
            );
            INSERT INTO "NOTIFICATION_DELIVERY" ("Id", "AttemptCount", "Channel", "LastAttemptAt", "LastError", "NextRetryAt", "NotificationId", "Status")
            SELECT "Id", "AttemptCount", "Channel", "LastAttemptAt", "LastError", "NextRetryAt", "NotificationId", "Status" FROM "IN_APP_NOTIFICATION" WHERE "NotificationRecordKind" = 'NotificationDelivery';
            INSERT INTO "STAFF_HANDLING_ITEM" ("Id", "AppointmentId", "AttemptCount", "BranchId", "CreatedAt", "Error", "Kind", "NotificationId", "Status")
            SELECT "Id", "AppointmentId", "AttemptCount", "BranchId", "CreatedAt", "Error", "Kind", "NotificationId", "Status" FROM "IN_APP_NOTIFICATION" WHERE "NotificationRecordKind" = 'StaffHandlingItem';
            DELETE FROM "IN_APP_NOTIFICATION" WHERE "NotificationRecordKind" IN ('NotificationDelivery', 'StaffHandlingItem');
            DROP INDEX "IX_IN_APP_NOTIFICATION_NotificationId";
            DROP INDEX "IX_IN_APP_NOTIFICATION_NotificationId_Channel";
            DROP INDEX "IX_IN_APP_NOTIFICATION_Status_CreatedAt";
            DROP INDEX "IX_IN_APP_NOTIFICATION_Status_NextRetryAt";
            ALTER TABLE "IN_APP_NOTIFICATION" DROP CONSTRAINT "FK_IN_APP_NOTIFICATION_IN_APP_NOTIFICATION_NotificationId";
            ALTER TABLE "IN_APP_NOTIFICATION" DROP COLUMN "NotificationId", DROP COLUMN "Channel", DROP COLUMN "Status", DROP COLUMN "AttemptCount", DROP COLUMN "LastAttemptAt", DROP COLUMN "NextRetryAt", DROP COLUMN "LastError", DROP COLUMN "Error";
            CREATE UNIQUE INDEX "IX_NOTIFICATION_DELIVERY_NotificationId_Channel" ON "NOTIFICATION_DELIVERY" ("NotificationId", "Channel");
            CREATE INDEX "IX_NOTIFICATION_DELIVERY_Status_NextRetryAt" ON "NOTIFICATION_DELIVERY" ("Status", "NextRetryAt");
            CREATE INDEX "IX_STAFF_HANDLING_ITEM_AppointmentId" ON "STAFF_HANDLING_ITEM" ("AppointmentId");
            CREATE INDEX "IX_STAFF_HANDLING_ITEM_NotificationId" ON "STAFF_HANDLING_ITEM" ("NotificationId");
            CREATE INDEX "IX_STAFF_HANDLING_ITEM_Status_CreatedAt" ON "STAFF_HANDLING_ITEM" ("Status", "CreatedAt");
            """);
    }
}

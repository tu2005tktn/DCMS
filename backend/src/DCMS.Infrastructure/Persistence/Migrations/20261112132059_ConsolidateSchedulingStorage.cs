using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations;

public partial class ConsolidateSchedulingStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE SEQUENCE scheduling_chair_seq AS integer;
            
            ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "StorageStamp" uuid NOT NULL DEFAULT gen_random_uuid();
            
            ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "Assets" jsonb NOT NULL DEFAULT '[]'::jsonb;
            
            ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "Chairs" jsonb NOT NULL DEFAULT '[]'::jsonb;
            
            ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "Members" jsonb NOT NULL DEFAULT '[]'::jsonb;
            
            ALTER TABLE "AUDIT_LOG" ALTER COLUMN "AuditType" TYPE character varying(34);
            
            ALTER TABLE "AUDIT_LOG" ADD COLUMN "AppointmentId" uuid;
            
            ALTER TABLE "AUDIT_LOG" ADD COLUMN "ActorKind" character varying(16);
            
            ALTER TABLE "AUDIT_LOG" ADD COLUMN "OldStart" timestamp with time zone;
            
            ALTER TABLE "AUDIT_LOG" ADD COLUMN "OldEnd" timestamp with time zone;
            
            ALTER TABLE "AUDIT_LOG" ADD COLUMN "OldDentistId" integer;
            
            ALTER TABLE "AUDIT_LOG" ADD COLUMN "OldChairId" integer;
            
            ALTER TABLE "AUDIT_LOG" ADD COLUMN "NewStart" timestamp with time zone;
            
            ALTER TABLE "AUDIT_LOG" ADD COLUMN "NewEnd" timestamp with time zone;
            
            ALTER TABLE "AUDIT_LOG" ADD COLUMN "NewDentistId" integer;
            
            ALTER TABLE "AUDIT_LOG" ADD COLUMN "NewChairId" integer;
            
            ALTER TABLE "AUDIT_LOG" ADD COLUMN "RequesterVerification" character varying(500);
            
            ALTER TABLE "AUDIT_LOG" ADD COLUMN "RequestKey" uuid;
            
            ALTER TABLE "AUDIT_LOG" ADD COLUMN "RequestId" uuid;
            
            ALTER TABLE "AUDIT_LOG" ADD COLUMN "Notes" character varying(500);
            
            ALTER TABLE "IN_APP_NOTIFICATION" ADD COLUMN "NotificationRecordKind" character varying(34) NOT NULL DEFAULT 'InAppNotification';
            
            ALTER TABLE "IN_APP_NOTIFICATION" ADD COLUMN "AggregateId" uuid;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ADD COLUMN "EventType" character varying(80);
            
            ALTER TABLE "IN_APP_NOTIFICATION" ADD COLUMN "PayloadJson" jsonb;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ADD COLUMN "OccurredAt" timestamp with time zone;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ADD COLUMN "DispatchedAt" timestamp with time zone;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ALTER COLUMN "PublicId" DROP NOT NULL;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ALTER COLUMN "Kind" DROP NOT NULL;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ALTER COLUMN "Title" DROP NOT NULL;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ALTER COLUMN "Body" DROP NOT NULL;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ALTER COLUMN "CreatedAt" DROP NOT NULL;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ALTER COLUMN "HasExternalFailure" DROP NOT NULL;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ALTER COLUMN "DedupeKey" DROP NOT NULL;
            
            UPDATE "SCHEDULING_BRANCH" b SET "Members"=COALESCE((SELECT jsonb_agg(to_jsonb(m) ORDER BY m."EmployeeId") FROM "SCHEDULING_MEMBER" m WHERE m."BranchId"=b."Id"),'[]'::jsonb), "Chairs"=COALESCE((SELECT jsonb_agg(to_jsonb(c) ORDER BY c."Id") FROM "SCHEDULING_CHAIR" c WHERE c."BranchId"=b."Id"),'[]'::jsonb), "Assets"=COALESCE((SELECT jsonb_agg(to_jsonb(a) ORDER BY a."Id") FROM "BRANCH_ASSET" a WHERE a."BranchId"=b."Id"),'[]'::jsonb);
            
            SELECT setval('scheduling_chair_seq',GREATEST(COALESCE((SELECT max("Id") FROM "SCHEDULING_CHAIR"),0),1),EXISTS(SELECT 1 FROM "SCHEDULING_CHAIR"));
            
            CREATE TABLE "WORKING_SCHEDULE" (
                "Id" uuid NOT NULL,
                "EmployeeId" integer NOT NULL,
                "Version" integer NOT NULL,
                "PublishedBy" integer NOT NULL,
                "PublishedAt" timestamp with time zone NOT NULL,
                "ScheduleKind" character varying(34) NOT NULL,
                "Date" date,
                "Kind" character varying(16),
                "Reason" character varying(500),
                "BranchId" integer,
                "WeekStart" date,
                "Periods" jsonb NOT NULL,
                CONSTRAINT "PK_WORKING_SCHEDULE" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_WORKING_SCHEDULE_KIND" CHECK ("ScheduleKind" <> 'WorkingScheduleRevision' OR ("Kind" IN ('Template','Exception') AND "Version" > 0)),
                CONSTRAINT "FK_WORKING_SCHEDULE_SCHEDULING_BRANCH_BranchId" FOREIGN KEY ("BranchId") REFERENCES "SCHEDULING_BRANCH" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_WORKING_SCHEDULE_USER_ACCOUNT_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT,
                CONSTRAINT "FK_WORKING_SCHEDULE_USER_ACCOUNT_PublishedBy" FOREIGN KEY ("PublishedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            INSERT INTO "WORKING_SCHEDULE" ("Id", "BranchId", "EmployeeId", "PublishedAt", "PublishedBy", "Version", "WeekStart", "Periods", "ScheduleKind")
            SELECT s."Id", s."BranchId", s."EmployeeId", s."PublishedAt", s."PublishedBy", s."Version", s."WeekStart", s."Periods", 'WorkingWeek' FROM "WORKING_WEEK" s;
            
            INSERT INTO "WORKING_SCHEDULE" ("Id", "Date", "EmployeeId", "Kind", "PublishedAt", "PublishedBy", "Reason", "Version", "Periods", "ScheduleKind")
            SELECT s."Id", s."Date", s."EmployeeId", s."Kind", s."PublishedAt", s."PublishedBy", s."Reason", s."Version", s."Periods", 'WorkingScheduleRevision' FROM "WORKING_SCHEDULE_REVISION" s;
            
            INSERT INTO "AUDIT_LOG" ("LegacyId", "Action", "ActorAccountId", "ActorKind", "AppointmentId", "NewChairId", "NewDentistId", "NewEnd", "NewStart", "OccurredAt", "OldChairId", "OldDentistId", "OldEnd", "OldStart", "Reason", "RequestKey", "RequesterVerification", "StorageId", "AuditType")
            SELECT s."Id", s."Action", s."ActorAccountId", s."ActorKind", s."AppointmentId", s."NewChairId", s."NewDentistId", s."NewEnd", s."NewStart", s."OccurredAt", s."OldChairId", s."OldDentistId", s."OldEnd", s."OldStart", s."Reason", s."RequestKey", s."RequesterVerification", gen_random_uuid(), 'AppointmentChange' FROM "APPOINTMENT_CHANGE" s;
            
            INSERT INTO "AUDIT_LOG" ("LegacyId", "Action", "ActorAccountId", "ActorKind", "AppointmentId", "Notes", "OccurredAt", "RequestId", "StorageId", "AuditType")
            SELECT s."Id", s."Action", s."ActorAccountId", s."ActorKind", s."AppointmentId", s."Notes", s."OccurredAt", s."RequestId", gen_random_uuid(), 'AppointmentSupportHistory' FROM "APPOINTMENT_SUPPORT_HISTORY" s;
            
            INSERT INTO "IN_APP_NOTIFICATION" ("Id", "AggregateId", "DispatchedAt", "EventType", "OccurredAt", "PayloadJson", "NotificationRecordKind")
            SELECT s."Id", s."AggregateId", s."DispatchedAt", s."EventType", s."OccurredAt", s."PayloadJson", 'AppointmentOutboxEvent' FROM "APPOINTMENT_OUTBOX" s;
            
            ALTER TABLE "IN_APP_NOTIFICATION" DROP CONSTRAINT "FK_IN_APP_NOTIFICATION_APPOINTMENT_AppointmentId";
            
            ALTER TABLE "IN_APP_NOTIFICATION" DROP CONSTRAINT "FK_IN_APP_NOTIFICATION_APPOINTMENT_SUPPORT_REQUEST_RequestId";
            
            ALTER TABLE "STAFF_HANDLING_ITEM" DROP CONSTRAINT "FK_STAFF_HANDLING_ITEM_APPOINTMENT_AppointmentId";
            
            ALTER TABLE "TREATMENT_DOSSIER" DROP CONSTRAINT "FK_TREATMENT_DOSSIER_MEMBER";
            
            ALTER TABLE "COMMAND_RECEIPT" DROP CONSTRAINT "FK_COMMAND_RECEIPT_APPOINTMENT_AppointmentId";
            
            ALTER TABLE "COMMAND_RECEIPT" DROP CONSTRAINT "FK_COMMAND_RECEIPT_APPOINTMENT_SUPPORT_REQUEST_SupportRequestId";
            
            ALTER TABLE "VISIT" DROP CONSTRAINT "FK_VISIT_APPOINTMENT_AppointmentId";
            
            ALTER TABLE "APPOINTMENT_CHANGE" DROP CONSTRAINT "FK_APPOINTMENT_CHANGE_APPOINTMENT_AppointmentId";
            
            ALTER TABLE "APPOINTMENT_OUTBOX" DROP CONSTRAINT "FK_APPOINTMENT_OUTBOX_APPOINTMENT_AggregateId";
            
            ALTER TABLE "APPOINTMENT_REMINDER" DROP CONSTRAINT "FK_APPOINTMENT_REMINDER_APPOINTMENT_AppointmentId";
            
            ALTER TABLE "APPOINTMENT_SUPPORT_HISTORY" DROP CONSTRAINT "FK_APPOINTMENT_SUPPORT_HISTORY_APPOINTMENT_AppointmentId";
            
            ALTER TABLE "APPOINTMENT_SUPPORT_REQUEST" DROP CONSTRAINT "FK_APPOINTMENT_SUPPORT_REQUEST_APPOINTMENT_AppointmentId";
            
            ALTER TABLE "APPOINTMENT" RENAME TO "APPOINTMENT_BEFORE";
            
            ALTER TABLE "APPOINTMENT_BEFORE" DROP CONSTRAINT "PK_APPOINTMENT";
            
            CREATE TABLE "APPOINTMENT" (
                "Id" uuid NOT NULL,
                "AppointmentId" uuid NOT NULL,
                "StorageStamp" uuid NOT NULL,
                "ReservationCode" character varying(40) NOT NULL,
                "BranchId" integer NOT NULL,
                "DentistId" integer NOT NULL,
                "AssistantId" integer,
                "ChairId" integer NOT NULL,
                "ServiceId" integer,
                "StartAt" timestamp with time zone NOT NULL,
                "EndAt" timestamp with time zone NOT NULL,
                "AttendeeKey" character varying(160),
                "AttendeeLabel" character varying(160) NOT NULL,
                "ReservationStatus" character varying(16) NOT NULL,
                "Code" character varying(40),
                "BookerAccountId" integer,
                "FamilyMemberId" uuid,
                "BookerName" character varying(100),
                "BookerPhone" character varying(24),
                "BookerEmail" character varying(254),
                "PatientId" integer,
                "AttendeeName" character varying(100),
                "AttendeeBirthDate" date,
                "AttendeeRelation" character varying(16),
                "Kind" character varying(16),
                "Source" character varying(24),
                "Reason" character varying(1000),
                "FollowUpId" uuid,
                "PatientConsentConfirmedAt" timestamp with time zone,
                "PatientConsentConfirmedBy" integer,
                "ExceptionReason" character varying(500),
                "Status" character varying(24),
                "ConfirmedToAttend" character varying(16) DEFAULT 'Pending',
                "Version" integer,
                "CreatedAt" timestamp with time zone,
                "CancelledAt" timestamp with time zone,
                "CancelledBy" integer,
                "CancelReason" character varying(500),
                "Reminders" jsonb NOT NULL DEFAULT '[]'::jsonb,
                "SupportRequests" jsonb NOT NULL DEFAULT '[]'::jsonb,
                CONSTRAINT "PK_APPOINTMENT" PRIMARY KEY ("Id"),
                CONSTRAINT "AK_APPOINTMENT_AppointmentId" UNIQUE ("AppointmentId"),
                CONSTRAINT "CK_APPOINTMENT_RANGE" CHECK ("StartAt" < "EndAt" AND ("AssistantId" IS NULL OR "AssistantId" <> "DentistId")),
                CONSTRAINT "FK_APPOINTMENT_CUSTOMER_FAMILY_MEMBER_FamilyMemberId" FOREIGN KEY ("FamilyMemberId") REFERENCES "CUSTOMER_FAMILY_MEMBER" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_PATIENT_PROFILE_PatientId" FOREIGN KEY ("PatientId") REFERENCES "PATIENT_PROFILE" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_SCHEDULING_BRANCH_BranchId" FOREIGN KEY ("BranchId") REFERENCES "SCHEDULING_BRANCH" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_SCHEDULING_SERVICE_ServiceId_BranchId" FOREIGN KEY ("ServiceId", "BranchId") REFERENCES "SCHEDULING_SERVICE" ("Id", "BranchId") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_USER_ACCOUNT_AssistantId" FOREIGN KEY ("AssistantId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_USER_ACCOUNT_BookerAccountId" FOREIGN KEY ("BookerAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_USER_ACCOUNT_CancelledBy" FOREIGN KEY ("CancelledBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_USER_ACCOUNT_DentistId" FOREIGN KEY ("DentistId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_USER_ACCOUNT_PatientConsentConfirmedBy" FOREIGN KEY ("PatientConsentConfirmedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            INSERT INTO "APPOINTMENT" ("Id", "AssistantId", "AttendeeKey", "AttendeeLabel", "BranchId", "ChairId", "ReservationCode", "DentistId", "EndAt", "ServiceId", "StartAt", "ReservationStatus", "AppointmentId", "AttendeeBirthDate", "AttendeeName", "AttendeeRelation", "BookerAccountId", "BookerEmail", "BookerName", "BookerPhone", "CancelReason", "CancelledAt", "CancelledBy", "Code", "ConfirmedToAttend", "CreatedAt", "ExceptionReason", "FamilyMemberId", "FollowUpId", "Kind", "PatientConsentConfirmedAt", "PatientConsentConfirmedBy", "PatientId", "Reason", "Source", "Status", "Version", "StorageStamp", "SupportRequests", "Reminders")
            SELECT r."Id", r."AssistantId", r."AttendeeKey", r."AttendeeLabel", r."BranchId", r."ChairId", r."Code", r."DentistId", r."End", r."ServiceId", r."Start", r."Status", COALESCE(a."Id",r."Id"), a."AttendeeBirthDate", a."AttendeeName", a."AttendeeRelation", a."BookerAccountId", a."BookerEmail", a."BookerName", a."BookerPhone", a."CancelReason", a."CancelledAt", a."CancelledBy", a."Code", a."ConfirmedToAttend", a."CreatedAt", a."ExceptionReason", a."FamilyMemberId", a."FollowUpId", a."Kind", a."PatientConsentConfirmedAt", a."PatientConsentConfirmedBy", a."PatientId", a."Reason", a."Source", a."Status", a."Version", gen_random_uuid(), COALESCE((SELECT jsonb_agg(to_jsonb(child) ORDER BY child."Id") FROM "APPOINTMENT_SUPPORT_REQUEST" child WHERE child."AppointmentId"=a."Id"),'[]'::jsonb), COALESCE((SELECT jsonb_agg(to_jsonb(child) ORDER BY child."Id") FROM "APPOINTMENT_REMINDER" child WHERE child."AppointmentId"=a."Id"),'[]'::jsonb) FROM "RESOURCE_RESERVATION" r LEFT JOIN "APPOINTMENT_BEFORE" a ON a."ReservationId"=r."Id";
            
            DO $validate$ BEGIN
            IF EXISTS(SELECT 1 FROM "APPOINTMENT_BEFORE" a JOIN "RESOURCE_RESERVATION" r ON r."Id"=a."ReservationId" WHERE (a."BranchId",a."DentistId",a."AssistantId",a."ChairId",a."ServiceId",a."StartAt",a."EndAt") IS DISTINCT FROM (r."BranchId",r."DentistId",r."AssistantId",r."ChairId",r."ServiceId",r."Start",r."End")) THEN RAISE EXCEPTION 'Appointment and reservation resource fields disagree'; END IF;
            IF (SELECT count(*) FROM "APPOINTMENT_BEFORE")<>(SELECT count(*) FROM "APPOINTMENT" WHERE "CreatedAt" IS NOT NULL) THEN RAISE EXCEPTION 'Incomplete appointment transfer'; END IF;
            IF (SELECT count(*) FROM "RESOURCE_RESERVATION")<>(SELECT count(*) FROM "APPOINTMENT") THEN RAISE EXCEPTION 'Incomplete reservation transfer'; END IF;
            END $validate$;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ADD CONSTRAINT "FK_IN_APP_NOTIFICATION_APPOINTMENT_AggregateId" FOREIGN KEY ("AggregateId") REFERENCES "APPOINTMENT" ("AppointmentId") ON DELETE RESTRICT;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ADD CONSTRAINT "FK_IN_APP_NOTIFICATION_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("AppointmentId") ON DELETE RESTRICT;
            
            ALTER TABLE "AUDIT_LOG" ADD CONSTRAINT "FK_AUDIT_LOG_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("AppointmentId") ON DELETE RESTRICT;
            
            ALTER TABLE "STAFF_HANDLING_ITEM" ADD CONSTRAINT "FK_STAFF_HANDLING_ITEM_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("AppointmentId") ON DELETE RESTRICT;
            
            ALTER TABLE "COMMAND_RECEIPT" ADD CONSTRAINT "FK_COMMAND_RECEIPT_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("AppointmentId") ON DELETE RESTRICT;
            
            ALTER TABLE "VISIT" ADD CONSTRAINT "FK_VISIT_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("AppointmentId") ON DELETE RESTRICT;
            
            DROP TABLE "APPOINTMENT_CHANGE", "APPOINTMENT_OUTBOX", "APPOINTMENT_REMINDER", "APPOINTMENT_SUPPORT_HISTORY", "APPOINTMENT_SUPPORT_REQUEST", "BRANCH_ASSET", "RESOURCE_RESERVATION", "SCHEDULING_CHAIR", "SCHEDULING_MEMBER", "WORKING_SCHEDULE_REVISION", "WORKING_WEEK", "APPOINTMENT_BEFORE";
            
            CREATE INDEX "IX_APPOINTMENT_AssistantId" ON "APPOINTMENT" ("AssistantId");
            
            CREATE INDEX "IX_APPOINTMENT_BookerAccountId_FamilyMemberId_StartAt" ON "APPOINTMENT" ("BookerAccountId", "FamilyMemberId", "StartAt");
            
            CREATE INDEX "IX_APPOINTMENT_BranchId" ON "APPOINTMENT" ("BranchId");
            
            CREATE INDEX "IX_APPOINTMENT_BranchId_StartAt" ON "APPOINTMENT" ("BranchId", "StartAt");
            
            CREATE INDEX "IX_APPOINTMENT_CancelledBy" ON "APPOINTMENT" ("CancelledBy");
            
            CREATE UNIQUE INDEX "IX_APPOINTMENT_Code" ON "APPOINTMENT" ("Code");
            
            CREATE INDEX "IX_APPOINTMENT_DentistId" ON "APPOINTMENT" ("DentistId");
            
            CREATE INDEX "IX_APPOINTMENT_FamilyMemberId" ON "APPOINTMENT" ("FamilyMemberId");
            
            CREATE UNIQUE INDEX "IX_APPOINTMENT_FollowUpId" ON "APPOINTMENT" ("FollowUpId") WHERE "FollowUpId" IS NOT NULL AND "Status" IN ('Confirmed','CheckedIn');
            
            CREATE INDEX "IX_APPOINTMENT_PatientConsentConfirmedBy" ON "APPOINTMENT" ("PatientConsentConfirmedBy");
            
            CREATE INDEX "IX_APPOINTMENT_PatientId" ON "APPOINTMENT" ("PatientId");
            
            CREATE UNIQUE INDEX "IX_APPOINTMENT_ReservationCode" ON "APPOINTMENT" ("ReservationCode");
            
            CREATE INDEX "IX_APPOINTMENT_ServiceId_BranchId" ON "APPOINTMENT" ("ServiceId", "BranchId");
            
            CREATE INDEX "IX_APPOINTMENT_StartAt_EndAt" ON "APPOINTMENT" ("StartAt", "EndAt");
            
            CREATE UNIQUE INDEX "IX_WORKING_SCHEDULE_BranchId_EmployeeId_WeekStart" ON "WORKING_SCHEDULE" ("BranchId", "EmployeeId", "WeekStart");
            
            CREATE INDEX "IX_WORKING_SCHEDULE_EmployeeId" ON "WORKING_SCHEDULE" ("EmployeeId");
            
            CREATE INDEX "IX_WORKING_SCHEDULE_EmployeeId_Date" ON "WORKING_SCHEDULE" ("EmployeeId", "Date");
            
            CREATE UNIQUE INDEX "IX_WORKING_SCHEDULE_EmployeeId_Version" ON "WORKING_SCHEDULE" ("EmployeeId", "Version") WHERE "ScheduleKind" = 'WorkingScheduleRevision';
            
            CREATE INDEX "IX_WORKING_SCHEDULE_PublishedBy" ON "WORKING_SCHEDULE" ("PublishedBy");
            
            CREATE INDEX "IX_AUDIT_LOG_RequestId_OccurredAt" ON "AUDIT_LOG" ("RequestId", "OccurredAt");
            
            CREATE INDEX "IX_AUDIT_LOG_AppointmentId" ON "AUDIT_LOG" ("AppointmentId");
            
            CREATE UNIQUE INDEX "IX_AUDIT_LOG_ActorAccountId_RequestKey" ON "AUDIT_LOG" ("ActorAccountId", "RequestKey") WHERE "AuditType" = 'AppointmentChange';
            
            CREATE INDEX "IX_AUDIT_LOG_AppointmentId_OccurredAt" ON "AUDIT_LOG" ("AppointmentId", "OccurredAt");
            
            DROP INDEX "IX_IN_APP_NOTIFICATION_RequestId";
            
            CREATE INDEX "IX_IN_APP_NOTIFICATION_AggregateId" ON "IN_APP_NOTIFICATION" ("AggregateId");
            
            CREATE INDEX "IX_IN_APP_NOTIFICATION_DispatchedAt_OccurredAt" ON "IN_APP_NOTIFICATION" ("DispatchedAt", "OccurredAt");
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "APPOINTMENT_CHANGE" (
                "Id" uuid NOT NULL,
                "Action" character varying(16) NOT NULL,
                "ActorAccountId" integer NOT NULL,
                "ActorKind" character varying(8) NOT NULL,
                "AppointmentId" uuid NOT NULL,
                "NewChairId" integer,
                "NewDentistId" integer,
                "NewEnd" timestamp with time zone,
                "NewStart" timestamp with time zone,
                "OccurredAt" timestamp with time zone NOT NULL,
                "OldChairId" integer NOT NULL,
                "OldDentistId" integer NOT NULL,
                "OldEnd" timestamp with time zone NOT NULL,
                "OldStart" timestamp with time zone NOT NULL,
                "Reason" character varying(500) NOT NULL,
                "RequestKey" uuid NOT NULL,
                "RequesterVerification" character varying(500),
                CONSTRAINT "PK_APPOINTMENT_CHANGE" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_APPOINTMENT_CHANGE_USER_ACCOUNT_ActorAccountId" FOREIGN KEY ("ActorAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            CREATE TABLE "APPOINTMENT_OUTBOX" (
                "Id" uuid NOT NULL,
                "AggregateId" uuid NOT NULL,
                "DispatchedAt" timestamp with time zone,
                "EventType" character varying(80) NOT NULL,
                "OccurredAt" timestamp with time zone NOT NULL,
                "PayloadJson" jsonb NOT NULL,
                CONSTRAINT "PK_APPOINTMENT_OUTBOX" PRIMARY KEY ("Id")
            );
            
            CREATE TABLE "APPOINTMENT_REMINDER" (
                "Id" uuid NOT NULL,
                "AccountId" integer NOT NULL,
                "AppointmentId" uuid NOT NULL,
                "AppointmentVersion" integer NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "DueAt" timestamp with time zone NOT NULL,
                "LeadMinutes" integer NOT NULL,
                "ProcessedAt" timestamp with time zone,
                "Status" character varying(16) NOT NULL,
                CONSTRAINT "PK_APPOINTMENT_REMINDER" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_APPOINTMENT_REMINDER_USER_ACCOUNT_AccountId" FOREIGN KEY ("AccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            CREATE TABLE "APPOINTMENT_SUPPORT_REQUEST" (
                "Id" uuid NOT NULL,
                "AppointmentActionRequestKey" uuid,
                "AppointmentId" uuid NOT NULL,
                "Code" character varying(32) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "DecidedAt" timestamp with time zone,
                "DecidedBy" integer,
                "DecisionReason" character varying(500),
                "Kind" character varying(24) NOT NULL,
                "ProposedReason" character varying(40),
                "ProposedStartAt" timestamp with time zone,
                "PublicNote" character varying(500),
                "Reason" character varying(1000),
                "RequestKey" uuid NOT NULL,
                "RequesterAccountId" integer,
                "RequesterName" character varying(160) NOT NULL,
                "RequesterPhone" character varying(24),
                "RequesterVerification" character varying(500),
                "RequesterVerifiedAt" timestamp with time zone,
                "RequesterVerifiedBy" integer,
                "Status" character varying(16) NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "Version" integer NOT NULL,
                CONSTRAINT "PK_APPOINTMENT_SUPPORT_REQUEST" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_SUPPORT_REQUEST_STATUS" CHECK ("Status" IN ('Pending','Resolved','Rejected','Withdrawn')),
                CONSTRAINT "FK_APPOINTMENT_SUPPORT_REQUEST_USER_ACCOUNT_DecidedBy" FOREIGN KEY ("DecidedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_SUPPORT_REQUEST_USER_ACCOUNT_RequesterAccountId" FOREIGN KEY ("RequesterAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_SUPPORT_REQUEST_USER_ACCOUNT_RequesterVerifiedBy" FOREIGN KEY ("RequesterVerifiedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            CREATE TABLE "BRANCH_ASSET" (
                "Id" uuid NOT NULL,
                "BranchId" integer NOT NULL,
                "ContentType" character varying(40) NOT NULL,
                "Sha256" character varying(64) NOT NULL,
                "SizeBytes" bigint NOT NULL,
                "UploadedAt" timestamp with time zone NOT NULL,
                "UploadedBy" integer NOT NULL,
                CONSTRAINT "PK_BRANCH_ASSET" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_BRANCH_ASSET_SCHEDULING_BRANCH_BranchId" FOREIGN KEY ("BranchId") REFERENCES "SCHEDULING_BRANCH" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_BRANCH_ASSET_USER_ACCOUNT_UploadedBy" FOREIGN KEY ("UploadedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            CREATE TABLE "SCHEDULING_CHAIR" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "Active" boolean NOT NULL,
                "BranchId" integer NOT NULL,
                "CloseMinute" integer NOT NULL,
                "Name" character varying(160) NOT NULL,
                "OpenMinute" integer NOT NULL,
                "Version" integer NOT NULL,
                "Code" character varying(40),
                CONSTRAINT "PK_SCHEDULING_CHAIR" PRIMARY KEY ("Id"),
                CONSTRAINT "AK_SCHEDULING_CHAIR_Id_BranchId" UNIQUE ("Id", "BranchId"),
                CONSTRAINT "CK_CHAIR_HOURS" CHECK ("OpenMinute" >= 0 AND "CloseMinute" <= 1440 AND "OpenMinute" < "CloseMinute" AND "OpenMinute" % 15 = 0 AND "CloseMinute" % 15 = 0),
                CONSTRAINT "FK_SCHEDULING_CHAIR_SCHEDULING_BRANCH_BranchId" FOREIGN KEY ("BranchId") REFERENCES "SCHEDULING_BRANCH" ("Id") ON DELETE RESTRICT
            );
            
            CREATE TABLE "SCHEDULING_MEMBER" (
                "BranchId" integer NOT NULL,
                "EmployeeId" integer NOT NULL,
                "Active" boolean NOT NULL,
                "CanDentist" boolean NOT NULL,
                "CanManageSchedules" boolean NOT NULL,
                CONSTRAINT "PK_SCHEDULING_MEMBER" PRIMARY KEY ("BranchId", "EmployeeId"),
                CONSTRAINT "FK_SCHEDULING_MEMBER_SCHEDULING_BRANCH_BranchId" FOREIGN KEY ("BranchId") REFERENCES "SCHEDULING_BRANCH" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_SCHEDULING_MEMBER_USER_ACCOUNT_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT
            );
            
            CREATE TABLE "WORKING_SCHEDULE_REVISION" (
                "Id" uuid NOT NULL,
                "Date" date NOT NULL,
                "EmployeeId" integer NOT NULL,
                "Kind" character varying(16) NOT NULL,
                "PublishedAt" timestamp with time zone NOT NULL,
                "PublishedBy" integer NOT NULL,
                "Reason" character varying(500) NOT NULL,
                "Version" integer NOT NULL,
                "Periods" jsonb NOT NULL,
                CONSTRAINT "PK_WORKING_SCHEDULE_REVISION" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_REVISION_KIND" CHECK ("Kind" IN ('Template','Exception') AND "Version" > 0),
                CONSTRAINT "FK_WORKING_SCHEDULE_REVISION_USER_ACCOUNT_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT,
                CONSTRAINT "FK_WORKING_SCHEDULE_REVISION_USER_ACCOUNT_PublishedBy" FOREIGN KEY ("PublishedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            CREATE TABLE "APPOINTMENT_SUPPORT_HISTORY" (
                "Id" uuid NOT NULL,
                "Action" character varying(40) NOT NULL,
                "ActorAccountId" integer,
                "ActorKind" character varying(16) NOT NULL,
                "AppointmentId" uuid NOT NULL,
                "Notes" character varying(500),
                "OccurredAt" timestamp with time zone NOT NULL,
                "RequestId" uuid NOT NULL,
                CONSTRAINT "PK_APPOINTMENT_SUPPORT_HISTORY" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_APPOINTMENT_SUPPORT_HISTORY_APPOINTMENT_SUPPORT_REQUEST_Req~" FOREIGN KEY ("RequestId") REFERENCES "APPOINTMENT_SUPPORT_REQUEST" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_SUPPORT_HISTORY_USER_ACCOUNT_ActorAccountId" FOREIGN KEY ("ActorAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            CREATE TABLE "RESOURCE_RESERVATION" (
                "Id" uuid NOT NULL,
                "AssistantId" integer,
                "AttendeeKey" character varying(160),
                "AttendeeLabel" character varying(160) NOT NULL,
                "BranchId" integer NOT NULL,
                "ChairId" integer NOT NULL,
                "Code" character varying(40) NOT NULL,
                "DentistId" integer NOT NULL,
                "End" timestamp with time zone NOT NULL,
                "ServiceId" integer,
                "Start" timestamp with time zone NOT NULL,
                "Status" character varying(16) NOT NULL,
                CONSTRAINT "PK_RESOURCE_RESERVATION" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_RESERVATION_RANGE" CHECK ("Start" < "End" AND ("AssistantId" IS NULL OR "AssistantId" <> "DentistId")),
                CONSTRAINT "FK_RESOURCE_RESERVATION_SCHEDULING_CHAIR_ChairId_BranchId" FOREIGN KEY ("ChairId", "BranchId") REFERENCES "SCHEDULING_CHAIR" ("Id", "BranchId") ON DELETE RESTRICT,
                CONSTRAINT "FK_RESOURCE_RESERVATION_SCHEDULING_MEMBER_BranchId_DentistId" FOREIGN KEY ("BranchId", "DentistId") REFERENCES "SCHEDULING_MEMBER" ("BranchId", "EmployeeId") ON DELETE RESTRICT,
                CONSTRAINT "FK_RESOURCE_RESERVATION_SCHEDULING_SERVICE_ServiceId_BranchId" FOREIGN KEY ("ServiceId", "BranchId") REFERENCES "SCHEDULING_SERVICE" ("Id", "BranchId") ON DELETE RESTRICT,
                CONSTRAINT "FK_RESOURCE_RESERVATION_USER_ACCOUNT_AssistantId" FOREIGN KEY ("AssistantId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT
            );
            
            CREATE TABLE "WORKING_WEEK" (
                "Id" uuid NOT NULL,
                "BranchId" integer NOT NULL,
                "EmployeeId" integer NOT NULL,
                "PublishedAt" timestamp with time zone NOT NULL,
                "PublishedBy" integer NOT NULL,
                "Version" integer NOT NULL,
                "WeekStart" date NOT NULL,
                "Periods" jsonb NOT NULL,
                CONSTRAINT "PK_WORKING_WEEK" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_WORKING_WEEK_SCHEDULING_MEMBER_BranchId_EmployeeId" FOREIGN KEY ("BranchId", "EmployeeId") REFERENCES "SCHEDULING_MEMBER" ("BranchId", "EmployeeId") ON DELETE RESTRICT,
                CONSTRAINT "FK_WORKING_WEEK_USER_ACCOUNT_PublishedBy" FOREIGN KEY ("PublishedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            INSERT INTO "SCHEDULING_MEMBER" SELECT restored.* FROM "SCHEDULING_BRANCH" p CROSS JOIN LATERAL jsonb_array_elements(p."Members") item CROSS JOIN LATERAL jsonb_populate_record(NULL::"SCHEDULING_MEMBER",item) restored;
            
            INSERT INTO "SCHEDULING_CHAIR" SELECT restored.* FROM "SCHEDULING_BRANCH" p CROSS JOIN LATERAL jsonb_array_elements(p."Chairs") item CROSS JOIN LATERAL jsonb_populate_record(NULL::"SCHEDULING_CHAIR",item) restored;
            
            INSERT INTO "BRANCH_ASSET" SELECT restored.* FROM "SCHEDULING_BRANCH" p CROSS JOIN LATERAL jsonb_array_elements(p."Assets") item CROSS JOIN LATERAL jsonb_populate_record(NULL::"BRANCH_ASSET",item) restored;
            
            INSERT INTO "APPOINTMENT_SUPPORT_REQUEST" SELECT restored.* FROM "APPOINTMENT" p CROSS JOIN LATERAL jsonb_array_elements(p."SupportRequests") item CROSS JOIN LATERAL jsonb_populate_record(NULL::"APPOINTMENT_SUPPORT_REQUEST",item) restored;
            
            INSERT INTO "APPOINTMENT_REMINDER" SELECT restored.* FROM "APPOINTMENT" p CROSS JOIN LATERAL jsonb_array_elements(p."Reminders") item CROSS JOIN LATERAL jsonb_populate_record(NULL::"APPOINTMENT_REMINDER",item) restored;
            
            INSERT INTO "WORKING_WEEK" ("Id", "BranchId", "EmployeeId", "PublishedAt", "PublishedBy", "Version", "WeekStart", "Periods")
            SELECT s."Id", s."BranchId", s."EmployeeId", s."PublishedAt", s."PublishedBy", s."Version", s."WeekStart", s."Periods" FROM "WORKING_SCHEDULE" s WHERE s."ScheduleKind"='WorkingWeek';
            
            INSERT INTO "WORKING_SCHEDULE_REVISION" ("Id", "Date", "EmployeeId", "Kind", "PublishedAt", "PublishedBy", "Reason", "Version", "Periods")
            SELECT s."Id", s."Date", s."EmployeeId", s."Kind", s."PublishedAt", s."PublishedBy", s."Reason", s."Version", s."Periods" FROM "WORKING_SCHEDULE" s WHERE s."ScheduleKind"='WorkingScheduleRevision';
            
            INSERT INTO "APPOINTMENT_CHANGE" ("Id", "Action", "ActorAccountId", "ActorKind", "AppointmentId", "NewChairId", "NewDentistId", "NewEnd", "NewStart", "OccurredAt", "OldChairId", "OldDentistId", "OldEnd", "OldStart", "Reason", "RequestKey", "RequesterVerification")
            SELECT s."LegacyId", s."Action", s."ActorAccountId", s."ActorKind", s."AppointmentId", s."NewChairId", s."NewDentistId", s."NewEnd", s."NewStart", s."OccurredAt", s."OldChairId", s."OldDentistId", s."OldEnd", s."OldStart", s."Reason", s."RequestKey", s."RequesterVerification" FROM "AUDIT_LOG" s WHERE s."AuditType"='AppointmentChange';
            
            INSERT INTO "APPOINTMENT_SUPPORT_HISTORY" ("Id", "Action", "ActorAccountId", "ActorKind", "AppointmentId", "Notes", "OccurredAt", "RequestId")
            SELECT s."LegacyId", s."Action", s."ActorAccountId", s."ActorKind", s."AppointmentId", s."Notes", s."OccurredAt", s."RequestId" FROM "AUDIT_LOG" s WHERE s."AuditType"='AppointmentSupportHistory';
            
            INSERT INTO "APPOINTMENT_OUTBOX" ("Id", "AggregateId", "DispatchedAt", "EventType", "OccurredAt", "PayloadJson")
            SELECT s."Id", s."AggregateId", s."DispatchedAt", s."EventType", s."OccurredAt", s."PayloadJson" FROM "IN_APP_NOTIFICATION" s WHERE s."NotificationRecordKind"='AppointmentOutboxEvent';
            
            INSERT INTO "RESOURCE_RESERVATION" ("Id", "AssistantId", "AttendeeKey", "AttendeeLabel", "BranchId", "ChairId", "Code", "DentistId", "End", "ServiceId", "Start", "Status") SELECT "Id", "AssistantId", "AttendeeKey", "AttendeeLabel", "BranchId", "ChairId", "ReservationCode", "DentistId", "EndAt", "ServiceId", "StartAt", "ReservationStatus" FROM "APPOINTMENT";
            
            ALTER TABLE "IN_APP_NOTIFICATION" DROP CONSTRAINT "FK_IN_APP_NOTIFICATION_APPOINTMENT_AggregateId";
            
            ALTER TABLE "IN_APP_NOTIFICATION" DROP CONSTRAINT "FK_IN_APP_NOTIFICATION_APPOINTMENT_AppointmentId";
            
            ALTER TABLE "AUDIT_LOG" DROP CONSTRAINT "FK_AUDIT_LOG_APPOINTMENT_AppointmentId";
            
            ALTER TABLE "STAFF_HANDLING_ITEM" DROP CONSTRAINT "FK_STAFF_HANDLING_ITEM_APPOINTMENT_AppointmentId";
            
            ALTER TABLE "COMMAND_RECEIPT" DROP CONSTRAINT "FK_COMMAND_RECEIPT_APPOINTMENT_AppointmentId";
            
            ALTER TABLE "VISIT" DROP CONSTRAINT "FK_VISIT_APPOINTMENT_AppointmentId";
            
            ALTER TABLE "APPOINTMENT" RENAME TO "APPOINTMENT_AFTER";
            
            ALTER TABLE "APPOINTMENT_AFTER" DROP CONSTRAINT "PK_APPOINTMENT";
            
            CREATE TABLE "APPOINTMENT" (
                "Id" uuid NOT NULL,
                "AssistantId" integer,
                "AttendeeBirthDate" date,
                "AttendeeName" character varying(100) NOT NULL,
                "AttendeeRelation" character varying(16) NOT NULL,
                "BookerAccountId" integer,
                "BookerEmail" character varying(254),
                "BookerName" character varying(100) NOT NULL,
                "BookerPhone" character varying(24),
                "BranchId" integer NOT NULL,
                "CancelReason" character varying(500),
                "CancelledAt" timestamp with time zone,
                "CancelledBy" integer,
                "ChairId" integer NOT NULL,
                "Code" character varying(40) NOT NULL,
                "ConfirmedToAttend" character varying(16) NOT NULL DEFAULT 'Pending',
                "CreatedAt" timestamp with time zone NOT NULL,
                "DentistId" integer NOT NULL,
                "EndAt" timestamp with time zone NOT NULL,
                "ExceptionReason" character varying(500),
                "FamilyMemberId" uuid,
                "FollowUpId" uuid,
                "Kind" character varying(16) NOT NULL,
                "PatientConsentConfirmedAt" timestamp with time zone,
                "PatientConsentConfirmedBy" integer,
                "PatientId" integer,
                "Reason" character varying(1000),
                "ReservationId" uuid NOT NULL,
                "ServiceId" integer,
                "Source" character varying(24) NOT NULL,
                "StartAt" timestamp with time zone NOT NULL,
                "Status" character varying(24) NOT NULL,
                "Version" integer NOT NULL,
                CONSTRAINT "PK_APPOINTMENT" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_APPOINTMENT_RANGE" CHECK ("StartAt" < "EndAt"),
                CONSTRAINT "FK_APPOINTMENT_CUSTOMER_FAMILY_MEMBER_FamilyMemberId" FOREIGN KEY ("FamilyMemberId") REFERENCES "CUSTOMER_FAMILY_MEMBER" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_PATIENT_PROFILE_PatientId" FOREIGN KEY ("PatientId") REFERENCES "PATIENT_PROFILE" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_RESOURCE_RESERVATION_ReservationId" FOREIGN KEY ("ReservationId") REFERENCES "RESOURCE_RESERVATION" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_USER_ACCOUNT_BookerAccountId" FOREIGN KEY ("BookerAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_USER_ACCOUNT_CancelledBy" FOREIGN KEY ("CancelledBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_APPOINTMENT_USER_ACCOUNT_PatientConsentConfirmedBy" FOREIGN KEY ("PatientConsentConfirmedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            INSERT INTO "APPOINTMENT" ("Id", "AssistantId", "AttendeeBirthDate", "AttendeeName", "AttendeeRelation", "BookerAccountId", "BookerEmail", "BookerName", "BookerPhone", "BranchId", "CancelReason", "CancelledAt", "CancelledBy", "ChairId", "Code", "ConfirmedToAttend", "CreatedAt", "DentistId", "EndAt", "ExceptionReason", "FamilyMemberId", "FollowUpId", "Kind", "PatientConsentConfirmedAt", "PatientConsentConfirmedBy", "PatientId", "Reason", "ReservationId", "ServiceId", "Source", "StartAt", "Status", "Version") SELECT "AppointmentId", "AssistantId", "AttendeeBirthDate", "AttendeeName", "AttendeeRelation", "BookerAccountId", "BookerEmail", "BookerName", "BookerPhone", "BranchId", "CancelReason", "CancelledAt", "CancelledBy", "ChairId", "Code", "ConfirmedToAttend", "CreatedAt", "DentistId", "EndAt", "ExceptionReason", "FamilyMemberId", "FollowUpId", "Kind", "PatientConsentConfirmedAt", "PatientConsentConfirmedBy", "PatientId", "Reason", "Id", "ServiceId", "Source", "StartAt", "Status", "Version" FROM "APPOINTMENT_AFTER" WHERE "CreatedAt" IS NOT NULL;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ADD CONSTRAINT "FK_IN_APP_NOTIFICATION_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ADD CONSTRAINT "FK_IN_APP_NOTIFICATION_APPOINTMENT_SUPPORT_REQUEST_RequestId" FOREIGN KEY ("RequestId") REFERENCES "APPOINTMENT_SUPPORT_REQUEST" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "STAFF_HANDLING_ITEM" ADD CONSTRAINT "FK_STAFF_HANDLING_ITEM_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "TREATMENT_DOSSIER" ADD CONSTRAINT "FK_TREATMENT_DOSSIER_MEMBER" FOREIGN KEY ("BranchId", "DentistId") REFERENCES "SCHEDULING_MEMBER" ("BranchId", "EmployeeId") ON DELETE RESTRICT;
            
            ALTER TABLE "COMMAND_RECEIPT" ADD CONSTRAINT "FK_COMMAND_RECEIPT_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "COMMAND_RECEIPT" ADD CONSTRAINT "FK_COMMAND_RECEIPT_APPOINTMENT_SUPPORT_REQUEST_SupportRequestId" FOREIGN KEY ("SupportRequestId") REFERENCES "APPOINTMENT_SUPPORT_REQUEST" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "VISIT" ADD CONSTRAINT "FK_VISIT_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "APPOINTMENT_CHANGE" ADD CONSTRAINT "FK_APPOINTMENT_CHANGE_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "APPOINTMENT_OUTBOX" ADD CONSTRAINT "FK_APPOINTMENT_OUTBOX_APPOINTMENT_AggregateId" FOREIGN KEY ("AggregateId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "APPOINTMENT_REMINDER" ADD CONSTRAINT "FK_APPOINTMENT_REMINDER_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "APPOINTMENT_SUPPORT_HISTORY" ADD CONSTRAINT "FK_APPOINTMENT_SUPPORT_HISTORY_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "APPOINTMENT_SUPPORT_REQUEST" ADD CONSTRAINT "FK_APPOINTMENT_SUPPORT_REQUEST_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT;
            
            DELETE FROM "AUDIT_LOG" WHERE "AuditType" IN ('AppointmentChange','AppointmentSupportHistory');
            
            DELETE FROM "IN_APP_NOTIFICATION" WHERE "NotificationRecordKind"='AppointmentOutboxEvent';
            
            DROP TABLE "APPOINTMENT_AFTER", "WORKING_SCHEDULE";
            
            CREATE UNIQUE INDEX "IX_APPOINTMENT_CHANGE_ActorAccountId_RequestKey" ON "APPOINTMENT_CHANGE" ("ActorAccountId", "RequestKey");
            
            CREATE INDEX "IX_APPOINTMENT_CHANGE_AppointmentId_OccurredAt" ON "APPOINTMENT_CHANGE" ("AppointmentId", "OccurredAt");
            
            CREATE INDEX "IX_APPOINTMENT_OUTBOX_AggregateId" ON "APPOINTMENT_OUTBOX" ("AggregateId");
            
            CREATE INDEX "IX_APPOINTMENT_OUTBOX_DispatchedAt_OccurredAt" ON "APPOINTMENT_OUTBOX" ("DispatchedAt", "OccurredAt");
            
            CREATE INDEX "IX_APPOINTMENT_REMINDER_AccountId" ON "APPOINTMENT_REMINDER" ("AccountId");
            
            CREATE UNIQUE INDEX "IX_APPOINTMENT_REMINDER_AppointmentId_AppointmentVersion_LeadM~" ON "APPOINTMENT_REMINDER" ("AppointmentId", "AppointmentVersion", "LeadMinutes");
            
            CREATE INDEX "IX_APPOINTMENT_REMINDER_Status_DueAt" ON "APPOINTMENT_REMINDER" ("Status", "DueAt");
            
            CREATE INDEX "IX_APPOINTMENT_SUPPORT_HISTORY_ActorAccountId" ON "APPOINTMENT_SUPPORT_HISTORY" ("ActorAccountId");
            
            CREATE INDEX "IX_APPOINTMENT_SUPPORT_HISTORY_AppointmentId" ON "APPOINTMENT_SUPPORT_HISTORY" ("AppointmentId");
            
            CREATE INDEX "IX_APPOINTMENT_SUPPORT_HISTORY_RequestId_OccurredAt" ON "APPOINTMENT_SUPPORT_HISTORY" ("RequestId", "OccurredAt");
            
            CREATE INDEX "IX_APPOINTMENT_SUPPORT_REQUEST_AppointmentId" ON "APPOINTMENT_SUPPORT_REQUEST" ("AppointmentId");
            
            CREATE UNIQUE INDEX "IX_APPOINTMENT_SUPPORT_REQUEST_Code" ON "APPOINTMENT_SUPPORT_REQUEST" ("Code");
            
            CREATE INDEX "IX_APPOINTMENT_SUPPORT_REQUEST_DecidedBy" ON "APPOINTMENT_SUPPORT_REQUEST" ("DecidedBy");
            
            CREATE INDEX "IX_APPOINTMENT_SUPPORT_REQUEST_RequesterAccountId" ON "APPOINTMENT_SUPPORT_REQUEST" ("RequesterAccountId");
            
            CREATE INDEX "IX_APPOINTMENT_SUPPORT_REQUEST_RequesterVerifiedBy" ON "APPOINTMENT_SUPPORT_REQUEST" ("RequesterVerifiedBy");
            
            CREATE UNIQUE INDEX "IX_APPOINTMENT_SUPPORT_REQUEST_RequestKey" ON "APPOINTMENT_SUPPORT_REQUEST" ("RequestKey");
            
            CREATE INDEX "IX_APPOINTMENT_SUPPORT_REQUEST_Status_UpdatedAt" ON "APPOINTMENT_SUPPORT_REQUEST" ("Status", "UpdatedAt");
            
            CREATE INDEX "IX_BRANCH_ASSET_BranchId" ON "BRANCH_ASSET" ("BranchId");
            
            CREATE INDEX "IX_BRANCH_ASSET_UploadedBy" ON "BRANCH_ASSET" ("UploadedBy");
            
            CREATE INDEX "IX_RESOURCE_RESERVATION_AssistantId" ON "RESOURCE_RESERVATION" ("AssistantId");
            
            CREATE INDEX "IX_RESOURCE_RESERVATION_BranchId_DentistId" ON "RESOURCE_RESERVATION" ("BranchId", "DentistId");
            
            CREATE INDEX "IX_RESOURCE_RESERVATION_ChairId_BranchId" ON "RESOURCE_RESERVATION" ("ChairId", "BranchId");
            
            CREATE UNIQUE INDEX "IX_RESOURCE_RESERVATION_Code" ON "RESOURCE_RESERVATION" ("Code");
            
            CREATE INDEX "IX_RESOURCE_RESERVATION_ServiceId_BranchId" ON "RESOURCE_RESERVATION" ("ServiceId", "BranchId");
            
            CREATE INDEX "IX_RESOURCE_RESERVATION_Start_End" ON "RESOURCE_RESERVATION" ("Start", "End");
            
            CREATE INDEX "IX_SCHEDULING_CHAIR_BranchId" ON "SCHEDULING_CHAIR" ("BranchId");
            
            CREATE UNIQUE INDEX "IX_SCHEDULING_CHAIR_BranchId_Code" ON "SCHEDULING_CHAIR" ("BranchId", "Code");
            
            SELECT setval(pg_get_serial_sequence('"SCHEDULING_CHAIR"','Id'),GREATEST(COALESCE((SELECT max("Id") FROM "SCHEDULING_CHAIR"),0),1),EXISTS(SELECT 1 FROM "SCHEDULING_CHAIR"));
            
            CREATE INDEX "IX_SCHEDULING_MEMBER_EmployeeId" ON "SCHEDULING_MEMBER" ("EmployeeId");
            
            CREATE INDEX "IX_WORKING_SCHEDULE_REVISION_EmployeeId_Date" ON "WORKING_SCHEDULE_REVISION" ("EmployeeId", "Date");
            
            CREATE UNIQUE INDEX "IX_WORKING_SCHEDULE_REVISION_EmployeeId_Version" ON "WORKING_SCHEDULE_REVISION" ("EmployeeId", "Version");
            
            CREATE INDEX "IX_WORKING_SCHEDULE_REVISION_PublishedBy" ON "WORKING_SCHEDULE_REVISION" ("PublishedBy");
            
            CREATE UNIQUE INDEX "IX_WORKING_WEEK_BranchId_EmployeeId_WeekStart" ON "WORKING_WEEK" ("BranchId", "EmployeeId", "WeekStart");
            
            CREATE INDEX "IX_WORKING_WEEK_PublishedBy" ON "WORKING_WEEK" ("PublishedBy");
            
            CREATE INDEX "IX_APPOINTMENT_BookerAccountId_FamilyMemberId_StartAt" ON "APPOINTMENT" ("BookerAccountId", "FamilyMemberId", "StartAt");
            
            CREATE INDEX "IX_APPOINTMENT_BranchId_StartAt" ON "APPOINTMENT" ("BranchId", "StartAt");
            
            CREATE INDEX "IX_APPOINTMENT_CancelledBy" ON "APPOINTMENT" ("CancelledBy");
            
            CREATE UNIQUE INDEX "IX_APPOINTMENT_Code" ON "APPOINTMENT" ("Code");
            
            CREATE INDEX "IX_APPOINTMENT_FamilyMemberId" ON "APPOINTMENT" ("FamilyMemberId");
            
            CREATE UNIQUE INDEX "IX_APPOINTMENT_FollowUpId" ON "APPOINTMENT" ("FollowUpId") WHERE "FollowUpId" IS NOT NULL AND "Status" IN ('Confirmed','CheckedIn');
            
            CREATE INDEX "IX_APPOINTMENT_PatientConsentConfirmedBy" ON "APPOINTMENT" ("PatientConsentConfirmedBy");
            
            CREATE INDEX "IX_APPOINTMENT_PatientId" ON "APPOINTMENT" ("PatientId");
            
            CREATE UNIQUE INDEX "IX_APPOINTMENT_ReservationId" ON "APPOINTMENT" ("ReservationId");
            
            ALTER TABLE "SCHEDULING_BRANCH" DROP COLUMN "StorageStamp", DROP COLUMN "Assets", DROP COLUMN "Chairs", DROP COLUMN "Members";
            
            DROP INDEX "IX_AUDIT_LOG_RequestId_OccurredAt";
            
            DROP INDEX "IX_AUDIT_LOG_AppointmentId";
            
            DROP INDEX "IX_AUDIT_LOG_ActorAccountId_RequestKey";
            
            DROP INDEX "IX_AUDIT_LOG_AppointmentId_OccurredAt";
            
            ALTER TABLE "AUDIT_LOG" DROP COLUMN "AppointmentId", DROP COLUMN "ActorKind", DROP COLUMN "OldStart", DROP COLUMN "OldEnd", DROP COLUMN "OldDentistId", DROP COLUMN "OldChairId", DROP COLUMN "NewStart", DROP COLUMN "NewEnd", DROP COLUMN "NewDentistId", DROP COLUMN "NewChairId", DROP COLUMN "RequesterVerification", DROP COLUMN "RequestKey", DROP COLUMN "RequestId", DROP COLUMN "Notes";
            
            DROP INDEX "IX_IN_APP_NOTIFICATION_AggregateId";
            
            DROP INDEX "IX_IN_APP_NOTIFICATION_DispatchedAt_OccurredAt";
            
            CREATE INDEX "IX_IN_APP_NOTIFICATION_RequestId" ON "IN_APP_NOTIFICATION" ("RequestId");
            
            ALTER TABLE "IN_APP_NOTIFICATION" ALTER COLUMN "Body" SET NOT NULL;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ALTER COLUMN "CreatedAt" SET NOT NULL;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ALTER COLUMN "DedupeKey" SET NOT NULL;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ALTER COLUMN "HasExternalFailure" SET NOT NULL;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ALTER COLUMN "Kind" SET NOT NULL;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ALTER COLUMN "PublicId" SET NOT NULL;
            
            ALTER TABLE "IN_APP_NOTIFICATION" ALTER COLUMN "Title" SET NOT NULL;
            
            ALTER TABLE "IN_APP_NOTIFICATION" DROP COLUMN "NotificationRecordKind", DROP COLUMN "AggregateId", DROP COLUMN "EventType", DROP COLUMN "PayloadJson", DROP COLUMN "OccurredAt", DROP COLUMN "DispatchedAt";
            
            DROP SEQUENCE scheduling_chair_seq;
            """);
    }
}

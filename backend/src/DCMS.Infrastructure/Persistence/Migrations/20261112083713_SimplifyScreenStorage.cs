using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations;

public partial class SimplifyScreenStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
                -- Copy each source before removing its table in the migration transaction.

                CREATE SEQUENCE audit_log_number_seq AS bigint START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;

                CREATE TABLE "PRESCRIPTION" (
                    "Id" uuid NOT NULL,
                    "PrescriptionKind" character varying(21) NOT NULL,
                    "VisitId" uuid,
                    "PatientId" integer,
                    "BranchId" integer,
                    "PrescribedByDoctorId" integer,
                    "Revision" integer,
                    "Status" character varying(20),
                    "GeneralAdvice" character varying(1000),
                    "Diagnosis" character varying(1000),
                    "CorrectionReason" character varying(500),
                    "SupersedesId" uuid,
                    "FinalizedAt" timestamp with time zone,
                    "FinalizedBy" integer,
                    "VerificationCode" character varying(16),
                    "Version" integer,
                    "CreatedBy" integer,
                    "CreatedAt" timestamp with time zone,
                    "UpdatedBy" integer,
                    "UpdatedAt" timestamp with time zone,
                    "RequiresExternalPurchase" boolean,
                    "PrescribedOn" date,
                    "OtherDiagnosis" character varying(1000),
                    "Advice" character varying(4000),
                    "PrescriberEmployeeId" integer,
                    "Code" character varying(40),
                    "Name" character varying(200),
                    "DiagnosisKind" character varying(100),
                    "Description" character varying(1000),
                    "IsActive" boolean,
                    "Items" jsonb NOT NULL,
                    "TemplateLines" jsonb NOT NULL,
                    "VisitLines" jsonb NOT NULL,
                    CONSTRAINT "PK_PRESCRIPTION" PRIMARY KEY ("Id"),
                    CONSTRAINT "CK_PRESCRIPTION_VISIT_REVISION" CHECK ("PrescriptionKind" <> 'ExternalPrescription' OR "Revision" >= 1),
                    CONSTRAINT "CK_PRESCRIPTION_VISIT_STATUS" CHECK ("PrescriptionKind" <> 'ExternalPrescription' OR "Status" IN ('Draft','Finalized','Superseded')),
                    CONSTRAINT "FK_PRESCRIPTION_EMPLOYEE_PROFILE_PrescriberEmployeeId" FOREIGN KEY ("PrescriberEmployeeId") REFERENCES "EMPLOYEE_PROFILE" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PRESCRIPTION_PATIENT_PROFILE_PatientId" FOREIGN KEY ("PatientId") REFERENCES "PATIENT_PROFILE" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PRESCRIPTION_PRESCRIPTION_SupersedesId" FOREIGN KEY ("SupersedesId") REFERENCES "PRESCRIPTION" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PRESCRIPTION_SCHEDULING_BRANCH_BranchId" FOREIGN KEY ("BranchId") REFERENCES "SCHEDULING_BRANCH" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PRESCRIPTION_USER_ACCOUNT_CreatedBy" FOREIGN KEY ("CreatedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PRESCRIPTION_USER_ACCOUNT_FinalizedBy" FOREIGN KEY ("FinalizedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PRESCRIPTION_USER_ACCOUNT_PrescribedByDoctorId" FOREIGN KEY ("PrescribedByDoctorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PRESCRIPTION_USER_ACCOUNT_UpdatedBy" FOREIGN KEY ("UpdatedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PRESCRIPTION_VISIT_VisitId" FOREIGN KEY ("VisitId") REFERENCES "VISIT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "AUDIT_LOG" (
                    "StorageId" uuid NOT NULL,
                    "AuditType" character varying(21) NOT NULL,
                    "LegacyNumber" bigint DEFAULT (nextval('audit_log_number_seq')),
                    "ActorId" integer,
                    "TargetId" integer,
                    "OccurredAt" timestamp with time zone,
                    "Action" character varying(64),
                    "Reason" character varying(1000),
                    "BeforeJson" jsonb,
                    "AfterJson" jsonb,
                    "ChannelType" character varying(16),
                    "Result" character varying(20),
                    "DocumentId" uuid,
                    "Note" character varying(500),
                    "AccessId" uuid,
                    "ActorAccountId" integer,
                    "LegacyId" uuid,
                    "BranchId" integer,
                    "VisitId" uuid,
                    "Target" character varying(100),
                    "PayloadJson" jsonb,
                    "At" timestamp with time zone,
                    "LinkId" integer,
                    "PatientId" integer,
                    "ChangesJson" jsonb,
                    "TemplateId" uuid,
                    "BranchIds" integer[],
                    "Section" character varying(40),
                    "Version" integer,
                    "DecidingDentistId" integer,
                    CONSTRAINT "PK_AUDIT_LOG" PRIMARY KEY ("StorageId"),
                    CONSTRAINT "FK_AUDIT_LOG_CLINICAL_DOCUMENT_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "CLINICAL_DOCUMENT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_AUDIT_LOG_EMPLOYEE_PROFILE_DecidingDentistId" FOREIGN KEY ("DecidingDentistId") REFERENCES "EMPLOYEE_PROFILE" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_AUDIT_LOG_FAMILY_RECORD_ACCESS_AccessId" FOREIGN KEY ("AccessId") REFERENCES "FAMILY_RECORD_ACCESS" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_AUDIT_LOG_PATIENT_ACCOUNT_LINK_LinkId" FOREIGN KEY ("LinkId") REFERENCES "PATIENT_ACCOUNT_LINK" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_AUDIT_LOG_PATIENT_PROFILE_PatientId" FOREIGN KEY ("PatientId") REFERENCES "PATIENT_PROFILE" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_AUDIT_LOG_USER_ACCOUNT_ActorAccountId" FOREIGN KEY ("ActorAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_AUDIT_LOG_USER_ACCOUNT_ActorId" FOREIGN KEY ("ActorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_AUDIT_LOG_USER_ACCOUNT_TargetId" FOREIGN KEY ("TargetId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_AUDIT_LOG_VISIT_VisitId" FOREIGN KEY ("VisitId") REFERENCES "VISIT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "COMMAND_RECEIPT" (
                    "StorageId" uuid NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL DEFAULT '',
                    "ResultJson" jsonb NOT NULL DEFAULT '{}',
                    "ReceiptType" character varying(34) NOT NULL,
                    "ActorAccountId" integer,
                    "AppointmentId" uuid,
                    "ActorScope" character varying(96),
                    "SupportRequestId" uuid,
                    "Id" uuid,
                    "SessionId" uuid,
                    "ExpectedVersion" integer,
                    "ActorId" integer,
                    "VisitId" uuid,
                    "OwnerAccountId" integer,
                    "CreatedAt" timestamp with time zone,
                    "AccessId" uuid,
                    "PatientId" integer,
                    "PrescriptionId" uuid,
                    "Command" character varying(40),
                    "ResultId" uuid,
                    "DossierId" uuid,
                    CONSTRAINT "PK_COMMAND_RECEIPT" PRIMARY KEY ("StorageId"),
                    CONSTRAINT "FK_COMMAND_RECEIPT_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_COMMAND_RECEIPT_APPOINTMENT_SUPPORT_REQUEST_SupportRequestId" FOREIGN KEY ("SupportRequestId") REFERENCES "APPOINTMENT_SUPPORT_REQUEST" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_COMMAND_RECEIPT_AUTH_SESSION_SessionId" FOREIGN KEY ("SessionId") REFERENCES "AUTH_SESSION" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_COMMAND_RECEIPT_FAMILY_RECORD_ACCESS_AccessId" FOREIGN KEY ("AccessId") REFERENCES "FAMILY_RECORD_ACCESS" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_COMMAND_RECEIPT_PATIENT_PROFILE_PatientId" FOREIGN KEY ("PatientId") REFERENCES "PATIENT_PROFILE" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_COMMAND_RECEIPT_PRESCRIPTION_PrescriptionId" FOREIGN KEY ("PrescriptionId") REFERENCES "PRESCRIPTION" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_COMMAND_RECEIPT_TREATMENT_DOSSIER_DossierId" FOREIGN KEY ("DossierId") REFERENCES "TREATMENT_DOSSIER" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_COMMAND_RECEIPT_USER_ACCOUNT_ActorAccountId" FOREIGN KEY ("ActorAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_COMMAND_RECEIPT_USER_ACCOUNT_ActorId" FOREIGN KEY ("ActorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_COMMAND_RECEIPT_USER_ACCOUNT_OwnerAccountId" FOREIGN KEY ("OwnerAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_COMMAND_RECEIPT_VISIT_VisitId" FOREIGN KEY ("VisitId") REFERENCES "VISIT" ("Id") ON DELETE RESTRICT
                );

                ALTER TABLE "PRESCRIPTION" ALTER COLUMN "Items" SET DEFAULT '[]'::jsonb;

                ALTER TABLE "PRESCRIPTION" ALTER COLUMN "TemplateLines" SET DEFAULT '[]'::jsonb;

                ALTER TABLE "PRESCRIPTION" ALTER COLUMN "VisitLines" SET DEFAULT '[]'::jsonb;

                ALTER TABLE "ACCESS_GROUP" ADD COLUMN "Grants" jsonb NOT NULL DEFAULT '[]'::jsonb;

                ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "Version" integer NOT NULL DEFAULT 0;

                ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "Code" character varying(40);

                ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "Address" character varying(1000);

                ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "Phone" character varying(20);

                ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "Email" character varying(254);

                ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "Website" character varying(255);

                ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "LogoAssetId" uuid;

                ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "TimeZoneId" character varying(64) DEFAULT 'Asia/Ho_Chi_Minh';

                ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "BankCode" character varying(20);

                ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "AccountNumber" character varying(50);

                ALTER TABLE "SCHEDULING_BRANCH" ADD COLUMN "PayeeName" character varying(160);

                ALTER TABLE "SCHEDULING_CHAIR" ADD COLUMN "Version" integer NOT NULL DEFAULT 0;

                ALTER TABLE "SCHEDULING_CHAIR" ADD COLUMN "Code" character varying(40);

                ALTER TABLE "TREATMENT_PAYOS_ORDER" ADD COLUMN "Lines" jsonb NOT NULL DEFAULT '[]'::jsonb;

                ALTER TABLE "WORKING_SCHEDULE_REVISION" ADD COLUMN "Periods" jsonb NOT NULL DEFAULT '[]'::jsonb;

                ALTER TABLE "WORKING_WEEK" ADD COLUMN "Periods" jsonb NOT NULL DEFAULT '[]'::jsonb;

                INSERT INTO "PRESCRIPTION" ("Id", "BranchId", "CorrectionReason", "CreatedAt", "CreatedBy", "Diagnosis", "FinalizedAt", "FinalizedBy", "GeneralAdvice", "PatientId", "PrescribedByDoctorId", "Revision", "Status", "SupersedesId", "UpdatedAt", "UpdatedBy", "VerificationCode", "Version", "VisitId", "PrescriptionKind", "VisitLines")
                SELECT s."Id",
                       s."BranchId",
                       s."CorrectionReason",
                       s."CreatedAt",
                       s."CreatedBy",
                       s."Diagnosis",
                       s."FinalizedAt",
                       s."FinalizedBy",
                       s."GeneralAdvice",
                       s."PatientId",
                       s."PrescribedByDoctorId",
                       s."Revision",
                       s."Status",
                       s."SupersedesId",
                       s."UpdatedAt",
                       s."UpdatedBy",
                       s."VerificationCode",
                       s."Version",
                       s."VisitId",
                       'ExternalPrescription',
                       (SELECT COALESCE(jsonb_agg(to_jsonb(child) ORDER BY child."CreatedAt", child."Id"), '[]'::jsonb) FROM "EXTERNAL_PRESCRIPTION_LINE" child WHERE child."PrescriptionId" = s."Id")
                FROM "EXTERNAL_PRESCRIPTION" s;

                INSERT INTO "PRESCRIPTION" ("Id", "Advice", "BranchId", "CreatedAt", "Diagnosis", "OtherDiagnosis", "PatientId", "PrescribedOn", "PrescriberEmployeeId", "UpdatedAt", "Version", "RequiresExternalPurchase", "PrescriptionKind", "Items")
                SELECT s."Id",
                       s."Advice",
                       s."BranchId",
                       s."CreatedAt",
                       s."Diagnosis",
                       s."OtherDiagnosis",
                       s."PatientId",
                       s."PrescribedOn",
                       s."PrescriberEmployeeId",
                       s."UpdatedAt",
                       s."Version",
                       s."RequiresExternalPurchase",
                       'PatientPrescription',
                       (SELECT COALESCE(jsonb_agg(to_jsonb(child) ORDER BY child."Position", child."Id"), '[]'::jsonb) FROM "PATIENT_PRESCRIPTION_ITEM" child WHERE child."PrescriptionId" = s."Id")
                FROM "PATIENT_PRESCRIPTION" s;

                INSERT INTO "PRESCRIPTION" ("Id", "Code", "CreatedAt", "CreatedBy", "Description", "DiagnosisKind", "IsActive", "Name", "UpdatedAt", "UpdatedBy", "Version", "PrescriptionKind", "TemplateLines")
                SELECT s."Id",
                       s."Code",
                       s."CreatedAt",
                       s."CreatedBy",
                       s."Description",
                       s."DiagnosisKind",
                       s."IsActive",
                       s."Name",
                       s."UpdatedAt",
                       s."UpdatedBy",
                       s."Version",
                       'PrescriptionTemplate',
                       (SELECT COALESCE(jsonb_agg(to_jsonb(child) ORDER BY child."SortOrder", child."Id"), '[]'::jsonb) FROM "PRESCRIPTION_TEMPLATE_LINE" child WHERE child."TemplateId" = s."Id")
                FROM "PRESCRIPTION_TEMPLATE" s;

                INSERT INTO "AUDIT_LOG" ("StorageId", "LegacyNumber", "Action", "ActorId", "AfterJson", "BeforeJson", "OccurredAt", "Reason", "TargetId", "AuditType")
                SELECT gen_random_uuid(),
                       s."Id",
                       s."Action",
                       s."ActorId",
                       s."AfterJson"::jsonb,
                       s."BeforeJson"::jsonb,
                       s."OccurredAt",
                       s."Reason",
                       s."TargetId",
                       'AccountAudit'
                FROM "ACCOUNT_AUDIT" s;

                INSERT INTO "AUDIT_LOG" ("StorageId", "LegacyNumber", "Action", "ActorId", "ChannelType", "OccurredAt", "Reason", "Result", "TargetId", "AuditType")
                SELECT gen_random_uuid(),
                       s."Id",
                       s."Action",
                       s."ActorId",
                       s."ChannelType",
                       s."OccurredAt",
                       s."Reason",
                       s."Result",
                       s."TargetId",
                       'RecoveryAudit'
                FROM "RECOVERY_AUDIT" s;

                INSERT INTO "AUDIT_LOG" ("StorageId", "LegacyNumber", "Action", "ActorId", "DocumentId", "Note", "OccurredAt", "AuditType")
                SELECT gen_random_uuid(),
                       s."Id",
                       s."Action",
                       s."ActorId",
                       s."DocumentId",
                       s."Note",
                       s."OccurredAt",
                       'DocumentAudit'
                FROM "DOCUMENT_AUDIT" s;

                INSERT INTO "AUDIT_LOG" ("StorageId", "LegacyNumber", "AccessId", "Action", "ActorAccountId", "OccurredAt", "Reason", "AuditType")
                SELECT gen_random_uuid(),
                       s."Id",
                       s."AccessId",
                       s."Action",
                       s."ActorAccountId",
                       s."OccurredAt",
                       s."Reason",
                       'FamilyRecordAudit'
                FROM "FAMILY_RECORD_AUDIT" s;

                INSERT INTO "AUDIT_LOG" ("StorageId", "LegacyId", "Action", "ActorId", "At", "BranchId", "PayloadJson", "Target", "VisitId", "AuditType")
                SELECT gen_random_uuid(),
                       s."Id",
                       s."Action",
                       s."ActorId",
                       s."At",
                       s."BranchId",
                       s."PayloadJson"::jsonb,
                       s."Target",
                       s."VisitId",
                       'FinanceAudit'
                FROM "FINANCE_AUDIT" s;

                INSERT INTO "AUDIT_LOG" ("StorageId", "LegacyNumber", "Action", "ActorAccountId", "AfterJson", "BeforeJson", "LinkId", "OccurredAt", "Reason", "AuditType")
                SELECT gen_random_uuid(),
                       s."Id",
                       s."Action",
                       s."ActorAccountId",
                       s."AfterJson"::jsonb,
                       s."BeforeJson"::jsonb,
                       s."LinkId",
                       s."OccurredAt",
                       s."Reason",
                       'PatientLinkAudit'
                FROM "PATIENT_LINK_AUDIT" s;

                INSERT INTO "AUDIT_LOG" ("StorageId", "LegacyNumber", "Action", "ActorAccountId", "ChangesJson", "OccurredAt", "PatientId", "Reason", "AuditType")
                SELECT gen_random_uuid(),
                       s."Id",
                       s."Action",
                       s."ActorAccountId",
                       s."ChangesJson"::jsonb,
                       s."OccurredAt",
                       s."PatientId",
                       s."Reason",
                       'PatientAudit'
                FROM "PATIENT_AUDIT" s;

                INSERT INTO "AUDIT_LOG" ("StorageId", "LegacyId", "Action", "ActorId", "AfterJson", "At", "BeforeJson", "BranchId", "Reason", "TemplateId", "VisitId", "AuditType")
                SELECT gen_random_uuid(),
                       s."Id",
                       s."Action",
                       s."ActorId",
                       s."AfterJson"::jsonb,
                       s."At",
                       s."BeforeJson"::jsonb,
                       s."BranchId",
                       s."Reason",
                       s."TemplateId",
                       s."VisitId",
                       'PrescriptionAudit'
                FROM "PRESCRIPTION_AUDIT" s;

                INSERT INTO "AUDIT_LOG" ("StorageId", "LegacyId", "Action", "ActorId", "AfterJson", "At", "BeforeJson", "BranchIds", "Reason", "Section", "Target", "Version", "AuditType")
                SELECT gen_random_uuid(),
                       s."Id",
                       s."Action",
                       s."ActorId",
                       s."AfterJson"::jsonb,
                       s."At",
                       s."BeforeJson"::jsonb,
                       s."BranchIds",
                       s."Reason",
                       s."Section",
                       s."Target",
                       s."Version",
                       'SettingsAudit'
                FROM "SETTINGS_AUDIT" s;

                INSERT INTO "AUDIT_LOG" ("StorageId", "LegacyId", "Action", "ActorAccountId", "AfterJson", "BeforeJson", "OccurredAt", "VisitId", "DecidingDentistId", "Reason", "AuditType")
                SELECT gen_random_uuid(),
                       s."Id",
                       s."Action",
                       s."ActorAccountId",
                       s."AfterJson"::jsonb,
                       s."BeforeJson"::jsonb,
                       s."OccurredAt",
                       s."VisitId",
                       NULLIF(to_jsonb(s)->>'DecidingDentistId', '')::integer,
                       COALESCE(to_jsonb(s)->>'Reason', ''),
                       'VisitAudit'
                FROM "VISIT_AUDIT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "ActorAccountId", "RequestKey", "Fingerprint", "ResultJson", "SupportRequestId", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."ActorAccountId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."ResultJson"::jsonb,
                       s."SupportRequestId",
                       'SupportRequestReceipt'
                FROM "SUPPORT_REQUEST_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "ActorAccountId", "RequestKey", "AppointmentId", "Fingerprint", "ResultJson", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."ActorAccountId",
                       s."RequestKey",
                       s."AppointmentId",
                       s."Fingerprint",
                       s."ResultJson"::jsonb,
                       'AppointmentChangeReceipt'
                FROM "APPOINTMENT_CHANGE_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "RequestKey", "ActorScope", "AppointmentId", "Fingerprint", "ResultJson", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."RequestKey",
                       s."ActorScope",
                       s."AppointmentId",
                       s."Fingerprint",
                       s."ResultJson"::jsonb,
                       'AppointmentReceipt'
                FROM "APPOINTMENT_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "Id", "ExpectedVersion", "RequestKey", "ResultJson", "SessionId", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."Id",
                       s."ExpectedVersion",
                       s."RequestKey",
                       s."ResultJson"::jsonb,
                       s."SessionId",
                       'StaffActivityCommandReceipt'
                FROM "STAFF_ACTIVITY_COMMAND_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "ActorId", "RequestKey", "Fingerprint", "VisitId", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."ActorId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."VisitId",
                       'ExaminationReceipt'
                FROM "EXAMINATION_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "ActorId", "RequestKey", "Fingerprint", "ResultJson", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."ActorId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."ResultJson"::jsonb,
                       'DocumentReceipt'
                FROM "DOCUMENT_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "OwnerAccountId", "RequestKey", "CreatedAt", "Fingerprint", "ResultJson", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."OwnerAccountId",
                       s."RequestKey",
                       s."CreatedAt",
                       s."Fingerprint",
                       s."ResultJson"::jsonb,
                       'FamilyMemberReceipt'
                FROM "CUSTOMER_FAMILY_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "ActorAccountId", "RequestKey", "AccessId", "Fingerprint", "ResultJson", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."ActorAccountId",
                       s."RequestKey",
                       s."AccessId",
                       s."Fingerprint",
                       s."ResultJson"::jsonb,
                       'FamilyRecordReceipt'
                FROM "FAMILY_RECORD_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "ActorId", "RequestKey", "Fingerprint", "ResultJson", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."ActorId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."ResultJson"::jsonb,
                       'FinanceCommandReceipt'
                FROM "FINANCE_COMMAND_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "ActorId", "RequestKey", "CreatedAt", "Fingerprint", "PatientId", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."ActorId",
                       s."RequestKey",
                       s."CreatedAt",
                       s."Fingerprint",
                       s."PatientId",
                       'PatientCreateReceipt'
                FROM "PATIENT_CREATE_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "ActorId", "RequestKey", "Fingerprint", "ResultJson", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."ActorId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."ResultJson"::jsonb,
                       'PrescriptionCommandReceipt'
                FROM "PRESCRIPTION_COMMAND_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "ActorId", "RequestKey", "Fingerprint", "PrescriptionId", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."ActorId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."PrescriptionId",
                       'PatientPrescriptionReceipt'
                FROM "PATIENT_PRESCRIPTION_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "ActorId", "RequestKey", "Fingerprint", "ResultJson", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."ActorId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."ResultJson"::jsonb,
                       'ScheduleReceipt'
                FROM "SCHEDULE_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "ActorId", "RequestKey", "Fingerprint", "ResultJson", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."ActorId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."ResultJson"::jsonb,
                       'SettingsReceipt'
                FROM "SETTINGS_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "RequestKey", "ActorId", "Command", "Fingerprint", "ResultId", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."RequestKey",
                       s."ActorId",
                       s."Command",
                       s."Fingerprint",
                       s."ResultId",
                       'ClinicalCommandReceipt'
                FROM "CLINICAL_COMMAND_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "RequestKey", "ActorId", "Command", "DossierId", "Fingerprint", "ResultJson", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."RequestKey",
                       s."ActorId",
                       s."Command",
                       s."DossierId",
                       s."Fingerprint",
                       s."ResultJson"::jsonb,
                       'TreatmentReceipt'
                FROM "TREATMENT_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "RequestKey", "ActorId", "Command", "Fingerprint", "ResultJson", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."RequestKey",
                       s."ActorId",
                       s."Command",
                       s."Fingerprint",
                       s."ResultJson"::jsonb,
                       'TreatmentProgressionReceipt'
                FROM "TREATMENT_PROGRESSION_RECEIPT" s;

                INSERT INTO "COMMAND_RECEIPT" ("StorageId", "ActorAccountId", "RequestKey", "Fingerprint", "VisitId", "ReceiptType")
                SELECT gen_random_uuid(),
                       s."ActorAccountId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."VisitId",
                       'ReceptionReceipt'
                FROM "RECEPTION_RECEIPT" s;

                UPDATE "WORKING_WEEK" parent SET "Periods" = (SELECT COALESCE(jsonb_agg(to_jsonb(child) ORDER BY child."Day", child."StartMinute", child."Id"), '[]'::jsonb) FROM "WORKING_PERIOD" child WHERE child."WeekId" = parent."Id");

                UPDATE "WORKING_SCHEDULE_REVISION" parent SET "Periods" = (SELECT COALESCE(jsonb_agg(to_jsonb(child) ORDER BY child."Day", child."StartMinute", child."Id"), '[]'::jsonb) FROM "WORKING_REVISION_PERIOD" child WHERE child."RevisionId" = parent."Id");

                UPDATE "ACCESS_GROUP" parent SET "Grants" = (SELECT COALESCE(jsonb_agg(to_jsonb(child) ORDER BY child."Permission"), '[]'::jsonb) FROM "ACCESS_GROUP_GRANT" child WHERE child."GroupId" = parent."Id");

                UPDATE "TREATMENT_PAYOS_ORDER" parent SET "Lines" = (SELECT COALESCE(jsonb_agg(to_jsonb(child) ORDER BY child."ActivityId"), '[]'::jsonb) FROM "TREATMENT_PAYOS_ORDER_LINE" child WHERE child."OrderId" = parent."Id");

                UPDATE "SCHEDULING_BRANCH" target SET "AccountNumber" = source."AccountNumber", "Address" = source."Address", "BankCode" = source."BankCode", "Code" = source."Code", "Email" = source."Email", "LogoAssetId" = source."LogoAssetId", "PayeeName" = source."PayeeName", "Phone" = source."Phone", "TimeZoneId" = source."TimeZoneId", "Version" = source."Version", "Website" = source."Website" FROM "BRANCH_PROFILE" source WHERE source."Id" = target."Id";

                UPDATE "SCHEDULING_CHAIR" target SET "Code" = source."Code", "Version" = source."Version" FROM "BRANCH_CHAIR" source WHERE source."Id" = target."Id";

                SELECT setval('audit_log_number_seq', GREATEST(COALESCE((SELECT max("LegacyNumber") FROM "AUDIT_LOG"), 0), 1), EXISTS (SELECT 1 FROM "AUDIT_LOG" WHERE "LegacyNumber" IS NOT NULL));

                DO $verify$
                BEGIN
                    IF (SELECT count(*) FROM "EXTERNAL_PRESCRIPTION") <> (SELECT count(*) FROM "PRESCRIPTION" WHERE "PrescriptionKind" = 'ExternalPrescription') THEN RAISE EXCEPTION 'Incomplete transfer of ExternalPrescription'; END IF;
                    IF (SELECT count(*) FROM "PATIENT_PRESCRIPTION") <> (SELECT count(*) FROM "PRESCRIPTION" WHERE "PrescriptionKind" = 'PatientPrescription') THEN RAISE EXCEPTION 'Incomplete transfer of PatientPrescription'; END IF;
                    IF (SELECT count(*) FROM "PRESCRIPTION_TEMPLATE") <> (SELECT count(*) FROM "PRESCRIPTION" WHERE "PrescriptionKind" = 'PrescriptionTemplate') THEN RAISE EXCEPTION 'Incomplete transfer of PrescriptionTemplate'; END IF;
                    IF (SELECT count(*) FROM "ACCOUNT_AUDIT") <> (SELECT count(*) FROM "AUDIT_LOG" WHERE "AuditType" = 'AccountAudit') THEN RAISE EXCEPTION 'Incomplete transfer of AccountAudit'; END IF;
                    IF (SELECT count(*) FROM "RECOVERY_AUDIT") <> (SELECT count(*) FROM "AUDIT_LOG" WHERE "AuditType" = 'RecoveryAudit') THEN RAISE EXCEPTION 'Incomplete transfer of RecoveryAudit'; END IF;
                    IF (SELECT count(*) FROM "DOCUMENT_AUDIT") <> (SELECT count(*) FROM "AUDIT_LOG" WHERE "AuditType" = 'DocumentAudit') THEN RAISE EXCEPTION 'Incomplete transfer of DocumentAudit'; END IF;
                    IF (SELECT count(*) FROM "FAMILY_RECORD_AUDIT") <> (SELECT count(*) FROM "AUDIT_LOG" WHERE "AuditType" = 'FamilyRecordAudit') THEN RAISE EXCEPTION 'Incomplete transfer of FamilyRecordAudit'; END IF;
                    IF (SELECT count(*) FROM "FINANCE_AUDIT") <> (SELECT count(*) FROM "AUDIT_LOG" WHERE "AuditType" = 'FinanceAudit') THEN RAISE EXCEPTION 'Incomplete transfer of FinanceAudit'; END IF;
                    IF (SELECT count(*) FROM "PATIENT_LINK_AUDIT") <> (SELECT count(*) FROM "AUDIT_LOG" WHERE "AuditType" = 'PatientLinkAudit') THEN RAISE EXCEPTION 'Incomplete transfer of PatientLinkAudit'; END IF;
                    IF (SELECT count(*) FROM "PATIENT_AUDIT") <> (SELECT count(*) FROM "AUDIT_LOG" WHERE "AuditType" = 'PatientAudit') THEN RAISE EXCEPTION 'Incomplete transfer of PatientAudit'; END IF;
                    IF (SELECT count(*) FROM "PRESCRIPTION_AUDIT") <> (SELECT count(*) FROM "AUDIT_LOG" WHERE "AuditType" = 'PrescriptionAudit') THEN RAISE EXCEPTION 'Incomplete transfer of PrescriptionAudit'; END IF;
                    IF (SELECT count(*) FROM "SETTINGS_AUDIT") <> (SELECT count(*) FROM "AUDIT_LOG" WHERE "AuditType" = 'SettingsAudit') THEN RAISE EXCEPTION 'Incomplete transfer of SettingsAudit'; END IF;
                    IF (SELECT count(*) FROM "VISIT_AUDIT") <> (SELECT count(*) FROM "AUDIT_LOG" WHERE "AuditType" = 'VisitAudit') THEN RAISE EXCEPTION 'Incomplete transfer of VisitAudit'; END IF;
                    IF (SELECT count(*) FROM "SUPPORT_REQUEST_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'SupportRequestReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of SupportRequestReceipt'; END IF;
                    IF (SELECT count(*) FROM "APPOINTMENT_CHANGE_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'AppointmentChangeReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of AppointmentChangeReceipt'; END IF;
                    IF (SELECT count(*) FROM "APPOINTMENT_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'AppointmentReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of AppointmentReceipt'; END IF;
                    IF (SELECT count(*) FROM "STAFF_ACTIVITY_COMMAND_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'StaffActivityCommandReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of StaffActivityCommandReceipt'; END IF;
                    IF (SELECT count(*) FROM "EXAMINATION_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'ExaminationReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of ExaminationReceipt'; END IF;
                    IF (SELECT count(*) FROM "DOCUMENT_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'DocumentReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of DocumentReceipt'; END IF;
                    IF (SELECT count(*) FROM "CUSTOMER_FAMILY_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'FamilyMemberReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of FamilyMemberReceipt'; END IF;
                    IF (SELECT count(*) FROM "FAMILY_RECORD_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'FamilyRecordReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of FamilyRecordReceipt'; END IF;
                    IF (SELECT count(*) FROM "FINANCE_COMMAND_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'FinanceCommandReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of FinanceCommandReceipt'; END IF;
                    IF (SELECT count(*) FROM "PATIENT_CREATE_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'PatientCreateReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of PatientCreateReceipt'; END IF;
                    IF (SELECT count(*) FROM "PRESCRIPTION_COMMAND_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'PrescriptionCommandReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of PrescriptionCommandReceipt'; END IF;
                    IF (SELECT count(*) FROM "PATIENT_PRESCRIPTION_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'PatientPrescriptionReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of PatientPrescriptionReceipt'; END IF;
                    IF (SELECT count(*) FROM "SCHEDULE_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'ScheduleReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of ScheduleReceipt'; END IF;
                    IF (SELECT count(*) FROM "SETTINGS_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'SettingsReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of SettingsReceipt'; END IF;
                    IF (SELECT count(*) FROM "CLINICAL_COMMAND_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'ClinicalCommandReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of ClinicalCommandReceipt'; END IF;
                    IF (SELECT count(*) FROM "TREATMENT_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'TreatmentReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of TreatmentReceipt'; END IF;
                    IF (SELECT count(*) FROM "TREATMENT_PROGRESSION_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'TreatmentProgressionReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of TreatmentProgressionReceipt'; END IF;
                    IF (SELECT count(*) FROM "RECEPTION_RECEIPT") <> (SELECT count(*) FROM "COMMAND_RECEIPT" WHERE "ReceiptType" = 'ReceptionReceipt') THEN RAISE EXCEPTION 'Incomplete transfer of ReceptionReceipt'; END IF;
                    IF (SELECT count(*) FROM "EXTERNAL_PRESCRIPTION_LINE") <> (SELECT COALESCE(sum(jsonb_array_length("VisitLines")), 0) FROM "PRESCRIPTION" WHERE "PrescriptionKind" = 'ExternalPrescription') THEN RAISE EXCEPTION 'Incomplete transfer of EXTERNAL_PRESCRIPTION_LINE'; END IF;
                    IF (SELECT count(*) FROM "PATIENT_PRESCRIPTION_ITEM") <> (SELECT COALESCE(sum(jsonb_array_length("Items")), 0) FROM "PRESCRIPTION" WHERE "PrescriptionKind" = 'PatientPrescription') THEN RAISE EXCEPTION 'Incomplete transfer of PATIENT_PRESCRIPTION_ITEM'; END IF;
                    IF (SELECT count(*) FROM "PRESCRIPTION_TEMPLATE_LINE") <> (SELECT COALESCE(sum(jsonb_array_length("TemplateLines")), 0) FROM "PRESCRIPTION" WHERE "PrescriptionKind" = 'PrescriptionTemplate') THEN RAISE EXCEPTION 'Incomplete transfer of PRESCRIPTION_TEMPLATE_LINE'; END IF;
                    IF (SELECT count(*) FROM "WORKING_PERIOD") <> (SELECT COALESCE(sum(jsonb_array_length("Periods")), 0) FROM "WORKING_WEEK") THEN RAISE EXCEPTION 'Incomplete transfer of WORKING_PERIOD'; END IF;
                    IF (SELECT count(*) FROM "WORKING_REVISION_PERIOD") <> (SELECT COALESCE(sum(jsonb_array_length("Periods")), 0) FROM "WORKING_SCHEDULE_REVISION") THEN RAISE EXCEPTION 'Incomplete transfer of WORKING_REVISION_PERIOD'; END IF;
                    IF (SELECT count(*) FROM "ACCESS_GROUP_GRANT") <> (SELECT COALESCE(sum(jsonb_array_length("Grants")), 0) FROM "ACCESS_GROUP") THEN RAISE EXCEPTION 'Incomplete transfer of ACCESS_GROUP_GRANT'; END IF;
                    IF (SELECT count(*) FROM "TREATMENT_PAYOS_ORDER_LINE") <> (SELECT COALESCE(sum(jsonb_array_length("Lines")), 0) FROM "TREATMENT_PAYOS_ORDER") THEN RAISE EXCEPTION 'Incomplete transfer of TREATMENT_PAYOS_ORDER_LINE'; END IF;
                END $verify$;

                CREATE INDEX "IX_PRESCRIPTION_BranchId" ON "PRESCRIPTION" ("BranchId");

                CREATE UNIQUE INDEX "IX_PRESCRIPTION_Code" ON "PRESCRIPTION" ("Code");

                CREATE INDEX "IX_PRESCRIPTION_CreatedBy" ON "PRESCRIPTION" ("CreatedBy");

                CREATE INDEX "IX_PRESCRIPTION_FinalizedBy" ON "PRESCRIPTION" ("FinalizedBy");

                CREATE INDEX "IX_PRESCRIPTION_PatientId_PrescribedOn" ON "PRESCRIPTION" ("PatientId", "PrescribedOn");

                CREATE INDEX "IX_PRESCRIPTION_PatientId_Status" ON "PRESCRIPTION" ("PatientId", "Status");

                CREATE INDEX "IX_PRESCRIPTION_PrescribedByDoctorId" ON "PRESCRIPTION" ("PrescribedByDoctorId");

                CREATE INDEX "IX_PRESCRIPTION_PrescriberEmployeeId" ON "PRESCRIPTION" ("PrescriberEmployeeId");

                CREATE INDEX "IX_PRESCRIPTION_SupersedesId" ON "PRESCRIPTION" ("SupersedesId");

                CREATE INDEX "IX_PRESCRIPTION_UpdatedBy" ON "PRESCRIPTION" ("UpdatedBy");

                CREATE UNIQUE INDEX "IX_PRESCRIPTION_VisitId_Revision" ON "PRESCRIPTION" ("VisitId", "Revision");

                CREATE INDEX "IX_AUDIT_LOG_AccessId_OccurredAt" ON "AUDIT_LOG" ("AccessId", "OccurredAt");

                CREATE INDEX "IX_AUDIT_LOG_ActorAccountId" ON "AUDIT_LOG" ("ActorAccountId");

                CREATE INDEX "IX_AUDIT_LOG_ActorId" ON "AUDIT_LOG" ("ActorId");

                CREATE INDEX "IX_AUDIT_LOG_BranchId_At" ON "AUDIT_LOG" ("BranchId", "At");

                CREATE INDEX "IX_AUDIT_LOG_DecidingDentistId" ON "AUDIT_LOG" ("DecidingDentistId");

                CREATE INDEX "IX_AUDIT_LOG_DocumentId_OccurredAt" ON "AUDIT_LOG" ("DocumentId", "OccurredAt");

                CREATE INDEX "IX_AUDIT_LOG_LinkId_OccurredAt" ON "AUDIT_LOG" ("LinkId", "OccurredAt");

                CREATE INDEX "IX_AUDIT_LOG_PatientId_OccurredAt" ON "AUDIT_LOG" ("PatientId", "OccurredAt");

                CREATE INDEX "IX_AUDIT_LOG_Section_At" ON "AUDIT_LOG" ("Section", "At");

                CREATE INDEX "IX_AUDIT_LOG_TargetId_OccurredAt" ON "AUDIT_LOG" ("TargetId", "OccurredAt");

                CREATE INDEX "IX_AUDIT_LOG_VisitId_OccurredAt" ON "AUDIT_LOG" ("VisitId", "OccurredAt");

                CREATE UNIQUE INDEX "UX_AUDIT_LOG_Type_Id" ON "AUDIT_LOG" ("AuditType", "LegacyId");

                CREATE UNIQUE INDEX "UX_AUDIT_LOG_Type_Number" ON "AUDIT_LOG" ("AuditType", "LegacyNumber");

                CREATE INDEX "IX_COMMAND_RECEIPT_AccessId" ON "COMMAND_RECEIPT" ("AccessId");

                CREATE INDEX "IX_COMMAND_RECEIPT_ActorAccountId" ON "COMMAND_RECEIPT" ("ActorAccountId");

                CREATE INDEX "IX_COMMAND_RECEIPT_ActorId" ON "COMMAND_RECEIPT" ("ActorId");

                CREATE INDEX "IX_COMMAND_RECEIPT_AppointmentId" ON "COMMAND_RECEIPT" ("AppointmentId");

                CREATE INDEX "IX_COMMAND_RECEIPT_DossierId" ON "COMMAND_RECEIPT" ("DossierId");

                CREATE INDEX "IX_COMMAND_RECEIPT_OwnerAccountId" ON "COMMAND_RECEIPT" ("OwnerAccountId");

                CREATE INDEX "IX_COMMAND_RECEIPT_PatientId" ON "COMMAND_RECEIPT" ("PatientId");

                CREATE INDEX "IX_COMMAND_RECEIPT_PrescriptionId" ON "COMMAND_RECEIPT" ("PrescriptionId");

                CREATE UNIQUE INDEX "IX_COMMAND_RECEIPT_SessionId_RequestKey" ON "COMMAND_RECEIPT" ("SessionId", "RequestKey");

                CREATE INDEX "IX_COMMAND_RECEIPT_SupportRequestId" ON "COMMAND_RECEIPT" ("SupportRequestId");

                CREATE INDEX "IX_COMMAND_RECEIPT_VisitId" ON "COMMAND_RECEIPT" ("VisitId");

                CREATE UNIQUE INDEX "UX_RECEIPT_ActorAccountId_RequestKey" ON "COMMAND_RECEIPT" ("ReceiptType", "ActorAccountId", "RequestKey");

                CREATE UNIQUE INDEX "UX_RECEIPT_ActorId_RequestKey" ON "COMMAND_RECEIPT" ("ReceiptType", "ActorId", "RequestKey");

                CREATE UNIQUE INDEX "UX_RECEIPT_Id" ON "COMMAND_RECEIPT" ("ReceiptType", "Id");

                CREATE UNIQUE INDEX "UX_RECEIPT_OwnerAccountId_RequestKey" ON "COMMAND_RECEIPT" ("ReceiptType", "OwnerAccountId", "RequestKey");

                CREATE UNIQUE INDEX "UX_RECEIPT_RequestKey" ON "COMMAND_RECEIPT" ("ReceiptType", "RequestKey");

                CREATE UNIQUE INDEX "IX_SCHEDULING_BRANCH_Code" ON "SCHEDULING_BRANCH" ("Code");

                CREATE UNIQUE INDEX "IX_SCHEDULING_CHAIR_BranchId_Code" ON "SCHEDULING_CHAIR" ("BranchId", "Code");

                DROP TABLE "ACCESS_GROUP_GRANT", "ACCOUNT_AUDIT", "APPOINTMENT_CHANGE_RECEIPT", "APPOINTMENT_RECEIPT", "BRANCH_CHAIR", "BRANCH_PROFILE", "CLINICAL_COMMAND_RECEIPT", "CUSTOMER_FAMILY_RECEIPT", "DOCUMENT_AUDIT", "DOCUMENT_RECEIPT", "EXAMINATION_RECEIPT", "EXTERNAL_PRESCRIPTION", "EXTERNAL_PRESCRIPTION_LINE", "FAMILY_RECORD_AUDIT", "FAMILY_RECORD_RECEIPT", "FINANCE_AUDIT", "FINANCE_COMMAND_RECEIPT", "PATIENT_AUDIT", "PATIENT_CREATE_RECEIPT", "PATIENT_LINK_AUDIT", "PATIENT_PRESCRIPTION", "PATIENT_PRESCRIPTION_ITEM", "PATIENT_PRESCRIPTION_RECEIPT", "PRESCRIPTION_AUDIT", "PRESCRIPTION_COMMAND_RECEIPT", "PRESCRIPTION_TEMPLATE", "PRESCRIPTION_TEMPLATE_LINE", "RECEPTION_RECEIPT", "RECOVERY_AUDIT", "SCHEDULE_RECEIPT", "SETTINGS_AUDIT", "SETTINGS_RECEIPT", "STAFF_ACTIVITY_COMMAND_RECEIPT", "SUPPORT_REQUEST_RECEIPT", "TREATMENT_PAYOS_ORDER_LINE", "TREATMENT_PROGRESSION_RECEIPT", "TREATMENT_RECEIPT", "VISIT_AUDIT", "WORKING_PERIOD", "WORKING_REVISION_PERIOD";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
                CREATE TABLE "ACCESS_GROUP_GRANT" (
                    "GroupId" uuid NOT NULL,
                    "Permission" character varying(100) NOT NULL,
                    CONSTRAINT "PK_ACCESS_GROUP_GRANT" PRIMARY KEY ("GroupId", "Permission"),
                    CONSTRAINT "FK_ACCESS_GROUP_GRANT_ACCESS_GROUP_GroupId" FOREIGN KEY ("GroupId") REFERENCES "ACCESS_GROUP" ("Id") ON DELETE CASCADE
                );

                CREATE TABLE "ACCOUNT_AUDIT" (
                    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                    "Action" character varying(40) NOT NULL,
                    "ActorId" integer NOT NULL,
                    "AfterJson" jsonb NOT NULL,
                    "BeforeJson" jsonb NOT NULL,
                    "OccurredAt" timestamp with time zone NOT NULL,
                    "Reason" character varying(500) NOT NULL,
                    "TargetId" integer NOT NULL,
                    CONSTRAINT "PK_ACCOUNT_AUDIT" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_ACCOUNT_AUDIT_USER_ACCOUNT_ActorId" FOREIGN KEY ("ActorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_ACCOUNT_AUDIT_USER_ACCOUNT_TargetId" FOREIGN KEY ("TargetId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "APPOINTMENT_CHANGE_RECEIPT" (
                    "ActorAccountId" integer NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "AppointmentId" uuid NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "ResultJson" jsonb NOT NULL,
                    CONSTRAINT "PK_APPOINTMENT_CHANGE_RECEIPT" PRIMARY KEY ("ActorAccountId", "RequestKey"),
                    CONSTRAINT "FK_APPOINTMENT_CHANGE_RECEIPT_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_APPOINTMENT_CHANGE_RECEIPT_USER_ACCOUNT_ActorAccountId" FOREIGN KEY ("ActorAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "APPOINTMENT_RECEIPT" (
                    "RequestKey" uuid NOT NULL,
                    "ActorScope" character varying(96) NOT NULL,
                    "AppointmentId" uuid NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "ResultJson" jsonb NOT NULL,
                    CONSTRAINT "PK_APPOINTMENT_RECEIPT" PRIMARY KEY ("RequestKey"),
                    CONSTRAINT "FK_APPOINTMENT_RECEIPT_APPOINTMENT_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "APPOINTMENT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "BRANCH_CHAIR" (
                    "Id" integer NOT NULL,
                    "BranchId" integer NOT NULL,
                    "CloseMinute" integer NOT NULL,
                    "Code" character varying(40) NOT NULL,
                    "IsActive" boolean NOT NULL,
                    "Name" character varying(160) NOT NULL,
                    "OpenMinute" integer NOT NULL,
                    "Version" integer NOT NULL,
                    CONSTRAINT "PK_BRANCH_CHAIR" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_BRANCH_CHAIR_SCHEDULING_BRANCH_BranchId" FOREIGN KEY ("BranchId") REFERENCES "SCHEDULING_BRANCH" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_BRANCH_CHAIR_SCHEDULING_CHAIR_Id" FOREIGN KEY ("Id") REFERENCES "SCHEDULING_CHAIR" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "BRANCH_PROFILE" (
                    "Id" integer NOT NULL,
                    "AccountNumber" character varying(50),
                    "Address" character varying(1000),
                    "BankCode" character varying(20),
                    "BookingSettingsVersion" integer NOT NULL,
                    "Code" character varying(40) NOT NULL,
                    "Email" character varying(254),
                    "IsActive" boolean NOT NULL,
                    "LogoAssetId" uuid,
                    "Name" character varying(160) NOT NULL,
                    "PayeeName" character varying(160),
                    "Phone" character varying(20),
                    "SlotIntervalMinutes" integer NOT NULL,
                    "TimeZoneId" character varying(64) NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',
                    "Version" integer NOT NULL,
                    "Website" character varying(255),
                    CONSTRAINT "PK_BRANCH_PROFILE" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_BRANCH_PROFILE_SCHEDULING_BRANCH_Id" FOREIGN KEY ("Id") REFERENCES "SCHEDULING_BRANCH" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "CLINICAL_COMMAND_RECEIPT" (
                    "RequestKey" uuid NOT NULL,
                    "ActorId" integer NOT NULL,
                    "Command" character varying(40) NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "ResultId" uuid NOT NULL,
                    CONSTRAINT "PK_CLINICAL_COMMAND_RECEIPT" PRIMARY KEY ("RequestKey")
                );

                CREATE TABLE "CUSTOMER_FAMILY_RECEIPT" (
                    "OwnerAccountId" integer NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "ResultJson" jsonb NOT NULL,
                    CONSTRAINT "PK_CUSTOMER_FAMILY_RECEIPT" PRIMARY KEY ("OwnerAccountId", "RequestKey"),
                    CONSTRAINT "FK_CUSTOMER_FAMILY_RECEIPT_USER_ACCOUNT_OwnerAccountId" FOREIGN KEY ("OwnerAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "DOCUMENT_AUDIT" (
                    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                    "Action" character varying(32) NOT NULL,
                    "ActorId" integer,
                    "DocumentId" uuid NOT NULL,
                    "Note" character varying(500),
                    "OccurredAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_DOCUMENT_AUDIT" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_DOCUMENT_AUDIT_CLINICAL_DOCUMENT_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "CLINICAL_DOCUMENT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "DOCUMENT_RECEIPT" (
                    "ActorId" integer NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "ResultJson" jsonb NOT NULL,
                    CONSTRAINT "PK_DOCUMENT_RECEIPT" PRIMARY KEY ("ActorId", "RequestKey")
                );

                CREATE TABLE "EXAMINATION_RECEIPT" (
                    "ActorId" integer NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "VisitId" uuid NOT NULL,
                    CONSTRAINT "PK_EXAMINATION_RECEIPT" PRIMARY KEY ("ActorId", "RequestKey"),
                    CONSTRAINT "FK_EXAMINATION_RECEIPT_VISIT_VisitId" FOREIGN KEY ("VisitId") REFERENCES "VISIT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "EXTERNAL_PRESCRIPTION" (
                    "Id" uuid NOT NULL,
                    "BranchId" integer NOT NULL,
                    "CorrectionReason" character varying(500),
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "CreatedBy" integer NOT NULL,
                    "Diagnosis" character varying(1000),
                    "FinalizedAt" timestamp with time zone,
                    "FinalizedBy" integer,
                    "GeneralAdvice" character varying(1000) NOT NULL,
                    "PatientId" integer NOT NULL,
                    "PrescribedByDoctorId" integer NOT NULL,
                    "Revision" integer NOT NULL,
                    "Status" character varying(20) NOT NULL,
                    "SupersedesId" uuid,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "UpdatedBy" integer NOT NULL,
                    "VerificationCode" character varying(16),
                    "Version" integer NOT NULL,
                    "VisitId" uuid NOT NULL,
                    CONSTRAINT "PK_EXTERNAL_PRESCRIPTION" PRIMARY KEY ("Id"),
                    CONSTRAINT "CK_EXTERNAL_PRESCRIPTION_REVISION" CHECK ("Revision" >= 1),
                    CONSTRAINT "CK_EXTERNAL_PRESCRIPTION_STATUS" CHECK ("Status" IN ('Draft','Finalized','Superseded')),
                    CONSTRAINT "FK_EXTERNAL_PRESCRIPTION_EXTERNAL_PRESCRIPTION_SupersedesId" FOREIGN KEY ("SupersedesId") REFERENCES "EXTERNAL_PRESCRIPTION" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_EXTERNAL_PRESCRIPTION_USER_ACCOUNT_FinalizedBy" FOREIGN KEY ("FinalizedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_EXTERNAL_PRESCRIPTION_USER_ACCOUNT_PrescribedByDoctorId" FOREIGN KEY ("PrescribedByDoctorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_EXTERNAL_PRESCRIPTION_VISIT_VisitId" FOREIGN KEY ("VisitId") REFERENCES "VISIT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "FAMILY_RECORD_AUDIT" (
                    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                    "AccessId" uuid NOT NULL,
                    "Action" character varying(40) NOT NULL,
                    "ActorAccountId" integer NOT NULL,
                    "OccurredAt" timestamp with time zone NOT NULL,
                    "Reason" character varying(1000),
                    CONSTRAINT "PK_FAMILY_RECORD_AUDIT" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_FAMILY_RECORD_AUDIT_FAMILY_RECORD_ACCESS_AccessId" FOREIGN KEY ("AccessId") REFERENCES "FAMILY_RECORD_ACCESS" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_FAMILY_RECORD_AUDIT_USER_ACCOUNT_ActorAccountId" FOREIGN KEY ("ActorAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "FAMILY_RECORD_RECEIPT" (
                    "ActorAccountId" integer NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "AccessId" uuid NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "ResultJson" jsonb NOT NULL,
                    CONSTRAINT "PK_FAMILY_RECORD_RECEIPT" PRIMARY KEY ("ActorAccountId", "RequestKey"),
                    CONSTRAINT "FK_FAMILY_RECORD_RECEIPT_FAMILY_RECORD_ACCESS_AccessId" FOREIGN KEY ("AccessId") REFERENCES "FAMILY_RECORD_ACCESS" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_FAMILY_RECORD_RECEIPT_USER_ACCOUNT_ActorAccountId" FOREIGN KEY ("ActorAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "FINANCE_AUDIT" (
                    "Id" uuid NOT NULL,
                    "Action" character varying(64) NOT NULL,
                    "ActorId" integer NOT NULL,
                    "At" timestamp with time zone NOT NULL,
                    "BranchId" integer NOT NULL,
                    "PayloadJson" jsonb NOT NULL,
                    "Target" character varying(100) NOT NULL,
                    "VisitId" uuid,
                    CONSTRAINT "PK_FINANCE_AUDIT" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_FINANCE_AUDIT_USER_ACCOUNT_ActorId" FOREIGN KEY ("ActorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "FINANCE_COMMAND_RECEIPT" (
                    "ActorId" integer NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "ResultJson" jsonb NOT NULL,
                    CONSTRAINT "PK_FINANCE_COMMAND_RECEIPT" PRIMARY KEY ("ActorId", "RequestKey"),
                    CONSTRAINT "FK_FINANCE_COMMAND_RECEIPT_USER_ACCOUNT_ActorId" FOREIGN KEY ("ActorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "PATIENT_AUDIT" (
                    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                    "Action" character varying(16) NOT NULL,
                    "ActorAccountId" integer NOT NULL,
                    "ChangesJson" jsonb NOT NULL,
                    "OccurredAt" timestamp with time zone NOT NULL,
                    "PatientId" integer NOT NULL,
                    "Reason" character varying(500),
                    CONSTRAINT "PK_PATIENT_AUDIT" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_PATIENT_AUDIT_PATIENT_PROFILE_PatientId" FOREIGN KEY ("PatientId") REFERENCES "PATIENT_PROFILE" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PATIENT_AUDIT_USER_ACCOUNT_ActorAccountId" FOREIGN KEY ("ActorAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "PATIENT_CREATE_RECEIPT" (
                    "ActorId" integer NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "PatientId" integer NOT NULL,
                    CONSTRAINT "PK_PATIENT_CREATE_RECEIPT" PRIMARY KEY ("ActorId", "RequestKey"),
                    CONSTRAINT "FK_PATIENT_CREATE_RECEIPT_PATIENT_PROFILE_PatientId" FOREIGN KEY ("PatientId") REFERENCES "PATIENT_PROFILE" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PATIENT_CREATE_RECEIPT_USER_ACCOUNT_ActorId" FOREIGN KEY ("ActorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "PATIENT_LINK_AUDIT" (
                    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                    "LinkId" integer NOT NULL,
                    "Action" character varying(40) NOT NULL,
                    "ActorAccountId" integer NOT NULL,
                    "AfterJson" jsonb NOT NULL,
                    "BeforeJson" jsonb NOT NULL,
                    "OccurredAt" timestamp with time zone NOT NULL,
                    "Reason" character varying(500),
                    CONSTRAINT "PK_PATIENT_LINK_AUDIT" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_PATIENT_LINK_AUDIT_PATIENT_ACCOUNT_LINK_LinkId" FOREIGN KEY ("LinkId") REFERENCES "PATIENT_ACCOUNT_LINK" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PATIENT_LINK_AUDIT_USER_ACCOUNT_ActorAccountId" FOREIGN KEY ("ActorAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "PATIENT_PRESCRIPTION" (
                    "Id" uuid NOT NULL,
                    "Advice" character varying(4000),
                    "BranchId" integer NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "Diagnosis" character varying(1000) NOT NULL,
                    "OtherDiagnosis" character varying(1000),
                    "PatientId" integer NOT NULL,
                    "PrescribedOn" date NOT NULL,
                    "PrescriberEmployeeId" integer NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "Version" integer NOT NULL,
                    CONSTRAINT "PK_PATIENT_PRESCRIPTION" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_PATIENT_PRESCRIPTION_EMPLOYEE_PROFILE_PrescriberEmployeeId" FOREIGN KEY ("PrescriberEmployeeId") REFERENCES "EMPLOYEE_PROFILE" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PATIENT_PRESCRIPTION_PATIENT_PROFILE_PatientId" FOREIGN KEY ("PatientId") REFERENCES "PATIENT_PROFILE" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PATIENT_PRESCRIPTION_SCHEDULING_BRANCH_BranchId" FOREIGN KEY ("BranchId") REFERENCES "SCHEDULING_BRANCH" ("Id") ON DELETE RESTRICT,
                    "RequiresExternalPurchase" boolean NOT NULL DEFAULT FALSE
                );

                CREATE TABLE "PRESCRIPTION_AUDIT" (
                    "Id" uuid NOT NULL,
                    "Action" character varying(40) NOT NULL,
                    "ActorId" integer NOT NULL,
                    "AfterJson" jsonb NOT NULL,
                    "At" timestamp with time zone NOT NULL,
                    "BeforeJson" jsonb NOT NULL,
                    "BranchId" integer NOT NULL,
                    "Reason" character varying(500) NOT NULL,
                    "TemplateId" uuid,
                    "VisitId" uuid,
                    CONSTRAINT "PK_PRESCRIPTION_AUDIT" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_PRESCRIPTION_AUDIT_USER_ACCOUNT_ActorId" FOREIGN KEY ("ActorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "PRESCRIPTION_COMMAND_RECEIPT" (
                    "ActorId" integer NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "ResultJson" jsonb NOT NULL,
                    CONSTRAINT "PK_PRESCRIPTION_COMMAND_RECEIPT" PRIMARY KEY ("ActorId", "RequestKey"),
                    CONSTRAINT "FK_PRESCRIPTION_COMMAND_RECEIPT_USER_ACCOUNT_ActorId" FOREIGN KEY ("ActorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "PRESCRIPTION_TEMPLATE" (
                    "Id" uuid NOT NULL,
                    "Code" character varying(40) NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "CreatedBy" integer NOT NULL,
                    "Description" character varying(1000),
                    "DiagnosisKind" character varying(100),
                    "IsActive" boolean NOT NULL,
                    "Name" character varying(200) NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "UpdatedBy" integer NOT NULL,
                    "Version" integer NOT NULL,
                    CONSTRAINT "PK_PRESCRIPTION_TEMPLATE" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_PRESCRIPTION_TEMPLATE_USER_ACCOUNT_CreatedBy" FOREIGN KEY ("CreatedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PRESCRIPTION_TEMPLATE_USER_ACCOUNT_UpdatedBy" FOREIGN KEY ("UpdatedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "RECEPTION_RECEIPT" (
                    "ActorAccountId" integer NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "VisitId" uuid NOT NULL,
                    CONSTRAINT "PK_RECEPTION_RECEIPT" PRIMARY KEY ("ActorAccountId", "RequestKey"),
                    CONSTRAINT "FK_RECEPTION_RECEIPT_USER_ACCOUNT_ActorAccountId" FOREIGN KEY ("ActorAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_RECEPTION_RECEIPT_VISIT_VisitId" FOREIGN KEY ("VisitId") REFERENCES "VISIT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "RECOVERY_AUDIT" (
                    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                    "Action" character varying(40) NOT NULL,
                    "ActorId" integer,
                    "ChannelType" character varying(16) NOT NULL,
                    "OccurredAt" timestamp with time zone NOT NULL,
                    "Reason" character varying(500) NOT NULL,
                    "Result" character varying(20) NOT NULL,
                    "TargetId" integer NOT NULL,
                    CONSTRAINT "PK_RECOVERY_AUDIT" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_RECOVERY_AUDIT_USER_ACCOUNT_ActorId" FOREIGN KEY ("ActorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_RECOVERY_AUDIT_USER_ACCOUNT_TargetId" FOREIGN KEY ("TargetId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "SCHEDULE_RECEIPT" (
                    "ActorId" integer NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "ResultJson" jsonb NOT NULL,
                    CONSTRAINT "PK_SCHEDULE_RECEIPT" PRIMARY KEY ("ActorId", "RequestKey"),
                    CONSTRAINT "FK_SCHEDULE_RECEIPT_USER_ACCOUNT_ActorId" FOREIGN KEY ("ActorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "SETTINGS_AUDIT" (
                    "Id" uuid NOT NULL,
                    "Action" character varying(40) NOT NULL,
                    "ActorId" integer NOT NULL,
                    "AfterJson" jsonb NOT NULL,
                    "At" timestamp with time zone NOT NULL,
                    "BeforeJson" jsonb NOT NULL,
                    "BranchIds" integer[] NOT NULL,
                    "Reason" character varying(1000) NOT NULL,
                    "Section" character varying(40) NOT NULL,
                    "Target" character varying(100) NOT NULL,
                    "Version" integer NOT NULL,
                    CONSTRAINT "PK_SETTINGS_AUDIT" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_SETTINGS_AUDIT_USER_ACCOUNT_ActorId" FOREIGN KEY ("ActorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "SETTINGS_RECEIPT" (
                    "ActorId" integer NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "ResultJson" jsonb NOT NULL,
                    CONSTRAINT "PK_SETTINGS_RECEIPT" PRIMARY KEY ("ActorId", "RequestKey"),
                    CONSTRAINT "FK_SETTINGS_RECEIPT_USER_ACCOUNT_ActorId" FOREIGN KEY ("ActorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "STAFF_ACTIVITY_COMMAND_RECEIPT" (
                    "Id" uuid NOT NULL,
                    "ExpectedVersion" integer NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "ResultJson" jsonb NOT NULL,
                    "SessionId" uuid NOT NULL,
                    CONSTRAINT "PK_STAFF_ACTIVITY_COMMAND_RECEIPT" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_STAFF_ACTIVITY_COMMAND_RECEIPT_AUTH_SESSION_SessionId" FOREIGN KEY ("SessionId") REFERENCES "AUTH_SESSION" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "SUPPORT_REQUEST_RECEIPT" (
                    "ActorAccountId" integer NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "ResultJson" jsonb NOT NULL,
                    "SupportRequestId" uuid NOT NULL,
                    CONSTRAINT "PK_SUPPORT_REQUEST_RECEIPT" PRIMARY KEY ("ActorAccountId", "RequestKey"),
                    CONSTRAINT "FK_SUPPORT_REQUEST_RECEIPT_APPOINTMENT_SUPPORT_REQUEST_Support~" FOREIGN KEY ("SupportRequestId") REFERENCES "APPOINTMENT_SUPPORT_REQUEST" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_SUPPORT_REQUEST_RECEIPT_USER_ACCOUNT_ActorAccountId" FOREIGN KEY ("ActorAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "TREATMENT_PAYOS_ORDER_LINE" (
                    "OrderId" uuid NOT NULL,
                    "ActivityId" uuid NOT NULL,
                    "AmountVnd" bigint NOT NULL,
                    CONSTRAINT "PK_TREATMENT_PAYOS_ORDER_LINE" PRIMARY KEY ("OrderId", "ActivityId"),
                    CONSTRAINT "CK_TREATMENT_PAYOS_ORDER_LINE_AMOUNT" CHECK ("AmountVnd" > 0),
                    CONSTRAINT "FK_TREATMENT_PAYOS_ORDER_LINE_CLINICAL_ACTIVITY_ActivityId" FOREIGN KEY ("ActivityId") REFERENCES "CLINICAL_ACTIVITY" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_TREATMENT_PAYOS_ORDER_LINE_TREATMENT_PAYOS_ORDER_OrderId" FOREIGN KEY ("OrderId") REFERENCES "TREATMENT_PAYOS_ORDER" ("Id") ON DELETE CASCADE
                );

                CREATE TABLE "TREATMENT_PROGRESSION_RECEIPT" (
                    "RequestKey" uuid NOT NULL,
                    "ActorId" integer NOT NULL,
                    "Command" character varying(40) NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "ResultJson" jsonb NOT NULL,
                    CONSTRAINT "PK_TREATMENT_PROGRESSION_RECEIPT" PRIMARY KEY ("RequestKey")
                );

                CREATE TABLE "TREATMENT_RECEIPT" (
                    "RequestKey" uuid NOT NULL,
                    "ActorId" integer NOT NULL,
                    "Command" character varying(40) NOT NULL,
                    "DossierId" uuid NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "ResultJson" jsonb NOT NULL,
                    CONSTRAINT "PK_TREATMENT_RECEIPT" PRIMARY KEY ("RequestKey"),
                    CONSTRAINT "FK_TREATMENT_RECEIPT_TREATMENT_DOSSIER_DossierId" FOREIGN KEY ("DossierId") REFERENCES "TREATMENT_DOSSIER" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "VISIT_AUDIT" (
                    "Id" uuid NOT NULL,
                    "Action" character varying(32) NOT NULL,
                    "ActorAccountId" integer NOT NULL,
                    "AfterJson" jsonb NOT NULL,
                    "BeforeJson" jsonb NOT NULL,
                    "OccurredAt" timestamp with time zone NOT NULL,
                    "VisitId" uuid NOT NULL,
                    CONSTRAINT "PK_VISIT_AUDIT" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_VISIT_AUDIT_USER_ACCOUNT_ActorAccountId" FOREIGN KEY ("ActorAccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_VISIT_AUDIT_VISIT_VisitId" FOREIGN KEY ("VisitId") REFERENCES "VISIT" ("Id") ON DELETE RESTRICT,
                    "DecidingDentistId" integer,
                    "Reason" character varying(500) NOT NULL DEFAULT ''::character varying,
                    CONSTRAINT "FK_VISIT_AUDIT_EMPLOYEE_PROFILE_DecidingDentistId" FOREIGN KEY ("DecidingDentistId") REFERENCES "EMPLOYEE_PROFILE" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "WORKING_PERIOD" (
                    "Id" uuid NOT NULL,
                    "Day" integer NOT NULL,
                    "EndMinute" integer NOT NULL,
                    "Kind" character varying(16) NOT NULL,
                    "Reason" character varying(500) NOT NULL,
                    "StartMinute" integer NOT NULL,
                    "WeekId" uuid NOT NULL,
                    CONSTRAINT "PK_WORKING_PERIOD" PRIMARY KEY ("Id"),
                    CONSTRAINT "CK_PERIOD_RANGE" CHECK ("Day" BETWEEN 0 AND 6 AND "StartMinute" >= 0 AND "EndMinute" <= 1440 AND "StartMinute" < "EndMinute" AND "StartMinute" % 15 = 0 AND "EndMinute" % 15 = 0 AND "Kind" IN ('Work','Break','Block')),
                    CONSTRAINT "FK_WORKING_PERIOD_WORKING_WEEK_WeekId" FOREIGN KEY ("WeekId") REFERENCES "WORKING_WEEK" ("Id") ON DELETE CASCADE
                );

                CREATE TABLE "WORKING_REVISION_PERIOD" (
                    "Id" uuid NOT NULL,
                    "BranchId" integer NOT NULL,
                    "Day" integer NOT NULL,
                    "EndMinute" integer NOT NULL,
                    "Kind" character varying(16) NOT NULL,
                    "Reason" character varying(500) NOT NULL,
                    "RevisionId" uuid NOT NULL,
                    "StartMinute" integer NOT NULL,
                    CONSTRAINT "PK_WORKING_REVISION_PERIOD" PRIMARY KEY ("Id"),
                    CONSTRAINT "CK_REVISION_PERIOD_RANGE" CHECK ("Day" BETWEEN 0 AND 6 AND "StartMinute" >= 0 AND "EndMinute" <= 1440 AND "StartMinute" < "EndMinute" AND "StartMinute" % 30 = 0 AND "EndMinute" % 30 = 0 AND "Kind" IN ('Work','Break','Block')),
                    CONSTRAINT "FK_WORKING_REVISION_PERIOD_SCHEDULING_BRANCH_BranchId" FOREIGN KEY ("BranchId") REFERENCES "SCHEDULING_BRANCH" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_WORKING_REVISION_PERIOD_WORKING_SCHEDULE_REVISION_RevisionId" FOREIGN KEY ("RevisionId") REFERENCES "WORKING_SCHEDULE_REVISION" ("Id") ON DELETE CASCADE
                );

                CREATE TABLE "EXTERNAL_PRESCRIPTION_LINE" (
                    "Id" uuid NOT NULL,
                    "ActiveIngredient" character varying(200),
                    "AfternoonDose" character varying(20) NOT NULL DEFAULT '0',
                    "BranchId" integer NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "CreatedBy" integer NOT NULL,
                    "Days" integer NOT NULL,
                    "DosageForm" character varying(200),
                    "EveningDose" character varying(20) NOT NULL DEFAULT '0',
                    "FinalizedAt" timestamp with time zone,
                    "Instructions" character varying(1000) NOT NULL,
                    "MealTiming" character varying(30) NOT NULL DEFAULT 'AfterMeal',
                    "MedicineCode" character varying(40) NOT NULL,
                    "MedicineId" uuid,
                    "MedicineName" character varying(200) NOT NULL,
                    "MorningDose" character varying(20) NOT NULL DEFAULT '0',
                    "NoonDose" character varying(20) NOT NULL DEFAULT '0',
                    "PatientId" integer NOT NULL,
                    "PrescribedByDoctorId" integer NOT NULL,
                    "PrescriptionId" uuid NOT NULL,
                    "Quantity" integer NOT NULL,
                    "Route" character varying(40) NOT NULL DEFAULT 'Uống',
                    "Status" character varying(20) NOT NULL DEFAULT 'Draft',
                    "Strength" character varying(200),
                    "Unit" character varying(100) NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "UpdatedBy" integer NOT NULL,
                    "Version" integer NOT NULL,
                    "VisitId" uuid NOT NULL,
                    CONSTRAINT "PK_EXTERNAL_PRESCRIPTION_LINE" PRIMARY KEY ("Id"),
                    CONSTRAINT "CK_EXTERNAL_PRESCRIPTION_QUANTITY" CHECK ("Quantity" > 0),
                    CONSTRAINT "FK_EXTERNAL_PRESCRIPTION_LINE_CATALOG_MEDICINE_MedicineId" FOREIGN KEY ("MedicineId") REFERENCES "CATALOG_MEDICINE" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_EXTERNAL_PRESCRIPTION_LINE_EXTERNAL_PRESCRIPTION_Prescriptio" FOREIGN KEY ("PrescriptionId") REFERENCES "EXTERNAL_PRESCRIPTION" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_EXTERNAL_PRESCRIPTION_LINE_USER_ACCOUNT_CreatedBy" FOREIGN KEY ("CreatedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_EXTERNAL_PRESCRIPTION_LINE_USER_ACCOUNT_UpdatedBy" FOREIGN KEY ("UpdatedBy") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_EXTERNAL_PRESCRIPTION_LINE_VISIT_VisitId" FOREIGN KEY ("VisitId") REFERENCES "VISIT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "PATIENT_PRESCRIPTION_ITEM" (
                    "Id" uuid NOT NULL,
                    "DosageForm" character varying(200),
                    "Instructions" character varying(1000) NOT NULL,
                    "MedicineCode" character varying(40) NOT NULL,
                    "MedicineId" uuid NOT NULL,
                    "MedicineName" character varying(200) NOT NULL,
                    "Position" integer NOT NULL,
                    "PrescriptionId" uuid NOT NULL,
                    "Quantity" integer NOT NULL,
                    "Strength" character varying(200),
                    "Unit" character varying(100) NOT NULL,
                    CONSTRAINT "PK_PATIENT_PRESCRIPTION_ITEM" PRIMARY KEY ("Id"),
                    CONSTRAINT "CK_PATIENT_PRESCRIPTION_ITEM_QUANTITY" CHECK ("Quantity" > 0),
                    CONSTRAINT "FK_PATIENT_PRESCRIPTION_ITEM_CATALOG_MEDICINE_MedicineId" FOREIGN KEY ("MedicineId") REFERENCES "CATALOG_MEDICINE" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PATIENT_PRESCRIPTION_ITEM_PATIENT_PRESCRIPTION_Prescription~" FOREIGN KEY ("PrescriptionId") REFERENCES "PATIENT_PRESCRIPTION" ("Id") ON DELETE CASCADE,
                    "IsExternalPurchase" boolean NOT NULL DEFAULT FALSE
                );

                CREATE TABLE "PATIENT_PRESCRIPTION_RECEIPT" (
                    "ActorId" integer NOT NULL,
                    "RequestKey" uuid NOT NULL,
                    "Fingerprint" character varying(64) NOT NULL,
                    "PrescriptionId" uuid NOT NULL,
                    CONSTRAINT "PK_PATIENT_PRESCRIPTION_RECEIPT" PRIMARY KEY ("ActorId", "RequestKey"),
                    CONSTRAINT "FK_PATIENT_PRESCRIPTION_RECEIPT_PATIENT_PRESCRIPTION_Prescript~" FOREIGN KEY ("PrescriptionId") REFERENCES "PATIENT_PRESCRIPTION" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PATIENT_PRESCRIPTION_RECEIPT_USER_ACCOUNT_ActorId" FOREIGN KEY ("ActorId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
                );

                CREATE TABLE "PRESCRIPTION_TEMPLATE_LINE" (
                    "Id" uuid NOT NULL,
                    "ActiveIngredient" character varying(200),
                    "AfternoonDose" character varying(20) NOT NULL DEFAULT '0',
                    "Days" integer NOT NULL,
                    "DosageForm" character varying(200),
                    "EveningDose" character varying(20) NOT NULL DEFAULT '0',
                    "Instructions" character varying(1000) NOT NULL,
                    "MealTiming" character varying(30) NOT NULL DEFAULT 'AfterMeal',
                    "MedicineCode" character varying(40) NOT NULL,
                    "MedicineId" uuid NOT NULL,
                    "MedicineName" character varying(200) NOT NULL,
                    "MorningDose" character varying(20) NOT NULL DEFAULT '0',
                    "NoonDose" character varying(20) NOT NULL DEFAULT '0',
                    "Quantity" integer NOT NULL,
                    "Route" character varying(40) NOT NULL DEFAULT 'Uống',
                    "SortOrder" integer NOT NULL,
                    "Strength" character varying(200),
                    "TemplateId" uuid NOT NULL,
                    "Unit" character varying(40) NOT NULL,
                    CONSTRAINT "PK_PRESCRIPTION_TEMPLATE_LINE" PRIMARY KEY ("Id"),
                    CONSTRAINT "CK_PRESCRIPTION_TEMPLATE_QUANTITY" CHECK ("Quantity" > 0),
                    CONSTRAINT "FK_PRESCRIPTION_TEMPLATE_LINE_CATALOG_MEDICINE_MedicineId" FOREIGN KEY ("MedicineId") REFERENCES "CATALOG_MEDICINE" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PRESCRIPTION_TEMPLATE_LINE_PRESCRIPTION_TEMPLATE_TemplateId" FOREIGN KEY ("TemplateId") REFERENCES "PRESCRIPTION_TEMPLATE" ("Id") ON DELETE CASCADE
                );

                INSERT INTO "EXTERNAL_PRESCRIPTION" ("Id", "BranchId", "CorrectionReason", "CreatedAt", "CreatedBy", "Diagnosis", "FinalizedAt", "FinalizedBy", "GeneralAdvice", "PatientId", "PrescribedByDoctorId", "Revision", "Status", "SupersedesId", "UpdatedAt", "UpdatedBy", "VerificationCode", "Version", "VisitId")
                SELECT s."Id",
                       s."BranchId",
                       s."CorrectionReason",
                       s."CreatedAt",
                       s."CreatedBy",
                       s."Diagnosis",
                       s."FinalizedAt",
                       s."FinalizedBy",
                       s."GeneralAdvice",
                       s."PatientId",
                       s."PrescribedByDoctorId",
                       s."Revision",
                       s."Status",
                       s."SupersedesId",
                       s."UpdatedAt",
                       s."UpdatedBy",
                       s."VerificationCode",
                       s."Version",
                       s."VisitId"
                FROM "PRESCRIPTION" s WHERE s."PrescriptionKind" = 'ExternalPrescription';

                INSERT INTO "PATIENT_PRESCRIPTION" ("Id", "Advice", "BranchId", "CreatedAt", "Diagnosis", "OtherDiagnosis", "PatientId", "PrescribedOn", "PrescriberEmployeeId", "UpdatedAt", "Version", "RequiresExternalPurchase")
                SELECT s."Id",
                       s."Advice",
                       s."BranchId",
                       s."CreatedAt",
                       s."Diagnosis",
                       s."OtherDiagnosis",
                       s."PatientId",
                       s."PrescribedOn",
                       s."PrescriberEmployeeId",
                       s."UpdatedAt",
                       s."Version",
                       s."RequiresExternalPurchase"
                FROM "PRESCRIPTION" s WHERE s."PrescriptionKind" = 'PatientPrescription';

                INSERT INTO "PRESCRIPTION_TEMPLATE" ("Id", "Code", "CreatedAt", "CreatedBy", "Description", "DiagnosisKind", "IsActive", "Name", "UpdatedAt", "UpdatedBy", "Version")
                SELECT s."Id",
                       s."Code",
                       s."CreatedAt",
                       s."CreatedBy",
                       s."Description",
                       s."DiagnosisKind",
                       s."IsActive",
                       s."Name",
                       s."UpdatedAt",
                       s."UpdatedBy",
                       s."Version"
                FROM "PRESCRIPTION" s WHERE s."PrescriptionKind" = 'PrescriptionTemplate';

                INSERT INTO "ACCOUNT_AUDIT" ("Id", "Action", "ActorId", "AfterJson", "BeforeJson", "OccurredAt", "Reason", "TargetId")
                SELECT s."LegacyNumber",
                       s."Action",
                       s."ActorId",
                       s."AfterJson"::jsonb,
                       s."BeforeJson"::jsonb,
                       s."OccurredAt",
                       s."Reason",
                       s."TargetId"
                FROM "AUDIT_LOG" s WHERE s."AuditType" = 'AccountAudit';

                INSERT INTO "RECOVERY_AUDIT" ("Id", "Action", "ActorId", "ChannelType", "OccurredAt", "Reason", "Result", "TargetId")
                SELECT s."LegacyNumber",
                       s."Action",
                       s."ActorId",
                       s."ChannelType",
                       s."OccurredAt",
                       s."Reason",
                       s."Result",
                       s."TargetId"
                FROM "AUDIT_LOG" s WHERE s."AuditType" = 'RecoveryAudit';

                INSERT INTO "DOCUMENT_AUDIT" ("Id", "Action", "ActorId", "DocumentId", "Note", "OccurredAt")
                SELECT s."LegacyNumber",
                       s."Action",
                       s."ActorId",
                       s."DocumentId",
                       s."Note",
                       s."OccurredAt"
                FROM "AUDIT_LOG" s WHERE s."AuditType" = 'DocumentAudit';

                INSERT INTO "FAMILY_RECORD_AUDIT" ("Id", "AccessId", "Action", "ActorAccountId", "OccurredAt", "Reason")
                SELECT s."LegacyNumber",
                       s."AccessId",
                       s."Action",
                       s."ActorAccountId",
                       s."OccurredAt",
                       s."Reason"
                FROM "AUDIT_LOG" s WHERE s."AuditType" = 'FamilyRecordAudit';

                INSERT INTO "FINANCE_AUDIT" ("Id", "Action", "ActorId", "At", "BranchId", "PayloadJson", "Target", "VisitId")
                SELECT s."LegacyId",
                       s."Action",
                       s."ActorId",
                       s."At",
                       s."BranchId",
                       s."PayloadJson"::jsonb,
                       s."Target",
                       s."VisitId"
                FROM "AUDIT_LOG" s WHERE s."AuditType" = 'FinanceAudit';

                INSERT INTO "PATIENT_LINK_AUDIT" ("Id", "Action", "ActorAccountId", "AfterJson", "BeforeJson", "LinkId", "OccurredAt", "Reason")
                SELECT s."LegacyNumber",
                       s."Action",
                       s."ActorAccountId",
                       s."AfterJson"::jsonb,
                       s."BeforeJson"::jsonb,
                       s."LinkId",
                       s."OccurredAt",
                       s."Reason"
                FROM "AUDIT_LOG" s WHERE s."AuditType" = 'PatientLinkAudit';

                INSERT INTO "PATIENT_AUDIT" ("Id", "Action", "ActorAccountId", "ChangesJson", "OccurredAt", "PatientId", "Reason")
                SELECT s."LegacyNumber",
                       s."Action",
                       s."ActorAccountId",
                       s."ChangesJson"::jsonb,
                       s."OccurredAt",
                       s."PatientId",
                       s."Reason"
                FROM "AUDIT_LOG" s WHERE s."AuditType" = 'PatientAudit';

                INSERT INTO "PRESCRIPTION_AUDIT" ("Id", "Action", "ActorId", "AfterJson", "At", "BeforeJson", "BranchId", "Reason", "TemplateId", "VisitId")
                SELECT s."LegacyId",
                       s."Action",
                       s."ActorId",
                       s."AfterJson"::jsonb,
                       s."At",
                       s."BeforeJson"::jsonb,
                       s."BranchId",
                       s."Reason",
                       s."TemplateId",
                       s."VisitId"
                FROM "AUDIT_LOG" s WHERE s."AuditType" = 'PrescriptionAudit';

                INSERT INTO "SETTINGS_AUDIT" ("Id", "Action", "ActorId", "AfterJson", "At", "BeforeJson", "BranchIds", "Reason", "Section", "Target", "Version")
                SELECT s."LegacyId",
                       s."Action",
                       s."ActorId",
                       s."AfterJson"::jsonb,
                       s."At",
                       s."BeforeJson"::jsonb,
                       s."BranchIds",
                       s."Reason",
                       s."Section",
                       s."Target",
                       s."Version"
                FROM "AUDIT_LOG" s WHERE s."AuditType" = 'SettingsAudit';

                INSERT INTO "VISIT_AUDIT" ("Id", "Action", "ActorAccountId", "AfterJson", "BeforeJson", "OccurredAt", "VisitId", "DecidingDentistId", "Reason")
                SELECT s."LegacyId",
                       s."Action",
                       s."ActorAccountId",
                       s."AfterJson"::jsonb,
                       s."BeforeJson"::jsonb,
                       s."OccurredAt",
                       s."VisitId",
                       s."DecidingDentistId",
                       s."Reason"
                FROM "AUDIT_LOG" s WHERE s."AuditType" = 'VisitAudit';

                INSERT INTO "SUPPORT_REQUEST_RECEIPT" ("ActorAccountId", "RequestKey", "Fingerprint", "ResultJson", "SupportRequestId")
                SELECT s."ActorAccountId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."ResultJson"::jsonb,
                       s."SupportRequestId"
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'SupportRequestReceipt';

                INSERT INTO "APPOINTMENT_CHANGE_RECEIPT" ("ActorAccountId", "RequestKey", "AppointmentId", "Fingerprint", "ResultJson")
                SELECT s."ActorAccountId",
                       s."RequestKey",
                       s."AppointmentId",
                       s."Fingerprint",
                       s."ResultJson"::jsonb
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'AppointmentChangeReceipt';

                INSERT INTO "APPOINTMENT_RECEIPT" ("RequestKey", "ActorScope", "AppointmentId", "Fingerprint", "ResultJson")
                SELECT s."RequestKey",
                       s."ActorScope",
                       s."AppointmentId",
                       s."Fingerprint",
                       s."ResultJson"::jsonb
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'AppointmentReceipt';

                INSERT INTO "STAFF_ACTIVITY_COMMAND_RECEIPT" ("Id", "ExpectedVersion", "RequestKey", "ResultJson", "SessionId")
                SELECT s."Id",
                       s."ExpectedVersion",
                       s."RequestKey",
                       s."ResultJson"::jsonb,
                       s."SessionId"
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'StaffActivityCommandReceipt';

                INSERT INTO "EXAMINATION_RECEIPT" ("ActorId", "RequestKey", "Fingerprint", "VisitId")
                SELECT s."ActorId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."VisitId"
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'ExaminationReceipt';

                INSERT INTO "DOCUMENT_RECEIPT" ("ActorId", "RequestKey", "Fingerprint", "ResultJson")
                SELECT s."ActorId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."ResultJson"::jsonb
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'DocumentReceipt';

                INSERT INTO "CUSTOMER_FAMILY_RECEIPT" ("OwnerAccountId", "RequestKey", "CreatedAt", "Fingerprint", "ResultJson")
                SELECT s."OwnerAccountId",
                       s."RequestKey",
                       s."CreatedAt",
                       s."Fingerprint",
                       s."ResultJson"::jsonb
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'FamilyMemberReceipt';

                INSERT INTO "FAMILY_RECORD_RECEIPT" ("ActorAccountId", "RequestKey", "AccessId", "Fingerprint", "ResultJson")
                SELECT s."ActorAccountId",
                       s."RequestKey",
                       s."AccessId",
                       s."Fingerprint",
                       s."ResultJson"::jsonb
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'FamilyRecordReceipt';

                INSERT INTO "FINANCE_COMMAND_RECEIPT" ("ActorId", "RequestKey", "Fingerprint", "ResultJson")
                SELECT s."ActorId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."ResultJson"::jsonb
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'FinanceCommandReceipt';

                INSERT INTO "PATIENT_CREATE_RECEIPT" ("ActorId", "RequestKey", "CreatedAt", "Fingerprint", "PatientId")
                SELECT s."ActorId",
                       s."RequestKey",
                       s."CreatedAt",
                       s."Fingerprint",
                       s."PatientId"
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'PatientCreateReceipt';

                INSERT INTO "PRESCRIPTION_COMMAND_RECEIPT" ("ActorId", "RequestKey", "Fingerprint", "ResultJson")
                SELECT s."ActorId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."ResultJson"::jsonb
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'PrescriptionCommandReceipt';

                INSERT INTO "PATIENT_PRESCRIPTION_RECEIPT" ("ActorId", "RequestKey", "Fingerprint", "PrescriptionId")
                SELECT s."ActorId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."PrescriptionId"
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'PatientPrescriptionReceipt';

                INSERT INTO "SCHEDULE_RECEIPT" ("ActorId", "RequestKey", "Fingerprint", "ResultJson")
                SELECT s."ActorId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."ResultJson"::jsonb
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'ScheduleReceipt';

                INSERT INTO "SETTINGS_RECEIPT" ("ActorId", "RequestKey", "Fingerprint", "ResultJson")
                SELECT s."ActorId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."ResultJson"::jsonb
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'SettingsReceipt';

                INSERT INTO "CLINICAL_COMMAND_RECEIPT" ("RequestKey", "ActorId", "Command", "Fingerprint", "ResultId")
                SELECT s."RequestKey",
                       s."ActorId",
                       s."Command",
                       s."Fingerprint",
                       s."ResultId"
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'ClinicalCommandReceipt';

                INSERT INTO "TREATMENT_RECEIPT" ("RequestKey", "ActorId", "Command", "DossierId", "Fingerprint", "ResultJson")
                SELECT s."RequestKey",
                       s."ActorId",
                       s."Command",
                       s."DossierId",
                       s."Fingerprint",
                       s."ResultJson"::jsonb
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'TreatmentReceipt';

                INSERT INTO "TREATMENT_PROGRESSION_RECEIPT" ("RequestKey", "ActorId", "Command", "Fingerprint", "ResultJson")
                SELECT s."RequestKey",
                       s."ActorId",
                       s."Command",
                       s."Fingerprint",
                       s."ResultJson"::jsonb
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'TreatmentProgressionReceipt';

                INSERT INTO "RECEPTION_RECEIPT" ("ActorAccountId", "RequestKey", "Fingerprint", "VisitId")
                SELECT s."ActorAccountId",
                       s."RequestKey",
                       s."Fingerprint",
                       s."VisitId"
                FROM "COMMAND_RECEIPT" s WHERE s."ReceiptType" = 'ReceptionReceipt';

                INSERT INTO "EXTERNAL_PRESCRIPTION_LINE"
                SELECT restored.* FROM "PRESCRIPTION" source
                CROSS JOIN LATERAL jsonb_array_elements(source."VisitLines") item(value)
                CROSS JOIN LATERAL jsonb_populate_record(NULL::"EXTERNAL_PRESCRIPTION_LINE", item.value || jsonb_build_object('PrescriptionId', source."Id")) restored WHERE source."PrescriptionKind" = 'ExternalPrescription';

                INSERT INTO "PATIENT_PRESCRIPTION_ITEM"
                SELECT restored.* FROM "PRESCRIPTION" source
                CROSS JOIN LATERAL jsonb_array_elements(source."Items") item(value)
                CROSS JOIN LATERAL jsonb_populate_record(NULL::"PATIENT_PRESCRIPTION_ITEM", item.value || jsonb_build_object('PrescriptionId', source."Id")) restored WHERE source."PrescriptionKind" = 'PatientPrescription';

                INSERT INTO "PRESCRIPTION_TEMPLATE_LINE"
                SELECT restored.* FROM "PRESCRIPTION" source
                CROSS JOIN LATERAL jsonb_array_elements(source."TemplateLines") item(value)
                CROSS JOIN LATERAL jsonb_populate_record(NULL::"PRESCRIPTION_TEMPLATE_LINE", item.value || jsonb_build_object('TemplateId', source."Id")) restored WHERE source."PrescriptionKind" = 'PrescriptionTemplate';

                INSERT INTO "WORKING_PERIOD"
                SELECT restored.* FROM "WORKING_WEEK" source
                CROSS JOIN LATERAL jsonb_array_elements(source."Periods") item(value)
                CROSS JOIN LATERAL jsonb_populate_record(NULL::"WORKING_PERIOD", item.value || jsonb_build_object('WeekId', source."Id")) restored;

                INSERT INTO "WORKING_REVISION_PERIOD"
                SELECT restored.* FROM "WORKING_SCHEDULE_REVISION" source
                CROSS JOIN LATERAL jsonb_array_elements(source."Periods") item(value)
                CROSS JOIN LATERAL jsonb_populate_record(NULL::"WORKING_REVISION_PERIOD", item.value || jsonb_build_object('RevisionId', source."Id")) restored;

                INSERT INTO "ACCESS_GROUP_GRANT"
                SELECT restored.* FROM "ACCESS_GROUP" source
                CROSS JOIN LATERAL jsonb_array_elements(source."Grants") item(value)
                CROSS JOIN LATERAL jsonb_populate_record(NULL::"ACCESS_GROUP_GRANT", item.value || jsonb_build_object('GroupId', source."Id")) restored;

                INSERT INTO "TREATMENT_PAYOS_ORDER_LINE"
                SELECT restored.* FROM "TREATMENT_PAYOS_ORDER" source
                CROSS JOIN LATERAL jsonb_array_elements(source."Lines") item(value)
                CROSS JOIN LATERAL jsonb_populate_record(NULL::"TREATMENT_PAYOS_ORDER_LINE", item.value || jsonb_build_object('OrderId', source."Id")) restored;

                INSERT INTO "BRANCH_PROFILE" ("Id", "AccountNumber", "Address", "BankCode", "BookingSettingsVersion", "Code", "Email", "IsActive", "LogoAssetId", "Name", "PayeeName", "Phone", "SlotIntervalMinutes", "TimeZoneId", "Version", "Website") SELECT "Id", "AccountNumber", "Address", "BankCode", "BookingSettingsVersion", "Code", "Email", "Active", "LogoAssetId", "Name", "PayeeName", "Phone", "SlotIntervalMinutes", "TimeZoneId", "Version", "Website" FROM "SCHEDULING_BRANCH" WHERE "Code" IS NOT NULL;

                INSERT INTO "BRANCH_CHAIR" ("Id", "BranchId", "CloseMinute", "Code", "IsActive", "Name", "OpenMinute", "Version") SELECT "Id", "BranchId", "CloseMinute", "Code", "Active", "Name", "OpenMinute", "Version" FROM "SCHEDULING_CHAIR" WHERE "Code" IS NOT NULL;

                CREATE INDEX "IX_ACCOUNT_AUDIT_ActorId" ON "ACCOUNT_AUDIT" ("ActorId");

                CREATE INDEX "IX_ACCOUNT_AUDIT_TargetId_OccurredAt" ON "ACCOUNT_AUDIT" ("TargetId", "OccurredAt");

                SELECT setval(pg_get_serial_sequence('"ACCOUNT_AUDIT"', 'Id'), GREATEST(COALESCE((SELECT max("Id") FROM "ACCOUNT_AUDIT"), 0), 1), EXISTS (SELECT 1 FROM "ACCOUNT_AUDIT"));

                CREATE INDEX "IX_APPOINTMENT_CHANGE_RECEIPT_AppointmentId" ON "APPOINTMENT_CHANGE_RECEIPT" ("AppointmentId");

                CREATE INDEX "IX_APPOINTMENT_RECEIPT_AppointmentId" ON "APPOINTMENT_RECEIPT" ("AppointmentId");

                CREATE UNIQUE INDEX "IX_BRANCH_CHAIR_BranchId_Code" ON "BRANCH_CHAIR" ("BranchId", "Code");

                CREATE UNIQUE INDEX "IX_BRANCH_PROFILE_Code" ON "BRANCH_PROFILE" ("Code");

                CREATE INDEX "IX_DOCUMENT_AUDIT_DocumentId_OccurredAt" ON "DOCUMENT_AUDIT" ("DocumentId", "OccurredAt");

                SELECT setval(pg_get_serial_sequence('"DOCUMENT_AUDIT"', 'Id'), GREATEST(COALESCE((SELECT max("Id") FROM "DOCUMENT_AUDIT"), 0), 1), EXISTS (SELECT 1 FROM "DOCUMENT_AUDIT"));

                CREATE INDEX "IX_EXAMINATION_RECEIPT_VisitId" ON "EXAMINATION_RECEIPT" ("VisitId");

                CREATE INDEX "IX_EXTERNAL_PRESCRIPTION_FinalizedBy" ON "EXTERNAL_PRESCRIPTION" ("FinalizedBy");

                CREATE INDEX "IX_EXTERNAL_PRESCRIPTION_PatientId_Status" ON "EXTERNAL_PRESCRIPTION" ("PatientId", "Status");

                CREATE INDEX "IX_EXTERNAL_PRESCRIPTION_PrescribedByDoctorId" ON "EXTERNAL_PRESCRIPTION" ("PrescribedByDoctorId");

                CREATE INDEX "IX_EXTERNAL_PRESCRIPTION_SupersedesId" ON "EXTERNAL_PRESCRIPTION" ("SupersedesId");

                CREATE UNIQUE INDEX "IX_EXTERNAL_PRESCRIPTION_VisitId_Revision" ON "EXTERNAL_PRESCRIPTION" ("VisitId", "Revision");

                CREATE INDEX "IX_EXTERNAL_PRESCRIPTION_LINE_CreatedBy" ON "EXTERNAL_PRESCRIPTION_LINE" ("CreatedBy");

                CREATE INDEX "IX_EXTERNAL_PRESCRIPTION_LINE_MedicineId" ON "EXTERNAL_PRESCRIPTION_LINE" ("MedicineId");

                CREATE INDEX "IX_EXTERNAL_PRESCRIPTION_LINE_PatientId" ON "EXTERNAL_PRESCRIPTION_LINE" ("PatientId");

                CREATE INDEX "IX_EXTERNAL_PRESCRIPTION_LINE_PrescriptionId" ON "EXTERNAL_PRESCRIPTION_LINE" ("PrescriptionId");

                CREATE INDEX "IX_EXTERNAL_PRESCRIPTION_LINE_UpdatedBy" ON "EXTERNAL_PRESCRIPTION_LINE" ("UpdatedBy");

                CREATE INDEX "IX_EXTERNAL_PRESCRIPTION_LINE_VisitId" ON "EXTERNAL_PRESCRIPTION_LINE" ("VisitId");

                CREATE INDEX "IX_FAMILY_RECORD_AUDIT_AccessId_OccurredAt" ON "FAMILY_RECORD_AUDIT" ("AccessId", "OccurredAt");

                CREATE INDEX "IX_FAMILY_RECORD_AUDIT_ActorAccountId" ON "FAMILY_RECORD_AUDIT" ("ActorAccountId");

                SELECT setval(pg_get_serial_sequence('"FAMILY_RECORD_AUDIT"', 'Id'), GREATEST(COALESCE((SELECT max("Id") FROM "FAMILY_RECORD_AUDIT"), 0), 1), EXISTS (SELECT 1 FROM "FAMILY_RECORD_AUDIT"));

                CREATE INDEX "IX_FAMILY_RECORD_RECEIPT_AccessId" ON "FAMILY_RECORD_RECEIPT" ("AccessId");

                CREATE INDEX "IX_FINANCE_AUDIT_ActorId" ON "FINANCE_AUDIT" ("ActorId");

                CREATE INDEX "IX_FINANCE_AUDIT_BranchId_At" ON "FINANCE_AUDIT" ("BranchId", "At");

                CREATE INDEX "IX_PATIENT_AUDIT_ActorAccountId" ON "PATIENT_AUDIT" ("ActorAccountId");

                CREATE INDEX "IX_PATIENT_AUDIT_PatientId_OccurredAt" ON "PATIENT_AUDIT" ("PatientId", "OccurredAt");

                SELECT setval(pg_get_serial_sequence('"PATIENT_AUDIT"', 'Id'), GREATEST(COALESCE((SELECT max("Id") FROM "PATIENT_AUDIT"), 0), 1), EXISTS (SELECT 1 FROM "PATIENT_AUDIT"));

                CREATE INDEX "IX_PATIENT_CREATE_RECEIPT_PatientId" ON "PATIENT_CREATE_RECEIPT" ("PatientId");

                CREATE INDEX "IX_PATIENT_LINK_AUDIT_ActorAccountId" ON "PATIENT_LINK_AUDIT" ("ActorAccountId");

                CREATE INDEX "IX_PATIENT_LINK_AUDIT_LinkId_OccurredAt" ON "PATIENT_LINK_AUDIT" ("LinkId", "OccurredAt");

                SELECT setval(pg_get_serial_sequence('"PATIENT_LINK_AUDIT"', 'Id'), GREATEST(COALESCE((SELECT max("Id") FROM "PATIENT_LINK_AUDIT"), 0), 1), EXISTS (SELECT 1 FROM "PATIENT_LINK_AUDIT"));

                CREATE INDEX "IX_PATIENT_PRESCRIPTION_BranchId" ON "PATIENT_PRESCRIPTION" ("BranchId");

                CREATE INDEX "IX_PATIENT_PRESCRIPTION_PatientId_PrescribedOn" ON "PATIENT_PRESCRIPTION" ("PatientId", "PrescribedOn");

                CREATE INDEX "IX_PATIENT_PRESCRIPTION_PrescriberEmployeeId" ON "PATIENT_PRESCRIPTION" ("PrescriberEmployeeId");

                CREATE INDEX "IX_PATIENT_PRESCRIPTION_ITEM_MedicineId" ON "PATIENT_PRESCRIPTION_ITEM" ("MedicineId");

                CREATE INDEX "IX_PATIENT_PRESCRIPTION_ITEM_PrescriptionId" ON "PATIENT_PRESCRIPTION_ITEM" ("PrescriptionId");

                CREATE INDEX "IX_PATIENT_PRESCRIPTION_RECEIPT_PrescriptionId" ON "PATIENT_PRESCRIPTION_RECEIPT" ("PrescriptionId");

                CREATE INDEX "IX_PRESCRIPTION_AUDIT_ActorId" ON "PRESCRIPTION_AUDIT" ("ActorId");

                CREATE UNIQUE INDEX "IX_PRESCRIPTION_TEMPLATE_Code" ON "PRESCRIPTION_TEMPLATE" ("Code");

                CREATE INDEX "IX_PRESCRIPTION_TEMPLATE_CreatedBy" ON "PRESCRIPTION_TEMPLATE" ("CreatedBy");

                CREATE INDEX "IX_PRESCRIPTION_TEMPLATE_UpdatedBy" ON "PRESCRIPTION_TEMPLATE" ("UpdatedBy");

                CREATE INDEX "IX_PRESCRIPTION_TEMPLATE_LINE_MedicineId" ON "PRESCRIPTION_TEMPLATE_LINE" ("MedicineId");

                CREATE INDEX "IX_PRESCRIPTION_TEMPLATE_LINE_TemplateId" ON "PRESCRIPTION_TEMPLATE_LINE" ("TemplateId");

                CREATE INDEX "IX_RECEPTION_RECEIPT_VisitId" ON "RECEPTION_RECEIPT" ("VisitId");

                CREATE INDEX "IX_RECOVERY_AUDIT_ActorId" ON "RECOVERY_AUDIT" ("ActorId");

                CREATE INDEX "IX_RECOVERY_AUDIT_TargetId_OccurredAt" ON "RECOVERY_AUDIT" ("TargetId", "OccurredAt");

                SELECT setval(pg_get_serial_sequence('"RECOVERY_AUDIT"', 'Id'), GREATEST(COALESCE((SELECT max("Id") FROM "RECOVERY_AUDIT"), 0), 1), EXISTS (SELECT 1 FROM "RECOVERY_AUDIT"));

                CREATE INDEX "IX_SETTINGS_AUDIT_ActorId" ON "SETTINGS_AUDIT" ("ActorId");

                CREATE INDEX "IX_SETTINGS_AUDIT_Section_At" ON "SETTINGS_AUDIT" ("Section", "At");

                CREATE UNIQUE INDEX "IX_STAFF_ACTIVITY_COMMAND_RECEIPT_SessionId_RequestKey" ON "STAFF_ACTIVITY_COMMAND_RECEIPT" ("SessionId", "RequestKey");

                CREATE INDEX "IX_SUPPORT_REQUEST_RECEIPT_SupportRequestId" ON "SUPPORT_REQUEST_RECEIPT" ("SupportRequestId");

                CREATE INDEX "IX_TREATMENT_PAYOS_ORDER_LINE_ActivityId" ON "TREATMENT_PAYOS_ORDER_LINE" ("ActivityId");

                CREATE INDEX "IX_TREATMENT_RECEIPT_DossierId" ON "TREATMENT_RECEIPT" ("DossierId");

                CREATE INDEX "IX_VISIT_AUDIT_ActorAccountId" ON "VISIT_AUDIT" ("ActorAccountId");

                CREATE INDEX "IX_VISIT_AUDIT_VisitId_OccurredAt" ON "VISIT_AUDIT" ("VisitId", "OccurredAt");

                CREATE INDEX "IX_VISIT_AUDIT_DecidingDentistId" ON "VISIT_AUDIT" ("DecidingDentistId");

                CREATE INDEX "IX_WORKING_PERIOD_WeekId" ON "WORKING_PERIOD" ("WeekId");

                CREATE INDEX "IX_WORKING_REVISION_PERIOD_BranchId" ON "WORKING_REVISION_PERIOD" ("BranchId");

                CREATE INDEX "IX_WORKING_REVISION_PERIOD_RevisionId" ON "WORKING_REVISION_PERIOD" ("RevisionId");

                ALTER TABLE "ACCESS_GROUP" DROP COLUMN "Grants";

                DROP INDEX "IX_SCHEDULING_BRANCH_Code";

                ALTER TABLE "SCHEDULING_BRANCH" DROP COLUMN "Version", DROP COLUMN "Code", DROP COLUMN "Address", DROP COLUMN "Phone", DROP COLUMN "Email", DROP COLUMN "Website", DROP COLUMN "LogoAssetId", DROP COLUMN "TimeZoneId", DROP COLUMN "BankCode", DROP COLUMN "AccountNumber", DROP COLUMN "PayeeName";

                DROP INDEX "IX_SCHEDULING_CHAIR_BranchId_Code";

                ALTER TABLE "SCHEDULING_CHAIR" DROP COLUMN "Version", DROP COLUMN "Code";

                ALTER TABLE "TREATMENT_PAYOS_ORDER" DROP COLUMN "Lines";

                ALTER TABLE "WORKING_SCHEDULE_REVISION" DROP COLUMN "Periods";

                ALTER TABLE "WORKING_WEEK" DROP COLUMN "Periods";

                DROP TABLE "COMMAND_RECEIPT", "AUDIT_LOG", "PRESCRIPTION";

                DROP SEQUENCE audit_log_number_seq;
            """);
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyRemainingNineTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "USER_ACCOUNT",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "AuthData",
                table: "USER_ACCOUNT",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "PATIENT_PROFILE",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "PATIENT_ACCOUNT_LINK",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "FAMILY_RECORD_ACCESS",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "CLINICAL_DOCUMENT",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "Data",
                table: "IN_APP_NOTIFICATION",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "IN_APP_NOTIFICATION",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "LAB_WORK_TYPE",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "CATALOG_PROVIDER",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "CARE_TASK",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.Sql("""
                UPDATE "USER_ACCOUNT"
                SET "FullName" = COALESCE(NULLIF("FullName", ''), "EmployeeFullName", ''),
                    "Version" = GREATEST("Version", COALESCE("AccessVersion", 1), 1),
                    "AuthData" = jsonb_build_object(
                        'SecurityStamp', "SecurityStamp",
                        'PhoneNumberConfirmed', "PhoneNumberConfirmed",
                        'TwoFactorEnabled', "TwoFactorEnabled",
                        'LockoutEnd', "LockoutEnd",
                        'LockoutEnabled', "LockoutEnabled",
                        'AccessFailedCount', "AccessFailedCount",
                        'Claims', COALESCE("Claims", '[]'::jsonb),
                        'Logins', CASE
                            WHEN "GoogleSubject" IS NOT NULL AND NOT EXISTS (
                                SELECT 1 FROM jsonb_array_elements(COALESCE("Logins", '[]'::jsonb)) l
                                WHERE l->>'Provider' = 'Google' AND l->>'Key' = "GoogleSubject"
                            ) THEN COALESCE("Logins", '[]'::jsonb) || jsonb_build_array(jsonb_build_object('Provider', 'Google', 'Key', "GoogleSubject", 'DisplayName', 'Google'))
                            ELSE COALESCE("Logins", '[]'::jsonb)
                        END,
                        'IdentityTokens', COALESCE("IdentityTokens", '[]'::jsonb),
                        'Sessions', COALESCE("Sessions", '[]'::jsonb),
                        'EmailVerifications', COALESCE("EmailVerifications", '[]'::jsonb),
                        'Recoveries', COALESCE("Recoveries", '[]'::jsonb),
                        'RecoveryDeliveries', COALESCE("RecoveryDeliveries", '[]'::jsonb)
                    );

                UPDATE "PATIENT_PROFILE"
                SET "Details" = jsonb_build_object(
                    'GuardianName', "GuardianName",
                    'GuardianRelationship', "GuardianRelationship",
                    'GuardianPhone', "GuardianPhone",
                    'Note', "Note",
                    'ProfileDate', to_char("ProfileDate", 'YYYY-MM-DD'),
                    'IntakeHistoryIds', to_jsonb(COALESCE("IntakeHistoryIds", '{}'::uuid[])),
                    'IntakeHistoryOther', "IntakeHistoryOther",
                    'IntakeRecordedAt', "IntakeRecordedAt",
                    'IntakeRecordedBy', "IntakeRecordedBy",
                    'ArchivedAt', "ArchivedAt",
                    'ArchiveReason', "ArchiveReason"
                );

                UPDATE "PATIENT_ACCOUNT_LINK"
                SET "Details" = jsonb_build_object(
                    'DeclaredBirthDate', to_char("DeclaredBirthDate", 'YYYY-MM-DD'),
                    'DeclaredPatientCode', "DeclaredPatientCode",
                    'RequestNote', "RequestNote",
                    'VerificationNote', "VerificationNote",
                    'DecisionReason', "DecisionReason",
                    'PublicNote', "PublicNote",
                    'ReviewedAt', "ReviewedAt",
                    'RevokedAt', "RevokedAt",
                    'WithdrawnAt', "WithdrawnAt"
                );
                ALTER TABLE "PATIENT_ACCOUNT_LINK" ALTER COLUMN "Details" DROP DEFAULT;

                UPDATE "FAMILY_RECORD_ACCESS"
                SET "Details" = jsonb_build_object(
                    'DeclaredFullName', COALESCE("DeclaredFullName", ''),
                    'DeclaredBirthDate', to_char("DeclaredBirthDate", 'YYYY-MM-DD'),
                    'Relationship', COALESCE("Relationship", ''),
                    'DeclaredPatientCode', "DeclaredPatientCode",
                    'MatchingInformation', "MatchingInformation",
                    'SharingBasis', "SharingBasis",
                    'VerificationNote', "VerificationNote",
                    'DecisionReason', "DecisionReason",
                    'PublicNote', "PublicNote",
                    'DecidedAt', "DecidedAt"
                );
                ALTER TABLE "FAMILY_RECORD_ACCESS" ALTER COLUMN "Details" DROP DEFAULT;

                UPDATE "CLINICAL_DOCUMENT"
                SET "Metadata" = jsonb_build_object(
                    'Source', COALESCE("Source", 'Clinic'),
                    'Description', "Description",
                    'SizeBytes', "SizeBytes",
                    'Sha256', COALESCE("Sha256", ''),
                    'RejectReason', "RejectReason",
                    'InvestigationId', "InvestigationId"
                );
                ALTER TABLE "CLINICAL_DOCUMENT" ALTER COLUMN "Metadata" DROP DEFAULT;

                UPDATE "IN_APP_NOTIFICATION"
                SET "Data" = jsonb_build_object(
                    'RequestId', "RequestId",
                    'DeepLink', "DeepLink",
                    'HasExternalFailure', COALESCE("HasExternalFailure", false),
                    'LastAttemptAt', NULL,
                    'PayloadJson', NULL,
                    'DispatchedAt', NULL
                )
                WHERE "NotificationRecordKind" = 'InAppNotification';

                UPDATE "IN_APP_NOTIFICATION"
                SET "Error" = COALESCE("Error", "LastError"),
                    "Data" = jsonb_build_object(
                        'RequestId', NULL,
                        'DeepLink', NULL,
                        'HasExternalFailure', false,
                        'LastAttemptAt', "LastAttemptAt",
                        'PayloadJson', NULL,
                        'DispatchedAt', NULL
                    )
                WHERE "NotificationRecordKind" = 'NotificationDelivery';

                UPDATE "IN_APP_NOTIFICATION"
                SET "AppointmentId" = COALESCE("AppointmentId", "AggregateId"),
                    "Kind" = COALESCE("Kind", "EventType"),
                    "CreatedAt" = COALESCE("CreatedAt", "OccurredAt"),
                    "Status" = CASE WHEN "DispatchedAt" IS NULL THEN 'Pending' ELSE 'Dispatched' END,
                    "Data" = jsonb_build_object(
                        'RequestId', NULL,
                        'DeepLink', NULL,
                        'HasExternalFailure', false,
                        'LastAttemptAt', NULL,
                        'PayloadJson', CASE WHEN "PayloadJson" IS NULL THEN NULL ELSE "PayloadJson"::text END,
                        'DispatchedAt', "DispatchedAt"
                    )
                WHERE "NotificationRecordKind" = 'AppointmentOutboxEvent';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_IN_APP_NOTIFICATION_APPOINTMENT_AggregateId",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "USER_ACCOUNT");

            migrationBuilder.DropIndex(
                name: "IX_USER_ACCOUNT_GoogleSubject",
                table: "USER_ACCOUNT");

            migrationBuilder.DropIndex(
                name: "IX_USER_ACCOUNT_NormalizedPhone",
                table: "USER_ACCOUNT");

            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                table: "USER_ACCOUNT");

            migrationBuilder.DropIndex(
                name: "IX_LAB_WORK_TYPE_NormalizedName",
                table: "LAB_WORK_TYPE");

            migrationBuilder.DropIndex(
                name: "IX_IN_APP_NOTIFICATION_AggregateId",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.DropIndex(
                name: "IX_IN_APP_NOTIFICATION_DispatchedAt_OccurredAt",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.DropIndex(
                name: "IX_CLINICAL_DOCUMENT_InvestigationId",
                table: "CLINICAL_DOCUMENT");

            migrationBuilder.DropIndex(
                name: "IX_CATALOG_PROVIDER_NormalizedName",
                table: "CATALOG_PROVIDER");

            migrationBuilder.DropIndex(
                name: "IX_CARE_TASK_EpisodeKey",
                table: "CARE_TASK");

            migrationBuilder.DropColumn(
                name: "NormalizedPhone",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "AccessFailedCount",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "AccessVersion",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "Claims",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "EmailVerifications",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "EmployeeFullName",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "GoogleSubject",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "IdentityTokens",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "LockoutEnabled",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "Logins",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "NormalizedUserName",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "PhoneNumberConfirmed",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "Recoveries",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "RecoveryDeliveries",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "Sessions",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "TwoFactorEnabled",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "GuardianName",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "GuardianPhone",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "GuardianRelationship",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "IntakeHistoryIds",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "IntakeHistoryOther",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "IntakeRecordedAt",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "IntakeRecordedBy",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "ProfileDate",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "DecisionReason",
                table: "PATIENT_ACCOUNT_LINK");

            migrationBuilder.DropColumn(
                name: "DeclaredBirthDate",
                table: "PATIENT_ACCOUNT_LINK");

            migrationBuilder.DropColumn(
                name: "DeclaredPatientCode",
                table: "PATIENT_ACCOUNT_LINK");

            migrationBuilder.DropColumn(
                name: "PublicNote",
                table: "PATIENT_ACCOUNT_LINK");

            migrationBuilder.DropColumn(
                name: "RequestNote",
                table: "PATIENT_ACCOUNT_LINK");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "PATIENT_ACCOUNT_LINK");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "PATIENT_ACCOUNT_LINK");

            migrationBuilder.DropColumn(
                name: "VerificationNote",
                table: "PATIENT_ACCOUNT_LINK");

            migrationBuilder.DropColumn(
                name: "WithdrawnAt",
                table: "PATIENT_ACCOUNT_LINK");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "LAB_WORK_TYPE");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "LAB_WORK_TYPE");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "LAB_WORK_TYPE");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "LAB_WORK_TYPE");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "LAB_WORK_TYPE");

            migrationBuilder.DropColumn(
                name: "AggregateId",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.DropColumn(
                name: "DeepLink",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.DropColumn(
                name: "DispatchedAt",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.DropColumn(
                name: "HasExternalFailure",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.DropColumn(
                name: "OccurredAt",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.DropColumn(
                name: "PayloadJson",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                table: "FAMILY_RECORD_ACCESS");

            migrationBuilder.DropColumn(
                name: "DecisionReason",
                table: "FAMILY_RECORD_ACCESS");

            migrationBuilder.DropColumn(
                name: "DeclaredBirthDate",
                table: "FAMILY_RECORD_ACCESS");

            migrationBuilder.DropColumn(
                name: "DeclaredFullName",
                table: "FAMILY_RECORD_ACCESS");

            migrationBuilder.DropColumn(
                name: "DeclaredPatientCode",
                table: "FAMILY_RECORD_ACCESS");

            migrationBuilder.DropColumn(
                name: "MatchingInformation",
                table: "FAMILY_RECORD_ACCESS");

            migrationBuilder.DropColumn(
                name: "PublicNote",
                table: "FAMILY_RECORD_ACCESS");

            migrationBuilder.DropColumn(
                name: "Relationship",
                table: "FAMILY_RECORD_ACCESS");

            migrationBuilder.DropColumn(
                name: "SharingBasis",
                table: "FAMILY_RECORD_ACCESS");

            migrationBuilder.DropColumn(
                name: "VerificationNote",
                table: "FAMILY_RECORD_ACCESS");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "CLINICAL_DOCUMENT");

            migrationBuilder.DropColumn(
                name: "InvestigationId",
                table: "CLINICAL_DOCUMENT");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "CLINICAL_DOCUMENT");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "CLINICAL_DOCUMENT");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "CLINICAL_DOCUMENT");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "CLINICAL_DOCUMENT");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CLINICAL_DOCUMENT");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CATALOG_PROVIDER");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CATALOG_PROVIDER");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "CATALOG_PROVIDER");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CATALOG_PROVIDER");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "CATALOG_PROVIDER");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CARE_TASK");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CARE_TASK");

            migrationBuilder.DropColumn(
                name: "EpisodeKey",
                table: "CARE_TASK");

            migrationBuilder.DropColumn(
                name: "Escalations",
                table: "CARE_TASK");

            migrationBuilder.DropColumn(
                name: "StorageStamp",
                table: "CARE_TASK");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CARE_TASK");

            migrationBuilder.RenameIndex(
                name: "IX_IN_APP_NOTIFICATION_Status_CreatedAt",
                table: "IN_APP_NOTIFICATION",
                newName: "IX_IN_APP_NOTIFICATION_Status_CreatedAt1");

            migrationBuilder.CreateIndex(
                name: "IX_IN_APP_NOTIFICATION_Status_CreatedAt",
                table: "IN_APP_NOTIFICATION",
                columns: new[] { "Status", "CreatedAt" },
                filter: "\"NotificationRecordKind\" = 'AppointmentOutboxEvent'");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UserNameIndex" ON "USER_ACCOUNT" (UPPER("UserName")) WHERE "UserName" IS NOT NULL;
                CREATE UNIQUE INDEX "EmailIndex" ON "USER_ACCOUNT" (UPPER("Email")) WHERE "Email" IS NOT NULL;
                CREATE UNIQUE INDEX "IX_USER_ACCOUNT_NormalizedPhone" ON "USER_ACCOUNT" ((NULLIF(regexp_replace(btrim("PhoneNumber"), '[ ()-]', '', 'g'), ''))) WHERE NULLIF(regexp_replace(btrim("PhoneNumber"), '[ ()-]', '', 'g'), '') IS NOT NULL;
                CREATE UNIQUE INDEX "IX_CATALOG_PROVIDER_Name_Upper" ON "CATALOG_PROVIDER" (UPPER("Name"));
                CREATE UNIQUE INDEX "IX_LAB_WORK_TYPE_Name_Upper" ON "LAB_WORK_TYPE" (UPPER("Name"));
                CREATE UNIQUE INDEX "UX_CARE_TASK_MissedAppointment" ON "CARE_TASK" ("AppointmentId") WHERE "AppointmentId" IS NOT NULL AND "Type" = 'MissedAppointment';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "UX_CARE_TASK_MissedAppointment";
                DROP INDEX IF EXISTS "IX_LAB_WORK_TYPE_Name_Upper";
                DROP INDEX IF EXISTS "IX_CATALOG_PROVIDER_Name_Upper";
                DROP INDEX IF EXISTS "IX_USER_ACCOUNT_NormalizedPhone";
                DROP INDEX IF EXISTS "EmailIndex";
                DROP INDEX IF EXISTS "UserNameIndex";
                """);

            migrationBuilder.DropIndex(
                name: "IX_IN_APP_NOTIFICATION_Status_CreatedAt",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.RenameIndex(
                name: "IX_IN_APP_NOTIFICATION_Status_CreatedAt1",
                table: "IN_APP_NOTIFICATION",
                newName: "IX_IN_APP_NOTIFICATION_Status_CreatedAt");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "USER_ACCOUNT");

            migrationBuilder.AddColumn<int>(
                name: "AccessFailedCount",
                table: "USER_ACCOUNT",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AccessVersion",
                table: "USER_ACCOUNT",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Claims",
                table: "USER_ACCOUNT",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "USER_ACCOUNT",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailVerifications",
                table: "USER_ACCOUNT",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeFullName",
                table: "USER_ACCOUNT",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleSubject",
                table: "USER_ACCOUNT",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentityTokens",
                table: "USER_ACCOUNT",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<bool>(
                name: "LockoutEnabled",
                table: "USER_ACCOUNT",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockoutEnd",
                table: "USER_ACCOUNT",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Logins",
                table: "USER_ACCOUNT",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "USER_ACCOUNT",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedUserName",
                table: "USER_ACCOUNT",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PhoneNumberConfirmed",
                table: "USER_ACCOUNT",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Recoveries",
                table: "USER_ACCOUNT",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "RecoveryDeliveries",
                table: "USER_ACCOUNT",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "USER_ACCOUNT",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sessions",
                table: "USER_ACCOUNT",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorEnabled",
                table: "USER_ACCOUNT",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "PATIENT_PROFILE",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "PATIENT_PROFILE",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianName",
                table: "PATIENT_PROFILE",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianPhone",
                table: "PATIENT_PROFILE",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianRelationship",
                table: "PATIENT_PROFILE",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid[]>(
                name: "IntakeHistoryIds",
                table: "PATIENT_PROFILE",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");

            migrationBuilder.AddColumn<string>(
                name: "IntakeHistoryOther",
                table: "PATIENT_PROFILE",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IntakeRecordedAt",
                table: "PATIENT_PROFILE",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntakeRecordedBy",
                table: "PATIENT_PROFILE",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "PATIENT_PROFILE",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ProfileDate",
                table: "PATIENT_PROFILE",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionReason",
                table: "PATIENT_ACCOUNT_LINK",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DeclaredBirthDate",
                table: "PATIENT_ACCOUNT_LINK",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeclaredPatientCode",
                table: "PATIENT_ACCOUNT_LINK",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicNote",
                table: "PATIENT_ACCOUNT_LINK",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestNote",
                table: "PATIENT_ACCOUNT_LINK",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "PATIENT_ACCOUNT_LINK",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "PATIENT_ACCOUNT_LINK",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationNote",
                table: "PATIENT_ACCOUNT_LINK",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WithdrawnAt",
                table: "PATIENT_ACCOUNT_LINK",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "LAB_WORK_TYPE",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "LAB_WORK_TYPE",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "LAB_WORK_TYPE",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "LAB_WORK_TYPE",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "LAB_WORK_TYPE",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AggregateId",
                table: "IN_APP_NOTIFICATION",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeepLink",
                table: "IN_APP_NOTIFICATION",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DispatchedAt",
                table: "IN_APP_NOTIFICATION",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "IN_APP_NOTIFICATION",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasExternalFailure",
                table: "IN_APP_NOTIFICATION",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAt",
                table: "IN_APP_NOTIFICATION",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "IN_APP_NOTIFICATION",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OccurredAt",
                table: "IN_APP_NOTIFICATION",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayloadJson",
                table: "IN_APP_NOTIFICATION",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                table: "IN_APP_NOTIFICATION",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecidedAt",
                table: "FAMILY_RECORD_ACCESS",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionReason",
                table: "FAMILY_RECORD_ACCESS",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DeclaredBirthDate",
                table: "FAMILY_RECORD_ACCESS",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeclaredFullName",
                table: "FAMILY_RECORD_ACCESS",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeclaredPatientCode",
                table: "FAMILY_RECORD_ACCESS",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchingInformation",
                table: "FAMILY_RECORD_ACCESS",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicNote",
                table: "FAMILY_RECORD_ACCESS",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Relationship",
                table: "FAMILY_RECORD_ACCESS",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SharingBasis",
                table: "FAMILY_RECORD_ACCESS",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationNote",
                table: "FAMILY_RECORD_ACCESS",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "CLINICAL_DOCUMENT",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvestigationId",
                table: "CLINICAL_DOCUMENT",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                table: "CLINICAL_DOCUMENT",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "CLINICAL_DOCUMENT",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "CLINICAL_DOCUMENT",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "CLINICAL_DOCUMENT",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CLINICAL_DOCUMENT",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "CATALOG_PROVIDER",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "CATALOG_PROVIDER",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "CATALOG_PROVIDER",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "CATALOG_PROVIDER",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "CATALOG_PROVIDER",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "CARE_TASK",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "CARE_TASK",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EpisodeKey",
                table: "CARE_TASK",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Escalations",
                table: "CARE_TASK",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<Guid>(
                name: "StorageStamp",
                table: "CARE_TASK",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "CARE_TASK",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.Sql("""
                UPDATE "USER_ACCOUNT"
                SET "NormalizedUserName" = UPPER("UserName"),
                    "NormalizedEmail" = UPPER("Email"),
                    "EmployeeFullName" = CASE WHEN "EmployeeCode" IS NOT NULL THEN "FullName" ELSE NULL END,
                    "AccessVersion" = CASE WHEN "GroupId" IS NOT NULL THEN "Version" ELSE NULL END,
                    "SecurityStamp" = "AuthData"->>'SecurityStamp',
                    "ConcurrencyStamp" = gen_random_uuid()::text,
                    "PhoneNumberConfirmed" = COALESCE(("AuthData"->>'PhoneNumberConfirmed')::boolean, false),
                    "TwoFactorEnabled" = COALESCE(("AuthData"->>'TwoFactorEnabled')::boolean, false),
                    "LockoutEnd" = ("AuthData"->>'LockoutEnd')::timestamptz,
                    "LockoutEnabled" = COALESCE(("AuthData"->>'LockoutEnabled')::boolean, false),
                    "AccessFailedCount" = COALESCE(("AuthData"->>'AccessFailedCount')::integer, 0),
                    "Claims" = COALESCE("AuthData"->'Claims', '[]'::jsonb),
                    "Logins" = COALESCE("AuthData"->'Logins', '[]'::jsonb),
                    "IdentityTokens" = COALESCE("AuthData"->'IdentityTokens', '[]'::jsonb),
                    "Sessions" = COALESCE("AuthData"->'Sessions', '[]'::jsonb),
                    "EmailVerifications" = COALESCE("AuthData"->'EmailVerifications', '[]'::jsonb),
                    "Recoveries" = COALESCE("AuthData"->'Recoveries', '[]'::jsonb),
                    "RecoveryDeliveries" = COALESCE("AuthData"->'RecoveryDeliveries', '[]'::jsonb),
                    "GoogleSubject" = (SELECT l->>'Key' FROM jsonb_array_elements(COALESCE("AuthData"->'Logins', '[]'::jsonb)) l WHERE l->>'Provider' = 'Google' LIMIT 1);

                UPDATE "PATIENT_PROFILE"
                SET "GuardianName" = "Details"->>'GuardianName',
                    "GuardianRelationship" = "Details"->>'GuardianRelationship',
                    "GuardianPhone" = "Details"->>'GuardianPhone',
                    "Note" = "Details"->>'Note',
                    "ProfileDate" = NULLIF("Details"->>'ProfileDate', '')::date,
                    "IntakeHistoryIds" = COALESCE(ARRAY(SELECT jsonb_array_elements_text(COALESCE("Details"->'IntakeHistoryIds', '[]'::jsonb))::uuid), '{}'::uuid[]),
                    "IntakeHistoryOther" = "Details"->>'IntakeHistoryOther',
                    "IntakeRecordedAt" = ("Details"->>'IntakeRecordedAt')::timestamptz,
                    "IntakeRecordedBy" = ("Details"->>'IntakeRecordedBy')::integer,
                    "ArchivedAt" = ("Details"->>'ArchivedAt')::timestamptz,
                    "ArchiveReason" = "Details"->>'ArchiveReason';

                UPDATE "PATIENT_ACCOUNT_LINK"
                SET "DeclaredBirthDate" = NULLIF("Details"->>'DeclaredBirthDate', '')::date,
                    "DeclaredPatientCode" = "Details"->>'DeclaredPatientCode',
                    "RequestNote" = "Details"->>'RequestNote',
                    "VerificationNote" = "Details"->>'VerificationNote',
                    "DecisionReason" = "Details"->>'DecisionReason',
                    "PublicNote" = "Details"->>'PublicNote',
                    "ReviewedAt" = ("Details"->>'ReviewedAt')::timestamptz,
                    "RevokedAt" = ("Details"->>'RevokedAt')::timestamptz,
                    "WithdrawnAt" = ("Details"->>'WithdrawnAt')::timestamptz;

                UPDATE "FAMILY_RECORD_ACCESS"
                SET "DeclaredFullName" = COALESCE("Details"->>'DeclaredFullName', ''),
                    "DeclaredBirthDate" = NULLIF("Details"->>'DeclaredBirthDate', '')::date,
                    "Relationship" = COALESCE("Details"->>'Relationship', ''),
                    "DeclaredPatientCode" = "Details"->>'DeclaredPatientCode',
                    "MatchingInformation" = "Details"->>'MatchingInformation',
                    "SharingBasis" = "Details"->>'SharingBasis',
                    "VerificationNote" = "Details"->>'VerificationNote',
                    "DecisionReason" = "Details"->>'DecisionReason',
                    "PublicNote" = "Details"->>'PublicNote',
                    "DecidedAt" = ("Details"->>'DecidedAt')::timestamptz;

                UPDATE "CLINICAL_DOCUMENT"
                SET "Source" = COALESCE("Metadata"->>'Source', 'Clinic'),
                    "Description" = "Metadata"->>'Description',
                    "SizeBytes" = COALESCE(("Metadata"->>'SizeBytes')::bigint, 0),
                    "Sha256" = COALESCE("Metadata"->>'Sha256', ''),
                    "RejectReason" = "Metadata"->>'RejectReason',
                    "InvestigationId" = ("Metadata"->>'InvestigationId')::uuid;

                UPDATE "IN_APP_NOTIFICATION"
                SET "RequestId" = ("Data"->>'RequestId')::uuid,
                    "DeepLink" = "Data"->>'DeepLink',
                    "HasExternalFailure" = COALESCE(("Data"->>'HasExternalFailure')::boolean, false)
                WHERE "NotificationRecordKind" = 'InAppNotification';

                UPDATE "IN_APP_NOTIFICATION"
                SET "LastAttemptAt" = ("Data"->>'LastAttemptAt')::timestamptz,
                    "LastError" = "Error",
                    "Error" = NULL
                WHERE "NotificationRecordKind" = 'NotificationDelivery';

                UPDATE "IN_APP_NOTIFICATION"
                SET "AggregateId" = "AppointmentId",
                    "EventType" = COALESCE("Kind", ''),
                    "OccurredAt" = COALESCE("CreatedAt", now()),
                    "DispatchedAt" = ("Data"->>'DispatchedAt')::timestamptz,
                    "PayloadJson" = CASE WHEN NULLIF("Data"->>'PayloadJson', '') IS NULL THEN '{}'::jsonb ELSE ("Data"->>'PayloadJson')::jsonb END,
                    "AppointmentId" = NULL,
                    "Kind" = NULL,
                    "CreatedAt" = NULL,
                    "Status" = NULL
                WHERE "NotificationRecordKind" = 'AppointmentOutboxEvent';

                UPDATE "CATALOG_PROVIDER"
                SET "NormalizedName" = UPPER("Name"),
                    "CreatedAt" = now(),
                    "UpdatedAt" = now(),
                    "CreatedBy" = COALESCE((SELECT MIN("Id") FROM "USER_ACCOUNT"), 1),
                    "UpdatedBy" = COALESCE((SELECT MIN("Id") FROM "USER_ACCOUNT"), 1);

                UPDATE "LAB_WORK_TYPE"
                SET "NormalizedName" = UPPER("Name"),
                    "CreatedAt" = now(),
                    "UpdatedAt" = now(),
                    "CreatedBy" = COALESCE((SELECT MIN("Id") FROM "USER_ACCOUNT"), 1),
                    "UpdatedBy" = COALESCE((SELECT MIN("Id") FROM "USER_ACCOUNT"), 1);

                UPDATE "CARE_TASK"
                SET "CreatedBy" = COALESCE("OwnerAccountId", (SELECT MIN("Id") FROM "USER_ACCOUNT"), 1),
                    "CreatedAt" = "DueAt",
                    "UpdatedAt" = "DueAt",
                    "EpisodeKey" = CASE WHEN "AppointmentId" IS NOT NULL AND "Type" = 'MissedAppointment' THEN 'missed:' || "AppointmentId"::text ELSE NULL END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "IN_APP_NOTIFICATION",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "LAB_WORK_TYPE",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "CATALOG_PROVIDER",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "CARE_TASK",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.DropColumn(
                name: "AuthData",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "PATIENT_ACCOUNT_LINK");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "FAMILY_RECORD_ACCESS");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "CLINICAL_DOCUMENT");

            migrationBuilder.DropColumn(
                name: "Data",
                table: "IN_APP_NOTIFICATION");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhone",
                table: "USER_ACCOUNT",
                type: "text",
                nullable: true,
                computedColumnSql: "NULLIF(regexp_replace(btrim(\"PhoneNumber\"), '[ ()-]', '', 'g'), '')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "USER_ACCOUNT",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USER_ACCOUNT_GoogleSubject",
                table: "USER_ACCOUNT",
                column: "GoogleSubject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USER_ACCOUNT_NormalizedPhone",
                table: "USER_ACCOUNT",
                column: "NormalizedPhone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "USER_ACCOUNT",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LAB_WORK_TYPE_NormalizedName",
                table: "LAB_WORK_TYPE",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IN_APP_NOTIFICATION_AggregateId",
                table: "IN_APP_NOTIFICATION",
                column: "AggregateId");

            migrationBuilder.CreateIndex(
                name: "IX_IN_APP_NOTIFICATION_DispatchedAt_OccurredAt",
                table: "IN_APP_NOTIFICATION",
                columns: new[] { "DispatchedAt", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_DOCUMENT_InvestigationId",
                table: "CLINICAL_DOCUMENT",
                column: "InvestigationId");

            migrationBuilder.CreateIndex(
                name: "IX_CATALOG_PROVIDER_NormalizedName",
                table: "CATALOG_PROVIDER",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CARE_TASK_EpisodeKey",
                table: "CARE_TASK",
                column: "EpisodeKey",
                unique: true,
                filter: "\"EpisodeKey\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_IN_APP_NOTIFICATION_APPOINTMENT_AggregateId",
                table: "IN_APP_NOTIFICATION",
                column: "AggregateId",
                principalTable: "APPOINTMENT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

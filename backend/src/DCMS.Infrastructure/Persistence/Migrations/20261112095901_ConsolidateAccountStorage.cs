using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations;

public partial class ConsolidateAccountStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE SEQUENCE account_entry_seq AS bigint;
            
            CREATE SEQUENCE employee_id_seq AS integer;
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "EmployeeId" integer;
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "AssignedRoles" text[] NOT NULL DEFAULT ARRAY[]::text[];
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "GoogleSubject" text;
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "EmployeeCode" character varying(40);
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "EmployeeFullName" character varying(160);
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "GroupId" uuid;
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "BranchIds" integer[];
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "AccessVersion" integer;
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "Claims" jsonb NOT NULL DEFAULT '[]'::jsonb;
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "EmailVerifications" jsonb NOT NULL DEFAULT '[]'::jsonb;
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "IdentityTokens" jsonb NOT NULL DEFAULT '[]'::jsonb;
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "Logins" jsonb NOT NULL DEFAULT '[]'::jsonb;
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "Recoveries" jsonb NOT NULL DEFAULT '[]'::jsonb;
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "RecoveryDeliveries" jsonb NOT NULL DEFAULT '[]'::jsonb;
            
            ALTER TABLE "USER_ACCOUNT" ADD COLUMN "Sessions" jsonb NOT NULL DEFAULT '[]'::jsonb;
            
            ALTER TABLE "ACCESS_GROUP" ADD COLUMN "RecordKind" character varying(24) NOT NULL DEFAULT 'PermissionGroup';
            
            ALTER TABLE "ACCESS_GROUP" ADD COLUMN "IdentityRoleId" integer;
            
            ALTER TABLE "ACCESS_GROUP" ADD COLUMN "RoleConcurrencyStamp" text;
            
            ALTER TABLE "ACCESS_GROUP" ADD COLUMN "RoleClaims" jsonb NOT NULL DEFAULT '[]'::jsonb;
            
            UPDATE "USER_ACCOUNT" u SET "EmployeeId"=e."Id", "EmployeeCode"=e."EmployeeCode", "EmployeeFullName"=e."FullName" FROM "EMPLOYEE_PROFILE" e WHERE e."AccountId"=u."Id";
            
            UPDATE "USER_ACCOUNT" u SET "GroupId"=a."GroupId", "BranchIds"=a."BranchIds", "AccessVersion"=a."Version" FROM "STAFF_ACCESS_ASSIGNMENT" a WHERE a."AccountId"=u."Id";
            
            UPDATE "USER_ACCOUNT" u SET "AssignedRoles"=ARRAY(SELECT r."Name" FROM "USER_ACCOUNT_ROLE" ur JOIN "ROLE" r ON r."Id"=ur."RoleId" WHERE ur."UserId"=u."Id" ORDER BY r."Name");
            
            UPDATE "USER_ACCOUNT" u SET "Sessions"=COALESCE((SELECT jsonb_agg(to_jsonb(s) || jsonb_build_object('RefreshTokens',COALESCE((SELECT jsonb_agg(to_jsonb(r) ORDER BY r."Id") FROM "AUTH_REFRESH_TOKEN" r WHERE r."SessionId"=s."Id"),'[]'::jsonb)) ORDER BY s."Id") FROM "AUTH_SESSION" s WHERE s."AccountId"=u."Id"),'[]'::jsonb);
            
            UPDATE "USER_ACCOUNT" u SET "Claims"=COALESCE((SELECT jsonb_agg(jsonb_build_object('Id',s."Id",'Type',s."ClaimType",'Value',s."ClaimValue") ORDER BY s."Id") FROM "AspNetUserClaims" s WHERE s."UserId"=u."Id"),'[]'::jsonb);
            
            UPDATE "USER_ACCOUNT" u SET "Logins"=COALESCE((SELECT jsonb_agg(jsonb_build_object('Provider',s."LoginProvider",'Key',s."ProviderKey",'DisplayName',s."ProviderDisplayName") ORDER BY s."LoginProvider",s."ProviderKey") FROM "AspNetUserLogins" s WHERE s."UserId"=u."Id"),'[]'::jsonb);
            
            UPDATE "USER_ACCOUNT" u SET "IdentityTokens"=COALESCE((SELECT jsonb_agg(jsonb_build_object('Provider',s."LoginProvider",'Name',s."Name",'Value',s."Value") ORDER BY s."LoginProvider",s."Name") FROM "AspNetUserTokens" s WHERE s."UserId"=u."Id"),'[]'::jsonb);
            
            UPDATE "USER_ACCOUNT" u SET "Recoveries"=COALESCE((SELECT jsonb_agg(to_jsonb(s) ORDER BY s."Id") FROM "RECOVERY_TOKEN" s WHERE s."AccountId"=u."Id"),'[]'::jsonb);
            
            UPDATE "USER_ACCOUNT" u SET "EmailVerifications"=COALESCE((SELECT jsonb_agg(to_jsonb(s) ORDER BY s."Id") FROM "EMAIL_VERIFICATION_TOKEN" s WHERE s."AccountId"=u."Id"),'[]'::jsonb);
            
            UPDATE "USER_ACCOUNT" u SET "RecoveryDeliveries"=COALESCE((SELECT jsonb_agg(to_jsonb(s) ORDER BY s."Id") FROM "RECOVERY_OUTBOX" s WHERE s."AccountId"=u."Id"),'[]'::jsonb);
            
            UPDATE "USER_ACCOUNT" SET "GoogleSubject"=(SELECT value->>'Key' FROM jsonb_array_elements("Logins") WHERE value->>'Provider'='Google');
            
            INSERT INTO "ACCESS_GROUP" ("Id","RecordKind","IdentityRoleId","RoleConcurrencyStamp","Code","Name","NormalizedCode","NormalizedName","BaseRole","Active","Version","Grants","RoleClaims")
            SELECT gen_random_uuid(),'IdentityRole',r."Id",r."ConcurrencyStamp",r."Name",r."Name",r."NormalizedName",r."NormalizedName",r."Name",TRUE,1,'[]'::jsonb,
            COALESCE((SELECT jsonb_agg(jsonb_build_object('Id',c."Id",'Type',c."ClaimType",'Value',c."ClaimValue") ORDER BY c."Id") FROM "AspNetRoleClaims" c WHERE c."RoleId"=r."Id"),'[]'::jsonb) FROM "ROLE" r;
            
            DO $check$ BEGIN
            IF (SELECT count(*) FROM "AUTH_SESSION")<>(SELECT COALESCE(sum(jsonb_array_length("Sessions")),0) FROM "USER_ACCOUNT") THEN RAISE EXCEPTION 'Incomplete AUTH_SESSION transfer'; END IF;
            IF (SELECT count(*) FROM "RECOVERY_TOKEN")<>(SELECT COALESCE(sum(jsonb_array_length("Recoveries")),0) FROM "USER_ACCOUNT") THEN RAISE EXCEPTION 'Incomplete RECOVERY_TOKEN transfer'; END IF;
            IF (SELECT count(*) FROM "RECOVERY_OUTBOX")<>(SELECT COALESCE(sum(jsonb_array_length("RecoveryDeliveries")),0) FROM "USER_ACCOUNT") THEN RAISE EXCEPTION 'Incomplete RECOVERY_OUTBOX transfer'; END IF;
            IF (SELECT count(*) FROM "EMAIL_VERIFICATION_TOKEN")<>(SELECT COALESCE(sum(jsonb_array_length("EmailVerifications")),0) FROM "USER_ACCOUNT") THEN RAISE EXCEPTION 'Incomplete EMAIL_VERIFICATION_TOKEN transfer'; END IF;
            IF (SELECT count(*) FROM "AspNetUserClaims")<>(SELECT COALESCE(sum(jsonb_array_length("Claims")),0) FROM "USER_ACCOUNT") THEN RAISE EXCEPTION 'Incomplete AspNetUserClaims transfer'; END IF;
            IF (SELECT count(*) FROM "AspNetUserLogins")<>(SELECT COALESCE(sum(jsonb_array_length("Logins")),0) FROM "USER_ACCOUNT") THEN RAISE EXCEPTION 'Incomplete AspNetUserLogins transfer'; END IF;
            IF (SELECT count(*) FROM "AspNetUserTokens")<>(SELECT COALESCE(sum(jsonb_array_length("IdentityTokens")),0) FROM "USER_ACCOUNT") THEN RAISE EXCEPTION 'Incomplete AspNetUserTokens transfer'; END IF;
            IF (SELECT count(*) FROM "AUTH_REFRESH_TOKEN")<>(SELECT count(*) FROM "USER_ACCOUNT" u CROSS JOIN LATERAL jsonb_array_elements(u."Sessions") s CROSS JOIN LATERAL jsonb_array_elements(s->'RefreshTokens') r) THEN RAISE EXCEPTION 'Incomplete refresh transfer'; END IF;
            IF (SELECT count(*) FROM "USER_ACCOUNT_ROLE")<>(SELECT COALESCE(sum(cardinality("AssignedRoles")),0) FROM "USER_ACCOUNT") THEN RAISE EXCEPTION 'Incomplete role transfer'; END IF;
            END $check$;
            
            ALTER TABLE "PATIENT_ACCOUNT_LINK" DROP CONSTRAINT "FK_PATIENT_ACCOUNT_LINK_EMPLOYEE_PROFILE_ReviewingEmployeeId";
            
            ALTER TABLE "PATIENT_ACCOUNT_LINK" DROP CONSTRAINT "FK_PATIENT_ACCOUNT_LINK_EMPLOYEE_PROFILE_RevokingEmployeeId";
            
            ALTER TABLE "SCHEDULING_MEMBER" DROP CONSTRAINT "FK_SCHEDULING_MEMBER_EMPLOYEE_PROFILE_EmployeeId";
            
            ALTER TABLE "TREATMENT_TEMPLATE_ITEM" DROP CONSTRAINT "FK_TREATMENT_TEMPLATE_ITEM_EMPLOYEE_PROFILE_AssistantId";
            
            ALTER TABLE "TREATMENT_TEMPLATE_ITEM" DROP CONSTRAINT "FK_TREATMENT_TEMPLATE_ITEM_EMPLOYEE_PROFILE_DentistId";
            
            ALTER TABLE "WORKING_SCHEDULE_REVISION" DROP CONSTRAINT "FK_WORKING_SCHEDULE_REVISION_EMPLOYEE_PROFILE_EmployeeId";
            
            ALTER TABLE "RESOURCE_RESERVATION" DROP CONSTRAINT "FK_RESOURCE_RESERVATION_EMPLOYEE_PROFILE_AssistantId";
            
            ALTER TABLE "VISIT" DROP CONSTRAINT "FK_VISIT_EMPLOYEE_PROFILE_DentistId";
            
            ALTER TABLE "PRESCRIPTION" DROP CONSTRAINT "FK_PRESCRIPTION_EMPLOYEE_PROFILE_PrescriberEmployeeId";
            
            ALTER TABLE "COMMAND_RECEIPT" DROP CONSTRAINT "FK_COMMAND_RECEIPT_AUTH_SESSION_SessionId";
            
            ALTER TABLE "AUDIT_LOG" DROP CONSTRAINT "FK_AUDIT_LOG_EMPLOYEE_PROFILE_DecidingDentistId";
            
            ALTER TABLE "USER_ACCOUNT" ADD CONSTRAINT "AK_USER_ACCOUNT_EmployeeId" UNIQUE ("EmployeeId");
            
            ALTER TABLE "USER_ACCOUNT" ADD CONSTRAINT "FK_USER_ACCOUNT_ACCESS_GROUP_GroupId" FOREIGN KEY ("GroupId") REFERENCES "ACCESS_GROUP" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "PATIENT_ACCOUNT_LINK" ADD CONSTRAINT "FK_PATIENT_ACCOUNT_LINK_USER_ACCOUNT_ReviewingEmployeeId" FOREIGN KEY ("ReviewingEmployeeId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT;
            
            ALTER TABLE "PATIENT_ACCOUNT_LINK" ADD CONSTRAINT "FK_PATIENT_ACCOUNT_LINK_USER_ACCOUNT_RevokingEmployeeId" FOREIGN KEY ("RevokingEmployeeId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT;
            
            ALTER TABLE "SCHEDULING_MEMBER" ADD CONSTRAINT "FK_SCHEDULING_MEMBER_USER_ACCOUNT_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT;
            
            ALTER TABLE "TREATMENT_TEMPLATE_ITEM" ADD CONSTRAINT "FK_TREATMENT_TEMPLATE_ITEM_USER_ACCOUNT_AssistantId" FOREIGN KEY ("AssistantId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT;
            
            ALTER TABLE "TREATMENT_TEMPLATE_ITEM" ADD CONSTRAINT "FK_TREATMENT_TEMPLATE_ITEM_USER_ACCOUNT_DentistId" FOREIGN KEY ("DentistId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT;
            
            ALTER TABLE "WORKING_SCHEDULE_REVISION" ADD CONSTRAINT "FK_WORKING_SCHEDULE_REVISION_USER_ACCOUNT_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT;
            
            ALTER TABLE "RESOURCE_RESERVATION" ADD CONSTRAINT "FK_RESOURCE_RESERVATION_USER_ACCOUNT_AssistantId" FOREIGN KEY ("AssistantId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT;
            
            ALTER TABLE "VISIT" ADD CONSTRAINT "FK_VISIT_USER_ACCOUNT_DentistId" FOREIGN KEY ("DentistId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT;
            
            ALTER TABLE "PRESCRIPTION" ADD CONSTRAINT "FK_PRESCRIPTION_USER_ACCOUNT_PrescriberEmployeeId" FOREIGN KEY ("PrescriberEmployeeId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT;
            
            ALTER TABLE "AUDIT_LOG" ADD CONSTRAINT "FK_AUDIT_LOG_USER_ACCOUNT_DecidingDentistId" FOREIGN KEY ("DecidingDentistId") REFERENCES "USER_ACCOUNT" ("EmployeeId") ON DELETE RESTRICT;
            
            DROP INDEX "IX_EMPLOYEE_PROFILE_EmployeeCode";
            
            CREATE UNIQUE INDEX "IX_EMPLOYEE_PROFILE_EmployeeCode" ON "USER_ACCOUNT" ("EmployeeCode");
            
            CREATE INDEX "IX_USER_ACCOUNT_GroupId" ON "USER_ACCOUNT" ("GroupId");
            
            CREATE UNIQUE INDEX "IX_USER_ACCOUNT_GoogleSubject" ON "USER_ACCOUNT" ("GoogleSubject");
            
            DROP INDEX "IX_ACCESS_GROUP_NormalizedCode";
            
            DROP INDEX "IX_ACCESS_GROUP_NormalizedName";
            
            CREATE UNIQUE INDEX "IX_ACCESS_GROUP_RecordKind_NormalizedCode" ON "ACCESS_GROUP" ("RecordKind", "NormalizedCode");
            
            CREATE UNIQUE INDEX "IX_ACCESS_GROUP_IdentityRoleId" ON "ACCESS_GROUP" ("IdentityRoleId");
            
            CREATE UNIQUE INDEX "IX_ACCESS_GROUP_RecordKind_NormalizedName" ON "ACCESS_GROUP" ("RecordKind", "NormalizedName");
            
            SELECT setval('employee_id_seq', GREATEST(COALESCE((SELECT max("EmployeeId") FROM "USER_ACCOUNT"),0),1),true);
            
            SELECT setval('account_entry_seq',GREATEST(1,(SELECT COALESCE(max("Id"),0) FROM "AUTH_REFRESH_TOKEN"),(SELECT COALESCE(max("Id"),0) FROM "RECOVERY_TOKEN"),(SELECT COALESCE(max("Id"),0) FROM "EMAIL_VERIFICATION_TOKEN"),(SELECT COALESCE(max("Id"),0) FROM "RECOVERY_OUTBOX"),(SELECT COALESCE(max("Id"),0) FROM "ROLE"),(SELECT COALESCE(max("Id"),0) FROM "AspNetUserClaims"),(SELECT COALESCE(max("Id"),0) FROM "AspNetRoleClaims")),true);
            
            ALTER TABLE "USER_ACCOUNT" ALTER COLUMN "EmployeeId" SET DEFAULT nextval('employee_id_seq');
            
            UPDATE "USER_ACCOUNT" SET "EmployeeId"=nextval('employee_id_seq') WHERE "EmployeeId" IS NULL;
            
            ALTER TABLE "USER_ACCOUNT" ALTER COLUMN "EmployeeId" SET NOT NULL;
            
            DO $legacy$ BEGIN
            IF EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='"VISIT"'::regclass AND conname='FK_VISIT_EMPLOYEE_PROFILE_PriorityDentistId') THEN
            ALTER TABLE "VISIT" DROP CONSTRAINT "FK_VISIT_EMPLOYEE_PROFILE_PriorityDentistId";
            ALTER TABLE "VISIT" ADD CONSTRAINT "FK_VISIT_EMPLOYEE_PROFILE_PriorityDentistId" FOREIGN KEY ("PriorityDentistId") REFERENCES "USER_ACCOUNT"("EmployeeId") ON DELETE RESTRICT;
            END IF; END $legacy$;
            
            DROP TABLE "AUTH_REFRESH_TOKEN", "AUTH_SESSION", "AspNetRoleClaims", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens", "EMAIL_VERIFICATION_TOKEN", "EMPLOYEE_PROFILE", "RECOVERY_OUTBOX", "RECOVERY_TOKEN", "ROLE", "STAFF_ACCESS_ASSIGNMENT", "USER_ACCOUNT_ROLE";
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "AUTH_SESSION" (
                "Id" uuid NOT NULL,
                "AccountId" integer NOT NULL,
                "ActivityVersion" integer NOT NULL,
                "ClientKind" character varying(20) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "DeviceLabel" character varying(120) NOT NULL,
                "ExpiresAt" timestamp with time zone NOT NULL,
                "LastInteractiveActivityAt" timestamp with time zone NOT NULL,
                "LastSeenAt" timestamp with time zone NOT NULL,
                "RevokedAt" timestamp with time zone,
                CONSTRAINT "PK_AUTH_SESSION" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_AUTH_SESSION_USER_ACCOUNT_AccountId" FOREIGN KEY ("AccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            CREATE TABLE "AspNetUserClaims" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "ClaimType" text,
                "ClaimValue" text,
                "UserId" integer NOT NULL,
                CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_AspNetUserClaims_USER_ACCOUNT_UserId" FOREIGN KEY ("UserId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE CASCADE
            );
            
            CREATE TABLE "AspNetUserLogins" (
                "LoginProvider" text NOT NULL,
                "ProviderKey" text NOT NULL,
                "ProviderDisplayName" text,
                "UserId" integer NOT NULL,
                CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
                CONSTRAINT "FK_AspNetUserLogins_USER_ACCOUNT_UserId" FOREIGN KEY ("UserId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE CASCADE
            );
            
            CREATE TABLE "AspNetUserTokens" (
                "UserId" integer NOT NULL,
                "LoginProvider" text NOT NULL,
                "Name" text NOT NULL,
                "Value" text,
                CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
                CONSTRAINT "FK_AspNetUserTokens_USER_ACCOUNT_UserId" FOREIGN KEY ("UserId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE CASCADE
            );
            
            CREATE TABLE "EMAIL_VERIFICATION_TOKEN" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                "AccountId" integer NOT NULL,
                "CodeHash" character varying(64) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "ExpiresAt" timestamp with time zone NOT NULL,
                "LastSentAt" timestamp with time zone NOT NULL,
                "NormalizedEmail" character varying(256) NOT NULL,
                "ResendCount" integer NOT NULL,
                "UsedAt" timestamp with time zone,
                "VerifyAttempts" integer NOT NULL,
                CONSTRAINT "PK_EMAIL_VERIFICATION_TOKEN" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_EMAIL_VERIFICATION_TOKEN_USER_ACCOUNT_AccountId" FOREIGN KEY ("AccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE CASCADE
            );
            
            CREATE TABLE "EMPLOYEE_PROFILE" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "AccountId" integer NOT NULL,
                "EmployeeCode" character varying(40) NOT NULL,
                "FullName" character varying(160) NOT NULL,
                CONSTRAINT "PK_EMPLOYEE_PROFILE" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_EMPLOYEE_PROFILE_USER_ACCOUNT_AccountId" FOREIGN KEY ("AccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            CREATE TABLE "RECOVERY_OUTBOX" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                "AccountId" integer NOT NULL,
                "ChannelType" character varying(16) NOT NULL,
                "Destination" character varying(256) NOT NULL,
                "OccurredAt" timestamp with time zone NOT NULL,
                "Provider" character varying(32) NOT NULL,
                "ProviderMessageId" character varying(64),
                "Status" character varying(16) NOT NULL,
                CONSTRAINT "PK_RECOVERY_OUTBOX" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_RECOVERY_OUTBOX_USER_ACCOUNT_AccountId" FOREIGN KEY ("AccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            CREATE TABLE "RECOVERY_TOKEN" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "AccountId" integer NOT NULL,
                "ChannelType" character varying(16) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "ExpiresAt" timestamp with time zone NOT NULL,
                "LastResentAt" timestamp with time zone,
                "NormalizedDestination" character varying(256) NOT NULL,
                "ResendCount" integer NOT NULL,
                "ResetGrantHash" character varying(256) NOT NULL,
                "TokenHash" character varying(256) NOT NULL,
                "UsedAt" timestamp with time zone,
                "VerifyAttempts" integer NOT NULL,
                CONSTRAINT "PK_RECOVERY_TOKEN" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_RECOVERY_TOKEN_USER_ACCOUNT_AccountId" FOREIGN KEY ("AccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE CASCADE
            );
            
            CREATE TABLE "ROLE" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "ConcurrencyStamp" text,
                "Name" character varying(256),
                "NormalizedName" character varying(256),
                CONSTRAINT "PK_ROLE" PRIMARY KEY ("Id")
            );
            
            CREATE TABLE "STAFF_ACCESS_ASSIGNMENT" (
                "AccountId" integer NOT NULL,
                "BranchIds" integer[] NOT NULL,
                "GroupId" uuid NOT NULL,
                "Version" integer NOT NULL,
                CONSTRAINT "PK_STAFF_ACCESS_ASSIGNMENT" PRIMARY KEY ("AccountId"),
                CONSTRAINT "FK_STAFF_ACCESS_ASSIGNMENT_ACCESS_GROUP_GroupId" FOREIGN KEY ("GroupId") REFERENCES "ACCESS_GROUP" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_STAFF_ACCESS_ASSIGNMENT_USER_ACCOUNT_AccountId" FOREIGN KEY ("AccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            CREATE TABLE "AUTH_REFRESH_TOKEN" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                "AccountId" integer NOT NULL,
                "ExpiresAt" timestamp with time zone NOT NULL,
                "IssuedAt" timestamp with time zone NOT NULL,
                "ReplacedById" bigint,
                "RevokedAt" timestamp with time zone,
                "SessionId" uuid NOT NULL,
                "Status" character varying(16) NOT NULL,
                "TokenHash" character varying(64) NOT NULL,
                CONSTRAINT "PK_AUTH_REFRESH_TOKEN" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_AUTH_REFRESH_TOKEN_AUTH_SESSION_SessionId" FOREIGN KEY ("SessionId") REFERENCES "AUTH_SESSION" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_AUTH_REFRESH_TOKEN_USER_ACCOUNT_AccountId" FOREIGN KEY ("AccountId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE RESTRICT
            );
            
            CREATE TABLE "AspNetRoleClaims" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "ClaimType" text,
                "ClaimValue" text,
                "RoleId" integer NOT NULL,
                CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_AspNetRoleClaims_ROLE_RoleId" FOREIGN KEY ("RoleId") REFERENCES "ROLE" ("Id") ON DELETE CASCADE
            );
            
            CREATE TABLE "USER_ACCOUNT_ROLE" (
                "UserId" integer NOT NULL,
                "RoleId" integer NOT NULL,
                CONSTRAINT "PK_USER_ACCOUNT_ROLE" PRIMARY KEY ("UserId", "RoleId"),
                CONSTRAINT "FK_USER_ACCOUNT_ROLE_ROLE_RoleId" FOREIGN KEY ("RoleId") REFERENCES "ROLE" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_USER_ACCOUNT_ROLE_USER_ACCOUNT_UserId" FOREIGN KEY ("UserId") REFERENCES "USER_ACCOUNT" ("Id") ON DELETE CASCADE
            );
            
            INSERT INTO "ROLE" ("Id","Name","NormalizedName","ConcurrencyStamp") SELECT "IdentityRoleId","Name","NormalizedName","RoleConcurrencyStamp" FROM "ACCESS_GROUP" WHERE "RecordKind"='IdentityRole';
            
            INSERT INTO "USER_ACCOUNT_ROLE" ("UserId","RoleId") SELECT u."Id",r."Id" FROM "USER_ACCOUNT" u CROSS JOIN LATERAL unnest(u."AssignedRoles") n JOIN "ROLE" r ON r."Name"=n;
            
            INSERT INTO "EMPLOYEE_PROFILE" ("Id","AccountId","EmployeeCode","FullName") SELECT "EmployeeId","Id","EmployeeCode","EmployeeFullName" FROM "USER_ACCOUNT" WHERE "EmployeeCode" IS NOT NULL;
            
            INSERT INTO "STAFF_ACCESS_ASSIGNMENT" ("AccountId","GroupId","BranchIds","Version") SELECT "Id","GroupId","BranchIds","AccessVersion" FROM "USER_ACCOUNT" WHERE "GroupId" IS NOT NULL;
            
            INSERT INTO "AUTH_SESSION" SELECT r.* FROM "USER_ACCOUNT" u CROSS JOIN LATERAL jsonb_array_elements(u."Sessions") j CROSS JOIN LATERAL jsonb_populate_record(NULL::"AUTH_SESSION",j) r;
            
            INSERT INTO "RECOVERY_TOKEN" SELECT r.* FROM "USER_ACCOUNT" u CROSS JOIN LATERAL jsonb_array_elements(u."Recoveries") j CROSS JOIN LATERAL jsonb_populate_record(NULL::"RECOVERY_TOKEN",j) r;
            
            INSERT INTO "RECOVERY_OUTBOX" SELECT r.* FROM "USER_ACCOUNT" u CROSS JOIN LATERAL jsonb_array_elements(u."RecoveryDeliveries") j CROSS JOIN LATERAL jsonb_populate_record(NULL::"RECOVERY_OUTBOX",j) r;
            
            INSERT INTO "EMAIL_VERIFICATION_TOKEN" SELECT r.* FROM "USER_ACCOUNT" u CROSS JOIN LATERAL jsonb_array_elements(u."EmailVerifications") j CROSS JOIN LATERAL jsonb_populate_record(NULL::"EMAIL_VERIFICATION_TOKEN",j) r;
            
            INSERT INTO "AUTH_REFRESH_TOKEN" SELECT r.* FROM "USER_ACCOUNT" u CROSS JOIN LATERAL jsonb_array_elements(u."Sessions") s CROSS JOIN LATERAL jsonb_array_elements(s->'RefreshTokens') j CROSS JOIN LATERAL jsonb_populate_record(NULL::"AUTH_REFRESH_TOKEN",j) r;
            
            INSERT INTO "AspNetUserClaims" ("Id","UserId","ClaimType","ClaimValue") SELECT (j->>'Id')::integer,u."Id",j->>'Type',j->>'Value' FROM "USER_ACCOUNT" u CROSS JOIN LATERAL jsonb_array_elements(u."Claims") j;
            
            INSERT INTO "AspNetRoleClaims" ("Id","RoleId","ClaimType","ClaimValue") SELECT (j->>'Id')::integer,g."IdentityRoleId",j->>'Type',j->>'Value' FROM "ACCESS_GROUP" g CROSS JOIN LATERAL jsonb_array_elements(g."RoleClaims") j WHERE g."RecordKind"='IdentityRole';
            
            INSERT INTO "AspNetUserLogins" ("UserId","LoginProvider","ProviderKey","ProviderDisplayName") SELECT u."Id",j->>'Provider',j->>'Key',j->>'DisplayName' FROM "USER_ACCOUNT" u CROSS JOIN LATERAL jsonb_array_elements(u."Logins") j;
            
            INSERT INTO "AspNetUserTokens" ("UserId","LoginProvider","Name","Value") SELECT u."Id",j->>'Provider',j->>'Name',j->>'Value' FROM "USER_ACCOUNT" u CROSS JOIN LATERAL jsonb_array_elements(u."IdentityTokens") j;
            
            ALTER TABLE "PATIENT_ACCOUNT_LINK" DROP CONSTRAINT "FK_PATIENT_ACCOUNT_LINK_USER_ACCOUNT_ReviewingEmployeeId";
            
            ALTER TABLE "PATIENT_ACCOUNT_LINK" ADD CONSTRAINT "FK_PATIENT_ACCOUNT_LINK_EMPLOYEE_PROFILE_ReviewingEmployeeId" FOREIGN KEY ("ReviewingEmployeeId") REFERENCES "EMPLOYEE_PROFILE" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "PATIENT_ACCOUNT_LINK" DROP CONSTRAINT "FK_PATIENT_ACCOUNT_LINK_USER_ACCOUNT_RevokingEmployeeId";
            
            ALTER TABLE "PATIENT_ACCOUNT_LINK" ADD CONSTRAINT "FK_PATIENT_ACCOUNT_LINK_EMPLOYEE_PROFILE_RevokingEmployeeId" FOREIGN KEY ("RevokingEmployeeId") REFERENCES "EMPLOYEE_PROFILE" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "SCHEDULING_MEMBER" DROP CONSTRAINT "FK_SCHEDULING_MEMBER_USER_ACCOUNT_EmployeeId";
            
            ALTER TABLE "SCHEDULING_MEMBER" ADD CONSTRAINT "FK_SCHEDULING_MEMBER_EMPLOYEE_PROFILE_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "EMPLOYEE_PROFILE" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "TREATMENT_TEMPLATE_ITEM" DROP CONSTRAINT "FK_TREATMENT_TEMPLATE_ITEM_USER_ACCOUNT_AssistantId";
            
            ALTER TABLE "TREATMENT_TEMPLATE_ITEM" ADD CONSTRAINT "FK_TREATMENT_TEMPLATE_ITEM_EMPLOYEE_PROFILE_AssistantId" FOREIGN KEY ("AssistantId") REFERENCES "EMPLOYEE_PROFILE" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "TREATMENT_TEMPLATE_ITEM" DROP CONSTRAINT "FK_TREATMENT_TEMPLATE_ITEM_USER_ACCOUNT_DentistId";
            
            ALTER TABLE "TREATMENT_TEMPLATE_ITEM" ADD CONSTRAINT "FK_TREATMENT_TEMPLATE_ITEM_EMPLOYEE_PROFILE_DentistId" FOREIGN KEY ("DentistId") REFERENCES "EMPLOYEE_PROFILE" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "WORKING_SCHEDULE_REVISION" DROP CONSTRAINT "FK_WORKING_SCHEDULE_REVISION_USER_ACCOUNT_EmployeeId";
            
            ALTER TABLE "WORKING_SCHEDULE_REVISION" ADD CONSTRAINT "FK_WORKING_SCHEDULE_REVISION_EMPLOYEE_PROFILE_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "EMPLOYEE_PROFILE" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "RESOURCE_RESERVATION" DROP CONSTRAINT "FK_RESOURCE_RESERVATION_USER_ACCOUNT_AssistantId";
            
            ALTER TABLE "RESOURCE_RESERVATION" ADD CONSTRAINT "FK_RESOURCE_RESERVATION_EMPLOYEE_PROFILE_AssistantId" FOREIGN KEY ("AssistantId") REFERENCES "EMPLOYEE_PROFILE" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "VISIT" DROP CONSTRAINT "FK_VISIT_USER_ACCOUNT_DentistId";
            
            ALTER TABLE "VISIT" ADD CONSTRAINT "FK_VISIT_EMPLOYEE_PROFILE_DentistId" FOREIGN KEY ("DentistId") REFERENCES "EMPLOYEE_PROFILE" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "PRESCRIPTION" DROP CONSTRAINT "FK_PRESCRIPTION_USER_ACCOUNT_PrescriberEmployeeId";
            
            ALTER TABLE "PRESCRIPTION" ADD CONSTRAINT "FK_PRESCRIPTION_EMPLOYEE_PROFILE_PrescriberEmployeeId" FOREIGN KEY ("PrescriberEmployeeId") REFERENCES "EMPLOYEE_PROFILE" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "COMMAND_RECEIPT" ADD CONSTRAINT "FK_COMMAND_RECEIPT_AUTH_SESSION_SessionId" FOREIGN KEY ("SessionId") REFERENCES "AUTH_SESSION" ("Id") ON DELETE RESTRICT;
            
            ALTER TABLE "AUDIT_LOG" DROP CONSTRAINT "FK_AUDIT_LOG_USER_ACCOUNT_DecidingDentistId";
            
            ALTER TABLE "AUDIT_LOG" ADD CONSTRAINT "FK_AUDIT_LOG_EMPLOYEE_PROFILE_DecidingDentistId" FOREIGN KEY ("DecidingDentistId") REFERENCES "EMPLOYEE_PROFILE" ("Id") ON DELETE RESTRICT;
            
            DROP INDEX "IX_EMPLOYEE_PROFILE_EmployeeCode";
            
            CREATE INDEX "IX_AUTH_REFRESH_TOKEN_AccountId" ON "AUTH_REFRESH_TOKEN" ("AccountId");
            
            CREATE INDEX "IX_AUTH_REFRESH_TOKEN_SessionId_Status" ON "AUTH_REFRESH_TOKEN" ("SessionId", "Status");
            
            CREATE UNIQUE INDEX "IX_AUTH_REFRESH_TOKEN_TokenHash" ON "AUTH_REFRESH_TOKEN" ("TokenHash");
            
            SELECT setval(pg_get_serial_sequence('"AUTH_REFRESH_TOKEN"','Id'),GREATEST(COALESCE((SELECT max("Id") FROM "AUTH_REFRESH_TOKEN"),0),1),EXISTS(SELECT 1 FROM "AUTH_REFRESH_TOKEN"));
            
            CREATE INDEX "IX_AUTH_SESSION_AccountId_RevokedAt" ON "AUTH_SESSION" ("AccountId", "RevokedAt");
            
            CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");
            
            SELECT setval(pg_get_serial_sequence('"AspNetRoleClaims"','Id'),GREATEST(COALESCE((SELECT max("Id") FROM "AspNetRoleClaims"),0),1),EXISTS(SELECT 1 FROM "AspNetRoleClaims"));
            
            CREATE INDEX "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");
            
            SELECT setval(pg_get_serial_sequence('"AspNetUserClaims"','Id'),GREATEST(COALESCE((SELECT max("Id") FROM "AspNetUserClaims"),0),1),EXISTS(SELECT 1 FROM "AspNetUserClaims"));
            
            CREATE INDEX "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");
            
            CREATE INDEX "IX_EMAIL_VERIFICATION_TOKEN_AccountId_CreatedAt" ON "EMAIL_VERIFICATION_TOKEN" ("AccountId", "CreatedAt");
            
            CREATE INDEX "IX_EMAIL_VERIFICATION_TOKEN_ExpiresAt" ON "EMAIL_VERIFICATION_TOKEN" ("ExpiresAt");
            
            SELECT setval(pg_get_serial_sequence('"EMAIL_VERIFICATION_TOKEN"','Id'),GREATEST(COALESCE((SELECT max("Id") FROM "EMAIL_VERIFICATION_TOKEN"),0),1),EXISTS(SELECT 1 FROM "EMAIL_VERIFICATION_TOKEN"));
            
            CREATE UNIQUE INDEX "IX_EMPLOYEE_PROFILE_AccountId" ON "EMPLOYEE_PROFILE" ("AccountId");
            
            CREATE UNIQUE INDEX "IX_EMPLOYEE_PROFILE_EmployeeCode" ON "EMPLOYEE_PROFILE" ("EmployeeCode");
            
            SELECT setval(pg_get_serial_sequence('"EMPLOYEE_PROFILE"','Id'),GREATEST(COALESCE((SELECT max("Id") FROM "EMPLOYEE_PROFILE"),0),1),EXISTS(SELECT 1 FROM "EMPLOYEE_PROFILE"));
            
            CREATE INDEX "IX_RECOVERY_OUTBOX_AccountId_OccurredAt" ON "RECOVERY_OUTBOX" ("AccountId", "OccurredAt");
            
            SELECT setval(pg_get_serial_sequence('"RECOVERY_OUTBOX"','Id'),GREATEST(COALESCE((SELECT max("Id") FROM "RECOVERY_OUTBOX"),0),1),EXISTS(SELECT 1 FROM "RECOVERY_OUTBOX"));
            
            CREATE INDEX "IX_RECOVERY_TOKEN_AccountId_CreatedAt" ON "RECOVERY_TOKEN" ("AccountId", "CreatedAt");
            
            CREATE INDEX "IX_RECOVERY_TOKEN_AccountId_UsedAt" ON "RECOVERY_TOKEN" ("AccountId", "UsedAt");
            
            CREATE INDEX "IX_RECOVERY_TOKEN_ExpiresAt" ON "RECOVERY_TOKEN" ("ExpiresAt");
            
            SELECT setval(pg_get_serial_sequence('"RECOVERY_TOKEN"','Id'),GREATEST(COALESCE((SELECT max("Id") FROM "RECOVERY_TOKEN"),0),1),EXISTS(SELECT 1 FROM "RECOVERY_TOKEN"));
            
            CREATE UNIQUE INDEX "RoleNameIndex" ON "ROLE" ("NormalizedName");
            
            SELECT setval(pg_get_serial_sequence('"ROLE"','Id'),GREATEST(COALESCE((SELECT max("Id") FROM "ROLE"),0),1),EXISTS(SELECT 1 FROM "ROLE"));
            
            CREATE INDEX "IX_STAFF_ACCESS_ASSIGNMENT_GroupId" ON "STAFF_ACCESS_ASSIGNMENT" ("GroupId");
            
            CREATE INDEX "IX_USER_ACCOUNT_ROLE_RoleId" ON "USER_ACCOUNT_ROLE" ("RoleId");
            
            DELETE FROM "ACCESS_GROUP" WHERE "RecordKind"='IdentityRole';
            
            DO $legacy$ BEGIN
            IF EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='"VISIT"'::regclass AND conname='FK_VISIT_EMPLOYEE_PROFILE_PriorityDentistId') THEN
            ALTER TABLE "VISIT" DROP CONSTRAINT "FK_VISIT_EMPLOYEE_PROFILE_PriorityDentistId";
            ALTER TABLE "VISIT" ADD CONSTRAINT "FK_VISIT_EMPLOYEE_PROFILE_PriorityDentistId" FOREIGN KEY ("PriorityDentistId") REFERENCES "EMPLOYEE_PROFILE"("Id") ON DELETE RESTRICT;
            END IF; END $legacy$;
            
            ALTER TABLE "USER_ACCOUNT" DROP CONSTRAINT "AK_USER_ACCOUNT_EmployeeId", DROP CONSTRAINT "FK_USER_ACCOUNT_ACCESS_GROUP_GroupId";
            
            DROP INDEX "IX_USER_ACCOUNT_GroupId";
            
            DROP INDEX "IX_USER_ACCOUNT_GoogleSubject";
            
            ALTER TABLE "USER_ACCOUNT" DROP COLUMN "EmployeeId", DROP COLUMN "AssignedRoles", DROP COLUMN "GoogleSubject", DROP COLUMN "EmployeeCode", DROP COLUMN "EmployeeFullName", DROP COLUMN "GroupId", DROP COLUMN "BranchIds", DROP COLUMN "AccessVersion", DROP COLUMN "Claims", DROP COLUMN "EmailVerifications", DROP COLUMN "IdentityTokens", DROP COLUMN "Logins", DROP COLUMN "Recoveries", DROP COLUMN "RecoveryDeliveries", DROP COLUMN "Sessions";
            
            DROP INDEX "IX_ACCESS_GROUP_RecordKind_NormalizedCode";
            
            DROP INDEX "IX_ACCESS_GROUP_IdentityRoleId";
            
            DROP INDEX "IX_ACCESS_GROUP_RecordKind_NormalizedName";
            
            CREATE UNIQUE INDEX "IX_ACCESS_GROUP_NormalizedCode" ON "ACCESS_GROUP" ("NormalizedCode");
            
            CREATE UNIQUE INDEX "IX_ACCESS_GROUP_NormalizedName" ON "ACCESS_GROUP" ("NormalizedName");
            
            ALTER TABLE "ACCESS_GROUP" DROP COLUMN "RecordKind", DROP COLUMN "IdentityRoleId", DROP COLUMN "RoleConcurrencyStamp", DROP COLUMN "RoleClaims";
            
            DROP SEQUENCE employee_id_seq;
            
            DROP SEQUENCE account_entry_seq;
            """);
    }
}

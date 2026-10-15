using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PatientMasterAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "patient_code_seq");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "PATIENT_PROFILE",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

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

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "PATIENT_PROFILE",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "PATIENT_PROFILE",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "PATIENT_PROFILE",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // Backfill search names for existing rows, matching PatientLinkSupport.NormalizeName for Vietnamese text.
            migrationBuilder.Sql("UPDATE \"PATIENT_PROFILE\" SET \"NormalizedName\" = upper(regexp_replace(btrim(translate(\"FullName\", 'àáâãèéêìíòóôõùúýăĩũơưạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝĂĨŨƠƯẠẢẤẦẨẪẬẮẰẲẴẶẸẺẼẾỀỂỄỆỈỊỌỎỐỒỔỖỘỚỜỞỠỢỤỦỨỪỬỮỰỲỴỶỸđĐ', 'aaaaeeeiioooouuyaiuouaaaaaaaaaaaaeeeeeeeeiioooooooooooouuuuuuuyyyyAAAAEEEIIOOOOUUYAIUOUAAAAAAAAAAAAEEEEEEEEIIOOOOOOOOOOOOUUUUUUUYYYYdD')), '\\s+', ' ', 'g')) WHERE \"NormalizedName\" = '';");

            migrationBuilder.CreateTable(
                name: "PATIENT_AUDIT",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    ActorAccountId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangesJson = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PATIENT_AUDIT", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PATIENT_AUDIT_PATIENT_PROFILE_PatientId",
                        column: x => x.PatientId,
                        principalTable: "PATIENT_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PATIENT_AUDIT_USER_ACCOUNT_ActorAccountId",
                        column: x => x.ActorAccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_PROFILE_NormalizedName",
                table: "PATIENT_PROFILE",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_PROFILE_Phone",
                table: "PATIENT_PROFILE",
                column: "Phone");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PATIENT_PROFILE_Gender",
                table: "PATIENT_PROFILE",
                sql: "\"Gender\" IS NULL OR \"Gender\" IN ('MALE','FEMALE','OTHER')");

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_AUDIT_ActorAccountId",
                table: "PATIENT_AUDIT",
                column: "ActorAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_AUDIT_PatientId_OccurredAt",
                table: "PATIENT_AUDIT",
                columns: new[] { "PatientId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PATIENT_AUDIT");

            migrationBuilder.DropIndex(
                name: "IX_PATIENT_PROFILE_NormalizedName",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropIndex(
                name: "IX_PATIENT_PROFILE_Phone",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PATIENT_PROFILE_Gender",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "PATIENT_PROFILE");

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
                name: "IsArchived",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "PATIENT_PROFILE");

            migrationBuilder.DropSequence(
                name: "patient_code_seq");
        }
    }
}

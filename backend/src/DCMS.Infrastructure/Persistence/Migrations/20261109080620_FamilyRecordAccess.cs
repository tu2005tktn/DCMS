using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FamilyRecordAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_APPOINTMENT_BookerAccountId",
                table: "APPOINTMENT");

            migrationBuilder.AddColumn<Guid>(
                name: "FamilyMemberId",
                table: "APPOINTMENT",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FAMILY_RECORD_ACCESS",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerAccountId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DeclaredFullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeclaredBirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Relationship = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeclaredPatientCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    MatchingInformation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: true),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    Scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<int>(type: "integer", nullable: true),
                    SharingBasis = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    VerificationNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PublicNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FAMILY_RECORD_ACCESS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FAMILY_RECORD_ACCESS_CUSTOMER_FAMILY_MEMBER_FamilyMemberId",
                        column: x => x.FamilyMemberId,
                        principalTable: "CUSTOMER_FAMILY_MEMBER",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FAMILY_RECORD_ACCESS_PATIENT_PROFILE_PatientId",
                        column: x => x.PatientId,
                        principalTable: "PATIENT_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FAMILY_RECORD_ACCESS_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FAMILY_RECORD_ACCESS_USER_ACCOUNT_DecidedBy",
                        column: x => x.DecidedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FAMILY_RECORD_ACCESS_USER_ACCOUNT_OwnerAccountId",
                        column: x => x.OwnerAccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FAMILY_RECORD_AUDIT",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccessId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorAccountId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FAMILY_RECORD_AUDIT", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FAMILY_RECORD_AUDIT_FAMILY_RECORD_ACCESS_AccessId",
                        column: x => x.AccessId,
                        principalTable: "FAMILY_RECORD_ACCESS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FAMILY_RECORD_AUDIT_USER_ACCOUNT_ActorAccountId",
                        column: x => x.ActorAccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FAMILY_RECORD_RECEIPT",
                columns: table => new
                {
                    ActorAccountId = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FAMILY_RECORD_RECEIPT", x => new { x.ActorAccountId, x.RequestKey });
                    table.ForeignKey(
                        name: "FK_FAMILY_RECORD_RECEIPT_FAMILY_RECORD_ACCESS_AccessId",
                        column: x => x.AccessId,
                        principalTable: "FAMILY_RECORD_ACCESS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FAMILY_RECORD_RECEIPT_USER_ACCOUNT_ActorAccountId",
                        column: x => x.ActorAccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_BookerAccountId_FamilyMemberId_StartAt",
                table: "APPOINTMENT",
                columns: new[] { "BookerAccountId", "FamilyMemberId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_FamilyMemberId",
                table: "APPOINTMENT",
                column: "FamilyMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_FAMILY_RECORD_ACCESS_BranchId",
                table: "FAMILY_RECORD_ACCESS",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_FAMILY_RECORD_ACCESS_Code",
                table: "FAMILY_RECORD_ACCESS",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FAMILY_RECORD_ACCESS_DecidedBy",
                table: "FAMILY_RECORD_ACCESS",
                column: "DecidedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FAMILY_RECORD_ACCESS_FamilyMemberId",
                table: "FAMILY_RECORD_ACCESS",
                column: "FamilyMemberId",
                unique: true,
                filter: "\"Status\" IN ('Pending','Verified')");

            migrationBuilder.CreateIndex(
                name: "IX_FAMILY_RECORD_ACCESS_OwnerAccountId_RequestedAt",
                table: "FAMILY_RECORD_ACCESS",
                columns: new[] { "OwnerAccountId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FAMILY_RECORD_ACCESS_PatientId",
                table: "FAMILY_RECORD_ACCESS",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_FAMILY_RECORD_AUDIT_AccessId_OccurredAt",
                table: "FAMILY_RECORD_AUDIT",
                columns: new[] { "AccessId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FAMILY_RECORD_AUDIT_ActorAccountId",
                table: "FAMILY_RECORD_AUDIT",
                column: "ActorAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FAMILY_RECORD_RECEIPT_AccessId",
                table: "FAMILY_RECORD_RECEIPT",
                column: "AccessId");

            migrationBuilder.AddForeignKey(
                name: "FK_APPOINTMENT_CUSTOMER_FAMILY_MEMBER_FamilyMemberId",
                table: "APPOINTMENT",
                column: "FamilyMemberId",
                principalTable: "CUSTOMER_FAMILY_MEMBER",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_APPOINTMENT_CUSTOMER_FAMILY_MEMBER_FamilyMemberId",
                table: "APPOINTMENT");

            migrationBuilder.DropTable(
                name: "FAMILY_RECORD_AUDIT");

            migrationBuilder.DropTable(
                name: "FAMILY_RECORD_RECEIPT");

            migrationBuilder.DropTable(
                name: "FAMILY_RECORD_ACCESS");

            migrationBuilder.DropIndex(
                name: "IX_APPOINTMENT_BookerAccountId_FamilyMemberId_StartAt",
                table: "APPOINTMENT");

            migrationBuilder.DropIndex(
                name: "IX_APPOINTMENT_FamilyMemberId",
                table: "APPOINTMENT");

            migrationBuilder.DropColumn(
                name: "FamilyMemberId",
                table: "APPOINTMENT");

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_BookerAccountId",
                table: "APPOINTMENT",
                column: "BookerAccountId");
        }
    }
}

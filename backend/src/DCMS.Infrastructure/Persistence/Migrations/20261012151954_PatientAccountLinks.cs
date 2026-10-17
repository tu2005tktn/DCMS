using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PatientAccountLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PATIENT_PROFILE",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FullName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Phone = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PATIENT_PROFILE", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PATIENT_ACCOUNT_LINK",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeclaredFullName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DeclaredBirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeclaredPatientCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RequestNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewingEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    VerificationNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PublicNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokingEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PATIENT_ACCOUNT_LINK", x => x.Id);
                    table.CheckConstraint("CK_PATIENT_ACCOUNT_LINK_Status", "\"Status\" IN ('PENDING','VERIFIED','REJECTED','REVOKED','WITHDRAWN') AND (\"Status\" <> 'VERIFIED' OR \"PatientId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PATIENT_ACCOUNT_LINK_EMPLOYEE_PROFILE_ReviewingEmployeeId",
                        column: x => x.ReviewingEmployeeId,
                        principalTable: "EMPLOYEE_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PATIENT_ACCOUNT_LINK_EMPLOYEE_PROFILE_RevokingEmployeeId",
                        column: x => x.RevokingEmployeeId,
                        principalTable: "EMPLOYEE_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PATIENT_ACCOUNT_LINK_PATIENT_PROFILE_PatientId",
                        column: x => x.PatientId,
                        principalTable: "PATIENT_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PATIENT_ACCOUNT_LINK_USER_ACCOUNT_AccountId",
                        column: x => x.AccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PATIENT_LINK_AUDIT",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LinkId = table.Column<int>(type: "integer", nullable: false),
                    ActorAccountId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: false),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PATIENT_LINK_AUDIT", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PATIENT_LINK_AUDIT_PATIENT_ACCOUNT_LINK_LinkId",
                        column: x => x.LinkId,
                        principalTable: "PATIENT_ACCOUNT_LINK",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PATIENT_LINK_AUDIT_USER_ACCOUNT_ActorAccountId",
                        column: x => x.ActorAccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_ACCOUNT_LINK_RequestCode",
                table: "PATIENT_ACCOUNT_LINK",
                column: "RequestCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_ACCOUNT_LINK_ReviewingEmployeeId",
                table: "PATIENT_ACCOUNT_LINK",
                column: "ReviewingEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_ACCOUNT_LINK_RevokingEmployeeId",
                table: "PATIENT_ACCOUNT_LINK",
                column: "RevokingEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_ACCOUNT_LINK_Status_RequestedAt",
                table: "PATIENT_ACCOUNT_LINK",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_PATIENT_ACCOUNT_LINK_Account_Pending",
                table: "PATIENT_ACCOUNT_LINK",
                column: "AccountId",
                unique: true,
                filter: "\"Status\" = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "UX_PATIENT_ACCOUNT_LINK_Account_Verified",
                table: "PATIENT_ACCOUNT_LINK",
                column: "AccountId",
                unique: true,
                filter: "\"Status\" = 'VERIFIED'");

            migrationBuilder.CreateIndex(
                name: "UX_PATIENT_ACCOUNT_LINK_Patient_Verified",
                table: "PATIENT_ACCOUNT_LINK",
                column: "PatientId",
                unique: true,
                filter: "\"Status\" = 'VERIFIED'");

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_LINK_AUDIT_ActorAccountId",
                table: "PATIENT_LINK_AUDIT",
                column: "ActorAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_LINK_AUDIT_LinkId_OccurredAt",
                table: "PATIENT_LINK_AUDIT",
                columns: new[] { "LinkId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PATIENT_PROFILE_PatientCode",
                table: "PATIENT_PROFILE",
                column: "PatientCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PATIENT_LINK_AUDIT");

            migrationBuilder.DropTable(
                name: "PATIENT_ACCOUNT_LINK");

            migrationBuilder.DropTable(
                name: "PATIENT_PROFILE");
        }
    }
}

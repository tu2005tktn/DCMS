using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentSupportRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "APPOINTMENT_SUPPORT_REQUEST",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PublicNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DecidedBy = table.Column<int>(type: "integer", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APPOINTMENT_SUPPORT_REQUEST", x => x.Id);
                    table.CheckConstraint("CK_APPOINTMENT_SUPPORT_REQUEST_Values", "\"Kind\" IN ('RESCHEDULE','CANCEL','OTHER') AND \"Status\" IN ('Pending','Resolved','Rejected','Withdrawn')");
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_SUPPORT_REQUEST_APPOINTMENT_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "APPOINTMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_SUPPORT_REQUEST_USER_ACCOUNT_AccountId",
                        column: x => x.AccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_SUPPORT_REQUEST_USER_ACCOUNT_DecidedBy",
                        column: x => x.DecidedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_SUPPORT_REQUEST_DecidedBy",
                table: "APPOINTMENT_SUPPORT_REQUEST",
                column: "DecidedBy");

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_SUPPORT_REQUEST_Status_CreatedAt",
                table: "APPOINTMENT_SUPPORT_REQUEST",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_APPOINTMENT_SUPPORT_REQUEST_Key",
                table: "APPOINTMENT_SUPPORT_REQUEST",
                columns: new[] { "AccountId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_APPOINTMENT_SUPPORT_REQUEST_Pending",
                table: "APPOINTMENT_SUPPORT_REQUEST",
                columns: new[] { "AppointmentId", "AccountId" },
                unique: true,
                filter: "\"Status\" = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "APPOINTMENT_SUPPORT_REQUEST");
        }
    }
}

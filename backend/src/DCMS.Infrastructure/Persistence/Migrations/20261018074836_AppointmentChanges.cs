using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "APPOINTMENT",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "APPOINTMENT",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancelledBy",
                table: "APPOINTMENT",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "APPOINTMENT_CHANGE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorAccountId = table.Column<int>(type: "integer", nullable: false),
                    ActorKind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OldStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OldEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OldChairId = table.Column<int>(type: "integer", nullable: false),
                    NewStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NewEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NewChairId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequesterVerification = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APPOINTMENT_CHANGE", x => x.Id);
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_CHANGE_APPOINTMENT_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "APPOINTMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_CHANGE_USER_ACCOUNT_ActorAccountId",
                        column: x => x.ActorAccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "APPOINTMENT_CHANGE_RECEIPT",
                columns: table => new
                {
                    ActorAccountId = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APPOINTMENT_CHANGE_RECEIPT", x => new { x.ActorAccountId, x.RequestKey });
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_CHANGE_RECEIPT_APPOINTMENT_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "APPOINTMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_CHANGE_RECEIPT_USER_ACCOUNT_ActorAccountId",
                        column: x => x.ActorAccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_CancelledBy",
                table: "APPOINTMENT",
                column: "CancelledBy");

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_CHANGE_ActorAccountId_RequestKey",
                table: "APPOINTMENT_CHANGE",
                columns: new[] { "ActorAccountId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_CHANGE_AppointmentId_OccurredAt",
                table: "APPOINTMENT_CHANGE",
                columns: new[] { "AppointmentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_CHANGE_RECEIPT_AppointmentId",
                table: "APPOINTMENT_CHANGE_RECEIPT",
                column: "AppointmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_APPOINTMENT_USER_ACCOUNT_CancelledBy",
                table: "APPOINTMENT",
                column: "CancelledBy",
                principalTable: "USER_ACCOUNT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_APPOINTMENT_USER_ACCOUNT_CancelledBy",
                table: "APPOINTMENT");

            migrationBuilder.DropTable(
                name: "APPOINTMENT_CHANGE");

            migrationBuilder.DropTable(
                name: "APPOINTMENT_CHANGE_RECEIPT");

            migrationBuilder.DropIndex(
                name: "IX_APPOINTMENT_CancelledBy",
                table: "APPOINTMENT");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "APPOINTMENT");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "APPOINTMENT");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "APPOINTMENT");
        }
    }
}

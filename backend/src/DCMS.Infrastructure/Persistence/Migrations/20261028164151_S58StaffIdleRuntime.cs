using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S58StaffIdleRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActivityVersion",
                table: "AUTH_SESSION",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ClientKind",
                table: "AUTH_SESSION",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastInteractiveActivityAt",
                table: "AUTH_SESSION",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "STAFF_ACTIVITY_COMMAND_RECEIPT",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedVersion = table.Column<int>(type: "integer", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STAFF_ACTIVITY_COMMAND_RECEIPT", x => x.Id);
                    table.ForeignKey(
                        name: "FK_STAFF_ACTIVITY_COMMAND_RECEIPT_AUTH_SESSION_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AUTH_SESSION",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_STAFF_ACTIVITY_COMMAND_RECEIPT_SessionId_RequestKey",
                table: "STAFF_ACTIVITY_COMMAND_RECEIPT",
                columns: new[] { "SessionId", "RequestKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "STAFF_ACTIVITY_COMMAND_RECEIPT");

            migrationBuilder.DropColumn(
                name: "ActivityVersion",
                table: "AUTH_SESSION");

            migrationBuilder.DropColumn(
                name: "ClientKind",
                table: "AUTH_SESSION");

            migrationBuilder.DropColumn(
                name: "LastInteractiveActivityAt",
                table: "AUTH_SESSION");
        }
    }
}

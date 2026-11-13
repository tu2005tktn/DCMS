using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FirebasePushDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PUSH_DEVICE",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PUSH_DEVICE", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PUSH_DEVICE_USER_ACCOUNT_AccountId",
                        column: x => x.AccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PUSH_DEVICE_AccountId",
                table: "PUSH_DEVICE",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PUSH_DEVICE_Token",
                table: "PUSH_DEVICE",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PUSH_DEVICE");
        }
    }
}

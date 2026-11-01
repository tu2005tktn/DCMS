using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerFamilyMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CUSTOMER_FAMILY_MEMBER",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerAccountId = table.Column<int>(type: "integer", nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Relationship = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUSTOMER_FAMILY_MEMBER", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CUSTOMER_FAMILY_MEMBER_USER_ACCOUNT_OwnerAccountId",
                        column: x => x.OwnerAccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CUSTOMER_FAMILY_RECEIPT",
                columns: table => new
                {
                    OwnerAccountId = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUSTOMER_FAMILY_RECEIPT", x => new { x.OwnerAccountId, x.RequestKey });
                    table.ForeignKey(
                        name: "FK_CUSTOMER_FAMILY_RECEIPT_USER_ACCOUNT_OwnerAccountId",
                        column: x => x.OwnerAccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CUSTOMER_FAMILY_MEMBER_OwnerAccountId_DeletedAt",
                table: "CUSTOMER_FAMILY_MEMBER",
                columns: new[] { "OwnerAccountId", "DeletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CUSTOMER_FAMILY_MEMBER");

            migrationBuilder.DropTable(
                name: "CUSTOMER_FAMILY_RECEIPT");
        }
    }
}

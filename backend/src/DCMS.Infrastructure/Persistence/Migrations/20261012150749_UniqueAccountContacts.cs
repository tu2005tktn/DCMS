using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueAccountContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "USER_ACCOUNT");

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
                name: "IX_USER_ACCOUNT_NormalizedPhone",
                table: "USER_ACCOUNT",
                column: "NormalizedPhone",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "USER_ACCOUNT");

            migrationBuilder.DropIndex(
                name: "IX_USER_ACCOUNT_NormalizedPhone",
                table: "USER_ACCOUNT");

            migrationBuilder.DropColumn(
                name: "NormalizedPhone",
                table: "USER_ACCOUNT");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "USER_ACCOUNT",
                column: "NormalizedEmail");
        }
    }
}

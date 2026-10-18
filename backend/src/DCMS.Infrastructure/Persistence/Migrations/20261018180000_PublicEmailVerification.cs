using System;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations;

// Additive only: existing accounts and clinical data are not modified.
[DbContext(typeof(DcmsDbContext))]
[Migration("20261018180000_PublicEmailVerification")]
public sealed class PublicEmailVerification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EMAIL_VERIFICATION_TOKEN",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AccountId = table.Column<int>(type: "integer", nullable: false),
                NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ResendCount = table.Column<int>(type: "integer", nullable: false),
                VerifyAttempts = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EMAIL_VERIFICATION_TOKEN", x => x.Id);
                table.ForeignKey("FK_EMAIL_VERIFICATION_TOKEN_USER_ACCOUNT_AccountId", x => x.AccountId,
                    "USER_ACCOUNT", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex("IX_EMAIL_VERIFICATION_TOKEN_AccountId_CreatedAt",
            "EMAIL_VERIFICATION_TOKEN", new[] { "AccountId", "CreatedAt" });
        migrationBuilder.CreateIndex("IX_EMAIL_VERIFICATION_TOKEN_ExpiresAt",
            "EMAIL_VERIFICATION_TOKEN", "ExpiresAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("EMAIL_VERIFICATION_TOKEN");
}

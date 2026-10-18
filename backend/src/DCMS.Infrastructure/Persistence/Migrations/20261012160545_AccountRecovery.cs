using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RECOVERY_AUDIT",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActorId = table.Column<int>(type: "integer", nullable: true),
                    TargetId = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChannelType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RECOVERY_AUDIT", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RECOVERY_AUDIT_USER_ACCOUNT_ActorId",
                        column: x => x.ActorId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RECOVERY_AUDIT_USER_ACCOUNT_TargetId",
                        column: x => x.TargetId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RECOVERY_OUTBOX",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    ChannelType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Destination = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RECOVERY_OUTBOX", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RECOVERY_OUTBOX_USER_ACCOUNT_AccountId",
                        column: x => x.AccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RECOVERY_TOKEN",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ResetGrantHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ChannelType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    NormalizedDestination = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerifyAttempts = table.Column<int>(type: "integer", nullable: false),
                    ResendCount = table.Column<int>(type: "integer", nullable: false),
                    LastResentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RECOVERY_TOKEN", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RECOVERY_TOKEN_USER_ACCOUNT_AccountId",
                        column: x => x.AccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RECOVERY_AUDIT_ActorId",
                table: "RECOVERY_AUDIT",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_RECOVERY_AUDIT_TargetId_OccurredAt",
                table: "RECOVERY_AUDIT",
                columns: new[] { "TargetId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RECOVERY_OUTBOX_AccountId_OccurredAt",
                table: "RECOVERY_OUTBOX",
                columns: new[] { "AccountId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RECOVERY_TOKEN_AccountId_CreatedAt",
                table: "RECOVERY_TOKEN",
                columns: new[] { "AccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RECOVERY_TOKEN_AccountId_UsedAt",
                table: "RECOVERY_TOKEN",
                columns: new[] { "AccountId", "UsedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RECOVERY_TOKEN_ExpiresAt",
                table: "RECOVERY_TOKEN",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RECOVERY_AUDIT");

            migrationBuilder.DropTable(
                name: "RECOVERY_OUTBOX");

            migrationBuilder.DropTable(
                name: "RECOVERY_TOKEN");
        }
    }
}

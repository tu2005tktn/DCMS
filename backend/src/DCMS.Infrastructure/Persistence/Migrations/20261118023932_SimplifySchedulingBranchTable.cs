using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifySchedulingBranchTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BRANCH_SLOT_INTERVAL",
                table: "SCHEDULING_BRANCH");

            migrationBuilder.Sql("UPDATE \"SCHEDULING_BRANCH\" SET \"Version\" = GREATEST(\"Version\", \"BookingSettingsVersion\", 1);");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "SCHEDULING_BRANCH");

            migrationBuilder.DropColumn(
                name: "Assets",
                table: "SCHEDULING_BRANCH");

            migrationBuilder.DropColumn(
                name: "BankCode",
                table: "SCHEDULING_BRANCH");

            migrationBuilder.DropColumn(
                name: "BookingSettingsVersion",
                table: "SCHEDULING_BRANCH");

            migrationBuilder.DropColumn(
                name: "LogoAssetId",
                table: "SCHEDULING_BRANCH");

            migrationBuilder.DropColumn(
                name: "PayeeName",
                table: "SCHEDULING_BRANCH");

            migrationBuilder.DropColumn(
                name: "StorageStamp",
                table: "SCHEDULING_BRANCH");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "SCHEDULING_BRANCH");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "SCHEDULING_BRANCH");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "SCHEDULING_BRANCH",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BRANCH_SLOT_INTERVAL",
                table: "SCHEDULING_BRANCH",
                sql: "\"SlotIntervalMinutes\" BETWEEN 1 AND 120 AND \"Version\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BRANCH_SLOT_INTERVAL",
                table: "SCHEDULING_BRANCH");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                table: "SCHEDULING_BRANCH",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "SCHEDULING_BRANCH",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Assets",
                table: "SCHEDULING_BRANCH",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "BankCode",
                table: "SCHEDULING_BRANCH",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BookingSettingsVersion",
                table: "SCHEDULING_BRANCH",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "LogoAssetId",
                table: "SCHEDULING_BRANCH",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayeeName",
                table: "SCHEDULING_BRANCH",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StorageStamp",
                table: "SCHEDULING_BRANCH",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "SCHEDULING_BRANCH",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                defaultValue: "Asia/Ho_Chi_Minh");

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "SCHEDULING_BRANCH",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.Sql("UPDATE \"SCHEDULING_BRANCH\" SET \"BookingSettingsVersion\" = GREATEST(\"Version\", 1), \"TimeZoneId\" = COALESCE(\"TimeZoneId\", 'Asia/Ho_Chi_Minh');");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BRANCH_SLOT_INTERVAL",
                table: "SCHEDULING_BRANCH",
                sql: "\"SlotIntervalMinutes\" BETWEEN 1 AND 120 AND \"BookingSettingsVersion\" > 0");
        }
    }
}

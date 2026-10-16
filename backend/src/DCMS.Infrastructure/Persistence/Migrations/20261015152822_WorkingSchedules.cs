using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkingSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SCHEDULE_RECEIPT",
                columns: table => new
                {
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCHEDULE_RECEIPT", x => new { x.ActorId, x.RequestKey });
                    table.ForeignKey(
                        name: "FK_SCHEDULE_RECEIPT_USER_ACCOUNT_ActorId",
                        column: x => x.ActorId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SCHEDULING_BRANCH",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCHEDULING_BRANCH", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SCHEDULING_CHAIR",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    OpenMinute = table.Column<int>(type: "integer", nullable: false),
                    CloseMinute = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCHEDULING_CHAIR", x => x.Id);
                    table.UniqueConstraint("AK_SCHEDULING_CHAIR_Id_BranchId", x => new { x.Id, x.BranchId });
                    table.CheckConstraint("CK_CHAIR_HOURS", "\"OpenMinute\" >= 0 AND \"CloseMinute\" <= 1440 AND \"OpenMinute\" < \"CloseMinute\" AND \"OpenMinute\" % 15 = 0 AND \"CloseMinute\" % 15 = 0");
                    table.ForeignKey(
                        name: "FK_SCHEDULING_CHAIR_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SCHEDULING_MEMBER",
                columns: table => new
                {
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CanDentist = table.Column<bool>(type: "boolean", nullable: false),
                    CanManageSchedules = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCHEDULING_MEMBER", x => new { x.BranchId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_SCHEDULING_MEMBER_EMPLOYEE_PROFILE_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "EMPLOYEE_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SCHEDULING_MEMBER_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SCHEDULING_SERVICE",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PublicDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCHEDULING_SERVICE", x => x.Id);
                    table.UniqueConstraint("AK_SCHEDULING_SERVICE_Id_BranchId", x => new { x.Id, x.BranchId });
                    table.CheckConstraint("CK_SERVICE_DURATION", "\"PublicDurationMinutes\" IN (30,60)");
                    table.ForeignKey(
                        name: "FK_SCHEDULING_SERVICE_SCHEDULING_BRANCH_BranchId",
                        column: x => x.BranchId,
                        principalTable: "SCHEDULING_BRANCH",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WORKING_WEEK",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PublishedBy = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WORKING_WEEK", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WORKING_WEEK_SCHEDULING_MEMBER_BranchId_EmployeeId",
                        columns: x => new { x.BranchId, x.EmployeeId },
                        principalTable: "SCHEDULING_MEMBER",
                        principalColumns: new[] { "BranchId", "EmployeeId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WORKING_WEEK_USER_ACCOUNT_PublishedBy",
                        column: x => x.PublishedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RESOURCE_RESERVATION",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    DentistId = table.Column<int>(type: "integer", nullable: false),
                    AssistantId = table.Column<int>(type: "integer", nullable: true),
                    ChairId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    Start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttendeeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    AttendeeLabel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RESOURCE_RESERVATION", x => x.Id);
                    table.CheckConstraint("CK_RESERVATION_RANGE", "\"Start\" < \"End\" AND (\"AssistantId\" IS NULL OR \"AssistantId\" <> \"DentistId\")");
                    table.ForeignKey(
                        name: "FK_RESOURCE_RESERVATION_EMPLOYEE_PROFILE_AssistantId",
                        column: x => x.AssistantId,
                        principalTable: "EMPLOYEE_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RESOURCE_RESERVATION_SCHEDULING_CHAIR_ChairId_BranchId",
                        columns: x => new { x.ChairId, x.BranchId },
                        principalTable: "SCHEDULING_CHAIR",
                        principalColumns: new[] { "Id", "BranchId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RESOURCE_RESERVATION_SCHEDULING_MEMBER_BranchId_DentistId",
                        columns: x => new { x.BranchId, x.DentistId },
                        principalTable: "SCHEDULING_MEMBER",
                        principalColumns: new[] { "BranchId", "EmployeeId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RESOURCE_RESERVATION_SCHEDULING_SERVICE_ServiceId_BranchId",
                        columns: x => new { x.ServiceId, x.BranchId },
                        principalTable: "SCHEDULING_SERVICE",
                        principalColumns: new[] { "Id", "BranchId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WORKING_PERIOD",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    StartMinute = table.Column<int>(type: "integer", nullable: false),
                    EndMinute = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WORKING_PERIOD", x => x.Id);
                    table.CheckConstraint("CK_PERIOD_RANGE", "\"Day\" BETWEEN 0 AND 6 AND \"StartMinute\" >= 0 AND \"EndMinute\" <= 1440 AND \"StartMinute\" < \"EndMinute\" AND \"StartMinute\" % 15 = 0 AND \"EndMinute\" % 15 = 0 AND \"Kind\" IN ('Work','Break','Block')");
                    table.ForeignKey(
                        name: "FK_WORKING_PERIOD_WORKING_WEEK_WeekId",
                        column: x => x.WeekId,
                        principalTable: "WORKING_WEEK",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RESOURCE_RESERVATION_AssistantId",
                table: "RESOURCE_RESERVATION",
                column: "AssistantId");

            migrationBuilder.CreateIndex(
                name: "IX_RESOURCE_RESERVATION_BranchId_DentistId",
                table: "RESOURCE_RESERVATION",
                columns: new[] { "BranchId", "DentistId" });

            migrationBuilder.CreateIndex(
                name: "IX_RESOURCE_RESERVATION_ChairId_BranchId",
                table: "RESOURCE_RESERVATION",
                columns: new[] { "ChairId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_RESOURCE_RESERVATION_Code",
                table: "RESOURCE_RESERVATION",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RESOURCE_RESERVATION_ServiceId_BranchId",
                table: "RESOURCE_RESERVATION",
                columns: new[] { "ServiceId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_RESOURCE_RESERVATION_Start_End",
                table: "RESOURCE_RESERVATION",
                columns: new[] { "Start", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_SCHEDULING_CHAIR_BranchId",
                table: "SCHEDULING_CHAIR",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SCHEDULING_MEMBER_EmployeeId",
                table: "SCHEDULING_MEMBER",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SCHEDULING_SERVICE_BranchId",
                table: "SCHEDULING_SERVICE",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_WORKING_PERIOD_WeekId",
                table: "WORKING_PERIOD",
                column: "WeekId");

            migrationBuilder.CreateIndex(
                name: "IX_WORKING_WEEK_BranchId_EmployeeId_WeekStart",
                table: "WORKING_WEEK",
                columns: new[] { "BranchId", "EmployeeId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WORKING_WEEK_PublishedBy",
                table: "WORKING_WEEK",
                column: "PublishedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RESOURCE_RESERVATION");

            migrationBuilder.DropTable(
                name: "SCHEDULE_RECEIPT");

            migrationBuilder.DropTable(
                name: "WORKING_PERIOD");

            migrationBuilder.DropTable(
                name: "SCHEDULING_CHAIR");

            migrationBuilder.DropTable(
                name: "SCHEDULING_SERVICE");

            migrationBuilder.DropTable(
                name: "WORKING_WEEK");

            migrationBuilder.DropTable(
                name: "SCHEDULING_MEMBER");

            migrationBuilder.DropTable(
                name: "SCHEDULING_BRANCH");
        }
    }
}

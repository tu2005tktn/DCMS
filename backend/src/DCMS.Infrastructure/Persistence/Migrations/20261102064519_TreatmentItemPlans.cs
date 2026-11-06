using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TreatmentItemPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefinitionKind",
                table: "TREATMENT_TEMPLATE",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "LegacySteps");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "TREATMENT_TEMPLATE",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TREATMENT_TEMPLATE_ITEM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    TeethJson = table.Column<string>(type: "jsonb", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DentistId = table.Column<int>(type: "integer", nullable: false),
                    DentistName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssistantId = table.Column<int>(type: "integer", nullable: true),
                    AssistantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CostVnd = table.Column<long>(type: "bigint", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TREATMENT_TEMPLATE_ITEM", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TREATMENT_TEMPLATE_ITEM_EMPLOYEE_PROFILE_AssistantId",
                        column: x => x.AssistantId,
                        principalTable: "EMPLOYEE_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TREATMENT_TEMPLATE_ITEM_EMPLOYEE_PROFILE_DentistId",
                        column: x => x.DentistId,
                        principalTable: "EMPLOYEE_PROFILE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TREATMENT_TEMPLATE_ITEM_SCHEDULING_SERVICE_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "SCHEDULING_SERVICE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TREATMENT_TEMPLATE_ITEM_TREATMENT_TEMPLATE_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "TREATMENT_TEMPLATE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_TEMPLATE_ITEM_AssistantId",
                table: "TREATMENT_TEMPLATE_ITEM",
                column: "AssistantId");

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_TEMPLATE_ITEM_DentistId",
                table: "TREATMENT_TEMPLATE_ITEM",
                column: "DentistId");

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_TEMPLATE_ITEM_ServiceId",
                table: "TREATMENT_TEMPLATE_ITEM",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_TREATMENT_TEMPLATE_ITEM_TemplateId_Position",
                table: "TREATMENT_TEMPLATE_ITEM",
                columns: new[] { "TemplateId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TREATMENT_TEMPLATE_ITEM");

            migrationBuilder.DropColumn(
                name: "DefinitionKind",
                table: "TREATMENT_TEMPLATE");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TREATMENT_TEMPLATE");
        }
    }
}

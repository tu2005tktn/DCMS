using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClinicalInvestigationOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InvestigationId",
                table: "CLINICAL_DOCUMENT",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "INVESTIGATION_ORDER",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Region = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequiredForDiagnosis = table.Column<bool>(type: "boolean", nullable: false),
                    State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ResultText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResultDocumentIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Interpretation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResultBy = table.Column<int>(type: "integer", nullable: true),
                    ResultAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<int>(type: "integer", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreateRequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INVESTIGATION_ORDER", x => x.Id);
                    table.CheckConstraint("CK_INVESTIGATION_ORDER_STATE", "\"State\" IN ('Ordered','ResultAvailable','Reviewed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_INVESTIGATION_ORDER_USER_ACCOUNT_CancelledBy",
                        column: x => x.CancelledBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INVESTIGATION_ORDER_USER_ACCOUNT_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INVESTIGATION_ORDER_USER_ACCOUNT_ResultBy",
                        column: x => x.ResultBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INVESTIGATION_ORDER_USER_ACCOUNT_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INVESTIGATION_ORDER_VISIT_VisitId",
                        column: x => x.VisitId,
                        principalTable: "VISIT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CLINICAL_DOCUMENT_InvestigationId",
                table: "CLINICAL_DOCUMENT",
                column: "InvestigationId");

            migrationBuilder.CreateIndex(
                name: "IX_INVESTIGATION_ORDER_CancelledBy",
                table: "INVESTIGATION_ORDER",
                column: "CancelledBy");

            migrationBuilder.CreateIndex(
                name: "IX_INVESTIGATION_ORDER_CreatedBy_CreateRequestKey",
                table: "INVESTIGATION_ORDER",
                columns: new[] { "CreatedBy", "CreateRequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INVESTIGATION_ORDER_ResultBy",
                table: "INVESTIGATION_ORDER",
                column: "ResultBy");

            migrationBuilder.CreateIndex(
                name: "IX_INVESTIGATION_ORDER_ReviewedBy",
                table: "INVESTIGATION_ORDER",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_INVESTIGATION_ORDER_VisitId_State",
                table: "INVESTIGATION_ORDER",
                columns: new[] { "VisitId", "State" });

            migrationBuilder.AddForeignKey(
                name: "FK_CLINICAL_DOCUMENT_INVESTIGATION_ORDER_InvestigationId",
                table: "CLINICAL_DOCUMENT",
                column: "InvestigationId",
                principalTable: "INVESTIGATION_ORDER",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CLINICAL_DOCUMENT_INVESTIGATION_ORDER_InvestigationId",
                table: "CLINICAL_DOCUMENT");

            migrationBuilder.DropTable(
                name: "INVESTIGATION_ORDER");

            migrationBuilder.DropIndex(
                name: "IX_CLINICAL_DOCUMENT_InvestigationId",
                table: "CLINICAL_DOCUMENT");

            migrationBuilder.DropColumn(
                name: "InvestigationId",
                table: "CLINICAL_DOCUMENT");
        }
    }
}

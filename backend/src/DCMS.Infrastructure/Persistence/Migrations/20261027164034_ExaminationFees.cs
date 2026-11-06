using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExaminationFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EXAMINATION_FEE",
                columns: table => new
                {
                    VisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: true),
                    ServiceName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AmountVnd = table.Column<long>(type: "bigint", nullable: true),
                    PriceVersion = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PriceSource = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    ChargeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PostedBy = table.Column<int>(type: "integer", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXAMINATION_FEE", x => x.VisitId);
                    table.ForeignKey(
                        name: "FK_EXAMINATION_FEE_FINANCE_CHARGE_ChargeId",
                        column: x => x.ChargeId,
                        principalTable: "FINANCE_CHARGE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EXAMINATION_FEE_SCHEDULING_SERVICE_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "SCHEDULING_SERVICE",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EXAMINATION_FEE_USER_ACCOUNT_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EXAMINATION_FEE_USER_ACCOUNT_PostedBy",
                        column: x => x.PostedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EXAMINATION_FEE_VISIT_VisitId",
                        column: x => x.VisitId,
                        principalTable: "VISIT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EXAMINATION_FEE_ChargeId",
                table: "EXAMINATION_FEE",
                column: "ChargeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EXAMINATION_FEE_CreatedBy",
                table: "EXAMINATION_FEE",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_EXAMINATION_FEE_PostedBy",
                table: "EXAMINATION_FEE",
                column: "PostedBy");

            migrationBuilder.CreateIndex(
                name: "IX_EXAMINATION_FEE_ServiceId",
                table: "EXAMINATION_FEE",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_EXAMINATION_FEE_State",
                table: "EXAMINATION_FEE",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EXAMINATION_FEE");
        }
    }
}

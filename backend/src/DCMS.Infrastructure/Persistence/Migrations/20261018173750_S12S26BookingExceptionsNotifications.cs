using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S12S26BookingExceptionsNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfirmedToAttend",
                table: "APPOINTMENT",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateTable(
                name: "APPOINTMENT_REMINDER",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentVersion = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    LeadMinutes = table.Column<int>(type: "integer", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APPOINTMENT_REMINDER", x => x.Id);
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_REMINDER_APPOINTMENT_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "APPOINTMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_REMINDER_USER_ACCOUNT_AccountId",
                        column: x => x.AccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "APPOINTMENT_SUPPORT_REQUEST",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterAccountId = table.Column<int>(type: "integer", nullable: true),
                    RequesterName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RequesterPhone = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProposedStartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProposedReason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AppointmentActionRequestKey = table.Column<Guid>(type: "uuid", nullable: true),
                    RequesterVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequesterVerifiedBy = table.Column<int>(type: "integer", nullable: true),
                    RequesterVerification = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PublicNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APPOINTMENT_SUPPORT_REQUEST", x => x.Id);
                    table.CheckConstraint("CK_SUPPORT_REQUEST_STATUS", "\"Status\" IN ('Pending','Resolved','Rejected','Withdrawn')");
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_SUPPORT_REQUEST_APPOINTMENT_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "APPOINTMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_SUPPORT_REQUEST_USER_ACCOUNT_DecidedBy",
                        column: x => x.DecidedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_SUPPORT_REQUEST_USER_ACCOUNT_RequesterAccountId",
                        column: x => x.RequesterAccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_SUPPORT_REQUEST_USER_ACCOUNT_RequesterVerifiedBy",
                        column: x => x.RequesterVerifiedBy,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "APPOINTMENT_SUPPORT_HISTORY",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorAccountId = table.Column<int>(type: "integer", nullable: true),
                    ActorKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APPOINTMENT_SUPPORT_HISTORY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_SUPPORT_HISTORY_APPOINTMENT_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "APPOINTMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_SUPPORT_HISTORY_APPOINTMENT_SUPPORT_REQUEST_Req~",
                        column: x => x.RequestId,
                        principalTable: "APPOINTMENT_SUPPORT_REQUEST",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_APPOINTMENT_SUPPORT_HISTORY_USER_ACCOUNT_ActorAccountId",
                        column: x => x.ActorAccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IN_APP_NOTIFICATION",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: true),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeepLink = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    HasExternalFailure = table.Column<bool>(type: "boolean", nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IN_APP_NOTIFICATION", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IN_APP_NOTIFICATION_APPOINTMENT_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "APPOINTMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IN_APP_NOTIFICATION_APPOINTMENT_SUPPORT_REQUEST_RequestId",
                        column: x => x.RequestId,
                        principalTable: "APPOINTMENT_SUPPORT_REQUEST",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IN_APP_NOTIFICATION_USER_ACCOUNT_AccountId",
                        column: x => x.AccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SUPPORT_REQUEST_RECEIPT",
                columns: table => new
                {
                    ActorAccountId = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SupportRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SUPPORT_REQUEST_RECEIPT", x => new { x.ActorAccountId, x.RequestKey });
                    table.ForeignKey(
                        name: "FK_SUPPORT_REQUEST_RECEIPT_APPOINTMENT_SUPPORT_REQUEST_Support~",
                        column: x => x.SupportRequestId,
                        principalTable: "APPOINTMENT_SUPPORT_REQUEST",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SUPPORT_REQUEST_RECEIPT_USER_ACCOUNT_ActorAccountId",
                        column: x => x.ActorAccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NOTIFICATION_DELIVERY",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOTIFICATION_DELIVERY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NOTIFICATION_DELIVERY_IN_APP_NOTIFICATION_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "IN_APP_NOTIFICATION",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "STAFF_HANDLING_ITEM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STAFF_HANDLING_ITEM", x => x.Id);
                    table.ForeignKey(
                        name: "FK_STAFF_HANDLING_ITEM_APPOINTMENT_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "APPOINTMENT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_STAFF_HANDLING_ITEM_IN_APP_NOTIFICATION_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "IN_APP_NOTIFICATION",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_REMINDER_AccountId",
                table: "APPOINTMENT_REMINDER",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_REMINDER_AppointmentId_AppointmentVersion_LeadM~",
                table: "APPOINTMENT_REMINDER",
                columns: new[] { "AppointmentId", "AppointmentVersion", "LeadMinutes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_REMINDER_Status_DueAt",
                table: "APPOINTMENT_REMINDER",
                columns: new[] { "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_SUPPORT_HISTORY_ActorAccountId",
                table: "APPOINTMENT_SUPPORT_HISTORY",
                column: "ActorAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_SUPPORT_HISTORY_AppointmentId",
                table: "APPOINTMENT_SUPPORT_HISTORY",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_SUPPORT_HISTORY_RequestId_OccurredAt",
                table: "APPOINTMENT_SUPPORT_HISTORY",
                columns: new[] { "RequestId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_SUPPORT_REQUEST_AppointmentId",
                table: "APPOINTMENT_SUPPORT_REQUEST",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_SUPPORT_REQUEST_Code",
                table: "APPOINTMENT_SUPPORT_REQUEST",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_SUPPORT_REQUEST_DecidedBy",
                table: "APPOINTMENT_SUPPORT_REQUEST",
                column: "DecidedBy");

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_SUPPORT_REQUEST_RequesterAccountId",
                table: "APPOINTMENT_SUPPORT_REQUEST",
                column: "RequesterAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_SUPPORT_REQUEST_RequesterVerifiedBy",
                table: "APPOINTMENT_SUPPORT_REQUEST",
                column: "RequesterVerifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_SUPPORT_REQUEST_RequestKey",
                table: "APPOINTMENT_SUPPORT_REQUEST",
                column: "RequestKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_APPOINTMENT_SUPPORT_REQUEST_Status_UpdatedAt",
                table: "APPOINTMENT_SUPPORT_REQUEST",
                columns: new[] { "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IN_APP_NOTIFICATION_AccountId_CreatedAt",
                table: "IN_APP_NOTIFICATION",
                columns: new[] { "AccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IN_APP_NOTIFICATION_AppointmentId",
                table: "IN_APP_NOTIFICATION",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IN_APP_NOTIFICATION_DedupeKey",
                table: "IN_APP_NOTIFICATION",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IN_APP_NOTIFICATION_PublicId",
                table: "IN_APP_NOTIFICATION",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IN_APP_NOTIFICATION_RequestId",
                table: "IN_APP_NOTIFICATION",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATION_DELIVERY_NotificationId_Channel",
                table: "NOTIFICATION_DELIVERY",
                columns: new[] { "NotificationId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATION_DELIVERY_Status_NextRetryAt",
                table: "NOTIFICATION_DELIVERY",
                columns: new[] { "Status", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_STAFF_HANDLING_ITEM_AppointmentId",
                table: "STAFF_HANDLING_ITEM",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_STAFF_HANDLING_ITEM_NotificationId",
                table: "STAFF_HANDLING_ITEM",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_STAFF_HANDLING_ITEM_Status_CreatedAt",
                table: "STAFF_HANDLING_ITEM",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SUPPORT_REQUEST_RECEIPT_SupportRequestId",
                table: "SUPPORT_REQUEST_RECEIPT",
                column: "SupportRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "APPOINTMENT_REMINDER");

            migrationBuilder.DropTable(
                name: "APPOINTMENT_SUPPORT_HISTORY");

            migrationBuilder.DropTable(
                name: "NOTIFICATION_DELIVERY");

            migrationBuilder.DropTable(
                name: "STAFF_HANDLING_ITEM");

            migrationBuilder.DropTable(
                name: "SUPPORT_REQUEST_RECEIPT");

            migrationBuilder.DropTable(
                name: "IN_APP_NOTIFICATION");

            migrationBuilder.DropTable(
                name: "APPOINTMENT_SUPPORT_REQUEST");

            migrationBuilder.DropColumn(
                name: "ConfirmedToAttend",
                table: "APPOINTMENT");
        }
    }
}

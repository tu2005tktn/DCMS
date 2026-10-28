using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SystemSettingsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ACCESS_GROUP",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BaseRole = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCESS_GROUP", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "REFERENCE_ENTRY",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    NormalizedCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Reserved = table.Column<bool>(type: "boolean", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REFERENCE_ENTRY", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SETTINGS_AUDIT",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    Section = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Target = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: false),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SETTINGS_AUDIT", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SETTINGS_AUDIT_USER_ACCOUNT_ActorId",
                        column: x => x.ActorId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SETTINGS_RECEIPT",
                columns: table => new
                {
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SETTINGS_RECEIPT", x => new { x.ActorId, x.RequestKey });
                    table.ForeignKey(
                        name: "FK_SETTINGS_RECEIPT_USER_ACCOUNT_ActorId",
                        column: x => x.ActorId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SETTINGS_VERSION",
                columns: table => new
                {
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SETTINGS_VERSION", x => x.Version);
                    table.ForeignKey(
                        name: "FK_SETTINGS_VERSION_USER_ACCOUNT_ActorId",
                        column: x => x.ActorId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACCESS_GROUP_GRANT",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCESS_GROUP_GRANT", x => new { x.GroupId, x.Permission });
                    table.ForeignKey(
                        name: "FK_ACCESS_GROUP_GRANT_ACCESS_GROUP_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ACCESS_GROUP",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "STAFF_ACCESS_ASSIGNMENT",
                columns: table => new
                {
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchIds = table.Column<int[]>(type: "integer[]", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STAFF_ACCESS_ASSIGNMENT", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_STAFF_ACCESS_ASSIGNMENT_ACCESS_GROUP_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ACCESS_GROUP",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_STAFF_ACCESS_ASSIGNMENT_USER_ACCOUNT_AccountId",
                        column: x => x.AccountId,
                        principalTable: "USER_ACCOUNT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ACCESS_GROUP_NormalizedCode",
                table: "ACCESS_GROUP",
                column: "NormalizedCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACCESS_GROUP_NormalizedName",
                table: "ACCESS_GROUP",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REFERENCE_ENTRY_Kind_Active",
                table: "REFERENCE_ENTRY",
                columns: new[] { "Kind", "Active" });

            migrationBuilder.CreateIndex(
                name: "IX_REFERENCE_ENTRY_Kind_NormalizedCode",
                table: "REFERENCE_ENTRY",
                columns: new[] { "Kind", "NormalizedCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REFERENCE_ENTRY_Kind_NormalizedName",
                table: "REFERENCE_ENTRY",
                columns: new[] { "Kind", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SETTINGS_AUDIT_ActorId",
                table: "SETTINGS_AUDIT",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_SETTINGS_AUDIT_Section_At",
                table: "SETTINGS_AUDIT",
                columns: new[] { "Section", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_SETTINGS_VERSION_ActorId",
                table: "SETTINGS_VERSION",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_STAFF_ACCESS_ASSIGNMENT_GroupId",
                table: "STAFF_ACCESS_ASSIGNMENT",
                column: "GroupId");

            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('9630c6c1-9075-51d5-a1d1-88d297becc4e','diagnoses','Áp-xe','ÁP-XE',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('36fa869a-4bc5-53b0-8140-b8954191c110','diagnoses','Biến chứng sau nhổ răng/tiểu phẫu','BIẾN CHỨNG SAU NHỔ RĂNG/TIỂU PHẪU',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('a8fc893c-95e3-5926-bc7c-768ac136b649','diagnoses','Chân răng bị hư/hỏng','CHÂN RĂNG BỊ HƯ/HỎNG',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('b60c5c16-3ada-5f00-8b0f-9e28551a9f19','diagnoses','Chấn thương khớp cắn','CHẤN THƯƠNG KHỚP CẮN',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('fa07d140-4bf2-598b-a228-0c9eb9f50ed0','diagnoses','Chấn thương răng','CHẤN THƯƠNG RĂNG',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('5807d3eb-bffa-5342-9592-ba0f16838765','diagnoses','Đau khớp hàm','ĐAU KHỚP HÀM',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('198adee2-a0aa-5131-890b-bebadb3bbb3d','diagnoses','Đau nhức răng','ĐAU NHỨC RĂNG',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('1f09e8ec-21ef-54df-a518-9672a27fbf13','diagnoses','Hàm răng không đều đặn','HÀM RĂNG KHÔNG ĐỀU ĐẶN',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('36648c55-66e3-54ea-b9a6-a38907e92c80','diagnoses','Mắc cài/khí cụ niềng răng bị hư hỏng, mất','MẮC CÀI/KHÍ CỤ NIỀNG RĂNG BỊ HƯ HỎNG, MẤT',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('220d7a14-0dec-5d56-a8aa-3b5f91e9d446','diagnoses','Mất răng','MẤT RĂNG',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('976ecb60-1862-5278-ae01-3f71e643f177','diagnoses','Miếng trám/hàn cũ bị hư hỏng/đổi màu','MIẾNG TRÁM/HÀN CŨ BỊ HƯ HỎNG/ĐỔI MÀU',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('fa55401f-b8bb-5e7f-88e8-70aa10cc6195','diagnoses','Mòn cổ răng','MÒN CỔ RĂNG',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('d26e3f19-7d85-50ea-8fa3-47d0a92ccee7','diagnoses','Nang quanh chóp răng','NANG QUANH CHÓP RĂNG',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('e0a8f978-35f7-58ce-8200-d8f5b3b05658','diagnoses','Nhiễm trùng ổ răng','NHIỄM TRÙNG Ổ RĂNG',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('2db29d14-0fac-5e9b-9bd0-e368e9ebe5f6','diagnoses','Răng bám vôi/cao','RĂNG BÁM VÔI/CAO',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('c4c6beaa-4184-5146-823f-1f580e56ba68','diagnoses','Răng bị ê buốt','RĂNG BỊ Ê BUỐT',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('2341493b-ab92-59a7-852d-c7d8e8ad635f','diagnoses','Răng bị lung lay','RĂNG BỊ LUNG LAY',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('4c811b9a-b427-5a29-aaf9-4affca3772be','diagnoses','Răng bị mẻ, vỡ','RĂNG BỊ MẺ, VỠ',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('70854e6e-c2ad-512c-b06d-1ef68c0ccfb8','diagnoses','Răng bị nhiễm fluor','RĂNG BỊ NHIỄM FLUOR',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('f21cc7d3-73a0-5280-ad23-d45416b1c31d','diagnoses','Răng bị nhiễm màu, ố','RĂNG BỊ NHIỄM MÀU, Ố',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('d36bc65b-89f6-56d2-a12a-9fdfc60cde96','diagnoses','Răng bị nhiễm tetra','RĂNG BỊ NHIỄM TETRA',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('72672780-6419-5437-9ce9-07abf81533b9','diagnoses','Răng bị nhô (vẩu, hô)','RĂNG BỊ NHÔ (VẨU, HÔ)',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('f6dffd5e-2527-5dde-8bee-4384c5de111d','diagnoses','Răng bị nứt/tét','RĂNG BỊ NỨT/TÉT',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('683c5f80-e563-530d-aa81-9cd963f146e6','diagnoses','Răng giả hư hỏng, lỏng, gãy vỡ, mất','RĂNG GIẢ HƯ HỎNG, LỎNG, GÃY VỠ, MẤT',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('a8e2c8ca-009b-51fc-8a79-7bcb72e0aad7','diagnoses','Răng khôn mọc lệch/ngầm','RĂNG KHÔN MỌC LỆCH/NGẦM',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('ccb6a84d-d0d9-55f4-b078-50eb00b4b15f','diagnoses','Răng mọc thưa','RĂNG MỌC THƯA',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('7d467d79-2687-52fc-a5ea-3bfc188c10ad','diagnoses','Sâu răng','SÂU RĂNG',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('67ac6ae1-3050-529c-8051-c92f4a3054bd','diagnoses','Tủy răng bị hoại tử','TỦY RĂNG BỊ HOẠI TỬ',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('0bb54a97-e47f-51c2-958e-c5149e034d54','diagnoses','Viêm lợi, nướu','VIÊM LỢI, NƯỚU',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('b9e09521-39ac-55cf-ad2d-9b29b7721352','diagnoses','Viêm nha chu','VIÊM NHA CHU',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('e7b43899-62bd-5912-bd6b-50e2c5164263','diagnoses','Viêm ổ răng khô','VIÊM Ổ RĂNG KHÔ',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('ce881686-221b-58de-a9c0-d75281dec1ad','diagnoses','Viêm quanh chóp răng','VIÊM QUANH CHÓP RĂNG',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('29da4f53-a803-56d2-aafd-c5a59cb75e36','diagnoses','Viêm tủy răng','VIÊM TỦY RĂNG',true,false,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP\" (\"Id\",\"Code\",\"Name\",\"NormalizedCode\",\"NormalizedName\",\"BaseRole\",\"Active\",\"Version\") VALUES ('173f6d78-96ec-594a-beaa-50c9382842c4','Doctors','Doctors','DOCTORS','DOCTORS','Dentist',true,1) ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('173f6d78-96ec-594a-beaa-50c9382842c4','patients.read') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('173f6d78-96ec-594a-beaa-50c9382842c4','reception.read') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('173f6d78-96ec-594a-beaa-50c9382842c4','examinations.read') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('173f6d78-96ec-594a-beaa-50c9382842c4','examinations.manage') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('173f6d78-96ec-594a-beaa-50c9382842c4','appointments.support.handle') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP\" (\"Id\",\"Code\",\"Name\",\"NormalizedCode\",\"NormalizedName\",\"BaseRole\",\"Active\",\"Version\") VALUES ('a3684ff0-0ce1-5139-8537-f780bc405d59','Assistants','Assistants','ASSISTANTS','ASSISTANTS','ClinicAssistant',true,1) ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('a3684ff0-0ce1-5139-8537-f780bc405d59','patients.read') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('a3684ff0-0ce1-5139-8537-f780bc405d59','patients.manage') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('a3684ff0-0ce1-5139-8537-f780bc405d59','reception.read') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('a3684ff0-0ce1-5139-8537-f780bc405d59','reception.check-in') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('a3684ff0-0ce1-5139-8537-f780bc405d59','examinations.read') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('a3684ff0-0ce1-5139-8537-f780bc405d59','patient-links.verify') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('a3684ff0-0ce1-5139-8537-f780bc405d59','appointments.support.handle') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP\" (\"Id\",\"Code\",\"Name\",\"NormalizedCode\",\"NormalizedName\",\"BaseRole\",\"Active\",\"Version\") VALUES ('90f80fc7-29d1-538a-ac45-b05b198c5742','Receptionists','Receptionists','RECEPTIONISTS','RECEPTIONISTS','ClinicAssistant',true,1) ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('90f80fc7-29d1-538a-ac45-b05b198c5742','patients.read') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('90f80fc7-29d1-538a-ac45-b05b198c5742','patients.manage') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('90f80fc7-29d1-538a-ac45-b05b198c5742','reception.read') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('90f80fc7-29d1-538a-ac45-b05b198c5742','reception.check-in') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('90f80fc7-29d1-538a-ac45-b05b198c5742','patient-links.verify') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP_GRANT\" (\"GroupId\",\"Permission\") VALUES ('90f80fc7-29d1-538a-ac45-b05b198c5742','appointments.support.handle') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"ACCESS_GROUP\" (\"Id\",\"Code\",\"Name\",\"NormalizedCode\",\"NormalizedName\",\"BaseRole\",\"Active\",\"Version\") VALUES ('899ad509-fbff-5b18-a5cb-9b5e066b713b','Accountants','Accountants','ACCOUNTANTS','ACCOUNTANTS','ClinicAssistant',true,1) ON CONFLICT DO NOTHING;");

            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Code\",\"NormalizedCode\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('a158c8eb-4513-5435-8f6f-65b443e2aac4','expense-categories','Supplier','SUPPLIER','Chi nhà cung cấp','CHI NHÀ CUNG CẤP',true,true,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Code\",\"NormalizedCode\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('780d9a72-7507-5484-8893-de236b48ced9','expense-categories','Lab','LAB','Chi labo','CHI LABO',true,true,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
            migrationBuilder.Sql("INSERT INTO \"REFERENCE_ENTRY\" (\"Id\",\"Kind\",\"Code\",\"NormalizedCode\",\"Name\",\"NormalizedName\",\"Active\",\"Reserved\",\"Version\",\"CreatedBy\",\"CreatedAt\") VALUES ('cf1ed00f-e09d-554a-b97f-64d1dd0180fb','expense-categories','Payroll','PAYROLL','Chi lương','CHI LƯƠNG',true,true,1,0,TIMESTAMPTZ '2026-10-27 00:00:00+00') ON CONFLICT DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ACCESS_GROUP_GRANT");

            migrationBuilder.DropTable(
                name: "REFERENCE_ENTRY");

            migrationBuilder.DropTable(
                name: "SETTINGS_AUDIT");

            migrationBuilder.DropTable(
                name: "SETTINGS_RECEIPT");

            migrationBuilder.DropTable(
                name: "SETTINGS_VERSION");

            migrationBuilder.DropTable(
                name: "STAFF_ACCESS_ASSIGNMENT");

            migrationBuilder.DropTable(
                name: "ACCESS_GROUP");
        }
    }
}

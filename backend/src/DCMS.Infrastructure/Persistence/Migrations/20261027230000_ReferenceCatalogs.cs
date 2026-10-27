using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace DCMS.Infrastructure.Persistence.Migrations;
// S58 danh mục tham chiếu. Seeds only the names written in the v2.5 specification (GBR-178); other catalogs are entered by the clinic.
[DbContext(typeof(DcmsDbContext))]
[Migration("20261027230000_ReferenceCatalogs")]
public sealed class ReferenceCatalogs : Migration
{
    private static readonly DateTimeOffset Seeded = new(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("REFERENCE_ENTRY", columns: t => new {
            Id = t.Column<Guid>("uuid", nullable: false), Kind = t.Column<string>("character varying(32)", maxLength: 32, nullable: false),
            Code = t.Column<string>("character varying(40)", maxLength: 40, nullable: true), Name = t.Column<string>("character varying(160)", maxLength: 160, nullable: false),
            NormalizedName = t.Column<string>("character varying(160)", maxLength: 160, nullable: false),
            Description = t.Column<string>("character varying(1000)", maxLength: 1000, nullable: true), IsReserved = t.Column<bool>("boolean", nullable: false),
            IsActive = t.Column<bool>("boolean", nullable: false), Version = t.Column<int>("integer", nullable: false),
            CreatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false), CreatedBy = t.Column<int>("integer", nullable: true),
            UpdatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false), UpdatedBy = t.Column<int>("integer", nullable: true)
        }, constraints: t => t.PrimaryKey("PK_REFERENCE_ENTRY", x => x.Id));
        m.CreateIndex("IX_REFERENCE_ENTRY_Kind_NormalizedName", "REFERENCE_ENTRY", new[] { "Kind", "NormalizedName" }, unique: true);
        m.CreateIndex("IX_REFERENCE_ENTRY_Kind_Code", "REFERENCE_ENTRY", new[] { "Kind", "Code" }, unique: true, filter: "\"Code\" IS NOT NULL");
        m.InsertData("REFERENCE_ENTRY", new[] { "Id", "Kind", "Code", "Name", "NormalizedName", "IsReserved", "IsActive", "Version", "CreatedAt", "UpdatedAt" },
            new[] { "uuid", "character varying(32)", "character varying(40)", "character varying(160)", "character varying(160)", "boolean", "boolean", "integer", "timestamp with time zone", "timestamp with time zone" },
            new object[,]
        {
            { new Guid("58010001-0000-4000-8000-000000000058"), "intake-history", "TS01", "Chảy máu lâu", "CHẢY MÁU LÂU", false, true, 1, Seeded, Seeded },
            { new Guid("58010002-0000-4000-8000-000000000058"), "intake-history", "TS02", "Phản ứng thuốc", "PHẢN ỨNG THUỐC", false, true, 1, Seeded, Seeded },
            { new Guid("58010003-0000-4000-8000-000000000058"), "intake-history", "TS03", "Thấp khớp", "THẤP KHỚP", false, true, 1, Seeded, Seeded },
            { new Guid("58010004-0000-4000-8000-000000000058"), "intake-history", "TS04", "Huyết áp cao", "HUYẾT ÁP CAO", false, true, 1, Seeded, Seeded },
            { new Guid("58010005-0000-4000-8000-000000000058"), "intake-history", "TS05", "Huyết áp thấp", "HUYẾT ÁP THẤP", false, true, 1, Seeded, Seeded },
            { new Guid("58010006-0000-4000-8000-000000000058"), "intake-history", "TS06", "Tiểu đường", "TIỂU ĐƯỜNG", false, true, 1, Seeded, Seeded },
            { new Guid("58010007-0000-4000-8000-000000000058"), "intake-history", "TS07", "Bệnh tim", "BỆNH TIM", false, true, 1, Seeded, Seeded },
            { new Guid("58010008-0000-4000-8000-000000000058"), "intake-history", "TS08", "Bệnh gan", "BỆNH GAN", false, true, 1, Seeded, Seeded },
            { new Guid("58010009-0000-4000-8000-000000000058"), "intake-history", "TS09", "Bệnh phổi", "BỆNH PHỔI", false, true, 1, Seeded, Seeded },
            { new Guid("58020001-0000-4000-8000-000000000058"), "expense-categories", "CHI-VATLIEU", "Dụng cụ - Vật liệu", "DỤNG CỤ - VẬT LIỆU", true, true, 1, Seeded, Seeded },
            { new Guid("58020002-0000-4000-8000-000000000058"), "expense-categories", "CHI-LABO", "Labo", "LABO", true, true, 1, Seeded, Seeded },
            { new Guid("58020003-0000-4000-8000-000000000058"), "expense-categories", "CHI-LUONG", "Trả lương", "TRẢ LƯƠNG", true, true, 1, Seeded, Seeded },
            { new Guid("58020004-0000-4000-8000-000000000058"), "expense-categories", "CHI-VPP", "Văn phòng phẩm", "VĂN PHÒNG PHẨM", false, true, 1, Seeded, Seeded },
            { new Guid("58030001-0000-4000-8000-000000000058"), "patient-sources", "KHAC", "Khác", "KHÁC", false, true, 1, Seeded, Seeded },
        });
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("REFERENCE_ENTRY");
}

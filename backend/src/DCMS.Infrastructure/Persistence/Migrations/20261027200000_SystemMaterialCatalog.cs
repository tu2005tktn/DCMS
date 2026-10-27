using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace DCMS.Infrastructure.Persistence.Migrations;
[DbContext(typeof(DcmsDbContext))]
[Migration("20261027200000_SystemMaterialCatalog")]
public sealed class SystemMaterialCatalog : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("MATERIAL_CATEGORY", columns: t => new {
            Id = t.Column<Guid>("uuid", nullable: false), Name = t.Column<string>("character varying(160)", maxLength:160, nullable:false),
            NormalizedName = t.Column<string>("character varying(160)", maxLength:160, nullable:false), Version = t.Column<int>("integer", nullable:false),
            IsDeleted = t.Column<bool>("boolean", nullable:false), CreatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable:false), UpdatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable:false)
        }, constraints: t => t.PrimaryKey("PK_MATERIAL_CATEGORY", x => x.Id));
        m.CreateTable("MATERIAL_CATALOG_ITEM", columns: t => new {
            Id = t.Column<Guid>("uuid", nullable:false), Name = t.Column<string>("character varying(160)", maxLength:160, nullable:false),
            NormalizedName = t.Column<string>("character varying(160)", maxLength:160, nullable:false), CategoryId = t.Column<Guid>("uuid", nullable:false),
            Version = t.Column<int>("integer", nullable:false), IsDeleted = t.Column<bool>("boolean", nullable:false),
            CreatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable:false), UpdatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable:false)
        }, constraints: t => { t.PrimaryKey("PK_MATERIAL_CATALOG_ITEM", x => x.Id); t.ForeignKey("FK_MATERIAL_CATALOG_ITEM_MATERIAL_CATEGORY_CategoryId", x => x.CategoryId, "MATERIAL_CATEGORY", "Id", onDelete: ReferentialAction.Restrict); });
        m.CreateTable("SYSTEM_CATALOG_CHANGE", columns: t => new {
            RequestKey = t.Column<Guid>("uuid", nullable:false), ActorId = t.Column<int>("integer", nullable:false), Command = t.Column<string>("character varying(48)", maxLength:48, nullable:false),
            ResourceId = t.Column<Guid>("uuid", nullable:false), Fingerprint = t.Column<string>("character varying(64)", maxLength:64, nullable:false),
            BeforeJson = t.Column<string>("jsonb", nullable:true), ResultJson = t.Column<string>("jsonb", nullable:false), OccurredAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable:false)
        }, constraints: t => t.PrimaryKey("PK_SYSTEM_CATALOG_CHANGE", x => x.RequestKey));
        m.CreateIndex("IX_MATERIAL_CATEGORY_NormalizedName", "MATERIAL_CATEGORY", "NormalizedName", unique:true, filter:"NOT \"IsDeleted\"");
        m.CreateIndex("IX_MATERIAL_CATALOG_ITEM_CategoryId_NormalizedName", "MATERIAL_CATALOG_ITEM", new[] { "CategoryId", "NormalizedName" }, unique:true, filter:"NOT \"IsDeleted\"");
        m.CreateIndex("IX_SYSTEM_CATALOG_CHANGE_ResourceId_OccurredAt", "SYSTEM_CATALOG_CHANGE", new[] { "ResourceId", "OccurredAt" });
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("MATERIAL_CATALOG_ITEM"); m.DropTable("SYSTEM_CATALOG_CHANGE"); m.DropTable("MATERIAL_CATEGORY");
    }
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace DCMS.Infrastructure.Persistence.Migrations;
// S58 đợt 4: companies (supplier/lab), lab work types and medicines. No seed: names and prices come from the clinic (Q01).
[DbContext(typeof(DcmsDbContext))]
[Migration("20261028000000_SupplyCatalogs")]
public sealed class SupplyCatalogs : Migration
{
    private static void Identity(MigrationBuilder m, string table)
    {
        m.CreateIndex($"IX_{table}_Code", table, "Code", unique: true);
        m.CreateIndex($"IX_{table}_NormalizedName", table, "NormalizedName", unique: true);
    }
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("CATALOG_PROVIDER", columns: t => new {
            Id = t.Column<Guid>("uuid", nullable: false), Code = t.Column<string>("character varying(40)", maxLength: 40, nullable: false),
            Name = t.Column<string>("character varying(160)", maxLength: 160, nullable: false), NormalizedName = t.Column<string>("character varying(160)", maxLength: 160, nullable: false),
            Type = t.Column<string>("character varying(16)", maxLength: 16, nullable: false), Address = t.Column<string>("character varying(1000)", maxLength: 1000, nullable: true),
            Phone = t.Column<string>("character varying(20)", maxLength: 20, nullable: true), Email = t.Column<string>("character varying(254)", maxLength: 254, nullable: true),
            IsActive = t.Column<bool>("boolean", nullable: false), Version = t.Column<int>("integer", nullable: false),
            CreatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false), CreatedBy = t.Column<int>("integer", nullable: false),
            UpdatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false), UpdatedBy = t.Column<int>("integer", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_CATALOG_PROVIDER", x => x.Id));
        Identity(m, "CATALOG_PROVIDER");

        m.CreateTable("LAB_WORK_TYPE", columns: t => new {
            Id = t.Column<Guid>("uuid", nullable: false), Code = t.Column<string>("character varying(40)", maxLength: 40, nullable: false),
            Name = t.Column<string>("character varying(160)", maxLength: 160, nullable: false), NormalizedName = t.Column<string>("character varying(160)", maxLength: 160, nullable: false),
            ProviderId = t.Column<Guid>("uuid", nullable: true), Unit = t.Column<string>("character varying(16)", maxLength: 16, nullable: false),
            ReferencePrice = t.Column<long>("bigint", nullable: true), Warranty = t.Column<string>("character varying(1000)", maxLength: 1000, nullable: true),
            IsActive = t.Column<bool>("boolean", nullable: false), Version = t.Column<int>("integer", nullable: false),
            CreatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false), CreatedBy = t.Column<int>("integer", nullable: false),
            UpdatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false), UpdatedBy = t.Column<int>("integer", nullable: false)
        }, constraints: t => {
            t.PrimaryKey("PK_LAB_WORK_TYPE", x => x.Id);
            t.ForeignKey("FK_LAB_WORK_TYPE_CATALOG_PROVIDER_ProviderId", x => x.ProviderId, "CATALOG_PROVIDER", "Id", onDelete: ReferentialAction.Restrict);
        });
        Identity(m, "LAB_WORK_TYPE");
        m.CreateIndex("IX_LAB_WORK_TYPE_ProviderId", "LAB_WORK_TYPE", "ProviderId");

        m.CreateTable("CATALOG_MEDICINE", columns: t => new {
            Id = t.Column<Guid>("uuid", nullable: false), Code = t.Column<string>("character varying(40)", maxLength: 40, nullable: false),
            Name = t.Column<string>("character varying(200)", maxLength: 200, nullable: false), NormalizedName = t.Column<string>("character varying(200)", maxLength: 200, nullable: false),
            ActiveIngredient = t.Column<string>("character varying(200)", maxLength: 200, nullable: true), Strength = t.Column<string>("character varying(200)", maxLength: 200, nullable: true),
            DosageForm = t.Column<string>("character varying(200)", maxLength: 200, nullable: true), BaseUnit = t.Column<string>("character varying(40)", maxLength: 40, nullable: false),
            Packaging = t.Column<string>("character varying(160)", maxLength: 160, nullable: true), Instructions = t.Column<string>("character varying(1000)", maxLength: 1000, nullable: true),
            ReferencePrice = t.Column<long>("bigint", nullable: true), PrescriptionEligible = t.Column<bool>("boolean", nullable: false),
            InternalStockTracked = t.Column<bool>("boolean", nullable: false),
            IsActive = t.Column<bool>("boolean", nullable: false), Version = t.Column<int>("integer", nullable: false),
            CreatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false), CreatedBy = t.Column<int>("integer", nullable: false),
            UpdatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false), UpdatedBy = t.Column<int>("integer", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_CATALOG_MEDICINE", x => x.Id));
        Identity(m, "CATALOG_MEDICINE");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("LAB_WORK_TYPE"); m.DropTable("CATALOG_MEDICINE"); m.DropTable("CATALOG_PROVIDER");
    }
}

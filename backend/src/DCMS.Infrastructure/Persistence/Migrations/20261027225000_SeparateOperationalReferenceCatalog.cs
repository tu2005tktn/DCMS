using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace DCMS.Infrastructure.Persistence.Migrations;
[DbContext(typeof(DcmsDbContext))]
[Migration("20261027225000_SeparateOperationalReferenceCatalog")]
public sealed class SeparateOperationalReferenceCatalog : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.RenameTable("REFERENCE_ENTRY",newName:"OPERATIONAL_REFERENCE_ENTRY");
        m.Sql("ALTER TABLE \"OPERATIONAL_REFERENCE_ENTRY\" RENAME CONSTRAINT \"PK_REFERENCE_ENTRY\" TO \"PK_OPERATIONAL_REFERENCE_ENTRY\"");
        foreach(var suffix in new[]{"Kind_Active","Kind_NormalizedCode","Kind_NormalizedName"})
            m.RenameIndex("IX_REFERENCE_ENTRY_"+suffix,table:"OPERATIONAL_REFERENCE_ENTRY",newName:"IX_OPERATIONAL_REFERENCE_ENTRY_"+suffix);
    }
    protected override void Down(MigrationBuilder m)
    {
        foreach(var suffix in new[]{"Kind_Active","Kind_NormalizedCode","Kind_NormalizedName"})
            m.RenameIndex("IX_OPERATIONAL_REFERENCE_ENTRY_"+suffix,table:"OPERATIONAL_REFERENCE_ENTRY",newName:"IX_REFERENCE_ENTRY_"+suffix);
        m.Sql("ALTER TABLE \"OPERATIONAL_REFERENCE_ENTRY\" RENAME CONSTRAINT \"PK_OPERATIONAL_REFERENCE_ENTRY\" TO \"PK_REFERENCE_ENTRY\"");
        m.RenameTable("OPERATIONAL_REFERENCE_ENTRY",newName:"REFERENCE_ENTRY");
    }
}

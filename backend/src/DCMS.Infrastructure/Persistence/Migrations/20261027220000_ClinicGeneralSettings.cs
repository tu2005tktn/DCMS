using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace DCMS.Infrastructure.Persistence.Migrations;
// S58 Thiết lập chung. No seed row: version 0 is the built-in default that matches pre-S58 behaviour.
[DbContext(typeof(DcmsDbContext))]
[Migration("20261027220000_ClinicGeneralSettings")]
public sealed class ClinicGeneralSettings : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("CLINIC_SETTINGS_VERSION", columns: t => new {
            Version = t.Column<int>("integer", nullable: false), EffectiveAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
            ActorId = t.Column<int>("integer", nullable: false), Reason = t.Column<string>("character varying(500)", maxLength: 500, nullable: false),
            RestoredFromVersion = t.Column<int>("integer", nullable: true), ContentJson = t.Column<string>("jsonb", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_CLINIC_SETTINGS_VERSION", x => x.Version));
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("CLINIC_SETTINGS_VERSION");
}

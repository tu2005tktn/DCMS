using DCMS.Application.Features.Settings;
using Xunit;

namespace DCMS.UnitTests;

public sealed class GrantDefinitionTests
{
    [Fact]
    public void EveryPermissionKeyIsDefinedOnce()
    {
        var duplicates = SettingsGrants.Definitions.GroupBy(x => x.Key).Where(x => x.Count() > 1).Select(x => x.Key).ToArray();
        Assert.Empty(duplicates);
    }

    [Fact]
    public void DentistsCanReadAndCreateMedicinesButCannotManageTheCatalog()
    {
        var roles = new[] { "Dentist" };
        var legacy = SettingsGrants.Legacy(roles);

        Assert.True(SettingsGrants.RoleAllows("catalog.medicines.read", roles));
        Assert.True(SettingsGrants.RoleAllows("catalog.medicines.create", roles));
        Assert.False(SettingsGrants.RoleAllows("catalog.medicines.update", roles));
        Assert.False(SettingsGrants.RoleAllows("catalog.medicines.deactivate", roles));
        Assert.False(SettingsGrants.RoleAllows("catalog.medicines.export", roles));
        Assert.Contains("catalog.medicines.read", legacy);
        Assert.Contains("catalog.medicines.create", legacy);
        Assert.DoesNotContain("catalog.medicines.update", legacy);
    }
}

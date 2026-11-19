using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
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
    public void FinanceStatisticsDefaultsToTheOwnerOnly()
    {
        var definition = Assert.Single(SettingsGrants.Definitions, x => x.Key == "finance.statistics.read");
        Assert.Equal("finance.read", definition.Parent);
        Assert.True(SettingsGrants.RoleAllows(definition.Key, [AccountRoles.ClinicOwner, AccountRoles.Dentist]));
        foreach (var role in new[] { AccountRoles.Admin, AccountRoles.Dentist, AccountRoles.ClinicAssistant })
            Assert.DoesNotContain(definition.Key, SettingsGrants.Legacy([role]));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    public void OnlyAssignedNonOwnerStaffAreGrantDriven(bool assigned, bool owner, bool expected) =>
        Assert.Equal(expected, new PermissionState(owner, assigned, [], [], []).GrantDriven);
}

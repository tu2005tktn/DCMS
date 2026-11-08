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
}

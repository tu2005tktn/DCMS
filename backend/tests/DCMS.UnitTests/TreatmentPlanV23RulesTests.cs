using DCMS.Domain.Features.Treatment;
using Xunit;

namespace DCMS.UnitTests;

public sealed class TreatmentPlanV23RulesTests
{
    [Fact]
    public void CrossServiceReductionOnlyLowersTheTargetAmountOnce()
    {
        var rootCanal = TreatmentPlanRules.Gross(1, 600000);
        var crown = TreatmentPlanRules.Gross(1, 1000000);
        var crownDiscount = TreatmentPlanRules.Discount(crown, "Fixed", 300000);
        Assert.Equal(1300000, rootCanal + crown - crownDiscount);
        Assert.Throws<ArgumentOutOfRangeException>(() => TreatmentPlanRules.Discount(crown, "Fixed", 1100000));
        Assert.Throws<ArgumentOutOfRangeException>(() => TreatmentPlanRules.Discount(crown, "Percent", 101));
    }
}

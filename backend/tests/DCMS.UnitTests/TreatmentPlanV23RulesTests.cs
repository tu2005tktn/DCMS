using DCMS.Domain.Features.Treatment;
using Xunit;

namespace DCMS.UnitTests;

public sealed class TreatmentPlanV23RulesTests
{
    [Fact]
    public void DurationRangeAndChairReservationHaveSeparateConstraints()
    {
        Assert.True(TreatmentPlanRules.ValidDuration(20, 25, 25, 30, false));
        Assert.False(TreatmentPlanRules.ValidDuration(20, 25, 25, 20, false));
        Assert.False(TreatmentPlanRules.ValidDuration(20, 25, 40, 30, false));
        Assert.True(TreatmentPlanRules.ValidDuration(20, 25, 40, 30, true));
        Assert.False(TreatmentPlanRules.ValidDuration(26, 25, 40, 30, true));
    }

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

    [Fact]
    public void ReturnWindowMustBeCompleteAndOrdered()
    {
        var earliest = new DateOnly(2027, 1, 1);
        var target = new DateOnly(2027, 1, 8);
        var latest = new DateOnly(2027, 1, 15);
        Assert.True(TreatmentPlanRules.ValidReturnWindow(earliest, target, latest));
        Assert.False(TreatmentPlanRules.ValidReturnWindow(earliest, null, latest));
        Assert.False(TreatmentPlanRules.ValidReturnWindow(target, earliest, latest));
    }
}

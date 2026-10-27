using DCMS.Domain.Features.Treatment;
using Xunit;
namespace DCMS.UnitTests;
public sealed class TreatmentSheetRulesTests
{
    [Theory]
    [InlineData(25, true)] [InlineData(46, true)] [InlineData(85, true)] [InlineData(99, false)]
    [InlineData(19, false)] [InlineData(56, false)] [InlineData(10, false)]
    public void FdiIncludesAdultAndPrimaryTeeth(int tooth, bool valid) => Assert.Equal(valid, TreatmentSheetRules.IsFdi(tooth));
    [Theory]
    [InlineData(1.5, "răng", false)] [InlineData(2, "răng", true)] [InlineData(0.125, "ml", true)] [InlineData(0, "lần", false)]
    public void QuantityRespectsClinicalUnit(decimal quantity, string unit, bool valid) => Assert.Equal(valid, TreatmentSheetRules.ValidQuantity(quantity, unit));
    [Theory]
    [InlineData("25, 26", "25", true)] [InlineData("25, 26", "46", false)]
    [InlineData("Hàm trên", "Hàm dưới", false)] [InlineData("Hàm trên", "Hàm trên", true)]
    public void ActualTeethMayBeASubsetButNeverOutsideAgreedScope(string planned, string actual, bool valid)
        => Assert.Equal(valid, TreatmentSheetRules.RegionWithinScope(planned, actual));
}

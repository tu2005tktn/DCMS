using System.Text;
using DCMS.Infrastructure.Features.Prescriptions;
using Xunit;

namespace DCMS.UnitTests;

public sealed class PrescriptionAndBranchAssetRulesTests
{
    [Theory]
    [InlineData(1, "Một")]
    [InlineData(10, "Mười")]
    [InlineData(15, "Mười lăm")]
    [InlineData(21, "Hai mươi mốt")]
    [InlineData(24, "Hai mươi tư")]
    [InlineData(105, "Một trăm lẻ năm")]
    [InlineData(1005, "Một nghìn không trăm lẻ năm")]
    [InlineData(10000, "Mười nghìn")]
    public void QuantityIsPrintedInVietnameseWords(int value, string expected) =>
        Assert.Equal(expected, VietnameseNumberWords.ToWords(value));
}

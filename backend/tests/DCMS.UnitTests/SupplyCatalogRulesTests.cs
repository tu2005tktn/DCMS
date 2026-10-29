using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.SystemSettings;
using Xunit;

namespace DCMS.UnitTests;

public sealed class SupplyCatalogRulesTests
{
    [Theory]
    [InlineData("providers")]
    [InlineData("lab-work-types")]
    [InlineData("materials")]
    [InlineData("material-categories")]
    [InlineData("units")]
    public void DentistManagesOnlyExplicitResources(string resource)
    {
        Assert.True(SupplyCatalogPermissions.CanManage(resource, ["Dentist"]));
        Assert.False(SupplyCatalogPermissions.CanManage(resource, ["Admin"]));
        Assert.False(SupplyCatalogPermissions.CanManage(resource, ["ClinicAssistant"]));
    }

    [Fact]
    public void MedicineAndUnknownKindsDoNotGainDentistPermission()
    {
        Assert.False(SupplyCatalogPermissions.CanManage("medicines", ["Dentist"]));
        Assert.False(SupplyCatalogPermissions.CanManage("unknown", ["Dentist"]));
        Assert.True(SupplyCatalogPermissions.CanManage("medicines", ["ClinicOwner"]));
    }

    [Theory]
    [InlineData("0.5", "2", "1")]
    [InlineData("1", "0.125", "0.125")]
    [InlineData("0.0001", "10", "0.001")]
    [InlineData("100000000000000000000000000000", "1", "100000000000000000000000000000")]
    public void ConversionIsExact(string quantity, string factor, string expected) =>
        Assert.Equal(expected, UnitConversionRules.ToBase(quantity, factor));

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1e3")]
    [InlineData("NaN")]
    [InlineData("1,5")]
    [InlineData("")]
    public void InvalidFactorIsRejected(string factor) =>
        Assert.Throws<ArgumentException>(() => UnitConversionRules.ValidateFactor(factor));

    [Fact]
    public void QuantitiesNeverRound()
    {
        Assert.Throws<ArgumentException>(() => UnitConversionRules.ToBase("1", "0.0001"));
        Assert.Throws<ArgumentException>(() => UnitConversionRules.ToBase("1", "0.5", medicine: true));
        Assert.Equal("1", UnitConversionRules.ToBase("2", "0.5", medicine: true));
    }
}

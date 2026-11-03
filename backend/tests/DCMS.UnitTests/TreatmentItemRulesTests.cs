using DCMS.Domain.Features.Treatment;
using Xunit;
namespace DCMS.UnitTests;
public sealed class TreatmentItemRulesTests
{
    private static readonly DateOnly Day = new(2026, 11, 10);
    private static readonly DateOnly Today = new(2026, 11, 3);

    [Fact]
    public void PlannedDateAllowsFourDaysEitherSide()
    {
        Assert.Equal(4, TreatmentItemRules.BookingToleranceDays);
        Assert.Equal((Day.AddDays(-4), Day, Day.AddDays(4)), TreatmentItemRules.PlanWindow(new TreatmentItem { EstimatedStart = Day }));
    }

    [Fact]
    public void ExplicitReturnWindowNarrowsButNeverWidensTheTolerance()
    {
        var narrow = new TreatmentItem { ReturnEarliestOn = Day.AddDays(-1), ReturnTargetOn = Day, ReturnLatestOn = Day.AddDays(2) };
        Assert.Equal((Day.AddDays(-1), Day, Day.AddDays(2)), TreatmentItemRules.PlanWindow(narrow));
        var wide = new TreatmentItem { ReturnEarliestOn = Day.AddDays(-20), ReturnTargetOn = Day, ReturnLatestOn = Day.AddDays(20) };
        Assert.Equal((Day.AddDays(-4), Day, Day.AddDays(4)), TreatmentItemRules.PlanWindow(wide));
    }

    [Fact]
    public void PartialReturnWindowFallsBackToThePlannedDate()
        => Assert.Equal(Day, TreatmentItemRules.PlannedTarget(new TreatmentItem { EstimatedStart = Day, ReturnTargetOn = Day.AddDays(5) }));

    [Fact]
    public void ItemWithoutAnyDateHasNoWindow()
    {
        Assert.Null(TreatmentItemRules.PlanWindow(new TreatmentItem()));
        Assert.Null(TreatmentItemRules.BookingWindow(new TreatmentItem(), Today));
    }

    [Fact]
    public void BookingWindowCutsOffThePast()
    {
        // Planned 5 days from today: the window starts 1 day from today, so nothing is cut.
        Assert.Equal((Today.AddDays(1), Today.AddDays(5), Today.AddDays(9)), TreatmentItemRules.BookingWindow(new TreatmentItem { EstimatedStart = Today.AddDays(5) }, Today));
        // Planned 2 days ago: today is the first bookable day, the end stays 4 days after the planned date.
        Assert.Equal((Today, Today, Today.AddDays(2)), TreatmentItemRules.BookingWindow(new TreatmentItem { EstimatedStart = Today.AddDays(-2) }, Today));
    }

    [Fact]
    public void ItemOverdueByMoreThanTheToleranceCountsAsPlannedForToday()
        => Assert.Equal((Today, Today, Today.AddDays(4)), TreatmentItemRules.BookingWindow(new TreatmentItem { EstimatedStart = Today.AddDays(-10) }, Today));
}

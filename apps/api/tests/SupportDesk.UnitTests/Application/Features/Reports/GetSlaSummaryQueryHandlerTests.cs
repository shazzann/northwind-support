using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Features.Reports.Queries.GetSlaSummary;

namespace SupportDesk.UnitTests.Application.Features.Reports;

/// <summary>The figures derived from a group's counts; the counting itself is tested on SQL Server.</summary>
public class GetSlaSummaryQueryHandlerTests
{
    [Fact]
    public void BreachRate_IsOverTicketsWithAnSla_IgnoringThoseWithout()
    {
        var group = GetSlaSummaryQueryHandler.Summarise(new SlaSummaryCounts("High", 5, 1, 1, 1, 0, null));

        Assert.Equal(2, group.NoSla);
        Assert.Equal(0.333, group.BreachRate);
    }

    [Fact]
    public void BreachRate_WithNoTicketsUnderAnSla_IsNull() =>
        Assert.Null(GetSlaSummaryQueryHandler.Summarise(new SlaSummaryCounts("Medium", 2, 0, 0, 0, 0, null)).BreachRate);

    [Fact]
    public void AverageResolution_IsInHours_OrNullWhenNothingIsResolved()
    {
        Assert.Equal(1.5, GetSlaSummaryQueryHandler.Summarise(new SlaSummaryCounts("Low", 1, 0, 0, 0, 1, 90)).AverageResolutionHours);
        Assert.Null(GetSlaSummaryQueryHandler.Summarise(new SlaSummaryCounts("Low", 1, 0, 0, 1, 0, null)).AverageResolutionHours);
    }

    [Fact]
    public void Query_NeedsAGrouping_AndARangeThatRunsForwards()
    {
        var validator = new SlaSummaryQueryValidator();
        var now = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);

        Assert.False(validator.Validate(new SlaSummaryQuery()).IsValid);
        Assert.False(validator.Validate(new SlaSummaryQuery { GroupBy = SlaSummaryGrouping.Agent, From = now, To = now.AddDays(-1) }).IsValid);
        Assert.True(validator.Validate(new SlaSummaryQuery { GroupBy = SlaSummaryGrouping.Agent, From = now.AddDays(-1), To = now }).IsValid);
    }
}

using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Triage;

public class SlaPolicyTests
{
    [Theory]
    [InlineData(TicketPriority.Critical, CustomerTier.Standard, 4 * 60)]
    [InlineData(TicketPriority.High, CustomerTier.Standard, 8 * 60)]
    [InlineData(TicketPriority.Medium, CustomerTier.Standard, 24 * 60)]
    [InlineData(TicketPriority.Low, CustomerTier.Standard, 72 * 60)]
    [InlineData(TicketPriority.Critical, CustomerTier.Premium, 2 * 60)]
    [InlineData(TicketPriority.High, CustomerTier.Premium, 4 * 60)]
    [InlineData(TicketPriority.Medium, CustomerTier.Premium, 12 * 60)]
    [InlineData(TicketPriority.Low, CustomerTier.Premium, 36 * 60)]
    public void WindowFor_IsTheBaseWindow_HalvedForPremium(TicketPriority priority, CustomerTier tier, int expectedMinutes) =>
        Assert.Equal(expectedMinutes, TestSla.Policy().WindowFor(priority, tier).Minutes);

    [Fact]
    public void WindowFor_NeverGoesBelowTheMinimum()
    {
        var settings = TestSla.Settings();
        settings.BaseWindowHours[TicketPriority.Critical] = 1.5;

        var window = new SlaPolicy(settings).WindowFor(TicketPriority.Critical, CustomerTier.Premium);

        Assert.Equal(60, window.Minutes);
        Assert.Contains("raised to the 1 h minimum", window.Reason);
    }

    [Fact]
    public void WindowFor_AtExactlyTheMinimum_IsNotRaised()
    {
        var settings = TestSla.Settings();
        settings.BaseWindowHours[TicketPriority.Critical] = 2;

        var window = new SlaPolicy(settings).WindowFor(TicketPriority.Critical, CustomerTier.Premium);

        Assert.Equal(60, window.Minutes);
        Assert.DoesNotContain("minimum", window.Reason);
    }

    [Fact]
    public void WindowFor_FollowsTheConfiguredNumbers()
    {
        var settings = TestSla.Settings();
        settings.BaseWindowHours[TicketPriority.High] = 10;
        settings.PremiumCustomerMultiplier = 0.25;

        Assert.Equal(150, new SlaPolicy(settings).WindowFor(TicketPriority.High, CustomerTier.Premium).Minutes);
    }

    [Fact]
    public void WindowFor_ExplainsItself()
    {
        var window = TestSla.Policy().WindowFor(TicketPriority.Critical, CustomerTier.Premium);

        Assert.Equal("Critical priority has a 4 h window, 2 h for a Premium customer.", window.Reason);
    }

    [Fact]
    public void Validate_NamesEveryProblem()
    {
        var settings = new SlaSettings { PremiumCustomerMultiplier = 2, AtRiskThresholdPercent = 150 };

        var errors = settings.Validate();

        Assert.Equal(7, errors.Count); // four missing windows, the multiplier, the minimum, the threshold
        Assert.Empty(TestSla.Settings().Validate());
    }
}

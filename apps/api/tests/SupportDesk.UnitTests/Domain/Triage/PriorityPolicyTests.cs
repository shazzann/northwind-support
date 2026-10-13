using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;

namespace SupportDesk.UnitTests.Domain.Triage;

public class PriorityPolicyTests
{
    private static readonly Category Technical = new("Technical", requiresSpecialist: false, forcesCriticalPriority: false);
    private static readonly Category Security = new("Security", requiresSpecialist: true, forcesCriticalPriority: true);

    [Fact]
    public void RequestedPriority_IsUsed() =>
        Assert.Equal(TicketPriority.Low, PriorityPolicy.Decide(TicketPriority.Low, Technical).Priority);

    [Fact]
    public void NoRequestedPriority_DefaultsToMedium()
    {
        var decision = PriorityPolicy.Decide(null, Technical);

        Assert.Equal(TicketPriority.Medium, decision.Priority);
        Assert.Equal("No priority was requested, so Medium.", decision.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.High)]
    public void ForcedCriticalCategory_IsAlwaysCritical(TicketPriority? requested) =>
        Assert.Equal(TicketPriority.Critical, PriorityPolicy.Decide(requested, Security).Priority);

    [Fact]
    public void ForcedCriticalCategory_SaysWhatItOverrode() =>
        Assert.Equal(
            "Security tickets are always Critical (Low was requested).",
            PriorityPolicy.Decide(TicketPriority.Low, Security).Reason);
}

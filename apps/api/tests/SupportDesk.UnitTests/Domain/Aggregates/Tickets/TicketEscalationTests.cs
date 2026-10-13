using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

/// <summary>BR-7, on the aggregate: what an escalation changes and what it refuses.</summary>
public class TicketEscalationTests
{
    private static readonly DateTime Later = FixedClock.DefaultNow.AddHours(3);

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Escalate_RaisesPriorityByExactlyOneLevel(TicketPriority from, TicketPriority to)
    {
        var ticket = new TicketBuilder().WithPriority(from).Build();

        Escalate(ticket);

        Assert.Equal(to, ticket.Priority);
    }

    [Fact]
    public void Escalate_RestartsTheWindowForTheNewPriority_FromTheMomentOfEscalation()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).Build();
        ticket.StartSla(FixedClock.DefaultNow, 24 * 60);

        Escalate(ticket, tier: CustomerTier.Premium);

        Assert.Equal(Later.AddHours(4), ticket.DueAtUtc); // High is 8 h, halved for Premium
        Assert.Equal(4 * 60, ticket.SlaWindowMinutes);
    }

    [Fact]
    public void Escalate_HandsTheTicketToTheGivenOwner()
    {
        var ticket = new TicketBuilder().AssignedTo(1).Build();

        Escalate(ticket, agentId: 4);

        Assert.Equal(4, ticket.AssignedAgentId);
    }

    [Fact]
    public void Escalate_RecordsTheBeforeAndAfter()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).AssignedTo(1).Build();
        ticket.StartSla(FixedClock.DefaultNow, 72 * 60);
        var dueBefore = ticket.DueAtUtc;

        var escalation = ticket.Escalate(TestSla.Policy(), CustomerTier.Standard, 2, "  Customer is blocked  ", "Team Lead", Later);

        Assert.Same(escalation, Assert.Single(ticket.Escalations));
        Assert.Equal(TicketPriority.Low, escalation.FromPriority);
        Assert.Equal(TicketPriority.Medium, escalation.ToPriority);
        Assert.Equal(1, escalation.FromAgentId);
        Assert.Equal(2, escalation.ToAgentId);
        Assert.Equal(dueBefore, escalation.FromDueAtUtc);
        Assert.Equal(Later.AddHours(24), escalation.ToDueAtUtc);
        Assert.Equal("Customer is blocked", escalation.Reason);
        Assert.Equal("Team Lead", escalation.EscalatedBy);
        Assert.Equal(Later, escalation.EscalatedAtUtc);
    }

    [Fact]
    public void Escalate_WhenAlreadyCritical_IsRefusedAndChangesNothing()
    {
        var ticket = new TicketBuilder().WithReference("TCK-0006").WithPriority(TicketPriority.Critical).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => Escalate(ticket));

        Assert.Equal("Ticket TCK-0006 is already Critical and cannot be escalated further.", exception.Message);
        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Empty(ticket.Escalations);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Escalate_WhenFinished_IsRefusedSayingWhy(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithReference("TCK-0025").WithStatus(status).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => Escalate(ticket));

        Assert.Equal($"Ticket TCK-0025 is {status} and cannot be escalated.", exception.Message);
        Assert.Empty(ticket.Escalations);
    }

    private static void Escalate(Ticket ticket, CustomerTier tier = CustomerTier.Standard, int? agentId = null) =>
        ticket.Escalate(TestSla.Policy(), tier, agentId, "Customer is blocked", "Team Lead", Later);
}

using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static readonly EscalateTicketRequest Request = new("Customer cannot trade", "Team Lead");

    private readonly TicketCommandTestContext _context = new();

    public EscalateTicketCommandHandlerTests()
    {
        _context.ExistingCategory(new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false), id: 1);
    }

    [Fact]
    public async Task HandleAsync_RaisesPriority_AndDatesTheNewWindowFromNow()
    {
        var ticket = ExistingTicket(TicketPriority.Medium);
        _context.Clock.Advance(TimeSpan.FromHours(5));

        await _context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(_context.Clock.UtcNow.AddHours(8), ticket.DueAtUtc);
        _context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_KeepsTheOwnerWhileStillEligible()
    {
        var ticket = ExistingTicket(TicketPriority.Low, agentId: 4);
        _context.Candidates.AddRange([Specialist(4, open: 5), Specialist(5, open: 0)]);

        await _context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Equal(4, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_ReassignsWhenTheOwnerIsNoLongerEligible()
    {
        var ticket = ExistingTicket(TicketPriority.Low, agentId: 4);
        _context.Candidates.AddRange([Specialist(4, open: 2, isActive: false), Specialist(5, open: 3)]);

        await _context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Equal(5, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_RecordsWhoEscalatedAndWhy()
    {
        var ticket = ExistingTicket(TicketPriority.Low);

        await _context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        var escalation = Assert.Single(ticket.Escalations);
        Assert.Equal("Customer cannot trade", escalation.Reason);
        Assert.Equal("Team Lead", escalation.EscalatedBy);
        Assert.Equal(_context.Clock.UtcNow, escalation.EscalatedAtUtc);
    }

    [Fact]
    public async Task HandleAsync_ForAnUnknownTicket_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _context.EscalateTicket.HandleAsync(404, Request, CancellationToken.None));

        _context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_ForACriticalTicket_IsRefusedBeforeAnyWork()
    {
        var ticket = ExistingTicket(TicketPriority.Critical);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => _context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None));

        _context.AgentQueries.Verify(a => a.GetCandidatesAsync(It.IsAny<CancellationToken>()), Times.Never());
        _context.VerifySaved(Times.Never());
    }

    private Ticket ExistingTicket(TicketPriority priority, int? agentId = null)
    {
        var ticket = new TicketBuilder().WithPriority(priority).AssignedTo(agentId).Build().WithId(41);
        ticket.StartSla(FixedClock.DefaultNow, 24 * 60);
        return _context.ExistingTicket(ticket);
    }

    private static AgentCandidate Specialist(int id, int open, bool isActive = true) =>
        new(id, $"Agent {id}", isActive, MaxOpenTickets: 6, open, SpecializationIds: [1]);
}

using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_InAForcedCriticalCategory_IsCriticalWhateverWasRequested()
    {
        var context = new TicketCommandTestContext();
        context.ExistingCategory(new Category("Security", requiresSpecialist: true, forcesCriticalPriority: true));

        var result = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, context.AddedTicket!.Priority);
        Assert.Equal("Security tickets are always Critical (Low was requested).", result.Triage.PriorityReason);
    }

    [Fact]
    public async Task HandleAsync_DueDateIsTheWindowForThePriorityAndTier()
    {
        var context = new TicketCommandTestContext();
        context.ExistingCustomer(CustomerTier.Premium);

        var result = await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(4), context.AddedTicket!.DueAtUtc);
        Assert.Equal(4 * 60, result.Triage.SlaWindowMinutes);
    }

    [Fact]
    public async Task HandleAsync_AssignsTheAgentTheRulesChoose()
    {
        var context = new TicketCommandTestContext();
        context.Candidates.AddRange(
        [
            new AgentCandidate(1, "Alex Turner", true, 10, 5, []),
            new AgentCandidate(2, "Ben Osei", true, 8, 3, [])
        ]);

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Equal(2, context.AddedTicket!.AssignedAgentId);
        Assert.StartsWith("Assigned to Ben Osei", result.Triage.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_WithNobodyEligible_StillCreatesTheTicketUnassigned()
    {
        var context = new TicketCommandTestContext();
        context.Candidates.Add(new AgentCandidate(1, "Alex Turner", true, 6, 6, []));

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Null(context.AddedTicket!.AssignedAgentId);
        Assert.Equal("Left unassigned: no active agent has capacity.", result.Triage.AssignmentReason);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request() with { CustomerId = 99 }, CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request() with { CategoryId = 99 }, CancellationToken.None));

        Assert.Contains("Category", exception.Message);
        context.VerifySaved(Times.Never());
    }
}

using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer and triages it: priority (BR-1), due date (BR-2, BR-3) and
/// owner (BR-4, BR-5) are decided by the triage rules, not by hand.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agents,
    SlaPolicy sla,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<RaisedTicketDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var now = clock.UtcNow;
        var priority = PriorityPolicy.Decide(request.RequestedPriority, category);
        var window = sla.WindowFor(priority.Priority, customer.Tier);
        var assignment = AgentSelector.Choose(category, await agents.GetCandidatesAsync(ct));

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            customer.Id,
            category.Id,
            priority.Priority,
            now);

        ticket.StartSla(now, window.Minutes);
        ticket.AssignTo(assignment.AgentId, now);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}: {Priority}, due {DueAtUtc}, agent {AgentId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId, ticket.Priority, ticket.DueAtUtc, ticket.AssignedAgentId);

        return new RaisedTicketDto(
            await getTicket.HandleAsync(ticket.Id, ct),
            new TicketTriageDto(priority.Reason, window.Minutes, window.Reason, assignment.Reason));
    }
}

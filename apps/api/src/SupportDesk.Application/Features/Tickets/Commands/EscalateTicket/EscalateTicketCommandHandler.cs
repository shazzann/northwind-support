using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates a ticket (BR-7). The ticket raises its priority, restarts its SLA window and
/// records the escalation against the signed-in agent; the owner is re-evaluated with the
/// same rules as on create.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agents,
    ITicketQueries ticketQueries,
    ICurrentUser currentUser,
    SlaPolicy sla,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">No ticket has this id.</exception>
    /// <exception cref="BusinessRuleViolationException">The ticket is Critical, Resolved or Closed.</exception>
    public async Task<EscalationResultDto> HandleAsync(int id, EscalateTicketRequest request, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        // Before any further reads: a refusal should not cost the work of a decision.
        ticket.EnsureCanBeEscalated();

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var assignment = AgentSelector.KeepOrChoose(
            category, await agents.GetCandidatesAsync(ct), ticket.AssignedAgentId);

        var escalation = ticket.Escalate(
            sla, customer.Tier, assignment.AgentId, request.Reason, currentUser.FullName, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {FromPriority} to {ToPriority} by {EscalatedBy}. {AssignmentReason}",
            ticket.Reference, escalation.FromPriority, escalation.ToPriority, escalation.EscalatedBy, assignment.Reason);

        var history = await ticketQueries.GetEscalationsAsync(id, ct) ?? [];

        return new EscalationResultDto(
            await getTicket.HandleAsync(id, ct),
            history.Single(recorded => recorded.Id == escalation.Id));
    }
}

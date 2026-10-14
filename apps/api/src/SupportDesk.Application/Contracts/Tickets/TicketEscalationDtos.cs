using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Body of POST /api/tickets/{id}/escalate. Who is escalating is not part of it: that is the
/// signed-in agent, taken from their token.
/// </summary>
/// <param name="Reason">Why the ticket needs escalating; 5 to 500 characters.</param>
public sealed record EscalateTicketRequest(string Reason);

/// <summary>One recorded escalation: the before and after of each thing it changed.</summary>
public sealed record TicketEscalationDto(
    int Id,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    AgentSummaryDto? FromAgent,
    AgentSummaryDto? ToAgent,
    DateTime? FromDueAtUtc,
    DateTime ToDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc);

/// <summary>Response of POST /api/tickets/{id}/escalate: the ticket now, and what was recorded.</summary>
public sealed record EscalationResultDto(TicketDetailDto Ticket, TicketEscalationDto Escalation);

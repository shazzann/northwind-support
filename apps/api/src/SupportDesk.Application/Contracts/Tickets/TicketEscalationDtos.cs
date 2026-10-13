using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>Body of POST /api/tickets/{id}/escalate.</summary>
/// <param name="Reason">Why the ticket needs escalating; 5 to 500 characters.</param>
/// <param name="EscalatedBy">Who is escalating it.</param>
public sealed record EscalateTicketRequest(string Reason, string EscalatedBy);

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

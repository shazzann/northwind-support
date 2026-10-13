namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Response of POST /api/tickets: the ticket as created - with the priority, owner, due date
/// and SLA status triage gave it - and the reasons for each decision.
/// </summary>
public sealed record RaisedTicketDto(TicketDetailDto Ticket, TicketTriageDto Triage);

/// <summary>Why triage decided what it did, in words an agent can read.</summary>
public sealed record TicketTriageDto(
    string PriorityReason,
    int SlaWindowMinutes,
    string SlaReason,
    string AssignmentReason);

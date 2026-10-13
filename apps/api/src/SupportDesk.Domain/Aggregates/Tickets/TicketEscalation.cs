using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// The record of one escalation: what changed, who asked for it, why, and when. Part of the
/// <see cref="Ticket"/> aggregate and written only by <see cref="Ticket.Escalate"/>. It has no
/// way to change once written - it is history.
/// </summary>
public sealed class TicketEscalation : Entity
{
    public const int ReasonMinLength = 5;
    public const int ReasonMaxLength = 500;
    public const int EscalatedByMaxLength = 100;

    private TicketEscalation()
    {
        // For EF Core.
    }

    internal TicketEscalation(
        Ticket ticket,
        TicketPriority toPriority,
        int? toAgentId,
        DateTime toDueAtUtc,
        string reason,
        string escalatedBy,
        DateTime escalatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(escalatedBy);

        TicketId = ticket.Id;
        FromPriority = ticket.Priority;
        ToPriority = toPriority;
        FromAgentId = ticket.AssignedAgentId;
        ToAgentId = toAgentId;
        FromDueAtUtc = ticket.DueAtUtc;
        ToDueAtUtc = toDueAtUtc;
        Reason = reason.Trim();
        EscalatedBy = escalatedBy.Trim();
        EscalatedAtUtc = escalatedAtUtc;
    }

    public int TicketId { get; private set; }

    public TicketPriority FromPriority { get; private set; }

    public TicketPriority ToPriority { get; private set; }

    public int? FromAgentId { get; private set; }

    public int? ToAgentId { get; private set; }

    public DateTime? FromDueAtUtc { get; private set; }

    public DateTime ToDueAtUtc { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>Who escalated the ticket.</summary>
    public string EscalatedBy { get; private set; } = string.Empty;

    public DateTime EscalatedAtUtc { get; private set; }
}

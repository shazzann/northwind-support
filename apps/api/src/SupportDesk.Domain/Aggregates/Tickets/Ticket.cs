using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// A support request raised by a customer. Every change to a ticket goes through one of its
/// methods, so its rules cannot be bypassed.
/// </summary>
/// <remarks>
/// The customer, the category and the assigned agent are other aggregates and are referenced
/// by id only.
/// </remarks>
public sealed class Ticket : AggregateRoot
{
    private readonly List<TicketEscalation> _escalations = [];

    private Ticket()
    {
        // For EF Core.
    }

    /// <summary>Human-friendly identifier shown to customers, e.g. TCK-0042.</summary>
    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int CustomerId { get; private set; }

    public int CategoryId { get; private set; }

    /// <summary>Null while the ticket is waiting for an owner.</summary>
    public int? AssignedAgentId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>When the team has committed to responding by. Null when no SLA applies.</summary>
    public DateTime? DueAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>
    /// The length of the window that <see cref="DueAtUtc"/> closes, which "at risk" is measured
    /// against. Null when no SLA applies.
    /// </summary>
    public int? SlaWindowMinutes { get; private set; }

    /// <summary>True while the ticket still needs work from an agent.</summary>
    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    /// <summary>
    /// Escalations recorded on this ticket. Only the ones made since it was loaded: the full
    /// history is read through the query side.
    /// </summary>
    public IReadOnlyCollection<TicketEscalation> Escalations => _escalations;

    /// <summary>
    /// Raises a new ticket for a customer. It starts as <see cref="TicketStatus.New"/>, with no
    /// owner and no due date.
    /// </summary>
    public static Ticket Raise(
        string reference,
        string title,
        string description,
        int customerId,
        int categoryId,
        TicketPriority priority,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Ticket
        {
            Reference = reference,
            Title = title.Trim(),
            Description = description.Trim(),
            CustomerId = customerId,
            CategoryId = categoryId,
            Priority = priority,
            Status = TicketStatus.New,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    /// <summary>
    /// Moves the ticket through its lifecycle, keeping the resolution timestamp in step.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">The ticket is closed.</exception>
    public void ChangeStatus(TicketStatus status, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed && status != TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is closed and cannot be moved to {status}.");
        }

        ResolvedAtUtc = status switch
        {
            TicketStatus.Resolved => nowUtc,
            TicketStatus.Closed => ResolvedAtUtc ?? nowUtc,
            _ => null
        };

        Status = status;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Starts the SLA clock: the ticket is due one window after <paramref name="startUtc"/>.
    /// </summary>
    public void StartSla(DateTime startUtc, int windowMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowMinutes);

        SlaWindowMinutes = windowMinutes;
        DueAtUtc = startUtc.AddMinutes(windowMinutes);
        UpdatedAtUtc = startUtc;
    }

    /// <summary>Refuses an escalation the rules do not allow (BR-7), saying which rule.</summary>
    /// <exception cref="BusinessRuleViolationException">The ticket is finished or already Critical.</exception>
    public void EnsureCanBeEscalated()
    {
        if (!IsOpen)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is {Status} and cannot be escalated.");
        }

        if (Priority == TicketPriority.Critical)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is already Critical and cannot be escalated further.");
        }
    }

    /// <summary>
    /// Escalates the ticket (BR-7): one priority level up, a fresh SLA window for the new
    /// priority starting now, and the owner the caller re-evaluated. Records the change.
    /// </summary>
    /// <param name="sla">Sizes the new window, so it always matches the new priority.</param>
    /// <param name="customerTier">The customer's tier, which the window depends on.</param>
    /// <param name="agentId">The owner after escalation; may be unchanged, or nobody.</param>
    /// <param name="reason">Why the ticket is being escalated.</param>
    /// <param name="escalatedBy">Who is escalating it.</param>
    /// <param name="nowUtc">The moment of escalation, which the new window starts from.</param>
    /// <exception cref="BusinessRuleViolationException">The ticket is finished or already Critical.</exception>
    public TicketEscalation Escalate(
        SlaPolicy sla,
        CustomerTier customerTier,
        int? agentId,
        string reason,
        string escalatedBy,
        DateTime nowUtc)
    {
        EnsureCanBeEscalated();

        var toPriority = Priority + 1;
        var window = sla.WindowFor(toPriority, customerTier);
        var escalation = new TicketEscalation(
            this, toPriority, agentId, nowUtc.AddMinutes(window.Minutes), reason, escalatedBy, nowUtc);

        Priority = toPriority;
        StartSla(nowUtc, window.Minutes);
        AssignTo(agentId, nowUtc);
        _escalations.Add(escalation);

        return escalation;
    }

    /// <summary>
    /// Hands the ticket to an agent, or takes it back off them when <paramref name="agentId"/>
    /// is null. Whether the agent may take it is checked by the caller, which can see agents.
    /// </summary>
    public void AssignTo(int? agentId, DateTime nowUtc)
    {
        AssignedAgentId = agentId;
        UpdatedAtUtc = nowUtc;
    }
}

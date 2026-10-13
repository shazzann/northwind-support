using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Triage;

/// <summary>
/// The priority a new ticket gets (BR-1): what the reporter asked for, Medium when they did not
/// say, and always Critical in a category flagged <see cref="Category.ForcesCriticalPriority"/>.
/// </summary>
public static class PriorityPolicy
{
    public const TicketPriority Default = TicketPriority.Medium;

    public static PriorityDecision Decide(TicketPriority? requested, Category category)
    {
        if (category.ForcesCriticalPriority)
        {
            var overridden = requested is { } asked && asked != TicketPriority.Critical
                ? $" ({asked} was requested)"
                : string.Empty;

            return new PriorityDecision(
                TicketPriority.Critical, $"{category.Name} tickets are always Critical{overridden}.");
        }

        return requested is { } priority
            ? new PriorityDecision(priority, $"{priority} priority, as requested.")
            : new PriorityDecision(Default, $"No priority was requested, so {Default}.");
    }
}

/// <summary>A ticket's priority, and why.</summary>
public sealed record PriorityDecision(TicketPriority Priority, string Reason);

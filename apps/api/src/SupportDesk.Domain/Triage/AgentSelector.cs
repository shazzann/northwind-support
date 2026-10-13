using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Triage;

/// <summary>
/// Who owns a ticket. An agent is eligible when they are active, are a specialist in the
/// category if it requires one (BR-4), and hold strictly fewer open tickets than their limit.
/// Among the eligible, the fewest open tickets wins; ties go to the lowest agent id, so the
/// same situation always gives the same answer (BR-5).
/// </summary>
public static class AgentSelector
{
    /// <summary>Chooses an owner for a new ticket, or none, with the reason.</summary>
    public static AssignmentDecision Choose(Category category, IReadOnlyCollection<AgentCandidate> candidates)
    {
        var eligible = candidates
            .Where(candidate => IsEligible(candidate, category, candidate.OpenTicketCount))
            .OrderBy(candidate => candidate.OpenTicketCount)
            .ThenBy(candidate => candidate.Id)
            .ToList();

        if (eligible.Count == 0)
        {
            var who = category.RequiresSpecialist ? $"active {category.Name} specialist" : "active agent";
            return new AssignmentDecision(null, null, $"Left unassigned: no {who} has capacity.");
        }

        var chosen = eligible[0];
        return new AssignmentDecision(
            chosen.Id,
            chosen.FullName,
            $"Assigned to {chosen.FullName}, who has the fewest open tickets ({chosen.OpenTicketCount}) " +
            $"of {eligible.Count} eligible {(eligible.Count == 1 ? "agent" : "agents")}.");
    }

    /// <summary>
    /// Re-evaluates the owner of an existing ticket (BR-7): the current agent keeps it while
    /// still eligible, otherwise it is chosen afresh as for a new ticket.
    /// </summary>
    /// <remarks>
    /// The ticket already counts towards its current agent's open tickets. Keeping it adds no
    /// work, so the limit is checked without it: an agent at their limit keeps what they hold.
    /// </remarks>
    public static AssignmentDecision KeepOrChoose(
        Category category,
        IReadOnlyCollection<AgentCandidate> candidates,
        int? currentAgentId)
    {
        var current = candidates.FirstOrDefault(candidate => candidate.Id == currentAgentId);

        if (current is not null && IsEligible(current, category, current.OpenTicketCount - 1))
        {
            return new AssignmentDecision(current.Id, current.FullName, $"Kept with {current.FullName}, who is still eligible.");
        }

        var decision = Choose(category, [.. candidates.Where(candidate => candidate.Id != currentAgentId)]);

        return current is null
            ? decision
            : decision with { Reason = $"{current.FullName} is no longer eligible. {decision.Reason}" };
    }

    private static bool IsEligible(AgentCandidate candidate, Category category, int openTickets) =>
        candidate.IsActive
        && (!category.RequiresSpecialist || candidate.SpecializationIds.Contains(category.Id))
        && openTickets < candidate.MaxOpenTickets;
}

/// <summary>An agent as assignment sees them: their limit, current workload and specialisms.</summary>
public sealed record AgentCandidate(
    int Id,
    string FullName,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTicketCount,
    IReadOnlyCollection<int> SpecializationIds);

/// <summary>Who owns a ticket - nobody, when no agent is eligible - and why.</summary>
public sealed record AssignmentDecision(int? AgentId, string? AgentName, string Reason);

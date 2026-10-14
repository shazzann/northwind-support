using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// The read side of agents.
/// </summary>
public interface IAgentQueries
{
    Task<IReadOnlyList<AgentDto>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Every agent with what assignment needs to judge them - limit, open tickets and
    /// specialisms - in a single query. Eligibility itself is decided by <see cref="AgentSelector"/>.
    /// </summary>
    Task<IReadOnlyList<AgentCandidate>> GetCandidatesAsync(CancellationToken ct);

    /// <summary>Whether the agent exists and may still work - checked on every authenticated request.</summary>
    Task<bool> IsActiveAsync(int agentId, CancellationToken ct);
}

using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads <see cref="Agent"/> aggregates.
/// </summary>
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>Loads an agent by sign-in email, untracked, or null when nobody has it.</summary>
    Task<Agent?> GetByEmailAsync(string email, CancellationToken ct);
}

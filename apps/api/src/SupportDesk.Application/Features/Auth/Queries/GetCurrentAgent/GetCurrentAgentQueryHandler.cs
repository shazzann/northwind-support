using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;

/// <summary>
/// The agent the current token belongs to, read fresh. Authentication already refuses tokens of
/// deactivated agents; this also covers an agent removed since the token was issued.
/// </summary>
public sealed class GetCurrentAgentQueryHandler(ICurrentUser currentUser, IAgentRepository agents)
{
    /// <exception cref="UnauthorizedException">The agent no longer exists or is inactive.</exception>
    public async Task<SignedInAgentDto> HandleAsync(CancellationToken ct)
    {
        var agent = await agents.GetByIdAsync(currentUser.AgentId, ct);

        if (agent is null || !agent.IsActive)
        {
            throw new UnauthorizedException("This session no longer belongs to an active agent.");
        }

        return new SignedInAgentDto(agent.Id, agent.FullName, agent.Email);
    }
}

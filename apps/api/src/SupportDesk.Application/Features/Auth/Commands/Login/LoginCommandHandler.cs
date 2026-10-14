using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Signs an agent in with their email and password, and issues their token.
/// </summary>
/// <remarks>
/// Every failure - unknown email, wrong password, inactive agent, no password set - gives the
/// same answer, so a caller cannot learn which emails belong to agents.
/// </remarks>
public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwords,
    ITokenIssuer tokens,
    ILogger<LoginCommandHandler> logger)
{
    public const string FailureMessage = "Email or password is incorrect.";

    /// <exception cref="UnauthorizedException">The credentials do not sign anyone in.</exception>
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);

        // Always verify, even for an unknown email, so both failures take as long.
        var passwordMatches = passwords.Verify(agent?.PasswordHash, request.Password);

        if (agent is null || !passwordMatches || !agent.IsActive)
        {
            logger.LogWarning("Failed sign-in attempt.");
            throw new UnauthorizedException(FailureMessage);
        }

        var token = tokens.Issue(agent);

        logger.LogInformation("Agent {AgentId} signed in.", agent.Id);

        return new LoginResponse(
            token.Token,
            token.ExpiresAtUtc,
            new SignedInAgentDto(agent.Id, agent.FullName, agent.Email));
    }
}

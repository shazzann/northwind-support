using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

/// <summary>Issues the bearer token a signed-in agent sends with every request.</summary>
public interface ITokenIssuer
{
    IssuedToken Issue(Agent agent);
}

/// <summary>A token, and the moment it stops being accepted.</summary>
public sealed record IssuedToken(string Token, DateTime ExpiresAtUtc);

using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Presentation.Auth;

/// <summary>
/// Issues a signed JWT naming the agent: <c>sub</c> is their id, <c>name</c> and <c>email</c>
/// are for display. Signing and validation share <see cref="JwtOptions"/>, so they cannot drift.
/// </summary>
public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, IClock clock) : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public IssuedToken Issue(Agent agent)
    {
        var jwt = options.Value;
        var now = clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(jwt.LifetimeMinutes);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAtUtc,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, agent.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Name, agent.FullName),
                new Claim(JwtRegisteredClaimNames.Email, agent.Email)
            ]),
            SigningCredentials = new SigningCredentials(jwt.SecurityKey(), SecurityAlgorithms.HmacSha256)
        });

        return new IssuedToken(token, expiresAtUtc);
    }
}

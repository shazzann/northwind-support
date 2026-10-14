using System.Globalization;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Auth;

/// <summary>The agent named by the validated token on the current request.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int AgentId => int.Parse(Claim(JwtRegisteredClaimNames.Sub), CultureInfo.InvariantCulture);

    public string FullName => Claim(JwtRegisteredClaimNames.Name);

    private string Claim(string type)
    {
        var user = accessor.HttpContext?.User
            ?? throw new InvalidOperationException("There is no request in progress.");

        return user.FindFirstValue(type)
            ?? throw new InvalidOperationException($"The token has no '{type}' claim.");
    }
}

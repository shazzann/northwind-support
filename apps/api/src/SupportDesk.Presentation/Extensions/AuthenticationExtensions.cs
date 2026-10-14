using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Presentation.Auth;

namespace SupportDesk.Presentation.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// JWT bearer authentication, and a fallback policy that requires a signed-in agent on
    /// every endpoint that does not explicitly allow anonymous access. A new endpoint is
    /// therefore closed by default; opening one takes a visible <c>[AllowAnonymous]</c>.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidation>();

        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep claim names as issued ("sub", "name") rather than mapping them to URIs.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.SecurityKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                bearer.Events = new JwtBearerEvents
                {
                    // A valid signature only proves who the agent was when the token was issued.
                    // Re-check that they are still active, so deactivating an agent takes effect on
                    // their next request rather than when the token expires.
                    OnTokenValidated = async context =>
                    {
                        var agents = context.HttpContext.RequestServices.GetRequiredService<IAgentQueries>();
                        var subject = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

                        if (!int.TryParse(subject, CultureInfo.InvariantCulture, out var agentId)
                            || !await agents.IsActiveAsync(agentId, context.HttpContext.RequestAborted))
                        {
                            context.Fail("The agent for this token is no longer active.");
                        }
                    },

                    // Answer 401 with a problem document, like every other error from this API.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;
                        await context.Response.WriteAsJsonAsync(new ProblemDetails
                        {
                            Title = "Unauthorized",
                            Status = StatusCodes.Status401Unauthorized,
                            Detail = "Sign in to continue."
                        });
                    }
                };
            });

        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}

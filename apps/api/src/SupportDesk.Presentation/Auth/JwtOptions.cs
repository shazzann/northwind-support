using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SupportDesk.Presentation.Auth;

/// <summary>
/// How tokens are signed and checked, bound from the <c>Jwt</c> configuration section. The
/// signing key never appears in code: it comes from configuration, which in any shared
/// environment means the <c>Jwt__SigningKey</c> environment variable.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 wants a key at least as long as its 256-bit output.</summary>
    public const int MinimumKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    /// <summary>How long a token is accepted for. There are no refresh tokens: then sign in again.</summary>
    public int LifetimeMinutes { get; set; }

    public SymmetricSecurityKey SecurityKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

/// <summary>Refuses to start with a missing or weak signing key, rather than issue weak tokens.</summary>
public sealed class JwtOptionsValidation : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var errors = new List<string>();

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < JwtOptions.MinimumKeyBytes)
        {
            errors.Add(
                $"Jwt:SigningKey must be at least {JwtOptions.MinimumKeyBytes} bytes. " +
                "Set it with the Jwt__SigningKey environment variable (see .env.example).");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            errors.Add("Jwt:Issuer and Jwt:Audience are required.");
        }

        if (options.LifetimeMinutes <= 0)
        {
            errors.Add("Jwt:LifetimeMinutes must be a positive number of minutes.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

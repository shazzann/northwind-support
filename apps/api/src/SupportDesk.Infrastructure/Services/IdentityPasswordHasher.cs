using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Infrastructure.Services;

/// <summary>
/// ASP.NET Core Identity's password hasher: PBKDF2 with HMAC-SHA512, a random 128-bit salt per
/// password and 100,000 iterations, all recorded in the hash itself so they can be raised later.
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    // Identity's hasher is generic over a user type it never reads; this stands in for one.
    private static readonly object User = new();

    private readonly PasswordHasher<object> _hasher = new();

    // Verified against when there is no real hash, to take as long as a real check.
    private readonly Lazy<string> _decoy;

    public IdentityPasswordHasher()
    {
        _decoy = new Lazy<string>(() => Hash(Guid.NewGuid().ToString()));
    }

    public string Hash(string password) => _hasher.HashPassword(User, password);

    public bool Verify(string? passwordHash, string password)
    {
        var result = _hasher.VerifyHashedPassword(User, passwordHash ?? _decoy.Value, password);

        return passwordHash is not null && result != PasswordVerificationResult.Failed;
    }
}

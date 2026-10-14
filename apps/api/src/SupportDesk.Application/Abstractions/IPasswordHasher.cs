namespace SupportDesk.Application.Abstractions;

/// <summary>
/// One-way, salted password hashing. The algorithm lives in Infrastructure; nothing else ever
/// sees how a password is stored.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Whether <paramref name="password"/> matches <paramref name="passwordHash"/>. With no
    /// hash it still does the same work before answering false, so the time taken does not
    /// reveal whether an account exists.
    /// </summary>
    bool Verify(string? passwordHash, string password);
}

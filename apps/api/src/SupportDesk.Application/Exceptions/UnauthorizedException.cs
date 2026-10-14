namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when the caller is not who they need to be: wrong credentials, or a session that no
/// longer belongs to an active agent. Surfaces as HTTP 401.
/// </summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}

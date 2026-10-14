namespace SupportDesk.Application.Contracts.Auth;

/// <summary>Body of POST /api/auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>A successful sign-in: the token to send as a bearer token, and who it is for.</summary>
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, SignedInAgentDto Agent);

/// <summary>Enough about the signed-in agent to greet them.</summary>
public sealed record SignedInAgentDto(int Id, string FullName, string Email);

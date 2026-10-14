namespace SupportDesk.Application.Abstractions;

/// <summary>
/// The signed-in agent making the current request, as their token describes them. Only ever
/// read where authorization has already required a valid token.
/// </summary>
public interface ICurrentUser
{
    int AgentId { get; }

    string FullName { get; }
}

using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginCommandHandlerTests
{
    private const string Password = "correct horse";

    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<ITokenIssuer> _tokens = new();
    private readonly IdentityPasswordHasher _passwords = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _tokens.Setup(t => t.Issue(It.IsAny<Agent>()))
            .Returns(new IssuedToken("signed.jwt.token", FixedClock.DefaultNow.AddHours(1)));

        _handler = new LoginCommandHandler(
            _agents.Object, _passwords, _tokens.Object, NullLogger<LoginCommandHandler>.Instance);
    }

    [Fact]
    public async Task RightPassword_SignsInWithATokenAndTheAgentsName()
    {
        ExistingAgent("alex@northwind.test");

        var response = await _handler.HandleAsync(new LoginRequest("alex@northwind.test", Password), CancellationToken.None);

        Assert.Equal("signed.jwt.token", response.Token);
        Assert.Equal(FixedClock.DefaultNow.AddHours(1), response.ExpiresAtUtc);
        Assert.Equal("Alex Turner", response.Agent.FullName);
    }

    [Fact]
    public async Task WrongPassword_And_UnknownEmail_FailIdentically_WithoutIssuingAToken()
    {
        ExistingAgent("alex@northwind.test");

        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest("alex@northwind.test", "not it"), CancellationToken.None));
        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest("nobody@northwind.test", Password), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.FailureMessage, wrongPassword.Message);
        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<Agent>()), Times.Never());
    }

    [Fact]
    public async Task InactiveAgent_CannotSignIn_EvenWithTheRightPassword()
    {
        ExistingAgent("yuki@northwind.test").Deactivate();

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest("yuki@northwind.test", Password), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.FailureMessage, exception.Message);
    }

    [Fact]
    public async Task AgentWithoutAPassword_CannotSignIn()
    {
        var agent = new Agent("Ben Osei", "ben@northwind.test", 8, FixedClock.DefaultNow);
        _agents.Setup(a => a.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest(agent.Email, ""), CancellationToken.None));
    }

    private Agent ExistingAgent(string email)
    {
        var agent = new Agent("Alex Turner", email, 10, FixedClock.DefaultNow).WithId(1);
        agent.SetPasswordHash(_passwords.Hash(Password));
        _agents.Setup(a => a.GetByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        return agent;
    }
}

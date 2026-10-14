using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.ApiTests;

public sealed class AuthenticationTests(SupportDeskApiFactory api) : IClassFixture<SupportDeskApiFactory>
{
    private const string Alex = "alex.turner@northwind-support.example";

    [Fact]
    public async Task ProtectedEndpoint_WithoutAToken_Is401()
    {
        var response = await api.CreateClient().GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAToken_Is200()
    {
        var client = await api.SignedInClientAsync();

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TamperedToken_Is401()
    {
        var client = await api.SignedInClientAsync();
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tickets")).StatusCode);
    }

    [Fact]
    public async Task WrongPassword_And_UnknownEmail_Give_TheSame401_AndNoToken()
    {
        var client = api.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Alex, "not-the-password"));
        var unknownEmail = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("nobody@northwind-support.example", SupportDbSeeder.DevelopmentPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        var wrongPasswordBody = await Problem(wrongPassword);
        Assert.Equal(LoginCommandHandler.FailureMessage, wrongPasswordBody.GetProperty("detail").GetString());
        Assert.Equal(wrongPasswordBody.GetProperty("detail").GetString(), (await Problem(unknownEmail)).GetProperty("detail").GetString());
        Assert.False(wrongPasswordBody.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task InactiveAgent_CannotSignIn()
    {
        var response = await api.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("yuki.tanaka@northwind-support.example", SupportDbSeeder.DevelopmentPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatingAnAgent_RefusesTheirTokenAtOnce_NotAtExpiry()
    {
        var client = await api.SignedInClientAsync("ben.osei@northwind-support.example");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/tickets")).StatusCode);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var ben = await db.Set<Agent>().SingleAsync(a => a.Email == "ben.osei@northwind-support.example");
            ben.Deactivate();
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tickets")).StatusCode);
    }

    [Fact]
    public async Task Me_IsTheSignedInAgent_Or401()
    {
        var client = await api.SignedInClientAsync();

        var me = await client.GetFromJsonAsync<SignedInAgentDto>("/api/auth/me");

        Assert.Equal("Alex Turner", me!.FullName);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Escalation_IsRecordedAgainstTheSignedInAgent_NotWhatTheBodyClaims()
    {
        var client = await api.SignedInClientAsync("priya.nair@northwind-support.example");

        // TCK-0002 is a Medium, open ticket in the seed data.
        var response = await client.PostAsJsonAsync(
            "/api/tickets/2/escalate", new { reason = "Customer cannot export anything", escalatedBy = "Somebody Else" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Priya Nair", body.GetProperty("escalation").GetProperty("escalatedBy").GetString());
    }

    [Fact]
    public void OnlyLoginAndHealth_AreOpenToAnonymousCallers()
    {
        var anonymous = api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Order();

        Assert.Equal(["/health", "api/auth/login"], anonymous);
    }

    [Fact]
    public async Task Health_IsOpen_AndReportsTheDatabase()
    {
        var response = await api.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", report.GetProperty("status").GetString());
        Assert.Equal("Healthy", report.GetProperty("checks").GetProperty("database").GetString());
    }

    private static async Task<JsonElement> Problem(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}

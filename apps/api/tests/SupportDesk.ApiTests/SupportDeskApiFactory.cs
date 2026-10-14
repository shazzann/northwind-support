using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.ApiTests;

/// <summary>
/// The real API, hosted in memory over SQLite in place of SQL Server, with the development
/// seed data - agents, their development password and the 40 tickets.
/// </summary>
public sealed class SupportDeskApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // The in-memory database lives exactly as long as this connection stays open.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<SupportDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<SupportDbSeeder>().SeedAsync();
    }

    /// <summary>A client carrying the token of the agent with this email.</summary>
    public async Task<HttpClient> SignedInClientAsync(string email = "alex.turner@northwind-support.example")
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(email, SupportDbSeeder.DevelopmentPassword));
        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", "replaced-with-sqlite-below");
        builder.UseSetting("Jwt:SigningKey", "api-tests-only-signing-key-of-32-bytes-or-more");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SupportDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();

            _connection.Open();
            services.AddDbContext<SupportDbContext>(options => options.UseSqlite(_connection));
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _connection.DisposeAsync();
        await DisposeAsync();
    }
}

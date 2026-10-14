using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SupportDesk.Presentation.Extensions;

public static class HealthCheckExtensions
{
    /// <summary>
    /// GET /health: 200 when every check (today, the database) passes and 503 when one does not,
    /// with a small JSON report. Open without a token, so an orchestrator or load balancer can
    /// probe it - the one deliberate exception to the fallback policy besides sign-in.
    /// </summary>
    public static IEndpointConventionBuilder MapHealthEndpoint(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteReportAsync })
            .AllowAnonymous();

    private static Task WriteReportAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())
        });
}

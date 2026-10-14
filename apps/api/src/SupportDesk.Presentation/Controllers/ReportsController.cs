using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Features.Reports.Queries.GetSlaSummary;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController : ControllerBase
{
    /// <summary>
    /// SLA outcomes per agent or per priority for tickets created in [from, to): totals, breached,
    /// at risk, within, met, breach rate and average resolution hours.
    /// </summary>
    [HttpGet("sla-summary")]
    [ProducesResponseType<SlaSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<SlaSummaryDto> SlaSummary(
        [FromQuery] SlaSummaryQuery query,
        [FromServices] GetSlaSummaryQueryHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(query, ct);
}

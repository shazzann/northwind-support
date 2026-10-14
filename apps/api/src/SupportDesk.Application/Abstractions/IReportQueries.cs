using SupportDesk.Application.Contracts.Reports;

namespace SupportDesk.Application.Abstractions;

/// <summary>Reporting reads: aggregated in the database, never by loading tickets.</summary>
public interface IReportQueries
{
    /// <summary>SLA outcomes per group, in a single aggregate query.</summary>
    /// <param name="query">The date range and grouping, already validated.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<SlaSummaryCounts>> GetSlaSummaryAsync(SlaSummaryQuery query, CancellationToken ct);
}

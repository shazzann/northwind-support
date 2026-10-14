using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Reports;

namespace SupportDesk.Application.Features.Reports.Queries.GetSlaSummary;

/// <summary>
/// The SLA summary: the database counts each group's outcomes in one aggregate query; this
/// turns those counts into rates and hours.
/// </summary>
public sealed class GetSlaSummaryQueryHandler(IReportQueries reports)
{
    public async Task<SlaSummaryDto> HandleAsync(SlaSummaryQuery query, CancellationToken ct)
    {
        var counts = await reports.GetSlaSummaryAsync(query, ct);

        return new SlaSummaryDto(query.From, query.To, query.GroupBy!.Value, [.. counts.Select(Summarise)]);
    }

    /// <summary>The figures a reader wants from one group's raw counts.</summary>
    public static SlaSummaryGroupDto Summarise(SlaSummaryCounts counts)
    {
        var withSla = counts.Breached + counts.AtRisk + counts.WithinSla + counts.Met;

        return new SlaSummaryGroupDto(
            counts.Group,
            counts.Total,
            counts.Breached,
            counts.AtRisk,
            counts.WithinSla,
            counts.Met,
            NoSla: counts.Total - withSla,
            BreachRate: withSla == 0 ? null : Math.Round((double)counts.Breached / withSla, 3),
            AverageResolutionHours: counts.AverageResolutionMinutes is { } minutes ? Math.Round(minutes / 60, 1) : null);
    }
}

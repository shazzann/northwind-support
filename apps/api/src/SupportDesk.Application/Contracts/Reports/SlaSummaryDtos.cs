namespace SupportDesk.Application.Contracts.Reports;

/// <summary>What the SLA summary groups tickets by.</summary>
public enum SlaSummaryGrouping
{
    Agent = 1,
    Priority = 2
}

/// <summary>Query string of GET /api/reports/sla-summary.</summary>
public sealed record SlaSummaryQuery
{
    /// <summary>Only tickets created at or after this moment (UTC). Open-ended when absent.</summary>
    public DateTime? From { get; init; }

    /// <summary>Only tickets created before this moment (UTC). Open-ended when absent.</summary>
    public DateTime? To { get; init; }

    /// <summary>agent | priority.</summary>
    public SlaSummaryGrouping? GroupBy { get; init; }
}

/// <summary>
/// One group's raw numbers, exactly as the database aggregated them. SLA statuses are as of
/// now, by the same rule the ticket list uses.
/// </summary>
/// <param name="Group">The agent's name, "Unassigned", or the priority.</param>
public sealed record SlaSummaryCounts(
    string Group,
    int Total,
    int Breached,
    int AtRisk,
    int WithinSla,
    int Met,
    double? AverageResolutionMinutes);

/// <summary>Response of GET /api/reports/sla-summary.</summary>
public sealed record SlaSummaryDto(
    DateTime? From,
    DateTime? To,
    SlaSummaryGrouping GroupBy,
    IReadOnlyList<SlaSummaryGroupDto> Groups);

/// <param name="NoSla">Tickets without a due date; counted in Total but not in BreachRate.</param>
/// <param name="BreachRate">Breached as a share (0-1) of the tickets that have an SLA; null when none do.</param>
/// <param name="AverageResolutionHours">Creation to resolution, over resolved tickets; null when none are.</param>
public sealed record SlaSummaryGroupDto(
    string Group,
    int Total,
    int Breached,
    int AtRisk,
    int WithinSla,
    int Met,
    int NoSla,
    double? BreachRate,
    double? AverageResolutionHours);

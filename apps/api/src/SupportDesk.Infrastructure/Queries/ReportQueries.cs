using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Reports, aggregated by SQL Server in one statement: GROUP BY with conditional counts. The
/// SLA status in each count is the domain's own rule, composed in as for the ticket list.
/// </summary>
/// <remarks>
/// Resolution time uses SQL Server's DATEDIFF, so these queries are tested against SQL Server
/// rather than SQLite.
/// </remarks>
public sealed class ReportQueries(SupportDbContext db, IClock clock, SlaEvaluator sla) : IReportQueries
{
    private const string Unassigned = "Unassigned";

    private static readonly Expression<Func<TicketWithAgent, SlaStatus, ReportRow>> ToRow =
        (x, slaStatus) => new ReportRow
        {
            AgentId = x.Ticket.AssignedAgentId,
            AgentName = x.Agent == null ? null : x.Agent.FullName,
            Priority = x.Ticket.Priority,
            SlaStatus = slaStatus,
            ResolutionMinutes = x.Ticket.ResolvedAtUtc == null
                ? null
                : EF.Functions.DateDiffMinute(x.Ticket.CreatedAtUtc, x.Ticket.ResolvedAtUtc.Value)
        };

    public async Task<IReadOnlyList<SlaSummaryCounts>> GetSlaSummaryAsync(SlaSummaryQuery query, CancellationToken ct)
    {
        Expression<Func<TicketWithAgent, Ticket>> ticketOf = x => x.Ticket;
        var rows = TicketsWithAgents(query.From, query.To).Select(ToRow.With(ticketOf.Then(sla.StatusAt(clock.UtcNow))));

        if (query.GroupBy == SlaSummaryGrouping.Agent)
        {
            var byAgent = await Count(rows, row => new AgentKey(row.AgentId, row.AgentName)).ToListAsync(ct);

            return
            [
                .. byAgent
                    .OrderBy(group => group.Key.Id is null)
                    .ThenBy(group => group.Key.Name)
                    .Select(group => group.Labelled(group.Key.Name ?? Unassigned))
            ];
        }

        var byPriority = await Count(rows, row => row.Priority).ToListAsync(ct);

        return [.. byPriority.OrderByDescending(group => group.Key).Select(group => group.Labelled(group.Key.ToString()))];
    }

    /// <summary>The one aggregation, for any grouping key.</summary>
    private static IQueryable<GroupCounts<TKey>> Count<TKey>(IQueryable<ReportRow> rows, Expression<Func<ReportRow, TKey>> key) =>
        rows.GroupBy(key).Select(group => new GroupCounts<TKey>
        {
            Key = group.Key,
            Total = group.Count(),
            Breached = group.Count(row => row.SlaStatus == SlaStatus.Breached),
            AtRisk = group.Count(row => row.SlaStatus == SlaStatus.AtRisk),
            WithinSla = group.Count(row => row.SlaStatus == SlaStatus.WithinSla),
            Met = group.Count(row => row.SlaStatus == SlaStatus.Met),
            AverageResolutionMinutes = group.Average(row => (double?)row.ResolutionMinutes)
        });

    private IQueryable<TicketWithAgent> TicketsWithAgents(DateTime? fromUtc, DateTime? toUtc) =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        where (fromUtc == null || ticket.CreatedAtUtc >= fromUtc) && (toUtc == null || ticket.CreatedAtUtc < toUtc)
        from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
        select new TicketWithAgent { Ticket = ticket, Agent = agent };

    private sealed class TicketWithAgent
    {
        public required Ticket Ticket { get; init; }

        public Agent? Agent { get; init; }
    }

    private sealed class ReportRow
    {
        public int? AgentId { get; init; }

        public string? AgentName { get; init; }

        public TicketPriority Priority { get; init; }

        public SlaStatus SlaStatus { get; init; }

        public int? ResolutionMinutes { get; init; }
    }

    private sealed record AgentKey(int? Id, string? Name);

    private sealed class GroupCounts<TKey>
    {
        public required TKey Key { get; init; }

        public int Total { get; init; }

        public int Breached { get; init; }

        public int AtRisk { get; init; }

        public int WithinSla { get; init; }

        public int Met { get; init; }

        public double? AverageResolutionMinutes { get; init; }

        public SlaSummaryCounts Labelled(string group) =>
            new(group, Total, Breached, AtRisk, WithinSla, Met, AverageResolutionMinutes);
    }
}

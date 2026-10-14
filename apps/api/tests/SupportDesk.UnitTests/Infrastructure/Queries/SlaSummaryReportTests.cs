using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Features.Reports.Queries.GetSlaSummary;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// The SLA summary's single aggregate query, against a real SQL Server (it uses DATEDIFF).
/// Every ticket has a 4 h window starting when it was raised; the clock reads
/// <see cref="FixedClock.DefaultNow"/>.
/// </summary>
public sealed class SlaSummaryReportTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly TicketQueryDatabase? _database;

    public SlaSummaryReportTests()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerFactAttribute.ConnectionStringVariable)))
        {
            return;
        }

        _database = TicketQueryDatabase.OnSqlServer();
        var db = _database;

        Raise("TCK-0001", TicketPriority.High, hoursAgo: 1, agent: db.Alex);                         // within SLA
        Raise("TCK-0002", TicketPriority.High, hoursAgo: 3.5, agent: db.Alex);                       // at risk
        Raise("TCK-0003", TicketPriority.Low, hoursAgo: 6, agent: db.Alex, resolvedAfterHours: 2);   // met, 2 h
        Raise("TCK-0004", TicketPriority.Low, hoursAgo: 10, agent: db.Sara, resolvedAfterHours: 6);  // breached, 6 h
        Raise("TCK-0005", TicketPriority.High, hoursAgo: 5);                                         // breached, unassigned
        db.AddTicket("TCK-0006", createdAt: Now.AddHours(-1));                                       // no SLA, unassigned
        Raise("TCK-0007", TicketPriority.High, hoursAgo: 24 * 30, agent: db.Alex, resolvedAfterHours: 1); // outside the range
    }

    public void Dispose() => _database?.Dispose();

    [SqlServerFact]
    public async Task ByAgent_CountsEachOutcome_WithUnassignedLast()
    {
        var report = await SummaryAsync(SlaSummaryGrouping.Agent);

        Assert.Equal(["Alex Turner", "Sara Lindqvist", "Unassigned"], report.Groups.Select(g => g.Group));
        Assert.Equal(new SlaSummaryGroupDto("Alex Turner", 3, 0, 1, 1, 1, 0, 0, 2), report.Groups[0]);
        Assert.Equal(new SlaSummaryGroupDto("Sara Lindqvist", 1, 1, 0, 0, 0, 0, 1, 6), report.Groups[1]);
        Assert.Equal(new SlaSummaryGroupDto("Unassigned", 2, 1, 0, 0, 0, 1, 1, null), report.Groups[2]);
    }

    [SqlServerFact]
    public async Task ByPriority_MostUrgentFirst_AveragingResolvedTicketsOnly()
    {
        var report = await SummaryAsync(SlaSummaryGrouping.Priority);

        Assert.Equal(["High", "Medium", "Low"], report.Groups.Select(g => g.Group));
        Assert.Equal(new SlaSummaryGroupDto("High", 3, 1, 1, 1, 0, 0, 0.333, null), report.Groups[0]);
        Assert.Equal(new SlaSummaryGroupDto("Low", 2, 1, 0, 0, 1, 0, 0.5, 4), report.Groups[2]);
    }

    [SqlServerFact]
    public async Task WithoutARange_IncludesEveryTicket()
    {
        var report = await SummaryAsync(SlaSummaryGrouping.Priority, wholeHistory: true);

        Assert.Equal(7, report.Groups.Sum(g => g.Total));
    }

    /// <summary>The last day up to now, unless <paramref name="wholeHistory"/>.</summary>
    private Task<SlaSummaryDto> SummaryAsync(SlaSummaryGrouping groupBy, bool wholeHistory = false) =>
        new GetSlaSummaryQueryHandler(new ReportQueries(_database!.Db, new FixedClock(), TestSla.Evaluator()))
            .HandleAsync(
                new SlaSummaryQuery
                {
                    From = wholeHistory ? null : Now.AddDays(-1),
                    To = wholeHistory ? null : Now,
                    GroupBy = groupBy
                },
                CancellationToken.None);

    private void Raise(
        string reference,
        TicketPriority priority,
        double hoursAgo,
        Agent? agent = null,
        double? resolvedAfterHours = null)
    {
        var raisedAt = Now.AddHours(-hoursAgo);

        _database!.AddTicket(
            reference,
            priority: priority,
            agent: agent,
            createdAt: raisedAt,
            slaStartedAt: raisedAt,
            resolvedAfter: resolvedAfterHours is { } hours ? TimeSpan.FromHours(hours) : null);
    }
}

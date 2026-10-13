using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// Escalations saved through the ticket aggregate and read back through the history query,
/// against a real database.
/// </summary>
public sealed class TicketEscalationHistoryTests : IDisposable
{
    private readonly TicketQueryDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task History_IsNewestFirst_WithAgentNames()
    {
        var ticket = _database.AddTicket("TCK-0001", priority: TicketPriority.Low, agent: _database.Alex);
        _database.Db.Attach(ticket);
        ticket.Escalate(TestSla.Policy(), CustomerTier.Standard, _database.Sara.Id, "First escalation", "Lead A", FixedClock.DefaultNow.AddHours(1));
        ticket.Escalate(TestSla.Policy(), CustomerTier.Standard, _database.Sara.Id, "Second escalation", "Lead B", FixedClock.DefaultNow.AddHours(2));
        await _database.Db.SaveChangesAsync();

        var history = await Queries().GetEscalationsAsync(ticket.Id, CancellationToken.None);

        Assert.NotNull(history);
        Assert.Equal(["Second escalation", "First escalation"], history.Select(e => e.Reason));
        Assert.Equal((TicketPriority.Medium, TicketPriority.High), (history[0].FromPriority, history[0].ToPriority));
        Assert.Equal("Alex Turner", history[1].FromAgent?.FullName);
        Assert.Equal("Sara Lindqvist", history[1].ToAgent?.FullName);
    }

    [Fact]
    public async Task History_OfATicketNeverEscalated_IsEmpty()
    {
        var ticket = _database.AddTicket("TCK-0001");

        Assert.Empty((await Queries().GetEscalationsAsync(ticket.Id, CancellationToken.None))!);
    }

    [Fact]
    public async Task History_OfAnUnknownTicket_IsNull() =>
        Assert.Null(await Queries().GetEscalationsAsync(404, CancellationToken.None));

    private TicketQueries Queries() => new(_database.Db, new FixedClock(), TestSla.Evaluator());
}

using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Queries.SearchTickets;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// The ticket list's filters, run as SQL against a real database through the same handler the
/// API uses, so normalising, filtering, counting and paging are all exercised together.
/// </summary>
public sealed class TicketQueriesFilterTests : IDisposable
{
    private readonly TicketQueryDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Priority_IsAnExactMatch()
    {
        _database.AddTicket("TCK-0001", priority: TicketPriority.Low);
        _database.AddTicket("TCK-0002", priority: TicketPriority.High);
        _database.AddTicket("TCK-0003", priority: TicketPriority.Critical);
        _database.AddTicket("TCK-0004", priority: TicketPriority.High);

        var result = await SearchAsync(new TicketQuery { Priority = TicketPriority.High });

        Assert.Equal(["TCK-0002", "TCK-0004"], References(result));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Status_IsAnExactMatch()
    {
        _database.AddTicket("TCK-0001", status: TicketStatus.Open);
        _database.AddTicket("TCK-0002", status: TicketStatus.InProgress);

        var result = await SearchAsync(new TicketQuery { Status = TicketStatus.InProgress });

        Assert.Equal(["TCK-0002"], References(result));
    }

    [Fact]
    public async Task CategoryCustomerAndAgent_AreExactMatches()
    {
        _database.AddTicket("TCK-0001", category: _database.Billing);
        _database.AddTicket("TCK-0002", customer: _database.Contoso);
        _database.AddTicket("TCK-0003", agent: _database.Sara);
        _database.AddTicket("TCK-0004");

        Assert.Equal(["TCK-0001"], References(await SearchAsync(new TicketQuery { CategoryId = _database.Billing.Id })));
        Assert.Equal(["TCK-0002"], References(await SearchAsync(new TicketQuery { CustomerId = _database.Contoso.Id })));
        Assert.Equal(["TCK-0003"], References(await SearchAsync(new TicketQuery { AssignedAgentId = _database.Sara.Id })));
    }

    [Fact]
    public async Task UnassignedOnly_ReturnsOnlyTicketsWithNoAgent()
    {
        _database.AddTicket("TCK-0001", agent: _database.Alex);
        _database.AddTicket("TCK-0002");

        var result = await SearchAsync(new TicketQuery { UnassignedOnly = true });

        Assert.Equal(["TCK-0002"], References(result));
    }

    [Fact]
    public async Task SuppliedFilters_CombineWithAnd()
    {
        _database.AddTicket("TCK-0001", customer: _database.Contoso, priority: TicketPriority.High, status: TicketStatus.Open);
        _database.AddTicket("TCK-0002", customer: _database.Contoso, priority: TicketPriority.High, status: TicketStatus.Resolved);
        _database.AddTicket("TCK-0003", customer: _database.Contoso, priority: TicketPriority.Low, status: TicketStatus.Open);
        _database.AddTicket("TCK-0004", customer: _database.Fabrikam, priority: TicketPriority.High, status: TicketStatus.Open);

        var result = await SearchAsync(new TicketQuery
        {
            CustomerId = _database.Contoso.Id,
            Priority = TicketPriority.High,
            Status = TicketStatus.Open
        });

        Assert.Equal(["TCK-0001"], References(result));
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_MatchesTheCustomerName()
    {
        _database.AddTicket("TCK-0001", customer: _database.Contoso);
        _database.AddTicket("TCK-0002", customer: _database.Fabrikam);

        var result = await SearchAsync(new TicketQuery { Search = "Contoso" });

        Assert.Equal(["TCK-0001"], References(result));
    }

    [Fact]
    public async Task Search_MatchesTheTitleOrTheReference()
    {
        _database.AddTicket("TCK-0001", title: "Invoice total is wrong");
        _database.AddTicket("TCK-0042", title: "Cannot sign in");
        _database.AddTicket("TCK-0003", title: "Printer on fire");

        Assert.Equal(["TCK-0001"], References(await SearchAsync(new TicketQuery { Search = "Invoice" })));
        Assert.Equal(["TCK-0042"], References(await SearchAsync(new TicketQuery { Search = "TCK-0042" })));
    }

    [Fact]
    public async Task Search_IsTrimmed()
    {
        _database.AddTicket("TCK-0001", customer: _database.Contoso);
        _database.AddTicket("TCK-0002", customer: _database.Fabrikam);

        var result = await SearchAsync(new TicketQuery { Search = "  Contoso  " });

        Assert.Equal(["TCK-0001"], References(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankSearch_IsIgnored(string search)
    {
        _database.AddTicket("TCK-0001");
        _database.AddTicket("TCK-0002");

        var result = await SearchAsync(new TicketQuery { Search = search });

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task CountAndPages_DescribeTheFilteredSet()
    {
        for (var i = 1; i <= 30; i++)
        {
            _database.AddTicket($"TCK-{i:0000}", priority: i <= 25 ? TicketPriority.High : TicketPriority.Low);
        }

        var result = await SearchAsync(new TicketQuery { Priority = TicketPriority.High, Page = 3, PageSize = 10 });

        Assert.Equal(25, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(5, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal(TicketPriority.High, item.Priority));
    }

    private Task<PagedResult<TicketListItemDto>> SearchAsync(TicketQuery query) =>
        new SearchTicketsQueryHandler(new TicketQueries(_database.Db, new FixedClock(), TestSla.Evaluator()))
            .HandleAsync(query, CancellationToken.None);

    private static string[] References(PagedResult<TicketListItemDto> result) =>
        [.. result.Items.Select(item => item.Reference).Order()];
}

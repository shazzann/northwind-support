using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// A real, throwaway database for read-side tests: SQLite in memory, built from the same EF
/// model as production. Queries run as SQL, so anything EF cannot translate fails here too.
/// </summary>
internal sealed class TicketQueryDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TicketQueryDatabase()
    {
        // The in-memory database lives exactly as long as this connection stays open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);
        Db.Database.EnsureCreated();

        Contoso = Add(new Customer("Contoso Ltd", "support@contoso.test", null, CustomerTier.Premium, FixedClock.DefaultNow));
        Fabrikam = Add(new Customer("Fabrikam Inc", "help@fabrikam.test", null, CustomerTier.Standard, FixedClock.DefaultNow));
        General = Add(new Category("General", requiresSpecialist: false, forcesCriticalPriority: false));
        Billing = Add(new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false));
        Alex = Add(new Agent("Alex Turner", "alex@northwind.test", 10, FixedClock.DefaultNow));
        Sara = Add(new Agent("Sara Lindqvist", "sara@northwind.test", 6, FixedClock.DefaultNow));
    }

    public SupportDbContext Db { get; }

    public Customer Contoso { get; }

    public Customer Fabrikam { get; }

    public Category General { get; }

    public Category Billing { get; }

    public Agent Alex { get; }

    public Agent Sara { get; }

    /// <summary>Saves a ticket; anything not given gets a harmless default.</summary>
    public Ticket AddTicket(
        string reference,
        string title = "Something is not working",
        Customer? customer = null,
        Category? category = null,
        TicketPriority priority = TicketPriority.Medium,
        TicketStatus status = TicketStatus.New,
        Agent? agent = null)
    {
        var ticket = Ticket.Raise(
            reference,
            title,
            "A description long enough to be realistic.",
            (customer ?? Fabrikam).Id,
            (category ?? General).Id,
            priority,
            FixedClock.DefaultNow);

        if (agent is not null)
        {
            ticket.AssignTo(agent.Id, FixedClock.DefaultNow);
        }

        if (status != TicketStatus.New)
        {
            ticket.ChangeStatus(status, FixedClock.DefaultNow);
        }

        return Add(ticket);
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }

    private T Add<T>(T entity)
        where T : class
    {
        Db.Add(entity);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return entity;
    }
}

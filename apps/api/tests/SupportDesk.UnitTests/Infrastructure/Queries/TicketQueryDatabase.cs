using Microsoft.Data.SqlClient;
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
/// A real, throwaway database for read-side tests, built from the same EF model as production.
/// Queries run as SQL, so anything EF cannot translate fails here too. SQLite in memory by
/// default; <see cref="OnSqlServer"/> for queries that use SQL Server's own functions.
/// </summary>
internal sealed class TicketQueryDatabase : IDisposable
{
    private readonly SqliteConnection? _connection;

    /// <summary>SQLite in memory: fast, and needs nothing installed.</summary>
    public TicketQueryDatabase()
        : this(OpenSqlite(out var connection), connection)
    {
    }

    private TicketQueryDatabase(DbContextOptions<SupportDbContext> options, SqliteConnection? connection)
    {
        _connection = connection;

        Db = new SupportDbContext(options);
        Db.Database.EnsureCreated();

        Contoso = Add(new Customer("Contoso Ltd", "support@contoso.test", null, CustomerTier.Premium, FixedClock.DefaultNow));
        Fabrikam = Add(new Customer("Fabrikam Inc", "help@fabrikam.test", null, CustomerTier.Standard, FixedClock.DefaultNow));
        General = Add(new Category("General", requiresSpecialist: false, forcesCriticalPriority: false));
        Billing = Add(new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false));
        Alex = Add(new Agent("Alex Turner", "alex@northwind.test", 10, FixedClock.DefaultNow));
        Sara = Add(new Agent("Sara Lindqvist", "sara@northwind.test", 6, FixedClock.DefaultNow));
    }

    public SupportDbContext Db { get; }

    /// <summary>
    /// A fresh database with a unique name on the SQL Server in
    /// <see cref="SqlServerFactAttribute.ConnectionStringVariable"/>, dropped on dispose.
    /// </summary>
    public static TicketQueryDatabase OnSqlServer()
    {
        var connectionString = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable(SqlServerFactAttribute.ConnectionStringVariable))
        {
            InitialCatalog = $"SupportDeskTests_{Guid.NewGuid():N}"
        }.ConnectionString;

        return new TicketQueryDatabase(
            new DbContextOptionsBuilder<SupportDbContext>().UseSqlServer(connectionString).Options,
            connection: null);
    }

    public Customer Contoso { get; }

    public Customer Fabrikam { get; }

    public Category General { get; }

    public Category Billing { get; }

    public Agent Alex { get; }

    public Agent Sara { get; }

    /// <summary>Saves a ticket; anything not given gets a harmless default.</summary>
    /// <param name="slaStartedAt">Starts a 4 h SLA window here; no SLA when null.</param>
    /// <param name="resolvedAfter">Resolves the ticket this long after its window started.</param>
    /// <param name="createdAt">When the ticket was raised; <see cref="FixedClock.DefaultNow"/> by default.</param>
    public Ticket AddTicket(
        string reference,
        string title = "Something is not working",
        Customer? customer = null,
        Category? category = null,
        TicketPriority priority = TicketPriority.Medium,
        TicketStatus status = TicketStatus.New,
        Agent? agent = null,
        DateTime? slaStartedAt = null,
        TimeSpan? resolvedAfter = null,
        DateTime? createdAt = null)
    {
        var ticket = Ticket.Raise(
            reference,
            title,
            "A description long enough to be realistic.",
            (customer ?? Fabrikam).Id,
            (category ?? General).Id,
            priority,
            createdAt ?? FixedClock.DefaultNow);

        if (agent is not null)
        {
            ticket.AssignTo(agent.Id, FixedClock.DefaultNow);
        }

        if (status != TicketStatus.New)
        {
            ticket.ChangeStatus(status, FixedClock.DefaultNow);
        }

        if (slaStartedAt is { } start)
        {
            ticket.StartSla(start, 4 * 60);

            if (resolvedAfter is { } after)
            {
                ticket.ChangeStatus(TicketStatus.Resolved, start + after);
            }
        }

        return Add(ticket);
    }

    public void Dispose()
    {
        if (_connection is null)
        {
            Db.Database.EnsureDeleted();
        }

        Db.Dispose();
        _connection?.Dispose();
    }

    private static DbContextOptions<SupportDbContext> OpenSqlite(out SqliteConnection connection)
    {
        // The in-memory database lives exactly as long as this connection stays open.
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        return new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options;
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

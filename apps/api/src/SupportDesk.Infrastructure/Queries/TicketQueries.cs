using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
/// <remarks>
/// SLA status is computed by the database, with the domain's own rule
/// (<see cref="SlaEvaluator.StatusAt"/>) composed into each projection.
/// </remarks>
public sealed class TicketQueries(SupportDbContext db, IClock clock, SlaEvaluator sla) : ITicketQueries
{
    private static readonly Expression<Func<TicketWithLabels, SlaStatus, TicketListItemDto>> ToListItem =
        (x, slaStatus) => new TicketListItemDto
        {
            Id = x.Ticket.Id,
            Reference = x.Ticket.Reference,
            Title = x.Ticket.Title,
            Status = x.Ticket.Status,
            Priority = x.Ticket.Priority,
            Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
            Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
            AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
            CreatedAtUtc = x.Ticket.CreatedAtUtc,
            UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
            DueAtUtc = x.Ticket.DueAtUtc,
            ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
            SlaStatus = slaStatus
        };

    private static readonly Expression<Func<TicketWithLabels, SlaStatus, TicketDetailDto>> ToDetail =
        (x, slaStatus) => new TicketDetailDto
        {
            Id = x.Ticket.Id,
            Reference = x.Ticket.Reference,
            Title = x.Ticket.Title,
            Description = x.Ticket.Description,
            Status = x.Ticket.Status,
            Priority = x.Ticket.Priority,
            Customer = new CustomerContactDto(
                x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
            Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
            AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
            CreatedAtUtc = x.Ticket.CreatedAtUtc,
            UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
            DueAtUtc = x.Ticket.DueAtUtc,
            ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
            SlaStatus = slaStatus
        };

    /// <remarks>
    /// Filters in SQL before counting and paging, so the total and the pager always describe
    /// the filtered set.
    /// </remarks>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        var slaStatus = SlaStatusAt(clock.UtcNow);

        var tickets = ApplyFilters(TicketsWithLabels(), query, slaStatus);

        var totalCount = await tickets.CountAsync(ct);

        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToListItem.With(slaStatus))
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct) =>
        TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(ToDetail.With(SlaStatusAt(clock.UtcNow)))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct) =>
        await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(ToListItem.With(SlaStatusAt(clock.UtcNow)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TicketEscalationDto>?> GetEscalationsAsync(int ticketId, CancellationToken ct)
    {
        if (!await db.Set<Ticket>().AnyAsync(t => t.Id == ticketId, ct))
        {
            return null;
        }

        return await (
                from escalation in db.Set<TicketEscalation>().AsNoTracking()
                where escalation.TicketId == ticketId
                from fromAgent in db.Set<Agent>().Where(a => a.Id == escalation.FromAgentId).DefaultIfEmpty()
                from toAgent in db.Set<Agent>().Where(a => a.Id == escalation.ToAgentId).DefaultIfEmpty()
                orderby escalation.EscalatedAtUtc descending, escalation.Id descending
                select new TicketEscalationDto(
                    escalation.Id,
                    escalation.FromPriority,
                    escalation.ToPriority,
                    fromAgent == null ? null : new AgentSummaryDto(fromAgent.Id, fromAgent.FullName),
                    toAgent == null ? null : new AgentSummaryDto(toAgent.Id, toAgent.FullName),
                    escalation.FromDueAtUtc,
                    escalation.ToDueAtUtc,
                    escalation.Reason,
                    escalation.EscalatedBy,
                    escalation.EscalatedAtUtc))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Every ticket with the rows that label it. Filter and sort this, then project.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>() on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>() on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
        select new TicketWithLabels { Ticket = ticket, Customer = customer, Category = category, Agent = agent };

    /// <summary>The domain's SLA rule, applied to the ticket in a joined row.</summary>
    private Expression<Func<TicketWithLabels, SlaStatus>> SlaStatusAt(DateTime nowUtc)
    {
        Expression<Func<TicketWithLabels, Ticket>> ticketOf = x => x.Ticket;

        return ticketOf.Then(sla.StatusAt(nowUtc));
    }

    /// <summary>
    /// Every supplied filter, combined with AND. Each is one <c>WhereIf</c>, so a further filter
    /// is one more line here; all of them become the SQL WHERE clause, ahead of the count and
    /// the page.
    /// </summary>
    /// <remarks>
    /// Search uses <c>Contains</c>, which EF sends as a parameterised, escaped <c>LIKE</c>; it is
    /// case-insensitive through the database's default collation.
    /// </remarks>
    /// <param name="source">The tickets to narrow.</param>
    /// <param name="query">The filters to apply.</param>
    /// <param name="slaStatus">The SLA rule as of now, shared with the projection.</param>
    private static IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source,
        TicketQuery query,
        Expression<Func<TicketWithLabels, SlaStatus>> slaStatus)
    {
        var search = query.Search;

        return source
            .WhereIf(query.Status is not null, x => x.Ticket.Status == query.Status)
            .WhereIf(query.Priority is not null, x => x.Ticket.Priority == query.Priority)
            .WhereIf(query.CategoryId is not null, x => x.Ticket.CategoryId == query.CategoryId)
            .WhereIf(query.CustomerId is not null, x => x.Ticket.CustomerId == query.CustomerId)
            .WhereIf(query.AssignedAgentId is not null, x => x.Ticket.AssignedAgentId == query.AssignedAgentId)
            .WhereIf(query.UnassignedOnly, x => x.Ticket.AssignedAgentId == null)
            .WhereIf(query.SlaStatus is not null, slaStatus.Then(status => status == query.SlaStatus))
            .WhereIf(search is not null, x =>
                x.Ticket.Title.Contains(search!)
                || x.Ticket.Reference.Contains(search!)
                || x.Customer.Name.Contains(search!));
    }

    private static IQueryable<TicketWithLabels> ApplySort(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),
            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),
            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Keeps paging stable when the sort column has duplicates.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }
    }
}

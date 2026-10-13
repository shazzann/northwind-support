using System.Linq.Expressions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date (BR-8). SLA status is always derived
/// from the ticket's dates and the current time - never stored.
/// </summary>
/// <remarks>
/// The rule is written once, as an expression tree. The database runs it when it lists, shows
/// or filters tickets by SLA status, and <see cref="Evaluate"/> runs the very same rule in
/// memory, so the two can never disagree.
/// </remarks>
public sealed class SlaEvaluator
{
    private readonly int _atRiskThresholdPercent;

    /// <param name="atRiskThresholdPercent">
    /// An open ticket is at risk once this share of its window, or less, remains.
    /// </param>
    public SlaEvaluator(int atRiskThresholdPercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(atRiskThresholdPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(atRiskThresholdPercent, 100);

        _atRiskThresholdPercent = atRiskThresholdPercent;
    }

    /// <summary>
    /// The rule as of <paramref name="nowUtc"/>. Translatable to SQL; "now" and the threshold
    /// become query parameters.
    /// </summary>
    /// <remarks>
    /// The at-risk margin is whole minutes (integer arithmetic), which SQL and C# compute
    /// identically. Due exactly now is at risk, not yet breached.
    /// </remarks>
    public Expression<Func<Ticket, SlaStatus>> StatusAt(DateTime nowUtc) =>
        ticket => ticket.DueAtUtc == null ? SlaStatus.NotApplicable
            : ticket.ResolvedAtUtc != null
                ? (ticket.ResolvedAtUtc <= ticket.DueAtUtc ? SlaStatus.Met : SlaStatus.Breached)
            : nowUtc > ticket.DueAtUtc ? SlaStatus.Breached
            : ticket.SlaWindowMinutes != null
              && nowUtc >= ticket.DueAtUtc.Value.AddMinutes(
                  -(ticket.SlaWindowMinutes.Value * _atRiskThresholdPercent / 100))
                ? SlaStatus.AtRisk
            : SlaStatus.WithinSla;

    /// <summary>The same rule for a single ticket in memory.</summary>
    public SlaStatus Evaluate(Ticket ticket, DateTime nowUtc) => StatusAt(nowUtc).Compile()(ticket);
}

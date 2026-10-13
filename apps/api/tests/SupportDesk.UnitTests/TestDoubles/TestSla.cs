using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>SLA settings as shipped in appsettings.json, for tests that rely on the specified numbers.</summary>
public static class TestSla
{
    public static SlaSettings Settings() => new()
    {
        BaseWindowHours = new Dictionary<TicketPriority, double>
        {
            [TicketPriority.Low] = 72,
            [TicketPriority.Medium] = 24,
            [TicketPriority.High] = 8,
            [TicketPriority.Critical] = 4
        },
        PremiumCustomerMultiplier = 0.5,
        MinimumWindowHours = 1,
        AtRiskThresholdPercent = 25
    };

    public static SlaPolicy Policy() => new(Settings());

    public static SlaEvaluator Evaluator() => new(Settings().AtRiskThresholdPercent);
}

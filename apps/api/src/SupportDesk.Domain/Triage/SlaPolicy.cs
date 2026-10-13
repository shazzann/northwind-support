using System.Globalization;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Triage;

/// <summary>
/// How long the team has to respond to a ticket (BR-2, BR-3): the priority's base window,
/// multiplied down for Premium customers, never shorter than the configured minimum. Used both
/// when a ticket is raised and when it is escalated.
/// </summary>
public sealed class SlaPolicy(SlaSettings settings)
{
    public SlaWindow WindowFor(TicketPriority priority, CustomerTier tier)
    {
        var baseHours = settings.BaseWindowHours[priority];
        var reason = $"{priority} priority has a {Hours(baseHours)} window";
        var hours = baseHours;

        if (tier == CustomerTier.Premium)
        {
            hours *= settings.PremiumCustomerMultiplier;
            reason += $", {Hours(hours)} for a Premium customer";
        }

        if (hours < settings.MinimumWindowHours)
        {
            hours = settings.MinimumWindowHours;
            reason += $", raised to the {Hours(hours)} minimum";
        }

        return new SlaWindow((int)Math.Round(hours * 60), reason + ".");
    }

    private static string Hours(double hours) => $"{hours.ToString("0.##", CultureInfo.InvariantCulture)} h";
}

/// <summary>A response window, and why it is that long.</summary>
public sealed record SlaWindow(int Minutes, string Reason);

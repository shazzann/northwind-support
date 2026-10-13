using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

/// <summary>
/// BR-8 at its exact boundaries. A 4 h window started at <see cref="FixedClock.DefaultNow"/> is
/// due at 13:00 and at risk from 12:00, when 25% (60 min) remains.
/// </summary>
public class SlaEvaluatorTests
{
    private static readonly DateTime Start = FixedClock.DefaultNow;
    private static readonly DateTime Due = Start.AddHours(4);
    private static readonly DateTime AtRiskFrom = Due.AddMinutes(-60);
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(1);

    private readonly SlaEvaluator _evaluator = TestSla.Evaluator();

    [Fact]
    public void WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, _evaluator.Evaluate(new TicketBuilder().Build(), Start));

    [Fact]
    public void OpenWithMoreThanTheThresholdLeft_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, _evaluator.Evaluate(OpenTicket(), AtRiskFrom - Tick));

    [Fact]
    public void OpenWithExactlyTheThresholdLeft_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, _evaluator.Evaluate(OpenTicket(), AtRiskFrom));

    [Fact]
    public void OpenAndDueExactlyNow_IsAtRiskNotBreached() =>
        Assert.Equal(SlaStatus.AtRisk, _evaluator.Evaluate(OpenTicket(), Due));

    [Fact]
    public void OpenAndJustPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, _evaluator.Evaluate(OpenTicket(), Due + Tick));

    [Fact]
    public void ResolvedExactlyAtTheDueDate_IsMet() =>
        Assert.Equal(SlaStatus.Met, _evaluator.Evaluate(ResolvedTicket(Due), Due.AddDays(1)));

    [Fact]
    public void ResolvedJustAfterTheDueDate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, _evaluator.Evaluate(ResolvedTicket(Due + Tick), Due.AddDays(1)));

    [Fact]
    public void ClosedTicket_IsJudgedOnItsResolutionTime()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.Closed).ResolvedAt(Start.AddHours(1)).Build();
        ticket.StartSla(Start, 4 * 60);

        Assert.Equal(SlaStatus.Met, _evaluator.Evaluate(ticket, Due.AddDays(1)));
    }

    [Fact]
    public void Threshold_IsConfigurable()
    {
        var halfway = new SlaEvaluator(atRiskThresholdPercent: 50);

        Assert.Equal(SlaStatus.WithinSla, halfway.Evaluate(OpenTicket(), Start.AddHours(2) - Tick));
        Assert.Equal(SlaStatus.AtRisk, halfway.Evaluate(OpenTicket(), Start.AddHours(2)));
    }

    private static Ticket OpenTicket()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.InProgress).Build();
        ticket.StartSla(Start, 4 * 60);
        return ticket;
    }

    private static Ticket ResolvedTicket(DateTime resolvedAtUtc)
    {
        var ticket = OpenTicket();
        ticket.ChangeStatus(TicketStatus.Resolved, resolvedAtUtc);
        return ticket;
    }
}

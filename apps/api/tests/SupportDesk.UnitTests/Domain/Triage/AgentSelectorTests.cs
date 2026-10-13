using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Triage;

public class AgentSelectorTests
{
    private static readonly Category General = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false).WithId(1);
    private static readonly Category Billing = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false).WithId(2);

    [Fact]
    public void Choose_PicksTheEligibleAgentWithFewestOpenTickets()
    {
        var decision = AgentSelector.Choose(General, [Agent(1, open: 5), Agent(2, open: 2), Agent(3, open: 4)]);

        Assert.Equal(2, decision.AgentId);
        Assert.Equal("Assigned to Agent 2, who has the fewest open tickets (2) of 3 eligible agents.", decision.Reason);
    }

    [Fact]
    public void Choose_ForASpecialistCategory_OnlyConsidersSpecialists()
    {
        var decision = AgentSelector.Choose(Billing, [Agent(1, open: 0), Agent(2, open: 3, specializations: [Billing.Id])]);

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void Choose_ForAnOrdinaryCategory_IgnoresSpecializations() =>
        Assert.Equal(1, AgentSelector.Choose(General, [Agent(1, open: 0, specializations: [Billing.Id])]).AgentId);

    [Fact]
    public void Choose_SkipsAgentsAtTheirLimit_ButNotOneBelowIt()
    {
        var decision = AgentSelector.Choose(General, [Agent(1, open: 6, max: 6), Agent(2, open: 7, max: 8)]);

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void Choose_SkipsInactiveAgents() =>
        Assert.Equal(2, AgentSelector.Choose(General, [Agent(1, open: 0, isActive: false), Agent(2, open: 5)]).AgentId);

    [Fact]
    public void Choose_BreaksTiesByLowestId_WhateverTheOrder()
    {
        Assert.Equal(3, AgentSelector.Choose(General, [Agent(7, open: 1), Agent(3, open: 1), Agent(5, open: 1)]).AgentId);
        Assert.Equal(3, AgentSelector.Choose(General, [Agent(5, open: 1), Agent(3, open: 1), Agent(7, open: 1)]).AgentId);
    }

    [Fact]
    public void Choose_WithNobodyEligible_LeavesTheTicketUnassignedWithAReason()
    {
        var decision = AgentSelector.Choose(Billing, [Agent(1, open: 0), Agent(2, open: 6, max: 6, specializations: [Billing.Id])]);

        Assert.Null(decision.AgentId);
        Assert.Equal("Left unassigned: no active Billing specialist has capacity.", decision.Reason);
    }

    [Fact]
    public void KeepOrChoose_KeepsAStillEligibleOwner_EvenOverAQuieterAgent()
    {
        var decision = AgentSelector.KeepOrChoose(General, [Agent(1, open: 5), Agent(2, open: 0)], currentAgentId: 1);

        Assert.Equal(1, decision.AgentId);
        Assert.Equal("Kept with Agent 1, who is still eligible.", decision.Reason);
    }

    [Fact]
    public void KeepOrChoose_KeepsAnOwnerAtTheirLimit_SinceTheTicketIsAlreadyTheirs() =>
        Assert.Equal(1, AgentSelector.KeepOrChoose(General, [Agent(1, open: 6, max: 6), Agent(2, open: 0)], currentAgentId: 1).AgentId);

    [Fact]
    public void KeepOrChoose_ReassignsWhenTheOwnerIsNoLongerEligible()
    {
        var decision = AgentSelector.KeepOrChoose(General, [Agent(1, open: 2, isActive: false), Agent(2, open: 4)], currentAgentId: 1);

        Assert.Equal(2, decision.AgentId);
        Assert.StartsWith("Agent 1 is no longer eligible. Assigned to Agent 2", decision.Reason);
    }

    [Fact]
    public void KeepOrChoose_ForAnUnassignedTicket_Chooses() =>
        Assert.Equal(2, AgentSelector.KeepOrChoose(General, [Agent(1, open: 3), Agent(2, open: 1)], currentAgentId: null).AgentId);

    private static AgentCandidate Agent(int id, int open, int max = 10, bool isActive = true, int[]? specializations = null) =>
        new(id, $"Agent {id}", isActive, max, open, specializations ?? []);
}

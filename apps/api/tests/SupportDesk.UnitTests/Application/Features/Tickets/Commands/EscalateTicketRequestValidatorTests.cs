using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketRequestValidatorTests
{
    private readonly EscalateTicketRequestValidator _validator = new();

    [Theory]
    [InlineData(5)]
    [InlineData(500)]
    public void Reason_AtEitherLimit_IsAccepted(int length) =>
        Assert.True(_validator.Validate(new EscalateTicketRequest(new string('x', length), "Team Lead")).IsValid);

    [Theory]
    [InlineData(4)]
    [InlineData(501)]
    public void Reason_JustOutsideTheLimits_IsRejected(int length) =>
        Assert.False(_validator.Validate(new EscalateTicketRequest(new string('x', length), "Team Lead")).IsValid);

    [Fact]
    public void Reason_IsMeasuredWithoutSurroundingSpaces() =>
        Assert.False(_validator.Validate(new EscalateTicketRequest("   abcd   ", "Team Lead")).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingActor_IsRejected(string escalatedBy)
    {
        var result = _validator.Validate(new EscalateTicketRequest("Customer cannot trade", escalatedBy));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(EscalateTicketRequest.EscalatedBy));
    }
}

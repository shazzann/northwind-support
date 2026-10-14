using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <remarks>The reason is measured as stored, without surrounding spaces.</remarks>
public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .Must(reason => reason.Trim().Length is >= TicketEscalation.ReasonMinLength and <= TicketEscalation.ReasonMaxLength)
            .WithMessage($"Reason must be between {TicketEscalation.ReasonMinLength} and {TicketEscalation.ReasonMaxLength} characters.");
    }
}

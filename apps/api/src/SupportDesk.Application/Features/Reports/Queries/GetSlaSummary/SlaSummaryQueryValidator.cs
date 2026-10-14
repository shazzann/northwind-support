using FluentValidation;
using SupportDesk.Application.Contracts.Reports;

namespace SupportDesk.Application.Features.Reports.Queries.GetSlaSummary;

public sealed class SlaSummaryQueryValidator : AbstractValidator<SlaSummaryQuery>
{
    public SlaSummaryQueryValidator()
    {
        RuleFor(x => x.GroupBy)
            .NotNull().WithMessage("groupBy is required: agent or priority.")
            .IsInEnum();

        RuleFor(x => x.To)
            .GreaterThan(x => x.From)
            .When(x => x.From is not null && x.To is not null)
            .WithMessage("to must be after from.");
    }
}

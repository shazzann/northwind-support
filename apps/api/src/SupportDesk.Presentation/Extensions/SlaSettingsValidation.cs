using Microsoft.Extensions.Options;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Presentation.Extensions;

/// <summary>
/// Refuses to start the API with SLA settings that are missing or make no sense, naming every
/// problem, rather than failing on the first ticket that needs a due date.
/// </summary>
public sealed class SlaSettingsValidation : IValidateOptions<SlaSettings>
{
    public ValidateOptionsResult Validate(string? name, SlaSettings options)
    {
        var errors = options.Validate();

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

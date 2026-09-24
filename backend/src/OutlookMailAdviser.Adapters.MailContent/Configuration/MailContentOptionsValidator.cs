using Microsoft.Extensions.Options;

namespace OutlookMailAdviser.Adapters.MailContent.Configuration;

internal sealed class MailContentOptionsValidator : IValidateOptions<MailContentOptions>
{
    public ValidateOptionsResult Validate(string? name, MailContentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.MaxInputCharacters is >= 1_000 and <= 1_000_000
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "MailProcessing:MaxInputCharacters must be between 1000 and 1000000.");
    }
}


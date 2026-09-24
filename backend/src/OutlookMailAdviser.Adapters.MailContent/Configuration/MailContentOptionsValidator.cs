using Microsoft.Extensions.Options;

namespace OutlookMailAdviser.Adapters.MailContent.Configuration;

internal sealed class MailContentOptionsValidator : IValidateOptions<MailContentOptions>
{
    public ValidateOptionsResult Validate(string? name, MailContentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.MaxInputCharacters is >= 1_000 and <= 1_000_000
            && options.MaxHistoryMessages is >= 0 and <= 20
            && options.MaxHistoryCharacters is >= 0 and <= 1_000_000
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "MailProcessing limits: input 1000..1000000, history messages 0..20, history characters 0..1000000.");
    }
}

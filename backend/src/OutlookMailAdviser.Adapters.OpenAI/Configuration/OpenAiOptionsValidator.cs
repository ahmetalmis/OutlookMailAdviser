using Microsoft.Extensions.Options;

namespace OutlookMailAdviser.Adapters.OpenAI.Configuration;

internal sealed class OpenAiOptionsValidator : IValidateOptions<OpenAiOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenAiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add("Ai:OpenAI:BaseUrl must be an absolute HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add("Set OPENAI_API_KEY when Ai:Provider is OpenAI.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            failures.Add("Ai:OpenAI:Model is required.");
        }

        if (options.TimeoutSeconds is < 1 or > 600)
        {
            failures.Add("Ai:OpenAI:TimeoutSeconds must be between 1 and 600.");
        }

        if (options.MaxOutputTokens is < 1 or > 32_768)
        {
            failures.Add("Ai:OpenAI:MaxOutputTokens must be between 1 and 32768.");
        }

        if (options.Temperature is < 0 or > 2)
        {
            failures.Add("Ai:OpenAI:Temperature must be between 0 and 2.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

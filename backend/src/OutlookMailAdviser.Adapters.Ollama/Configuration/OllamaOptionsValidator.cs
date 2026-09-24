using Microsoft.Extensions.Options;

namespace OutlookMailAdviser.Adapters.Ollama.Configuration;

internal sealed class OllamaOptionsValidator : IValidateOptions<OllamaOptions>
{
    public ValidateOptionsResult Validate(string? name, OllamaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add("Ai:Ollama:BaseUrl must be an absolute HTTP or HTTPS URL.");
        }
        else if (!baseUri.IsLoopback)
        {
            failures.Add("Ai:Ollama:BaseUrl must target a loopback address for the local-only POC.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            failures.Add("Ai:Ollama:Model is required.");
        }

        if (options.TimeoutSeconds is < 1 or > 600)
        {
            failures.Add("Ai:Ollama:TimeoutSeconds must be between 1 and 600.");
        }

        if (options.Temperature is < 0 or > 2)
        {
            failures.Add("Ai:Ollama:Temperature must be between 0 and 2.");
        }

        if (options.ContextWindow is < 512 or > 262_144)
        {
            failures.Add("Ai:Ollama:ContextWindow must be between 512 and 262144.");
        }

        if (options.MaxOutputTokens is < 1 or > 8192)
        {
            failures.Add("Ai:Ollama:MaxOutputTokens must be between 1 and 8192.");
        }

        if (string.IsNullOrWhiteSpace(options.KeepAlive))
        {
            failures.Add("Ai:Ollama:KeepAlive is required.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

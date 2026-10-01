using System.Net;
using System.Text.Json;
using OutlookMailAdviser.Application.MailAnalysis.Exceptions;

namespace OutlookMailAdviser.Adapters.OpenAI;

internal static class OpenAiFailure
{
    public static async Task<string> GetCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized) return "openai_authentication_failed";
        if (response.StatusCode == HttpStatusCode.Forbidden) return "openai_access_denied";
        if (response.StatusCode == HttpStatusCode.NotFound) return "model_not_found";

        // Read only documented codes. Never propagate the provider's message or raw payload.
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.TooManyRequests)
        {
            try
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (json.RootElement.ValueKind == JsonValueKind.Object
                    && json.RootElement.TryGetProperty("error", out var error)
                    && error.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in new[] { "code", "type" })
                    {
                        if (!error.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String) continue;
                        if (value.GetString() is "insufficient_quota" or "organization_spend_limit_exceeded"
                            or "project_spend_limit_exceeded" or "organization_usage_limit_exceeded") return "openai_quota_exceeded";
                        if (value.GetString() == "context_length_exceeded") return "openai_context_limit";
                    }
                }
            }
            catch (JsonException) { /* A proxy may return HTML instead of a provider error. */ }
        }

        return response.StatusCode == HttpStatusCode.TooManyRequests
            ? "openai_rate_limited" : "model_provider_error";
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var code = await GetCodeAsync(response, cancellationToken);
        throw new ModelProviderException(ProviderErrorDetails.GetDetail(code), code);
    }
}

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OutlookMailAdviser.Adapters.Ollama.Configuration;

namespace OutlookMailAdviser.Adapters.Ollama.Health;

internal sealed class OllamaHealthCheck(
    HttpClient httpClient,
    IOptionsMonitor<OllamaOptions> optionsMonitor) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var options = optionsMonitor.CurrentValue;
        var tagsUri = new Uri(new Uri(EnsureTrailingSlash(options.BaseUrl)), "api/tags");

        try
        {
            using var response = await httpClient.GetAsync(tagsUri, cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy(
                    "Ollama is reachable.",
                    new Dictionary<string, object> { ["model"] = options.Model })
                : HealthCheckResult.Unhealthy(
                    $"Ollama returned HTTP {(int)response.StatusCode}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Ollama health check timed out.");
        }
        catch (HttpRequestException)
        {
            return HealthCheckResult.Unhealthy("Ollama is not reachable.");
        }
    }

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith('/') ? value : $"{value}/";
}

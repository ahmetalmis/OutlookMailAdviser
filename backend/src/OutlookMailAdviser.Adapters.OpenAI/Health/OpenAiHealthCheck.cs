using System.Net.Http.Headers;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OutlookMailAdviser.Adapters.OpenAI.Configuration;

namespace OutlookMailAdviser.Adapters.OpenAI.Health;

internal sealed class OpenAiHealthCheck(
    HttpClient httpClient,
    IOptionsMonitor<OpenAiOptions> optionsMonitor) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var options = optionsMonitor.CurrentValue;
        var endpoint = new Uri(
            new Uri(EnsureTrailingSlash(options.BaseUrl)),
            $"models/{Uri.EscapeDataString(options.Model)}");
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy(
                    "OpenAI is reachable.",
                    new Dictionary<string, object> { ["model"] = options.Model })
                : HealthCheckResult.Unhealthy(
                    $"OpenAI returned HTTP {(int)response.StatusCode}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("OpenAI health check timed out.");
        }
        catch (HttpRequestException)
        {
            return HealthCheckResult.Unhealthy("OpenAI is not reachable.");
        }
    }

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith('/') ? value : $"{value}/";
}

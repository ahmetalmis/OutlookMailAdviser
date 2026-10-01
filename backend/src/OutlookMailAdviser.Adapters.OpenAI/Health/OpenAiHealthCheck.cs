using System.Net.Http.Headers;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OutlookMailAdviser.Adapters.OpenAI.Configuration;
using OutlookMailAdviser.Application.MailAnalysis.Exceptions;

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

            if (response.IsSuccessStatusCode)
                return HealthCheckResult.Healthy(
                    "OpenAI is reachable.",
                    new Dictionary<string, object> { ["model"] = options.Model });

            return Failure(await OpenAiFailure.GetCodeAsync(response, cancellationToken));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("model_timeout");
        }
        catch (HttpRequestException)
        {
            return Failure("openai_unavailable");
        }
    }

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith('/') ? value : $"{value}/";

    private static HealthCheckResult Failure(string code) => HealthCheckResult.Unhealthy(
        ProviderErrorDetails.GetDetail(code), data: new Dictionary<string, object> { ["code"] = code });
}

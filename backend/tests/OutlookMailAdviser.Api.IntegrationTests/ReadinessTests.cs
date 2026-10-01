using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OutlookMailAdviser.Api.IntegrationTests;

public sealed class ReadinessTests
{
    [Theory]
    [InlineData(false, "openai_authentication_failed", "openai_authentication_failed")]
    [InlineData(false, "PRIVATE_SENTINEL", "model_provider_error")]
    [InlineData(true, null, null)]
    public async Task ReadinessExposesSafeCodesWithoutProviderPayloads(bool healthy, string? inputCode, string? expectedCode)
    {
        using var factory = new ApiFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
                options.Registrations.Add(new HealthCheckRegistration("openai",
                    new StubHealthCheck(healthy, inputCode), null, ["ready"]));
            })));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        Assert.Equal(healthy ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PRIVATE_SENTINEL", body, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(expectedCode, json.RootElement.GetProperty("code").GetString());
        Assert.Equal("OpenAI", json.RootElement.GetProperty("data").GetProperty("provider").GetString());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    private sealed class StubHealthCheck(bool healthy, string? code) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HealthCheckResult(healthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                "PRIVATE_SENTINEL upstream body", data: new Dictionary<string, object>
                {
                    ["code"] = code ?? "unused", ["model"] = "test-model", ["secret"] = "PRIVATE_SENTINEL"
                }));
    }
}

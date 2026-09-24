using System.Net;
using System.Text.Json;

namespace OutlookMailAdviser.Api.IntegrationTests;

public sealed class ApiBootstrapTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ServiceInfoIsAvailable()
    {
        using var response = await _client.GetAsync(new Uri("/", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var content = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(content);

        Assert.Equal(
            "Outlook Mail Adviser API",
            document.RootElement.GetProperty("service").GetString());
    }

    [Fact]
    public async Task LivenessDoesNotDependOnOllama()
    {
        using var response = await _client.GetAsync(
            new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocumentIsExposedInDevelopment()
    {
        using var response = await _client.GetAsync(
            new Uri("/swagger/v1/swagger.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TestScreenIsAvailable()
    {
        using var response = await _client.GetAsync(new Uri("/test", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Outlook Mail Adviser", html, StringComparison.Ordinal);
        Assert.Contains("Maili analiz et", html, StringComparison.Ordinal);
    }
}

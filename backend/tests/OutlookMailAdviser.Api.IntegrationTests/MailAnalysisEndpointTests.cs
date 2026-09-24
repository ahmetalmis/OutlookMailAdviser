using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OutlookMailAdviser.Api.IntegrationTests;

public sealed class MailAnalysisEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task AnalyzeMailReturnsStructuredContractAndUsesSanitizedContent()
    {
        var clientRequestId = Guid.NewGuid();
        var request = new
        {
            clientRequestId,
            preferredLanguage = "tr",
            currentUser = new { name = "Ali", address = "ali@example.com" },
            message = new
            {
                itemId = "sample-1",
                subject = "Üretim geçiş planı",
                from = new { name = "Ayşe", address = "ayse@example.com" },
                to = new[] { new { name = "Ali", address = "ali@example.com" } },
                cc = Array.Empty<object>(),
                sentAt = "2026-09-02T09:00:00+03:00",
                body = "<p>Merhaba Ali</p><script>unsafe()</script><p>Tarihi teyit eder misin?</p>",
                bodyFormat = "html"
            }
        };

        using var response = await _client.PostAsJsonAsync("/api/v1/mail/analysis", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            clientRequestId,
            document.RootElement.GetProperty("clientRequestId").GetGuid());
        Assert.Equal("high", document.RootElement.GetProperty("priority").GetString());
        Assert.Equal("test-model", document.RootElement.GetProperty("model").GetString());
        var processing = document.RootElement.GetProperty("contentProcessing");
        Assert.True(processing.GetProperty("includedCharacters").GetInt32() <= 16_000);
        Assert.Equal(0, processing.GetProperty("includedHistoryMessages").GetInt32());
        Assert.False(processing.GetProperty("currentMessageTruncated").GetBoolean());
        Assert.True(document.RootElement.GetProperty("actionRequiredFromCurrentUser").GetBoolean());
        var action = Assert.Single(document.RootElement.GetProperty("actions").EnumerateArray());
        Assert.True(action.GetProperty("assignedToCurrentUser").GetBoolean());
        Assert.Equal("ali@example.com", factory.IntelligenceGateway.ReceivedOptions?.CurrentUser?.Address);
        Assert.DoesNotContain(
            "unsafe",
            factory.IntelligenceGateway.ReceivedConversation?.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeMailReturnsProblemDetailsForMissingMessage()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/mail/analysis",
            new { preferredLanguage = "tr", message = (object?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "invalid_request",
            document.RootElement.GetProperty("code").GetString());
    }
}

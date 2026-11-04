using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OutlookMailAdviser.Api.IntegrationTests;

public sealed class MailboxTriageEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task TriageDropsNewslettersBeforeClassification()
    {
        var clientRequestId = Guid.NewGuid();
        var request = new
        {
            clientRequestId,
            preferredLanguage = "tr",
            currentUser = new { name = "Ali", address = "ali@example.com" },
            messages = new object[]
            {
                new
                {
                    itemId = "direct-1",
                    conversationId = "c-1",
                    subject = "Bütçe onayı",
                    from = new { name = "Ayşe", address = "ayse@example.com" },
                    to = new[] { new { name = "Ali", address = "ali@example.com" } },
                    cc = Array.Empty<object>(),
                    receivedAt = "2026-10-19T08:00:00Z",
                    preview = "Cuma 17:00'e kadar yazılı onayınızı bekliyorum.",
                    hasUnsubscribeHint = false,
                    isAutoSubmitted = false
                },
                new
                {
                    itemId = "news-1",
                    conversationId = "c-2",
                    subject = "Haftalık bülten",
                    from = new { name = "Haber", address = "news@example.com" },
                    to = new[] { new { name = "Ali", address = "ali@example.com" } },
                    cc = Array.Empty<object>(),
                    receivedAt = "2026-10-19T07:00:00Z",
                    preview = "Bu haftanın duyuruları",
                    hasUnsubscribeHint = true,
                    isAutoSubmitted = false
                }
            }
        };

        using var response = await _client.PostAsJsonAsync("/api/v1/mailbox/triage", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(clientRequestId, document.RootElement.GetProperty("clientRequestId").GetGuid());
        Assert.Equal(2, document.RootElement.GetProperty("scannedCount").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("headerFilteredCount").GetInt32());
        var actionItems = document.RootElement.GetProperty("actionItems");
        Assert.Equal(1, actionItems.GetArrayLength());
        Assert.Equal("direct-1", actionItems[0].GetProperty("itemId").GetString());
        Assert.Equal("high", actionItems[0].GetProperty("priority").GetString());
        Assert.Equal(["m1"], factory.PreviewClassifier.ReceivedIds);
    }
}

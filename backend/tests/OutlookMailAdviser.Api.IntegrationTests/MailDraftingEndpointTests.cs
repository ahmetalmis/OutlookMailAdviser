using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OutlookMailAdviser.Domain.MailDrafts;

namespace OutlookMailAdviser.Api.IntegrationTests;

public sealed class MailDraftingEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task DraftMailReturnsStructuredDraftAndPassesUserChoices()
    {
        var clientRequestId = Guid.NewGuid();
        var request = new
        {
            clientRequestId,
            preferredLanguage = "tr",
            tone = "friendly",
            toneDetails = "Hafif sert ve uyarıcı olsun.",
            instructions = "Tarihi teyit et ve teşekkür et.",
            message = new
            {
                itemId = "sample-1",
                subject = "Üretim geçiş planı",
                from = new { name = "Ayşe", address = "ayse@example.com" },
                to = new[] { new { name = "Ali", address = "ali@example.com" } },
                cc = Array.Empty<object>(),
                sentAt = "2026-09-02T09:00:00+03:00",
                body = "<p>Geçiş tarihini teyit eder misin?</p>",
                bodyFormat = "html"
            }
        };

        using var response = await _client.PostAsJsonAsync("/api/v1/mail/draft", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(clientRequestId, document.RootElement.GetProperty("clientRequestId").GetGuid());
        Assert.Equal("Re: Üretim geçiş planı", document.RootElement.GetProperty("subject").GetString());
        Assert.Contains("teyit ediyorum", document.RootElement.GetProperty("body").GetString());
        Assert.Equal("friendly", document.RootElement.GetProperty("tone").GetString());
        Assert.Equal("test-model", document.RootElement.GetProperty("model").GetString());
        Assert.Equal(DraftTone.Friendly, factory.DraftGateway.ReceivedOptions?.Tone);
        Assert.Equal(
            "Hafif sert ve uyarıcı olsun.",
            factory.DraftGateway.ReceivedOptions?.ToneDetails);
        Assert.Equal("tr", factory.DraftGateway.ReceivedOptions?.PreferredLanguage);
        Assert.DoesNotContain(
            "<p>",
            factory.DraftGateway.ReceivedConversation?.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DraftMailRejectsMissingInstructions()
    {
        using var response = await _client.PostAsJsonAsync("/api/v1/mail/draft", new
        {
            preferredLanguage = "tr",
            tone = "professional",
            instructions = "",
            message = new
            {
                subject = "Test",
                from = new { address = "sender@example.com" },
                to = new[] { new { address = "recipient@example.com" } },
                cc = Array.Empty<object>(),
                body = "Test",
                bodyFormat = "plainText"
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OutlookMailAdviser.Adapters.Ollama.Analysis;
using OutlookMailAdviser.Adapters.Ollama.Configuration;
using OutlookMailAdviser.Adapters.OpenAI.Analysis;
using OutlookMailAdviser.Adapters.OpenAI.Configuration;
using OutlookMailAdviser.Application.MailAnalysis.Exceptions;

namespace OutlookMailAdviser.Adapters.Tests;

public sealed class MailQuestionGatewayTests
{
    [Theory]
    [InlineData("From: Ayşe", "Sent: yesterday", "To: Ali", "Subject: Past")]
    [InlineData("Gönderen: Ayşe", "Tarih: dün", "Kime: Ali", "Konu: Geçmiş")]
    public void FullBodyReaderKeepsNestedHistoryBeyondAnalysisLimits(string from, string sent, string to, string subject)
    {
        var sanitizer = new OutlookMailAdviser.Adapters.MailContent.HtmlMailContentSanitizer(
            new TestOptionsMonitor<OutlookMailAdviser.Adapters.MailContent.Configuration.MailContentOptions>(new()));
        var body = "<p>Current</p><script>danger()</script>" + string.Concat(Enumerable.Repeat(
            $"<blockquote><p>{from}</p><p>{sent}</p><p>{to}</p><p>{subject}</p>", 5))
            + new string('x', 18000) + "<p>En eski bilgi</p>" + string.Concat(Enumerable.Repeat("</blockquote>", 5));
        var message = new OutlookMailAdviser.Domain.Mails.MailMessage(null, "Test", new(null, "a@example.com"),
            [new(null, "b@example.com")], [], null, body, OutlookMailAdviser.Domain.Mails.MailBodyFormat.Html);
        var full = sanitizer.ReadFullBody(message);
        Assert.Contains("En eski bilgi", full, StringComparison.Ordinal);
        Assert.Contains(from, full, StringComparison.Ordinal);
        Assert.DoesNotContain("danger()", full, StringComparison.Ordinal);
        Assert.True(full.Length > 18000);
    }

    private const string AnswerJson = """{"answer":"Teslim 12 Ekim.","quotes":["Teslim 12 Ekim."]}""";

    [Fact]
    public async Task OllamaUsesSchemaAndRepairsInvalidJsonOnce()
    {
        using var handler = new StubHttpMessageHandler((_, attempt) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { message = new { role = "assistant", content = attempt == 1 ? "invalid" : AnswerJson } })
        });
        using var client = new HttpClient(handler);
        var gateway = new OllamaMailIntelligenceGateway(client, new TestOptionsMonitor<OllamaOptions>(new()));
        var result = await gateway.AnswerAsync("Teslim 12 Ekim.", "Tarih?", "tr", CancellationToken.None);
        Assert.Single(result.Quotes);
        Assert.Equal(2, handler.RequestBodies.Count);
        using var request = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.Equal("object", request.RootElement.GetProperty("format").GetProperty("type").GetString());
        Assert.False(request.RootElement.GetProperty("think").GetBoolean());
        Assert.True(gateway.GetContentByteBudget("Tarih?", "tr") >= 256);
    }

    [Fact]
    public async Task OpenAiUsesStrictSchemaWithoutStorage()
    {
        using var handler = new StubHttpMessageHandler((_, _) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                status = "completed",
                output = new[] {
                new { content = new[] { new { type = "output_text", text = AnswerJson } } } }
            })
        });
        using var client = new HttpClient(handler);
        var gateway = new OpenAiMailIntelligenceGateway(client, new TestOptionsMonitor<OpenAiOptions>(new() { ApiKey = "test-key" }));
        var result = await gateway.AnswerAsync("Teslim 12 Ekim.", "Tarih?", "tr", CancellationToken.None);
        Assert.Single(result.Quotes);
        using var request = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.False(request.RootElement.GetProperty("store").GetBoolean());
        Assert.True(request.RootElement.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
    }

    [Fact]
    public async Task OpenAiRejectsIncompleteResponses()
    {
        using var handler = new StubHttpMessageHandler((_, _) => new(HttpStatusCode.OK)
        { Content = JsonContent.Create(new { status = "incomplete" }) });
        using var client = new HttpClient(handler);
        var gateway = new OpenAiMailIntelligenceGateway(client, new TestOptionsMonitor<OpenAiOptions>(new()));
        await Assert.ThrowsAsync<InvalidModelResponseException>(() => gateway.AnswerAsync("Metin", "Soru", "tr", CancellationToken.None));
    }
}

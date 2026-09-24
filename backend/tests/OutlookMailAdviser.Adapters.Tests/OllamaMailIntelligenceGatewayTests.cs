using System.Net;
using System.Text;
using System.Text.Json;
using OutlookMailAdviser.Adapters.Ollama.Analysis;
using OutlookMailAdviser.Adapters.Ollama.Configuration;
using OutlookMailAdviser.Application.MailAnalysis.Exceptions;
using OutlookMailAdviser.Application.MailAnalysis.Models;
using OutlookMailAdviser.Application.MailDrafting.Models;
using OutlookMailAdviser.Domain.MailDrafts;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Adapters.Tests;

public sealed class OllamaMailIntelligenceGatewayTests
{
    [Fact]
    public async Task AnalyzeAsyncMapsStructuredResponseAndSendsSchema()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(new Uri("http://127.0.0.1:11434/api/chat"), request.RequestUri);
            return CreateOllamaResponse(CreateValidAnalysisJson());
        });
        using var httpClient = new HttpClient(handler);
        var gateway = CreateGateway(httpClient);

        var result = await gateway.AnalyzeAsync(
            CreateSanitizedConversation(),
            new AnalysisOptions(
                "tr",
                new MailParticipant("Ali", "ali@example.com")),
            CancellationToken.None);

        Assert.Equal("Tarih teyidi isteniyor.", result.Analysis.Summary);
        Assert.True(result.Analysis.ActionRequired);
        Assert.Single(result.Analysis.Actions);
        Assert.Equal(new DateOnly(2026, 9, 3), result.Analysis.Actions[0].DueDate);
        Assert.True(result.Analysis.Actions[0].AssignedToCurrentUser);
        Assert.Equal(4, result.DurationMilliseconds);

        using var requestDocument = JsonDocument.Parse(Assert.Single(handler.RequestBodies));
        Assert.Equal("qwen3.5:4b", requestDocument.RootElement.GetProperty("model").GetString());
        Assert.Equal(
            "object",
            requestDocument.RootElement.GetProperty("format").GetProperty("type").GetString());
        Assert.Equal(
            4096,
            requestDocument.RootElement.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.Equal(
            512,
            requestDocument.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.False(requestDocument.RootElement.GetProperty("think").GetBoolean());
        Assert.Contains(
            "ali@example.com",
            requestDocument.RootElement.GetProperty("messages")[1].GetProperty("content").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeAsyncRetriesInvalidJsonOnceWithDeterministicTemperature()
    {
        var handler = new StubHttpMessageHandler((_, requestNumber) =>
            requestNumber == 1
                ? CreateOllamaResponse("not-json")
                : CreateOllamaResponse(CreateValidAnalysisJson()));
        using var httpClient = new HttpClient(handler);
        var gateway = CreateGateway(httpClient);

        var result = await gateway.AnalyzeAsync(
            CreateSanitizedConversation(),
            new AnalysisOptions("tr"),
            CancellationToken.None);

        Assert.Equal("Tarih teyidi isteniyor.", result.Analysis.Summary);
        Assert.Equal(2, handler.RequestBodies.Count);

        using var repairRequest = JsonDocument.Parse(handler.RequestBodies[1]);
        Assert.Equal(
            0,
            repairRequest.RootElement.GetProperty("options").GetProperty("temperature").GetDouble());
    }

    [Fact]
    public async Task AnalyzeAsyncNormalizesIsoDateTimeDueDate()
    {
        var analysisJson = CreateValidAnalysisJson()
            .Replace("2026-09-03", "2026-09-03T15:00:00+03:00", StringComparison.Ordinal);
        var handler = new StubHttpMessageHandler((_, _) => CreateOllamaResponse(analysisJson));
        using var httpClient = new HttpClient(handler);
        var gateway = CreateGateway(httpClient);

        var result = await gateway.AnalyzeAsync(
            CreateSanitizedConversation(),
            new AnalysisOptions("tr"),
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 9, 3), result.Analysis.Actions[0].DueDate);
        Assert.Single(handler.RequestBodies);
    }

    [Fact]
    public async Task AnalyzeAsyncMapsNotFoundToModelNotFoundException()
    {
        var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var httpClient = new HttpClient(handler);
        var gateway = CreateGateway(httpClient);

        await Assert.ThrowsAsync<ModelNotFoundException>(() => gateway.AnalyzeAsync(
            CreateSanitizedConversation(),
            new AnalysisOptions("tr"),
            CancellationToken.None));
    }

    [Fact]
    public async Task AnalyzeAsyncSuppressesPersonalAssignmentWithoutCurrentUserIdentity()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            CreateOllamaResponse(CreateValidAnalysisJson()));
        using var httpClient = new HttpClient(handler);
        var gateway = CreateGateway(httpClient);

        var result = await gateway.AnalyzeAsync(
            CreateSanitizedConversation(),
            new AnalysisOptions("tr"),
            CancellationToken.None);

        Assert.False(result.Analysis.Actions[0].AssignedToCurrentUser);
    }

    [Fact]
    public async Task GenerateDraftAsyncMapsStructuredDraftAndSendsTone()
    {
        var draftJson = JsonSerializer.Serialize(new
        {
            subject = "Re: Üretim geçişi",
            body = "Merhaba Ayşe, geçiş tarihini teyit ediyorum."
        });
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(new Uri("http://127.0.0.1:11434/api/chat"), request.RequestUri);
            return CreateOllamaResponse(draftJson);
        });
        using var httpClient = new HttpClient(handler);
        var gateway = CreateGateway(httpClient);

        var result = await gateway.GenerateDraftAsync(
            CreateSanitizedConversation(),
            new DraftOptions(
                DraftTone.Professional,
                "Tarihi teyit et.",
                "tr",
                "Hafif sert ve uyarıcı olsun."),
            CancellationToken.None);

        Assert.Equal("Re: Üretim geçişi", result.Draft.Subject);
        Assert.Contains("teyit ediyorum", result.Draft.Body, StringComparison.Ordinal);

        using var requestDocument = JsonDocument.Parse(Assert.Single(handler.RequestBodies));
        var messages = requestDocument.RootElement.GetProperty("messages");
        Assert.Contains("Tone: Professional", messages[1].GetProperty("content").GetString(), StringComparison.Ordinal);
        Assert.Contains(
            "Additional tone guidance: Hafif sert ve uyarıcı olsun.",
            messages[1].GetProperty("content").GetString(),
            StringComparison.Ordinal);
        Assert.Contains(
            requestDocument.RootElement.GetProperty("format").GetProperty("required")
                .EnumerateArray().Select(item => item.GetString()),
            item => item == "body");
    }

    private static OllamaMailIntelligenceGateway CreateGateway(HttpClient httpClient) =>
        new(httpClient, new TestOptionsMonitor<OllamaOptions>(new OllamaOptions()));

    private static SanitizedConversation CreateSanitizedConversation() =>
        new(
            "Üretim geçişi",
            "Ayşe <ayse@example.com>",
            ["Ali <ali@example.com>"],
            [],
            new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.FromHours(3)),
            "Üretim geçiş tarihini teyit eder misin?",
            false,
            []);

    private static HttpResponseMessage CreateOllamaResponse(string analysisJson)
    {
        var json = JsonSerializer.Serialize(new
        {
            model = "qwen3.5:4b",
            message = new { role = "assistant", content = analysisJson },
            total_duration = 4_200_000
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static string CreateValidAnalysisJson() => JsonSerializer.Serialize(new
    {
        summary = "Tarih teyidi isteniyor.",
        actionRequired = true,
        actions = new[]
        {
            new
            {
                description = "Geçiş tarihini teyit et",
                owner = "Ali",
                dueDate = "2026-09-03",
                confidence = 0.9,
                assignedToCurrentUser = true
            }
        },
        priority = "high",
        sentiment = "neutral",
        warnings = Array.Empty<string>()
    });
}

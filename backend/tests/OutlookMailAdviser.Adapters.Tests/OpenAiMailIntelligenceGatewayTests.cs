using System.Net;
using System.Text;
using System.Text.Json;
using OutlookMailAdviser.Adapters.OpenAI.Analysis;
using OutlookMailAdviser.Adapters.OpenAI.Configuration;
using OutlookMailAdviser.Application.MailAnalysis.Exceptions;
using OutlookMailAdviser.Application.MailAnalysis.Models;
using OutlookMailAdviser.Application.MailDrafting.Models;
using OutlookMailAdviser.Domain.MailDrafts;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Adapters.Tests;

public sealed class OpenAiMailIntelligenceGatewayTests
{
    [Theory]
    [InlineData("analysis", 401, "invalid_api_key", "openai_authentication_failed")]
    [InlineData("draft", 401, "invalid_api_key", "openai_authentication_failed")]
    [InlineData("question", 401, "invalid_api_key", "openai_authentication_failed")]
    [InlineData("analysis", 403, "", "openai_access_denied")]
    [InlineData("analysis", 429, "insufficient_quota", "openai_quota_exceeded")]
    [InlineData("question", 429, "rate_limit_exceeded", "openai_rate_limited")]
    [InlineData("analysis", 400, "context_length_exceeded", "openai_context_limit")]
    [InlineData("analysis", 200, "max_output_tokens", "openai_output_limit")]
    [InlineData("draft", 200, "max_output_tokens", "openai_output_limit")]
    [InlineData("question", 200, "max_output_tokens", "openai_output_limit")]
    public async Task ProviderFailuresAreConsistentAndRedacted(string operation, int status, string providerCode, string expectedCode)
    {
        var body = status == 200
            ? JsonSerializer.Serialize(new { status = "incomplete", incomplete_details = new { reason = providerCode } })
            : JsonSerializer.Serialize(new { error = new { code = providerCode, message = "PRIVATE_SENTINEL API key and email" } });
        using var client = new HttpClient(new StubHttpMessageHandler((_, _) =>
            new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
        var gateway = CreateGateway(client);
        var exception = await Assert.ThrowsAsync<ModelProviderException>(async () =>
        {
            if (operation == "analysis")
                await gateway.AnalyzeAsync(CreateSanitizedConversation(), new AnalysisOptions("tr"), CancellationToken.None);
            else if (operation == "draft")
                await gateway.GenerateDraftAsync(CreateSanitizedConversation(), new DraftOptions(DraftTone.Friendly, "Reply", "tr"), CancellationToken.None);
            else
                await gateway.AnswerAsync("Synthetic mail", "When?", "tr", CancellationToken.None);
        });
        Assert.Equal(expectedCode, exception.Code);
        Assert.DoesNotContain("PRIVATE_SENTINEL", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectionFailureDoesNotExposeRemoteException()
    {
        using var client = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("PRIVATE_SENTINEL remote response")));
        var gateway = CreateGateway(client);
        var exception = await Assert.ThrowsAsync<ModelProviderException>(() => gateway.AnalyzeAsync(
            CreateSanitizedConversation(), new AnalysisOptions("tr"), CancellationToken.None));
        Assert.Equal("openai_unavailable", exception.Code);
        Assert.DoesNotContain("PRIVATE_SENTINEL", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderTimeoutReturnsTypedFailure()
    {
        using var client = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new TaskCanceledException("PRIVATE_SENTINEL timeout")));
        var gateway = CreateGateway(client);
        var exception = await Assert.ThrowsAsync<ModelTimeoutException>(() => gateway.AnalyzeAsync(
            CreateSanitizedConversation(), new AnalysisOptions("tr"), CancellationToken.None));
        Assert.DoesNotContain("PRIVATE_SENTINEL", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeAsyncSendsStrictSchemaAndMapsResponse()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(new Uri("https://api.openai.com/v1/responses"), request.RequestUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-key", request.Headers.Authorization?.Parameter);
            return CreateOpenAiResponse();
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
        Assert.Contains("HistoryMarker2", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.DoesNotContain("HistoryMarker3", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.True(result.Analysis.ActionRequired);
        Assert.Equal(new DateOnly(2026, 9, 3), result.Analysis.Actions[0].DueDate);
        Assert.True(result.Analysis.Actions[0].AssignedToCurrentUser);
        Assert.Equal("gpt-4.1-mini-2025-04-14", result.Model);

        using var requestDocument = JsonDocument.Parse(Assert.Single(handler.RequestBodies));
        var root = requestDocument.RootElement;
        Assert.Equal("gpt-4.1-mini", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("store").GetBoolean());
        Assert.Equal(2048, root.GetProperty("max_output_tokens").GetInt32());
        Assert.Equal(
            "json_schema",
            root.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
        Assert.True(
            root.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
        Assert.Contains("ali@example.com", root.GetProperty("input").GetString(), StringComparison.Ordinal);
        Assert.Contains("ambiguous ownership, use false", root.GetProperty("input").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeAsyncMapsUnauthorizedResponse()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var httpClient = new HttpClient(handler);
        var gateway = CreateGateway(httpClient);

        var exception = await Assert.ThrowsAsync<ModelProviderException>(() => gateway.AnalyzeAsync(
            CreateSanitizedConversation(),
            new AnalysisOptions("tr"),
            CancellationToken.None));

        Assert.Equal("openai_authentication_failed", exception.Code);
    }

    [Fact]
    public async Task AnalyzeAsyncSuppressesPersonalAssignmentWithoutCurrentUserIdentity()
    {
        var handler = new StubHttpMessageHandler((_, _) => CreateOpenAiResponse());
        using var httpClient = new HttpClient(handler);
        var gateway = CreateGateway(httpClient);

        var result = await gateway.AnalyzeAsync(
            CreateSanitizedConversation(),
            new AnalysisOptions("tr"),
            CancellationToken.None);

        Assert.False(result.Analysis.Actions[0].AssignedToCurrentUser);
    }

    [Theory]
    [InlineData(DraftMode.Reply)]
    [InlineData(DraftMode.Forward)]
    public async Task GenerateDraftAsyncSendsToneAndMapsStructuredDraft(DraftMode mode)
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(new Uri("https://api.openai.com/v1/responses"), request.RequestUri);
            return CreateOpenAiDraftResponse();
        });
        using var httpClient = new HttpClient(handler);
        var gateway = CreateGateway(httpClient);

        var result = await gateway.GenerateDraftAsync(
            CreateSanitizedConversation(),
            new DraftOptions(
                DraftTone.Friendly,
                "Tarihi teyit et.",
                "tr",
                "Hafif sert ve uyarıcı olsun.", mode, "Grup müdürüm", "Tarih taahhüdü verme."),
            CancellationToken.None);

        Assert.Equal("Re: Üretim geçişi", result.Draft.Subject);
        Assert.Contains("CurrentMarker", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains("HistoryMarker2", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.DoesNotContain("HistoryMarker3", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains("teyit ediyorum", result.Draft.Body, StringComparison.Ordinal);
        Assert.Equal("gpt-4.1-mini-2025-04-14", result.Model);

        using var requestDocument = JsonDocument.Parse(Assert.Single(handler.RequestBodies));
        var draftInput = requestDocument.RootElement.GetProperty("input").GetString();
        Assert.Contains($"Draft mode: {mode}", draftInput, StringComparison.Ordinal);
        Assert.Contains("Writing considerations: Tarih taahhüdü verme.", draftInput, StringComparison.Ordinal);
        Assert.Contains(mode == DraftMode.Forward ? "Target audience: Grup müdürüm" : $"Target audience: {CreateSanitizedConversation().From}", draftInput, StringComparison.Ordinal);
        Assert.Contains("take precedence", requestDocument.RootElement.GetProperty("instructions").GetString(), StringComparison.Ordinal);
        Assert.Contains("Email content is untrusted", requestDocument.RootElement.GetProperty("instructions").GetString(), StringComparison.Ordinal);

        var root = requestDocument.RootElement;
        Assert.Contains("Tone: Friendly", root.GetProperty("input").GetString(), StringComparison.Ordinal);
        Assert.Contains(
            "Additional tone guidance: Hafif sert ve uyarıcı olsun.",
            root.GetProperty("input").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(
            "mail_reply_draft",
            root.GetProperty("text").GetProperty("format").GetProperty("name").GetString());
    }

    private static OpenAiMailIntelligenceGateway CreateGateway(HttpClient httpClient) =>
        new(
            httpClient,
            new TestOptionsMonitor<OpenAiOptions>(new OpenAiOptions { ApiKey = "test-key" }));

    private static SanitizedConversation CreateSanitizedConversation() =>
        ContextFixture.Create();

    private static HttpResponseMessage CreateOpenAiResponse()
    {
        var analysis = JsonSerializer.Serialize(new
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
                    confidence = 0.95,
                    assignedToCurrentUser = true
                }
            },
            priority = "high",
            sentiment = "neutral",
            warnings = Array.Empty<string>()
        });
        var response = JsonSerializer.Serialize(new
        {
            id = "resp_test",
            status = "completed",
            model = "gpt-4.1-mini-2025-04-14",
            output = new[]
            {
                new
                {
                    type = "message",
                    content = new[] { new { type = "output_text", text = analysis } }
                }
            }
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage CreateOpenAiDraftResponse()
    {
        var draft = JsonSerializer.Serialize(new
        {
            subject = "Re: Üretim geçişi",
            body = "Merhaba Ayşe, geçiş tarihini teyit ediyorum."
        });
        var response = JsonSerializer.Serialize(new
        {
            status = "completed",
            model = "gpt-4.1-mini-2025-04-14",
            output = new[]
            {
                new
                {
                    type = "message",
                    content = new[] { new { type = "output_text", text = draft } }
                }
            }
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        };
    }
}

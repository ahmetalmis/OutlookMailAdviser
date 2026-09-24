using OutlookMailAdviser.Application.MailAnalysis;
using OutlookMailAdviser.Application.MailAnalysis.Models;
using OutlookMailAdviser.Application.MailAnalysis.Ports;
using OutlookMailAdviser.Domain.Analysis;
using OutlookMailAdviser.Domain.Mails;
using DomainMailAnalysis = OutlookMailAdviser.Domain.Analysis.MailAnalysis;

namespace OutlookMailAdviser.Application.Tests;

public sealed class AnalyzeMailHandlerTests
{
    [Fact]
    public async Task ExecuteAsyncMergesWarningsAndDefaultsLanguageToTurkish()
    {
        var sanitizedConversation = new SanitizedConversation(
            "Subject",
            "sender@example.com",
            ["recipient@example.com"],
            [],
            null,
            "Clean body",
            true,
            ["content_truncated"]);
        var sanitizer = new StubSanitizer(sanitizedConversation);
        var gateway = new StubGateway(new MailIntelligenceResult(
            new DomainMailAnalysis(
                "Özet",
                true,
                [new MailAction("Tarihi teyit et", null, null, 0.8)],
                PriorityLevel.High,
                MailSentiment.Neutral,
                ["model_warning"]),
            "qwen3.5:4b",
            1200));
        var handler = new AnalyzeMailHandler(sanitizer, gateway);

        var result = await handler.ExecuteAsync(
            new AnalyzeMailCommand(
                CreateConversation(),
                string.Empty,
                new MailParticipant("Ali", "ali@example.com")),
            CancellationToken.None);

        Assert.Equal("tr", gateway.ReceivedOptions?.PreferredLanguage);
        Assert.Equal("ali@example.com", gateway.ReceivedOptions?.CurrentUser?.Address);
        Assert.True(result.WasTruncated);
        Assert.Equal(["content_truncated", "model_warning"], result.Analysis.Warnings);
        Assert.Equal("qwen3.5:4b", result.Model);
    }

    private static MailConversation CreateConversation() =>
        new([
            new MailMessage(
                null,
                "Subject",
                new MailParticipant(null, "sender@example.com"),
                [new MailParticipant(null, "recipient@example.com")],
                [],
                null,
                "Body",
                MailBodyFormat.PlainText)
        ]);

    private sealed class StubSanitizer(SanitizedConversation result) : IMailContentSanitizer
    {
        public SanitizedConversation Sanitize(MailConversation conversation) => result;
    }

    private sealed class StubGateway(MailIntelligenceResult result) : IMailIntelligenceGateway
    {
        public AnalysisOptions? ReceivedOptions { get; private set; }

        public Task<MailIntelligenceResult> AnalyzeAsync(
            SanitizedConversation conversation,
            AnalysisOptions options,
            CancellationToken cancellationToken)
        {
            ReceivedOptions = options;
            return Task.FromResult(result);
        }
    }
}

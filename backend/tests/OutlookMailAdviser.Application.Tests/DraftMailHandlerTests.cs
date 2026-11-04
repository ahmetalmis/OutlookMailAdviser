using OutlookMailAdviser.Application.MailAnalysis.Models;
using OutlookMailAdviser.Application.MailAnalysis.Ports;
using OutlookMailAdviser.Application.MailDrafting;
using OutlookMailAdviser.Application.MailDrafting.Models;
using OutlookMailAdviser.Application.MailDrafting.Ports;
using OutlookMailAdviser.Domain.MailDrafts;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Application.Tests;

public sealed class DraftMailHandlerTests
{
    [Fact]
    public async Task ExecuteAsyncSanitizesConversationAndPassesDraftOptions()
    {
        var sanitized = new SanitizedConversation(
            "Subject",
            "sender@example.com",
            ["recipient@example.com"],
            [],
            null,
            "Clean body",
            true,
            ["content_truncated"]);
        var gateway = new StubDraftGateway();
        var handler = new DraftMailHandler(new StubSanitizer(sanitized), gateway);

        var result = await handler.ExecuteAsync(
            new DraftMailCommand(
                CreateConversation(),
                DraftTone.Empathetic,
                "  Gecikme için özür dile.  ",
                string.Empty,
                "  Hafif sert ve uyarıcı olsun.  "),
            CancellationToken.None);

        Assert.Equal(DraftTone.Empathetic, gateway.ReceivedOptions?.Tone);
        Assert.Equal("Gecikme için özür dile.", gateway.ReceivedOptions?.Instructions);
        Assert.Equal("tr", gateway.ReceivedOptions?.PreferredLanguage);
        Assert.Equal("Hafif sert ve uyarıcı olsun.", gateway.ReceivedOptions?.ToneDetails);
        Assert.True(result.WasTruncated);
        Assert.Equal("Taslak konu", result.Draft.Subject);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsToneDetailsOverFiveHundredCharacters()
    {
        var sanitized = new SanitizedConversation(
            "Subject",
            "sender@example.com",
            ["recipient@example.com"],
            [],
            null,
            "Clean body",
            false,
            []);
        var handler = new DraftMailHandler(
            new StubSanitizer(sanitized),
            new StubDraftGateway());

        await Assert.ThrowsAsync<ArgumentException>(() => handler.ExecuteAsync(
            new DraftMailCommand(
                CreateConversation(),
                DraftTone.Professional,
                "Tarihi teyit et.",
                "tr",
                new string('x', 501)),
            CancellationToken.None));
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

    private sealed class StubDraftGateway : IMailDraftGateway
    {
        public DraftOptions? ReceivedOptions { get; private set; }

        public Task<MailDraftGatewayResult> GenerateDraftAsync(
            SanitizedConversation conversation,
            DraftOptions options,
            CancellationToken cancellationToken)
        {
            ReceivedOptions = options;
            return Task.FromResult(new MailDraftGatewayResult(
                new MailDraft("Taslak konu", "Taslak gövde"),
                "test-model",
                10));
        }
    }
}

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

    [Theory]
    [InlineData(DraftMode.Reply, null, null)]
    [InlineData(DraftMode.Forward, "  Grup müdürüm  ", "  Tarih taahhüdü verme.  ")]
    public async Task PassesNormalizedAudienceAndConsiderations(DraftMode mode, string? audience, string? considerations)
    {
        var gateway = new StubDraftGateway();
        var sanitized = new SanitizedConversation("Subject", "sender@example.com", ["recipient@example.com"], [], null, "Body", false, []);
        var handler = new DraftMailHandler(new StubSanitizer(sanitized), gateway);
        await handler.ExecuteAsync(new DraftMailCommand(CreateConversation(), DraftTone.Professional, "Aksiyon iste.", "tr",
            DraftMode: mode, TargetAudience: audience, Considerations: considerations), CancellationToken.None);
        Assert.Equal(mode, gateway.ReceivedOptions?.DraftMode);
        Assert.Equal(audience?.Trim(), gateway.ReceivedOptions?.TargetAudience);
        Assert.Equal(considerations?.Trim(), gateway.ReceivedOptions?.Considerations);
    }

    [Theory]
    [InlineData(DraftMode.Forward, 0, 0)]
    [InlineData(DraftMode.Forward, 501, 0)]
    [InlineData(DraftMode.Reply, 0, 2001)]
    [InlineData((DraftMode)99, 0, 0)]
    public async Task RejectsInvalidDraftChoices(DraftMode mode, int audienceLength, int considerationsLength)
    {
        var gateway = new StubDraftGateway();
        var sanitized = new SanitizedConversation("Subject", "sender@example.com", ["recipient@example.com"], [], null, "Body", false, []);
        var handler = new DraftMailHandler(new StubSanitizer(sanitized), gateway);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.ExecuteAsync(
            new DraftMailCommand(CreateConversation(), DraftTone.Professional, "Aksiyon iste.", "tr", DraftMode: mode,
                TargetAudience: audienceLength == 0 ? "  " : new string('x', audienceLength),
                Considerations: new string('x', considerationsLength)), CancellationToken.None));
        Assert.Null(gateway.ReceivedOptions);
    }

    [Fact]
    public async Task AcceptsExactLimitsAndIgnoresAudienceInReplyMode()
    {
        var gateway = new StubDraftGateway();
        var sanitized = new SanitizedConversation("Subject", "sender@example.com", ["recipient@example.com"], [], null, "Body", false, []);
        var handler = new DraftMailHandler(new StubSanitizer(sanitized), gateway);
        var command = new DraftMailCommand(CreateConversation(), DraftTone.Professional, "Aksiyon iste.", "tr",
            DraftMode: DraftMode.Forward, TargetAudience: new string('x', 500), Considerations: new string('x', 2000));
        await handler.ExecuteAsync(command, CancellationToken.None);
        Assert.Equal(500, gateway.ReceivedOptions?.TargetAudience?.Length);
        Assert.Equal(2000, gateway.ReceivedOptions?.Considerations?.Length);
        await handler.ExecuteAsync(command with { DraftMode = DraftMode.Reply }, CancellationToken.None);
        Assert.Null(gateway.ReceivedOptions?.TargetAudience);
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

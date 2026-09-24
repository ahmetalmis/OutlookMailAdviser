using OutlookMailAdviser.Adapters.MailContent;
using OutlookMailAdviser.Adapters.MailContent.Configuration;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Adapters.Tests;

public sealed class HtmlMailContentSanitizerTests
{
    [Fact]
    public void SanitizeRemovesExecutableHtmlAndPreservesReadableText()
    {
        var sanitizer = CreateSanitizer(maxInputCharacters: 10_000);
        var conversation = CreateConversation(
            "<style>.hidden{display:none}</style><p>Merhaba&nbsp;Ali</p>"
            + "<script>alert('x')</script><div>Tarihi teyit eder misin?</div>");

        var result = sanitizer.Sanitize(conversation);

        Assert.Contains("Merhaba Ali", result.Content, StringComparison.Ordinal);
        Assert.Contains("Tarihi teyit eder misin?", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("alert", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("display:none", result.Content, StringComparison.Ordinal);
        Assert.False(result.WasTruncated);
    }

    [Fact]
    public void SanitizeTruncatesAtConfiguredLimitAndReportsWarning()
    {
        const int limit = 1_000;
        var sanitizer = CreateSanitizer(limit);
        var conversation = CreateConversation($"<p>En yeni talep</p><p>{new string('x', 2_000)}</p>");

        var result = sanitizer.Sanitize(conversation);

        Assert.True(result.WasTruncated);
        Assert.Equal(limit, result.Content.Length);
        Assert.Contains("En yeni talep", result.Content, StringComparison.Ordinal);
        Assert.Single(result.Warnings);
    }

    [Theory]
    [InlineData("From", "Sent", "To", "Subject")]
    [InlineData("Kimden", "Tarih", "Kime", "Konu")]
    public void KeepsOnlyTwoPreviousMessages(string from, string sent, string to, string subject)
    {
        string Previous(int index) => $"<p>{from}: Person{index}</p><p>{sent}: 2026-09-24</p>"
            + $"<p>{to}: Ali</p><p>{subject}: Request{index}</p><p>Action{index}</p>";
        var result = CreateSanitizer(16_000).Sanitize(CreateConversation(
            "<p>Ali, 25 Eylül tarihinde teyit eder misin?</p>" + Previous(1) + Previous(2) + Previous(3)));
        Assert.Contains("25 Eylül", result.Content, StringComparison.Ordinal);
        Assert.Contains("Action2", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Action3", result.Content, StringComparison.Ordinal);
        Assert.Equal(2, result.ContentProcessing!.IncludedHistoryMessages);
        Assert.True(result.ContentProcessing.HistoryLimited);
        Assert.False(result.ContentProcessing.CurrentMessageTruncated);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void PreservesInterleavedRepliesAndReportsUncertainty()
    {
        var result = CreateSanitizer(16_000).Sanitize(CreateConversation(
            "<blockquote><p>From: Ayşe</p><p>Sent: yesterday</p><p>To: Ali</p>"
            + "<p>Subject: Test</p><p>Can you confirm?</p></blockquote><p>Yes, on Friday.</p>"));
        Assert.Contains("Yes, on Friday.", result.Content, StringComparison.Ordinal);
        Assert.True(result.ContentProcessing!.ParsingUncertain);
        Assert.False(result.WasTruncated);
    }

    [Fact]
    public void RemovesExplicitSignatureButPreservesThanksAndNames()
    {
        var result = CreateSanitizer(16_000).Sanitize(CreateConversation(
            "<p>Teşekkürler Ali, teyit eder misin?</p><div id='Signature'>Phone 123</div>"));
        Assert.Contains("Teşekkürler Ali", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Phone 123", result.Content, StringComparison.Ordinal);
        Assert.False(result.WasTruncated);
    }

    [Fact]
    public void AmbiguousHeaderIsNotTreatedAsHistory()
    {
        var result = CreateSanitizer(1_000).Sanitize(CreateConversation(
            "<p>From: someone</p><p>" + new string('x', 2_000) + "</p>"));
        Assert.True(result.ContentProcessing!.ParsingUncertain);
        Assert.True(result.ContentProcessing.CurrentMessageTruncated);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void HistoryCannotConsumeCurrentMessageBudget()
    {
        var result = CreateSanitizer(16_000).Sanitize(CreateConversation(
            "<p>" + new string('a', 13_000) + "</p><p>From: Old</p><p>Sent: yesterday</p>"
            + "<p>To: Ali</p><p>Subject: Old</p><p>" + new string('b', 8_000) + "</p>"));
        Assert.False(result.ContentProcessing!.CurrentMessageTruncated);
        Assert.True(result.ContentProcessing.HistoryLimited);
        Assert.True(result.Content.Length <= 16_000);
        Assert.Contains(new string('a', 13_000), result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateHistoryDoesNotConsumeMessageCount()
    {
        const string previous = "<p>From: Old</p><p>Sent: yesterday</p><p>To: Ali</p><p>Subject: Old</p><p>Confirm</p>";
        var result = CreateSanitizer(16_000).Sanitize(CreateConversation("<p>Latest</p>" + previous + previous));
        Assert.Equal(1, result.ContentProcessing!.IncludedHistoryMessages);
        Assert.False(result.WasTruncated);
    }

    [Fact]
    public void TruncationDoesNotSplitUnicodeTextElements()
    {
        var result = CreateSanitizer(1_000).Sanitize(CreateConversation(
            "<p>" + string.Concat(Enumerable.Repeat("😀e\u0301", 600)) + "</p>"));
        Assert.True(result.WasTruncated);
        Assert.False(char.IsHighSurrogate(result.Content[^1]));
        Assert.NotEqual('e', result.Content[^1]);
    }

    private static HtmlMailContentSanitizer CreateSanitizer(int maxInputCharacters) =>
        CreateConfiguredSanitizer(maxInputCharacters);

    [Fact]
    public void ForwardedMessageIsRetainedAndHistoryBudgetIncludesSeparator()
    {
        var result = CreateConfiguredSanitizer(16_000, 200).Sanitize(CreateConversation(
            "<p>Please review the forwarded request.</p><div id='divRplyFwdMsg'><b>From:</b> Sender<br>"
            + "Sent: yesterday<br>To: Ali<br>Subject: Forward</div><p>" + new string('x', 500) + "</p>"));
        Assert.Equal(1, result.ContentProcessing!.IncludedHistoryMessages);
        Assert.True(result.ContentProcessing.HistoryLimited);
        var start = result.Content.IndexOf("\n\n--- Previous message ---", StringComparison.Ordinal);
        Assert.InRange(result.Content.Length - start, 1, 200);
        Assert.Contains("Please review", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatedKnownFooterIsRemovedWithoutTruncation()
    {
        var footer = "This email is confidential. " + new string('x', 100);
        var result = CreateSanitizer(16_000).Sanitize(CreateConversation(
            "<p>Latest request</p><p>" + footer + "</p><p>From: Old</p><p>Sent: yesterday</p>"
            + "<p>To: Ali</p><p>Subject: Test</p><p>Earlier request</p><p>" + footer + "</p>"));
        Assert.DoesNotContain(footer, result.Content, StringComparison.Ordinal);
        Assert.Contains("Earlier request", result.Content, StringComparison.Ordinal);
        Assert.False(result.WasTruncated);
    }

    private static HtmlMailContentSanitizer CreateConfiguredSanitizer(int maxInputCharacters, int historyCharacters = 4_000) =>
        new(new TestOptionsMonitor<MailContentOptions>(new MailContentOptions
        {
            MaxInputCharacters = maxInputCharacters,
            MaxHistoryCharacters = historyCharacters
        }));

    private static MailConversation CreateConversation(string body)
    {
        var message = new MailMessage(
            "item-1",
            "Üretim geçişi",
            new MailParticipant("Ayşe", "ayse@example.com"),
            [new MailParticipant("Ali", "ali@example.com")],
            [],
            new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.FromHours(3)),
            body,
            MailBodyFormat.Html);

        return new MailConversation([message]);
    }
}

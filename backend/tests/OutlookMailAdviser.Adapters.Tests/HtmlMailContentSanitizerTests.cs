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

    private static HtmlMailContentSanitizer CreateSanitizer(int maxInputCharacters) =>
        new(new TestOptionsMonitor<MailContentOptions>(new MailContentOptions
        {
            MaxInputCharacters = maxInputCharacters
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


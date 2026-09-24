using OutlookMailAdviser.Adapters.MailContent;
using OutlookMailAdviser.Adapters.MailContent.Configuration;
using OutlookMailAdviser.Application.MailAnalysis.Models;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Adapters.Tests;

internal static class ContextFixture
{
    public static SanitizedConversation Create()
    {
        string History(int n) => $"<p>From: Person{n}</p><p>Sent: yesterday</p><p>To: Ali</p><p>Subject: Past{n}</p><p>HistoryMarker{n}</p>";
        var message = new MailMessage(null, "Üretim geçişi",
            new MailParticipant("Ayşe", "ayse@example.com"),
            [new MailParticipant("Ali", "ali@example.com")], [], null,
            "<p>CurrentMarker: Üretim geçiş tarihini teyit eder misin?</p>" + History(1) + History(2) + History(3),
            MailBodyFormat.Html);
        return new HtmlMailContentSanitizer(new TestOptionsMonitor<MailContentOptions>(new()))
            .Sanitize(new MailConversation([message]));
    }
}

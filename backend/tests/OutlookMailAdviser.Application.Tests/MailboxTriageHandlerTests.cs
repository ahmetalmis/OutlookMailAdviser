using System.Globalization;
using OutlookMailAdviser.Application.MailboxTriage;
using OutlookMailAdviser.Application.MailboxTriage.Models;
using OutlookMailAdviser.Application.MailboxTriage.Ports;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Application.Tests;

public sealed class MailboxTriageHandlerTests
{
    [Fact]
    public void HeaderFilterDropsAutoReplyAndKeepsDirectCopy()
    {
        var user = new MailParticipant("Ali", "ali@example.com");
        var autoReply = Message("a", "Otomatik yanıt: toplantı", "bot@example.com", user.Address, autoSubmitted: true);
        var copied = Message("b", "Karar notu", "ayse@example.com", user.Address, ccOnly: true);
        var broadcast = Message("c", "Duyuru", "ekip@example.com", "list@example.com");

        var unknownRecipients = Message("d", "Konu", "ayse@example.com", "ali@example.com") with
        {
            To = [],
            Cc = []
        };
        var exchangeName = Message("e", "Toplantı", "meryem@innova.com.tr", "list@example.com") with
        {
            To =
            [
                new MailParticipant("Kamil Ozan ÖZDEMİR", "/O=INNOVA/OU=EXCHANGE/CN=OZOZDEMIR"),
                new MailParticipant("Seher SEVEN", "sseven@innova.com.tr")
            ]
        };

        Assert.True(MailboxHeaderFilter.ShouldDrop(autoReply, user));
        Assert.False(MailboxHeaderFilter.ShouldDrop(copied, user));
        Assert.True(MailboxHeaderFilter.ShouldDrop(broadcast, user));
        Assert.False(MailboxHeaderFilter.ShouldDrop(unknownRecipients, user));
        Assert.False(MailboxHeaderFilter.ShouldDrop(
            exchangeName,
            new MailParticipant("Kamil Ozan ÖZDEMİR", "ozozdemir@innova.com.tr")));
    }

    [Fact]
    public async Task ExecuteAsyncKeepsEachMessageInTheSameConversation()
    {
        var older = Message("old", "Konu", "ayse@example.com", "ali@example.com", receivedAt: "2026-10-19T07:00:00Z", conversationId: "same");
        var newer = Message("new", "Konu", "ayse@example.com", "ali@example.com", receivedAt: "2026-10-19T09:00:00Z", conversationId: "same");
        var classifier = new RecordingClassifier();
        var handler = new MailboxTriageHandler(classifier);

        var result = await handler.ExecuteAsync(
            new MailboxTriageCommand(
                "tr",
                new MailParticipant("Ali", "ali@example.com"),
                [older, newer]),
            CancellationToken.None);

        Assert.Equal(["m1", "m2"], classifier.ReceivedIds);
        Assert.Equal("new", result.ActionItems[0].ItemId);
        Assert.Equal("old", result.ActionItems[1].ItemId);
        Assert.Equal(2, result.ScannedCount);
        Assert.Equal(2, result.ClassifiedCount);
        Assert.Equal(0, result.HeaderFilteredCount);
    }

    [Fact]
    public async Task ExecuteAsyncPromotesADirectQuestionTheModelIgnored()
    {
        var user = new MailParticipant("Kamil Ozan ÖZDEMİR", "ozozdemir@innova.com.tr");
        var direct = Message("burak", "Rahle Analiz Hk.", "bcakir@innova.com.tr", user.Address) with
        {
            To = [new MailParticipant("Kamil Ozan ÖZDEMİR", user.Address)],
            Preview = "Bu planlar çok uzun sürüyor ne yapacağız, biz artık analiz beklemekten yorulduk."
        };
        var broadcast = Message("duyuru", "Duyuru", "ekip@example.com", user.Address) with
        {
            To = [user, new MailParticipant("Seher SEVEN", "sseven@innova.com.tr")],
            Preview = "Bilginize."
        };
        var handler = new MailboxTriageHandler(new FixedClassifier(MailboxVerdict.Ignore));

        var result = await handler.ExecuteAsync(
            new MailboxTriageCommand("tr", user, [direct, broadcast]),
            CancellationToken.None);

        Assert.Contains(result.ActionItems, item => item.ItemId == "burak");
        Assert.DoesNotContain(result.ActionItems, item => item.ItemId == "duyuru");
        Assert.DoesNotContain(result.ReviewItems, item => item.ItemId == "duyuru");
    }

    [Fact]
    public async Task ExecuteAsyncPromotesALaterDirectRequestInTheSameConversation()
    {
        var user = new MailParticipant("Kamil Ozan ÖZDEMİR", "ozozdemir@innova.com.tr");
        var later = Message(
            "font",
            "RE: Rahle Analiz Hk.",
            "bcakir@innova.com.tr",
            user.Address,
            receivedAt: "2026-10-20T14:10:00Z",
            conversationId: "rahle") with
        {
            To = [new MailParticipant("Kamil Ozan ÖZDEMİR", user.Address)],
            Preview = "Merhaba Ozan Bey, Öncelikle mail puntonuz çok küçük, biraz büyültürseniz, Calibri 11 olsun bari."
        };
        var handler = new MailboxTriageHandler(new FixedClassifier(MailboxVerdict.Ignore));

        var result = await handler.ExecuteAsync(
            new MailboxTriageCommand("tr", user, [later]),
            CancellationToken.None);

        Assert.Contains(result.ActionItems, item => item.ItemId == "font");
    }

    [Fact]
    public async Task ExecuteAsyncShowsInformationalMailWithoutAReply()
    {
        var user = new MailParticipant("Kamil Ozan ÖZDEMİR", "ozozdemir@innova.com.tr");
        var info = Message("bilgi", "Rahle Analiz Hk.", "bcakir@innova.com.tr", user.Address) with
        {
            To = [user, new MailParticipant("Seher SEVEN", "sseven@innova.com.tr")],
            Preview = "Punto ayarı Calibri 11 olarak güncellendi. Kontak listesi ekte. Hasan Hüseyin Binboğa ile iletişim kurulacak."
        };
        var thin = Message("duyuru", "Duyuru", "ekip@example.com", user.Address) with
        {
            To = [user, new MailParticipant("Seher SEVEN", "sseven@innova.com.tr")],
            Preview = "Bilginize."
        };
        var handler = new MailboxTriageHandler(new FixedClassifier(MailboxVerdict.Ignore));

        var result = await handler.ExecuteAsync(
            new MailboxTriageCommand("tr", user, [info, thin]),
            CancellationToken.None);

        Assert.Contains(result.ActionItems, item => item.ItemId == "bilgi");
        Assert.DoesNotContain(result.ActionItems, item => item.ItemId == "duyuru");
        Assert.DoesNotContain(result.ReviewItems, item => item.ItemId == "duyuru");
    }

    private static MailboxPreviewMessage Message(
        string id,
        string subject,
        string from,
        string to,
        bool autoSubmitted = false,
        bool ccOnly = false,
        string? receivedAt = null,
        string? conversationId = null) =>
        new(
            id,
            conversationId,
            subject,
            new MailParticipant(null, from),
            ccOnly ? [] : [new MailParticipant(null, to)],
            ccOnly ? [new MailParticipant(null, to)] : [],
            receivedAt is null ? null : DateTimeOffset.Parse(receivedAt, CultureInfo.InvariantCulture),
            "Önizleme",
            false,
            autoSubmitted);

    private sealed class FixedClassifier(MailboxVerdict verdict) : IMailboxPreviewClassifier
    {
        public Task<MailboxPreviewClassification> ClassifyAsync(
            IReadOnlyList<MailboxPreviewMessage> messages,
            MailParticipant? currentUser,
            string preferredLanguage,
            CancellationToken cancellationToken) =>
            Task.FromResult(new MailboxPreviewClassification(
                "stub",
                10,
                messages.Select(message => new MailboxPreviewVerdict(
                    message.ItemId,
                    verdict,
                    "Yok sayıldı.",
                    "low")).ToArray()));
    }

    private sealed class RecordingClassifier : IMailboxPreviewClassifier
    {
        public IReadOnlyList<string> ReceivedIds { get; private set; } = [];

        public Task<MailboxPreviewClassification> ClassifyAsync(
            IReadOnlyList<MailboxPreviewMessage> messages,
            MailParticipant? currentUser,
            string preferredLanguage,
            CancellationToken cancellationToken)
        {
            ReceivedIds = messages.Select(message => message.ItemId).ToArray();
            return Task.FromResult(new MailboxPreviewClassification(
                "stub",
                10,
                messages.Select(message => new MailboxPreviewVerdict(
                    message.ItemId,
                    MailboxVerdict.NeedsAction,
                    "Cevap gerekli.",
                    "high")).ToArray()));
        }
    }
}

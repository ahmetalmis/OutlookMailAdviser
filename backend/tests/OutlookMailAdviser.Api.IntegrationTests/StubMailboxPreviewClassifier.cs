using OutlookMailAdviser.Application.MailboxTriage.Models;
using OutlookMailAdviser.Application.MailboxTriage.Ports;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Api.IntegrationTests;

public sealed class StubMailboxPreviewClassifier : IMailboxPreviewClassifier
{
    public IReadOnlyList<string> ReceivedIds { get; private set; } = [];

    public Task<MailboxPreviewClassification> ClassifyAsync(
        IReadOnlyList<MailboxPreviewMessage> messages,
        MailParticipant? currentUser,
        string preferredLanguage,
        CancellationToken cancellationToken)
    {
        ReceivedIds = messages.Select(message => message.ItemId).ToArray();
        var verdicts = messages
            .Select(message => new MailboxPreviewVerdict(
                message.ItemId,
                MailboxVerdict.NeedsAction,
                "Yanıt bekleniyor.",
                "high"))
            .ToArray();

        return Task.FromResult(new MailboxPreviewClassification("stub-model", 25, verdicts));
    }
}

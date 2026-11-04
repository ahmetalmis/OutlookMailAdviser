using OutlookMailAdviser.Application.MailboxTriage.Models;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Application.MailboxTriage.Ports;

public interface IMailboxPreviewClassifier
{
    Task<MailboxPreviewClassification> ClassifyAsync(
        IReadOnlyList<MailboxPreviewMessage> messages,
        MailParticipant? currentUser,
        string preferredLanguage,
        CancellationToken cancellationToken);
}

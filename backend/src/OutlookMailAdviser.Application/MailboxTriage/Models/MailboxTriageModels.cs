using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Application.MailboxTriage.Models;

public enum MailboxVerdict
{
    NeedsAction = 0,
    Review = 1,
    Ignore = 2
}

public sealed record MailboxPreviewMessage(
    string ItemId,
    string? ConversationId,
    string Subject,
    MailParticipant From,
    IReadOnlyList<MailParticipant> To,
    IReadOnlyList<MailParticipant> Cc,
    DateTimeOffset? ReceivedAt,
    string Preview,
    bool HasUnsubscribeHint,
    bool IsAutoSubmitted);

public sealed record MailboxTriageCommand(
    string PreferredLanguage,
    MailParticipant? CurrentUser,
    IReadOnlyList<MailboxPreviewMessage> Messages);

public sealed record MailboxPreviewVerdict(
    string ItemId,
    MailboxVerdict Verdict,
    string Reason,
    string Priority);

public sealed record MailboxPreviewClassification(
    string Model,
    long DurationMilliseconds,
    IReadOnlyList<MailboxPreviewVerdict> Verdicts);

public sealed record MailboxTriageItem(
    string ItemId,
    string Subject,
    string? FromName,
    string FromAddress,
    DateTimeOffset? ReceivedAt,
    string Reason,
    string Priority);

public sealed record MailboxTriageResult(
    int ScannedCount,
    int HeaderFilteredCount,
    int ClassifiedCount,
    string Model,
    long DurationMilliseconds,
    IReadOnlyList<MailboxTriageItem> ActionItems,
    IReadOnlyList<MailboxTriageItem> ReviewItems);

namespace OutlookMailAdviser.Application.MailAnalysis.Models;

public sealed record SanitizedConversation(
    string Subject,
    string From,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    DateTimeOffset? SentAt,
    string Content,
    bool WasTruncated,
    IReadOnlyList<string> Warnings,
    ContentProcessing? ContentProcessing = null);

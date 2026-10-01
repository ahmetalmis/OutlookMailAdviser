using OutlookMailAdviser.Domain.MailDrafts;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Application.MailDrafting.Models;

public sealed record DraftMailCommand(
    MailConversation Conversation,
    DraftTone Tone,
    string Instructions,
    string PreferredLanguage,
    string? ToneDetails = null,
    IReadOnlyList<string>? SourceQuotes = null,
    DraftMode DraftMode = DraftMode.Reply,
    string? TargetAudience = null,
    string? Considerations = null);

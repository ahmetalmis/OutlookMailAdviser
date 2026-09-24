using OutlookMailAdviser.Domain.MailDrafts;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Application.MailDrafting.Models;

public sealed record DraftMailCommand(
    MailConversation Conversation,
    DraftTone Tone,
    string Instructions,
    string PreferredLanguage,
    string? ToneDetails = null);

using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Application.MailAnalysis.Models;

public sealed record AnalyzeMailCommand(
    MailConversation Conversation,
    string PreferredLanguage,
    MailParticipant? CurrentUser = null);

using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Application.MailAnalysis.Models;

public sealed record AnalysisOptions(
    string PreferredLanguage,
    MailParticipant? CurrentUser = null);

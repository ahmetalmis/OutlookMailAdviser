using OutlookMailAdviser.Application.MailAnalysis.Models;

namespace OutlookMailAdviser.Application.MailAnalysis.Ports;

public interface IMailIntelligenceGateway
{
    Task<MailIntelligenceResult> AnalyzeAsync(
        SanitizedConversation conversation,
        AnalysisOptions options,
        CancellationToken cancellationToken);
}


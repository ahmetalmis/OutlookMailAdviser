using OutlookMailAdviser.Application.MailAnalysis.Models;
using OutlookMailAdviser.Application.MailAnalysis.Ports;
using OutlookMailAdviser.Domain.Analysis;

namespace OutlookMailAdviser.Api.IntegrationTests;

public sealed class StubMailIntelligenceGateway : IMailIntelligenceGateway
{
    public SanitizedConversation? ReceivedConversation { get; private set; }

    public AnalysisOptions? ReceivedOptions { get; private set; }

    public Task<MailIntelligenceResult> AnalyzeAsync(
        SanitizedConversation conversation,
        AnalysisOptions options,
        CancellationToken cancellationToken)
    {
        ReceivedConversation = conversation;
        ReceivedOptions = options;

        return Task.FromResult(new MailIntelligenceResult(
            new MailAnalysis(
                "Üretim geçiş tarihi için teyit isteniyor.",
                true,
                [new MailAction(
                    "Geçiş tarihini teyit et",
                    "Ali",
                    new DateOnly(2026, 9, 3),
                    0.9,
                    assignedToCurrentUser: true)],
                PriorityLevel.High,
                MailSentiment.Neutral),
            "test-model",
            25));
    }
}

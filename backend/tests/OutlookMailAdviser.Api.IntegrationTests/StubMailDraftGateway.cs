using OutlookMailAdviser.Application.MailAnalysis.Models;
using OutlookMailAdviser.Application.MailDrafting.Models;
using OutlookMailAdviser.Application.MailDrafting.Ports;
using OutlookMailAdviser.Domain.MailDrafts;

namespace OutlookMailAdviser.Api.IntegrationTests;

public sealed class StubMailDraftGateway : IMailDraftGateway
{
    public SanitizedConversation? ReceivedConversation { get; private set; }

    public DraftOptions? ReceivedOptions { get; private set; }

    public Task<MailDraftGatewayResult> GenerateDraftAsync(
        SanitizedConversation conversation,
        DraftOptions options,
        CancellationToken cancellationToken)
    {
        ReceivedConversation = conversation;
        ReceivedOptions = options;

        return Task.FromResult(new MailDraftGatewayResult(
            new MailDraft(
                "Re: Üretim geçiş planı",
                "Merhaba Ayşe,\n\nGeçiş tarihini teyit ediyorum. Teşekkürler."),
            "test-model",
            30));
    }
}

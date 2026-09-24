using OutlookMailAdviser.Application.MailAnalysis.Models;
using OutlookMailAdviser.Application.MailDrafting.Models;

namespace OutlookMailAdviser.Application.MailDrafting.Ports;

public interface IMailDraftGateway
{
    Task<MailDraftGatewayResult> GenerateDraftAsync(
        SanitizedConversation conversation,
        DraftOptions options,
        CancellationToken cancellationToken);
}

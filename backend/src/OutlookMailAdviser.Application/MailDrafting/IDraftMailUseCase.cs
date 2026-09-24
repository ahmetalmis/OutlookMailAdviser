using OutlookMailAdviser.Application.MailDrafting.Models;

namespace OutlookMailAdviser.Application.MailDrafting;

public interface IDraftMailUseCase
{
    Task<DraftMailResult> ExecuteAsync(
        DraftMailCommand command,
        CancellationToken cancellationToken);
}

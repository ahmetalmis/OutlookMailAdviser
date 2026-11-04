using OutlookMailAdviser.Application.MailboxTriage.Models;

namespace OutlookMailAdviser.Application.MailboxTriage;

public interface IMailboxTriageUseCase
{
    Task<MailboxTriageResult> ExecuteAsync(
        MailboxTriageCommand command,
        CancellationToken cancellationToken);
}

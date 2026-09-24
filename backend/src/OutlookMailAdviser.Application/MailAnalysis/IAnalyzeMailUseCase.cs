using OutlookMailAdviser.Application.MailAnalysis.Models;

namespace OutlookMailAdviser.Application.MailAnalysis;

public interface IAnalyzeMailUseCase
{
    Task<AnalyzeMailResult> ExecuteAsync(
        AnalyzeMailCommand command,
        CancellationToken cancellationToken);
}


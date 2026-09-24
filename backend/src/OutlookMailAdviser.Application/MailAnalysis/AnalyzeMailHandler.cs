using System.Globalization;
using OutlookMailAdviser.Application.MailAnalysis.Models;
using OutlookMailAdviser.Application.MailAnalysis.Ports;
using DomainMailAnalysis = OutlookMailAdviser.Domain.Analysis.MailAnalysis;

namespace OutlookMailAdviser.Application.MailAnalysis;

public sealed class AnalyzeMailHandler(
    IMailContentSanitizer sanitizer,
    IMailIntelligenceGateway intelligenceGateway) : IAnalyzeMailUseCase
{
    public async Task<AnalyzeMailResult> ExecuteAsync(
        AnalyzeMailCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Conversation);

        var preferredLanguage = NormalizeLanguage(command.PreferredLanguage);
        var sanitizedConversation = sanitizer.Sanitize(command.Conversation);
        var intelligenceResult = await intelligenceGateway.AnalyzeAsync(
            sanitizedConversation,
            new AnalysisOptions(preferredLanguage, command.CurrentUser),
            cancellationToken);

        var combinedWarnings = sanitizedConversation.Warnings
            .Concat(intelligenceResult.Analysis.Warnings)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var analysis = new DomainMailAnalysis(
            intelligenceResult.Analysis.Summary,
            intelligenceResult.Analysis.ActionRequired,
            intelligenceResult.Analysis.Actions,
            intelligenceResult.Analysis.Priority,
            intelligenceResult.Analysis.Sentiment,
            combinedWarnings);

        return new AnalyzeMailResult(
            analysis,
            intelligenceResult.Model,
            intelligenceResult.DurationMilliseconds,
            sanitizedConversation.WasTruncated);
    }

    private static string NormalizeLanguage(string preferredLanguage)
    {
        if (string.IsNullOrWhiteSpace(preferredLanguage))
        {
            return "tr";
        }

        var normalized = preferredLanguage.Trim();
        if (normalized.Length > 15)
        {
            throw new ArgumentException(
                "Preferred language cannot exceed 15 characters.",
                nameof(preferredLanguage));
        }

        try
        {
            _ = CultureInfo.GetCultureInfo(normalized);
        }
        catch (CultureNotFoundException exception)
        {
            throw new ArgumentException(
                "Preferred language must be a valid culture name such as 'tr' or 'en-US'.",
                nameof(preferredLanguage),
                exception);
        }

        return normalized;
    }
}

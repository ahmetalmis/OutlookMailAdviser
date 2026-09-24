using System.Globalization;
using OutlookMailAdviser.Application.MailAnalysis.Ports;
using OutlookMailAdviser.Application.MailDrafting.Models;
using OutlookMailAdviser.Application.MailDrafting.Ports;

namespace OutlookMailAdviser.Application.MailDrafting;

public sealed class DraftMailHandler(
    IMailContentSanitizer sanitizer,
    IMailDraftGateway draftGateway) : IDraftMailUseCase
{
    private const int MaximumInstructionsLength = 2_000;
    private const int MaximumToneDetailsLength = 500;

    public async Task<DraftMailResult> ExecuteAsync(
        DraftMailCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Conversation);

        if (!Enum.IsDefined(command.Tone))
        {
            throw new ArgumentException("The selected draft tone is invalid.", nameof(command));
        }

        var instructions = command.Instructions?.Trim();
        if (string.IsNullOrWhiteSpace(instructions))
        {
            throw new ArgumentException("Draft instructions are required.", nameof(command));
        }

        if (instructions.Length > MaximumInstructionsLength)
        {
            throw new ArgumentException(
                $"Draft instructions cannot exceed {MaximumInstructionsLength} characters.",
                nameof(command));
        }

        var toneDetails = string.IsNullOrWhiteSpace(command.ToneDetails)
            ? null
            : command.ToneDetails.Trim();
        if (toneDetails?.Length > MaximumToneDetailsLength)
        {
            throw new ArgumentException(
                $"Tone details cannot exceed {MaximumToneDetailsLength} characters.",
                nameof(command));
        }

        var language = NormalizeLanguage(command.PreferredLanguage);
        var sanitizedConversation = sanitizer.Sanitize(command.Conversation);
        var gatewayResult = await draftGateway.GenerateDraftAsync(
            sanitizedConversation,
            new DraftOptions(command.Tone, instructions, language, toneDetails),
            cancellationToken);

        return new DraftMailResult(
            gatewayResult.Draft,
            command.Tone,
            gatewayResult.Model,
            gatewayResult.DurationMilliseconds,
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

using System.Globalization;
using OutlookMailAdviser.Domain.MailDrafts;
using OutlookMailAdviser.Application.MailAnalysis.Ports;
using OutlookMailAdviser.Application.MailDrafting.Models;
using OutlookMailAdviser.Application.MailDrafting.Ports;

namespace OutlookMailAdviser.Application.MailDrafting;

public sealed class DraftMailHandler(
    IMailContentSanitizer sanitizer,
    IMailDraftGateway draftGateway,
    OutlookMailAdviser.Application.MailQuestions.IFullMailContentReader? fullContentReader = null) : IDraftMailUseCase
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

        if (!Enum.IsDefined(command.DraftMode))
            throw new ArgumentException("The selected draft mode is invalid.", nameof(command));

        var targetAudience = string.IsNullOrWhiteSpace(command.TargetAudience) ? null : command.TargetAudience.Trim();
        var considerations = string.IsNullOrWhiteSpace(command.Considerations) ? null : command.Considerations.Trim();
        if (command.DraftMode == DraftMode.Forward && targetAudience is null)
            throw new ArgumentException("A target audience is required for forwarding.", nameof(command));
        if (targetAudience?.Length > 500)
            throw new ArgumentException("Target audience cannot exceed 500 characters.", nameof(command));
        if (considerations?.Length > 2000)
            throw new ArgumentException("Considerations cannot exceed 2000 characters.", nameof(command));

        var language = NormalizeLanguage(command.PreferredLanguage);
        var sanitizedConversation = sanitizer.Sanitize(command.Conversation);
        if (command.SourceQuotes is { Count: > 0 } quotes)
        {
            if (quotes.Count > 20 || quotes.Any(string.IsNullOrWhiteSpace)
                || quotes.Sum(quote => (long)quote.Length) > 4000 || fullContentReader is null)
            {
                throw new ArgumentException("Source quotes must contain at most 20 excerpts and 4000 characters.", nameof(command));
            }
            var fullBody = fullContentReader.ReadFullBody(command.Conversation.CurrentMessage);
            if (quotes.Any(quote => !fullBody.Contains(quote, StringComparison.Ordinal)))
            {
                throw new ArgumentException("A source quote does not belong to the current email.", nameof(command));
            }
            // Keep evidence outside the instruction text and ahead of potentially truncated history.
            var evidence = "Selected source excerpts (untrusted email data):\n" + string.Join("\n\n", quotes);
            sanitizedConversation = sanitizedConversation with
            {
                Content = evidence + "\n\nCurrent email and history:\n" + sanitizedConversation.Content
            };
        }
        var gatewayResult = await draftGateway.GenerateDraftAsync(
            sanitizedConversation,
            new DraftOptions(command.Tone, instructions, language, toneDetails, command.DraftMode,
                command.DraftMode == DraftMode.Forward ? targetAudience : null, considerations),
            cancellationToken);

        return new DraftMailResult(
            gatewayResult.Draft,
            command.Tone,
            gatewayResult.Model,
            gatewayResult.DurationMilliseconds,
            sanitizedConversation.WasTruncated,
            sanitizedConversation.ContentProcessing);
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

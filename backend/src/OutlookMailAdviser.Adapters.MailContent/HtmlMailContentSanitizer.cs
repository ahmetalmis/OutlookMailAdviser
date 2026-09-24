using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OutlookMailAdviser.Adapters.MailContent.Configuration;
using OutlookMailAdviser.Application.MailAnalysis.Models;
using OutlookMailAdviser.Application.MailAnalysis.Ports;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Adapters.MailContent;

public sealed partial class HtmlMailContentSanitizer(
    IOptionsMonitor<MailContentOptions> optionsMonitor) : IMailContentSanitizer
{
    private const string TruncationWarning =
        "Mail content exceeded the local processing limit and was truncated.";

    public SanitizedConversation Sanitize(MailConversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        var currentMessage = conversation.CurrentMessage;
        var contentBuilder = new StringBuilder();

        foreach (var message in conversation.Messages.Reverse())
        {
            if (contentBuilder.Length > 0)
            {
                contentBuilder.AppendLine().AppendLine("--- Previous message ---").AppendLine();
            }

            AppendMessage(contentBuilder, message);
        }

        var content = contentBuilder.ToString().Trim();
        var maxInputCharacters = optionsMonitor.CurrentValue.MaxInputCharacters;
        var wasTruncated = content.Length > maxInputCharacters;

        if (wasTruncated)
        {
            content = content[..maxInputCharacters].TrimEnd();
        }

        return new SanitizedConversation(
            currentMessage.Subject,
            FormatParticipant(currentMessage.From),
            currentMessage.To.Select(FormatParticipant).ToArray(),
            currentMessage.Cc.Select(FormatParticipant).ToArray(),
            currentMessage.SentAt,
            content,
            wasTruncated,
            wasTruncated ? [TruncationWarning] : []);
    }

    private static void AppendMessage(StringBuilder builder, MailMessage message)
    {
        builder.Append("Subject: ").AppendLine(message.Subject);
        builder.Append("From: ").AppendLine(FormatParticipant(message.From));
        builder.Append("To: ").AppendLine(string.Join(", ", message.To.Select(FormatParticipant)));

        if (message.Cc.Count > 0)
        {
            builder.Append("Cc: ").AppendLine(string.Join(", ", message.Cc.Select(FormatParticipant)));
        }

        if (message.SentAt is not null)
        {
            builder.Append("Sent: ").AppendLine(message.SentAt.Value.ToString("O"));
        }

        builder.AppendLine().Append(CleanBody(message.Body, message.BodyFormat));
    }

    private static string CleanBody(string body, MailBodyFormat bodyFormat)
    {
        var text = bodyFormat == MailBodyFormat.Html
            ? CleanHtml(body)
            : body;

        text = WebUtility.HtmlDecode(text).Replace('\u00A0', ' ');
        text = HorizontalWhitespaceRegex().Replace(text, " ");
        text = LineWhitespaceRegex().Replace(text, "\n");
        text = ExcessBlankLinesRegex().Replace(text, "\n\n");

        return text.Trim();
    }

    private static string CleanHtml(string html)
    {
        var text = ScriptAndStyleRegex().Replace(html, string.Empty);
        text = HtmlCommentRegex().Replace(text, string.Empty);
        text = BlockBreakRegex().Replace(text, "\n");
        return HtmlTagRegex().Replace(text, " ");
    }

    private static string FormatParticipant(MailParticipant participant) =>
        participant.Name is null
            ? participant.Address
            : $"{participant.Name} <{participant.Address}>";

    [GeneratedRegex(
        "<(?:script|style)\\b[^>]*>.*?</(?:script|style)\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptAndStyleRegex();

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlCommentRegex();

    [GeneratedRegex(
        "<(?:br\\s*/?|/p|/div|/li|/tr|/h[1-6])\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BlockBreakRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("[\\t\\f\\v ]+", RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalWhitespaceRegex();

    [GeneratedRegex(" *\\r?\\n *", RegexOptions.CultureInvariant)]
    private static partial Regex LineWhitespaceRegex();

    [GeneratedRegex("\\n{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex ExcessBlankLinesRegex();
}


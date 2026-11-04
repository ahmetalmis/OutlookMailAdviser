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
        "İçeriğin bir bölümü analize dahil edilemedi.";

    public SanitizedConversation Sanitize(MailConversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        var currentMessage = conversation.CurrentMessage;
        var settings = optionsMonitor.CurrentValue;
        var original = new StringBuilder();
        var blocks = new List<string>();
        var uncertain = false;
        foreach (var message in conversation.Messages.Reverse())
        {
            var raw = new StringBuilder();
            AppendMessage(raw, message);
            original.Append(raw);
            var cleaned = message.BodyFormat == MailBodyFormat.Html
                ? RemoveMarkedSignatures(message.Body)
                : message.Body;
            var htmlReply = Regex.IsMatch(cleaned, """\bid\s*=\s*["']divRplyFwdMsg["']""",
                RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            // Recognize Outlook's structural boundary before stripping HTML.
            cleaned = Regex.Replace(cleaned, """<div\b(?=[^>]*\bid\s*=\s*["']divRplyFwdMsg["'])[^>]*>""",
                "<br>", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            var body = CleanBody(cleaned, message.BodyFormat);
            // A blockquote may contain interleaved answers. Preserve it as a unit.
            var interleaved = Regex.IsMatch(cleaned, @"</blockquote>\s*<(?:p|div)\b",
                RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            var boundaries = ReplyHeaderRegex().Matches(body);
            var suspect = htmlReply || ReplyHintRegex().IsMatch(body) || cleaned.Contains("blockquote", StringComparison.OrdinalIgnoreCase);
            uncertain |= interleaved || (suspect && boundaries.Count == 0);
            var header = new StringBuilder();
            AppendMessage(header, message, includeBody: false);
            if (interleaved || boundaries.Count == 0)
            {
                blocks.Add(header.ToString() + body);
                continue;
            }

            blocks.Add(header.ToString() + body[..boundaries[0].Index].Trim());
            for (var i = 0; i < boundaries.Count; i++)
            {
                var end = i + 1 < boundaries.Count ? boundaries[i + 1].Index : body.Length;
                blocks.Add(body[boundaries[i].Index..end].Trim());
            }
        }

        // Exact normalized blocks only: never merge similar but different requests.
        var normalized = blocks.Select(RemovePlainSignature).ToList();
        var footers = normalized.Select(FindFooter).Where(x => x.Length >= 80)
            .GroupBy(x => x, StringComparer.Ordinal).Where(x => x.Count() > 1)
            .Select(x => x.Key).ToArray();
        var unique = normalized.Select(block =>
        {
            foreach (var footer in footers)
            {
                if (block.EndsWith(footer, StringComparison.Ordinal))
                {
                    return block[..^footer.Length].TrimEnd();
                }
            }
            return block;
        }).Distinct(StringComparer.Ordinal).ToList();
        var content = Cut(unique[0], settings.MaxInputCharacters);
        var currentTruncated = content.Length < unique[0].Length;
        var included = 0;
        var historySize = 0;
        var historyLimited = false;
        const string separator = "\n\n--- Previous message ---\n\n";
        foreach (var block in unique.Skip(1))
        {
            var budget = Math.Min(settings.MaxInputCharacters - content.Length,
                settings.MaxHistoryCharacters - historySize);
            if (included >= settings.MaxHistoryMessages || budget <= separator.Length)
            {
                historyLimited = true;
                break;
            }
            var part = Cut(block, budget - separator.Length);
            if (part.Length == 0) { historyLimited = true; break; }
            content += separator + part;
            historySize += separator.Length + part.Length;
            included++;
            historyLimited |= part.Length < block.Length;
        }
        var wasTruncated = currentTruncated || historyLimited;
        var processing = new ContentProcessing(original.Length, content.Length, included,
            historyLimited, currentTruncated, uncertain);

        return new SanitizedConversation(
            currentMessage.Subject,
            FormatParticipant(currentMessage.From),
            currentMessage.To.Select(FormatParticipant).ToArray(),
            currentMessage.Cc.Select(FormatParticipant).ToArray(),
            currentMessage.SentAt,
            content,
            wasTruncated,
            currentTruncated || (uncertain && wasTruncated) ? [TruncationWarning] : [],
            processing);
    }

    private static string Cut(string value, int budget)
    {
        if (value.Length <= budget) { return value; }
        if (budget <= 0) { return ""; }
        var length = budget;
        var newline = value.LastIndexOf('\n', budget - 1, budget);
        if (newline >= budget * 3 / 4) { length = newline; }
        // StringInfo protects surrogate pairs and combining character sequences.
        var starts = System.Globalization.StringInfo.ParseCombiningCharacters(value);
        var boundary = Array.BinarySearch(starts, length);
        if (boundary < 0) { length = starts[Math.Max(0, ~boundary - 1)]; }
        return value[..length].TrimEnd();
    }

    private static string RemovePlainSignature(string value)
    {
        var signature = value.IndexOf("\n--\n", StringComparison.Ordinal);
        return signature < 0 ? value.Trim() : value[..signature].Trim();
    }

    private static string FindFooter(string value)
    {
        var match = Regex.Match(value,
            @"(?im)^(?:This (?:email|e-mail|message) (?:is confidential|and any attachments)|Bu (?:e-posta|elektronik posta|ileti) (?:ve ekleri|gizlidir))",
            RegexOptions.None, TimeSpan.FromSeconds(1));
        return match.Success ? value[match.Index..] : "";
    }

    private static string RemoveMarkedSignatures(string html) =>
        Regex.Replace(html,
            """<(div|span)\b[^>]*(?:id|class)\s*=\s*["'](?:Signature|gmail_signature)["'][^>]*>(?:(?!<\1\b).)*?</\1>""",
            "", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));

    [GeneratedRegex(
        @"(?im)^(?:From|Kimden|Gönderen):[^\n]+\n\s*(?:(?:Sent|Gönderildi|Gönderilme tarihi|Tarih|Date|To|Kime|Cc|Bilgi):[^\n]*\n\s*){2,6}(?:Subject|Konu):[^\n]*(?:\n|$)")]
    private static partial Regex ReplyHeaderRegex();

    [GeneratedRegex(@"(?im)^(?:From|Kimden|Gönderen):|^-{2,}\s*(?:Original Message|Özgün İleti|Forwarded message)|^On .+wrote:")]
    private static partial Regex ReplyHintRegex();

    private static void AppendMessage(StringBuilder builder, MailMessage message, bool includeBody = true)
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

        builder.AppendLine();
        if (includeBody) { builder.Append(CleanBody(message.Body, message.BodyFormat)); }
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

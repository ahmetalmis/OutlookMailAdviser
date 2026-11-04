using System.Globalization;
using OutlookMailAdviser.Application.MailboxTriage.Models;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Application.MailboxTriage;

public static class MailboxHeaderFilter
{
    private static readonly string[] AutoSubjectPrefixes =
    [
        "automatic reply",
        "automatische antwort",
        "out of office",
        "otomatik yanıt",
        "otomatik cevap",
        "undeliverable",
        "delivery status notification",
        "teslim edilemedi"
    ];

    private static readonly string[] NoReplyTokens =
    [
        "noreply",
        "no-reply",
        "donotreply",
        "do-not-reply",
        "mailer-daemon",
        "newsletter",
        "notifications",
        "notification"
    ];

    public static bool ShouldDrop(MailboxPreviewMessage message, MailParticipant? currentUser)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.IsAutoSubmitted || IsAutoSubject(message.Subject))
        {
            return true;
        }

        if (IsNoReplyAddress(message.From.Address) || message.HasUnsubscribeHint)
        {
            return true;
        }

        if (currentUser is null || string.IsNullOrWhiteSpace(currentUser.Address))
        {
            return false;
        }

        if (message.To.Count == 0 && message.Cc.Count == 0)
        {
            return false;
        }

        if (ContainsUser(message.To, currentUser) || ContainsUser(message.Cc, currentUser))
        {
            return false;
        }

        return true;
    }

    public static bool IsAddressedTo(MailboxPreviewMessage message, MailParticipant currentUser)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(currentUser);
        return ContainsUser(message.To, currentUser) || ContainsUser(message.Cc, currentUser);
    }

    public static bool IsAddressedOnlyTo(MailboxPreviewMessage message, MailParticipant currentUser)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(currentUser);
        var recipients = message.To.Concat(message.Cc).ToArray();
        return recipients.Length > 0 && recipients.All(participant => ContainsUser([participant], currentUser));
    }

    private static bool IsAutoSubject(string subject)
    {
        var trimmed = subject.Trim();
        return AutoSubjectPrefixes.Any(prefix =>
            trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNoReplyAddress(string address)
    {
        var localPart = address.Split('@')[0];
        var compact = localPart.Replace(".", string.Empty, StringComparison.Ordinal);
        return NoReplyTokens.Any(token =>
            localPart.Contains(token, StringComparison.OrdinalIgnoreCase)
            || compact.Contains(token.Replace("-", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsUser(IReadOnlyList<MailParticipant> participants, MailParticipant currentUser)
    {
        var userName = Fold(currentUser.Name);
        var userAddress = currentUser.Address.Trim();
        var userLocal = LocalPart(userAddress);
        return participants.Any(participant =>
        {
            if (string.Equals(participant.Address, userAddress, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var participantLocal = LocalPart(participant.Address);
            if (userLocal.Length >= 3
                && string.Equals(participantLocal, userLocal, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var participantName = Fold(participant.Name);
            return userName.Length >= 5 && string.Equals(participantName, userName, StringComparison.Ordinal);
        });
    }

    private static string LocalPart(string address)
    {
        var at = address.IndexOf('@');
        return at > 0 ? address[..at] : string.Empty;
    }

    private static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lower = value.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR"));
        var folded = lower
            .Replace('ı', 'i')
            .Replace('ö', 'o')
            .Replace('ü', 'u')
            .Replace('ş', 's')
            .Replace('ğ', 'g')
            .Replace('ç', 'c');
        return string.Join(' ', folded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

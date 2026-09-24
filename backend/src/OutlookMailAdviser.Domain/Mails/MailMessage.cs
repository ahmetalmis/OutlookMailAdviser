namespace OutlookMailAdviser.Domain.Mails;

public sealed class MailMessage
{
    public MailMessage(
        string? itemId,
        string? subject,
        MailParticipant from,
        IEnumerable<MailParticipant> to,
        IEnumerable<MailParticipant>? cc,
        DateTimeOffset? sentAt,
        string body,
        MailBodyFormat bodyFormat)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("A mail body is required.", nameof(body));
        }

        var recipients = to.ToArray();
        if (recipients.Length == 0)
        {
            throw new ArgumentException("At least one recipient is required.", nameof(to));
        }

        ItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim();
        Subject = subject?.Trim() ?? string.Empty;
        From = from;
        To = recipients;
        Cc = cc?.ToArray() ?? [];
        SentAt = sentAt;
        Body = body;
        BodyFormat = bodyFormat;
    }

    public string? ItemId { get; }

    public string Subject { get; }

    public MailParticipant From { get; }

    public IReadOnlyList<MailParticipant> To { get; }

    public IReadOnlyList<MailParticipant> Cc { get; }

    public DateTimeOffset? SentAt { get; }

    public string Body { get; }

    public MailBodyFormat BodyFormat { get; }
}


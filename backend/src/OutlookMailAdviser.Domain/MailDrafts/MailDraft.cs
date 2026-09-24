namespace OutlookMailAdviser.Domain.MailDrafts;

public sealed class MailDraft
{
    public MailDraft(string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("A draft subject is required.", nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("A draft body is required.", nameof(body));
        }

        Subject = subject.Trim();
        Body = body.Trim();
    }

    public string Subject { get; }

    public string Body { get; }
}

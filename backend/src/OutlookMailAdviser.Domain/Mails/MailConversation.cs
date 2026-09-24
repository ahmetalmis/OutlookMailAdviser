namespace OutlookMailAdviser.Domain.Mails;

public sealed class MailConversation
{
    public MailConversation(IEnumerable<MailMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var messageList = messages.ToArray();
        if (messageList.Length == 0)
        {
            throw new ArgumentException("A conversation must contain at least one message.", nameof(messages));
        }

        Messages = messageList;
    }

    public IReadOnlyList<MailMessage> Messages { get; }

    public MailMessage CurrentMessage => Messages[^1];
}


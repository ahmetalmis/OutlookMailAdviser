namespace OutlookMailAdviser.Domain.Mails;

public sealed record MailParticipant
{
    public MailParticipant(string? name, string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("A participant address is required.", nameof(address));
        }

        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Address = address.Trim();
    }

    public string? Name { get; }

    public string Address { get; }
}


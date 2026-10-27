namespace OutlookMailAdviser.Domain.ActionNotes;

public sealed record ActionNote(Guid Id, string MailSubject, string Description, string? Owner,
    DateOnly? DueDate, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? CompletedAt)
{
    public bool IsCompleted => CompletedAt.HasValue;

    public static ActionNote Create(Guid id, string mailSubject, string description, string? owner,
        DateOnly? dueDate, DateTimeOffset now)
    {
        if (id == Guid.Empty) throw new ArgumentException("A non-empty action note ID is required.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(mailSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (mailSubject.Length > 1000 || description.Length > 8000 || owner?.Length > 1000)
            throw new ArgumentException("Action note fields exceed their length limits.");
        return new(id, mailSubject.Trim(), description.Trim(), string.IsNullOrWhiteSpace(owner) ? null : owner.Trim(),
            dueDate, now, now, null);
    }
}

namespace OutlookMailAdviser.Domain.Analysis;

public sealed record MailAction
{
    public MailAction(
        string description,
        string? owner,
        DateOnly? dueDate,
        double confidence,
        bool assignedToCurrentUser = false)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("An action description is required.", nameof(description));
        }

        if (confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidence),
                confidence,
                "Confidence must be between 0 and 1.");
        }

        Description = description.Trim();
        Owner = string.IsNullOrWhiteSpace(owner) ? null : owner.Trim();
        DueDate = dueDate;
        Confidence = confidence;
        AssignedToCurrentUser = assignedToCurrentUser;
    }

    public string Description { get; }

    public string? Owner { get; }

    public DateOnly? DueDate { get; }

    public double Confidence { get; }

    public bool AssignedToCurrentUser { get; }
}

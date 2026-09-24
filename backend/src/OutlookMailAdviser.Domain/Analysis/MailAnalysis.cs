namespace OutlookMailAdviser.Domain.Analysis;

public sealed class MailAnalysis
{
    public MailAnalysis(
        string summary,
        bool actionRequired,
        IEnumerable<MailAction> actions,
        PriorityLevel priority,
        MailSentiment sentiment,
        IEnumerable<string>? warnings = null)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("An analysis summary is required.", nameof(summary));
        }

        ArgumentNullException.ThrowIfNull(actions);

        Summary = summary.Trim();
        ActionRequired = actionRequired;
        Actions = actions.ToArray();
        Priority = priority;
        Sentiment = sentiment;
        Warnings = warnings?
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Select(warning => warning.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
    }

    public string Summary { get; }

    public bool ActionRequired { get; }

    public IReadOnlyList<MailAction> Actions { get; }

    public PriorityLevel Priority { get; }

    public MailSentiment Sentiment { get; }

    public IReadOnlyList<string> Warnings { get; }
}


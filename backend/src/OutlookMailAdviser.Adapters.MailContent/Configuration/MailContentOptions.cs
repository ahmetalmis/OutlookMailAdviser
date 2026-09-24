namespace OutlookMailAdviser.Adapters.MailContent.Configuration;

public sealed class MailContentOptions
{
    public const string SectionName = "MailProcessing";

    public int MaxInputCharacters { get; init; } = 50_000;
}


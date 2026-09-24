namespace OutlookMailAdviser.Adapters.MailContent.Configuration;

public sealed class MailContentOptions
{
    public const string SectionName = "MailProcessing";

    public int MaxInputCharacters { get; init; } = 16_000;
    public int MaxHistoryMessages { get; init; } = 2;
    public int MaxHistoryCharacters { get; init; } = 4_000;
}

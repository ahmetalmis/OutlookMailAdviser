namespace OutlookMailAdviser.Application.MailAnalysis.Models;

public sealed record ContentProcessing(
    int OriginalCharacters,
    int IncludedCharacters,
    int IncludedHistoryMessages,
    bool HistoryLimited,
    bool CurrentMessageTruncated,
    bool ParsingUncertain);

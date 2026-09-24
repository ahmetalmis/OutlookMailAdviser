namespace OutlookMailAdviser.Api.Features.MailAnalysis;

public sealed record AnalyzeMailResponse(
    Guid ClientRequestId,
    string Summary,
    bool ActionRequired,
    bool ActionRequiredFromCurrentUser,
    IReadOnlyList<MailActionResponse> Actions,
    string Priority,
    string Sentiment,
    IReadOnlyList<string> Warnings,
    string Model,
    long DurationMilliseconds,
    bool WasTruncated);

public sealed record MailActionResponse(
    string Description,
    string? Owner,
    DateOnly? DueDate,
    double Confidence,
    bool AssignedToCurrentUser);

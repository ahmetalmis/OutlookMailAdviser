namespace OutlookMailAdviser.Api.Features.MailDrafting;

public sealed record DraftMailResponse(
    Guid ClientRequestId,
    string Subject,
    string Body,
    string Tone,
    string Model,
    long DurationMilliseconds,
    bool WasTruncated);

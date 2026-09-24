namespace OutlookMailAdviser.Api.Features.MailAnalysis;

public sealed record AnalyzeMailRequest(
    Guid? ClientRequestId,
    string? PreferredLanguage,
    MailMessageRequest? Message,
    MailParticipantRequest? CurrentUser = null);

public sealed record MailMessageRequest(
    string? ItemId,
    string? Subject,
    MailParticipantRequest? From,
    IReadOnlyList<MailParticipantRequest>? To,
    IReadOnlyList<MailParticipantRequest>? Cc,
    DateTimeOffset? SentAt,
    string? Body,
    MailBodyFormatRequest BodyFormat);

public sealed record MailParticipantRequest(string? Name, string? Address);

public enum MailBodyFormatRequest
{
    PlainText = 0,
    Html = 1
}

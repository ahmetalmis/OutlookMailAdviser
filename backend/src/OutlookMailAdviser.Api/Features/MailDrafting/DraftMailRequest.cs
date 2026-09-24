using OutlookMailAdviser.Api.Features.MailAnalysis;
using OutlookMailAdviser.Domain.MailDrafts;

namespace OutlookMailAdviser.Api.Features.MailDrafting;

public sealed record DraftMailRequest(
    Guid? ClientRequestId,
    string? PreferredLanguage,
    DraftTone Tone,
    string? Instructions,
    MailMessageRequest? Message,
    string? ToneDetails = null);

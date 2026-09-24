using OutlookMailAdviser.Domain.MailDrafts;

namespace OutlookMailAdviser.Application.MailDrafting.Models;

public sealed record MailDraftGatewayResult(
    MailDraft Draft,
    string Model,
    long DurationMilliseconds);

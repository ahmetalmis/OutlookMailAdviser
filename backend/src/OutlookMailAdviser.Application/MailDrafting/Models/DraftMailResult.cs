using OutlookMailAdviser.Domain.MailDrafts;

namespace OutlookMailAdviser.Application.MailDrafting.Models;

public sealed record DraftMailResult(
    MailDraft Draft,
    DraftTone Tone,
    string Model,
    long DurationMilliseconds,
    bool WasTruncated);

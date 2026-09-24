using OutlookMailAdviser.Domain.MailDrafts;

namespace OutlookMailAdviser.Application.MailDrafting.Models;

public sealed record DraftOptions(
    DraftTone Tone,
    string Instructions,
    string PreferredLanguage,
    string? ToneDetails = null);

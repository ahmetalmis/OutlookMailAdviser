using DomainMailAnalysis = OutlookMailAdviser.Domain.Analysis.MailAnalysis;

namespace OutlookMailAdviser.Application.MailAnalysis.Models;

public sealed record MailIntelligenceResult(
    DomainMailAnalysis Analysis,
    string Model,
    long DurationMilliseconds);

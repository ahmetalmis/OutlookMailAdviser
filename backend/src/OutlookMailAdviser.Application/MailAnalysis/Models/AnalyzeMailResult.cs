using DomainMailAnalysis = OutlookMailAdviser.Domain.Analysis.MailAnalysis;

namespace OutlookMailAdviser.Application.MailAnalysis.Models;

public sealed record AnalyzeMailResult(
    DomainMailAnalysis Analysis,
    string Model,
    long DurationMilliseconds,
    bool WasTruncated,
    ContentProcessing? ContentProcessing = null);

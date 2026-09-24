namespace OutlookMailAdviser.Application.MailAnalysis.Exceptions;

public sealed class ModelNotFoundException(string model)
    : MailIntelligenceException(
        "model_not_found",
        $"The configured model '{model}' is not available.");

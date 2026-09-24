namespace OutlookMailAdviser.Application.MailAnalysis.Exceptions;

public sealed class ModelTimeoutException(Exception? innerException = null)
    : MailIntelligenceException(
        "model_timeout",
        "The local model did not complete within the configured timeout.",
        innerException);


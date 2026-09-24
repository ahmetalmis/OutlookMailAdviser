namespace OutlookMailAdviser.Application.MailAnalysis.Exceptions;

public sealed class InvalidModelResponseException(Exception? innerException = null)
    : MailIntelligenceException(
        "invalid_model_response",
        "The local model returned a response that does not match the analysis contract.",
        innerException);


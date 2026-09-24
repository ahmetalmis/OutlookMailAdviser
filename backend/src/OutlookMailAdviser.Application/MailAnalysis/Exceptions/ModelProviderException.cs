namespace OutlookMailAdviser.Application.MailAnalysis.Exceptions;

public sealed class ModelProviderException(
    string message,
    string code = "model_provider_error")
    : MailIntelligenceException(code, message);

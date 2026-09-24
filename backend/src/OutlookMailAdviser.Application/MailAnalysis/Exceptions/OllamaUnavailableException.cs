namespace OutlookMailAdviser.Application.MailAnalysis.Exceptions;

public sealed class OllamaUnavailableException(Exception? innerException = null)
    : MailIntelligenceException(
        "ollama_unavailable",
        "Ollama is not reachable on the configured local endpoint.",
        innerException);


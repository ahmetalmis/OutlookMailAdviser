namespace OutlookMailAdviser.Application.MailAnalysis.Exceptions;

public static class ProviderErrorDetails
{
    public static string NormalizeCode(string code) => code switch
    {
        "openai_authentication_failed" or "openai_access_denied" or "openai_quota_exceeded"
            or "openai_rate_limited" or "openai_context_limit" or "openai_output_limit"
            or "openai_unavailable" or "ollama_unavailable" or "model_timeout"
            or "model_not_found" or "invalid_model_response" => code,
        _ => "model_provider_error"
    };

    // Only these descriptions may leave the API; provider response bodies can contain secrets.
    public static string GetDetail(string code) => code switch
    {
        "openai_authentication_failed" => "OpenAI rejected the configured API key.",
        "openai_access_denied" => "The configured API key does not have permission to use this resource.",
        "openai_quota_exceeded" => "OpenAI quota or credit is exhausted. Check API billing and project limits.",
        "openai_rate_limited" => "OpenAI request or token rate limit was exceeded. Try again later.",
        "openai_context_limit" => "The input exceeds the model context token limit. Shorten the email or question.",
        "openai_output_limit" => "The response reached the output token limit. Increase the configured output limit.",
        "openai_unavailable" => "OpenAI is unreachable. Check the connection and provider configuration.",
        "ollama_unavailable" => "Ollama is unreachable. Check that the local model service is running.",
        "model_timeout" => "The AI provider did not respond in time. Try again later.",
        "model_not_found" => "The configured model is unavailable. Check the provider configuration.",
        "invalid_model_response" => "The model response is not in the expected format. Try again.",
        _ => "The AI provider could not complete the request. Check the provider configuration."
    };
}

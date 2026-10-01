using Microsoft.Extensions.Diagnostics.HealthChecks;
using OutlookMailAdviser.Application.MailAnalysis.Exceptions;

namespace OutlookMailAdviser.Api.Infrastructure;

internal static class ReadinessResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var failure = report.Entries.FirstOrDefault(entry => entry.Value.Status != HealthStatus.Healthy);
        var healthy = report.Status == HealthStatus.Healthy;
        var code = healthy ? null : failure.Value.Data?.GetValueOrDefault("code") as string;
        code ??= healthy ? null : failure.Key == "ollama" ? "ollama_unavailable" : "model_provider_error";
        if (code is not null) code = ProviderErrorDetails.NormalizeCode(code);
        // Descriptions, exception messages and arbitrary check data are intentionally not serialized.
        var entry = healthy ? report.Entries.FirstOrDefault() : failure;
        var provider = entry.Key switch { "openai" => "OpenAI", "ollama" => "Ollama", _ => null };
        var model = healthy ? entry.Value.Data?.GetValueOrDefault("model") as string : null;
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(new
        {
            state = healthy ? "healthy" : "unhealthy",
            code,
            detail = code is null ? null : ProviderErrorDetails.GetDetail(code),
            data = new { provider, model }
        }, context.RequestAborted);
    }
}

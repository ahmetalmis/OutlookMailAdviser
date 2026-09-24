using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OutlookMailAdviser.Application.MailAnalysis.Exceptions;

namespace OutlookMailAdviser.Api.Infrastructure;

internal sealed partial class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var error = MapException(exception);

        if (error.Status >= StatusCodes.Status500InternalServerError)
        {
            LogRequestFailed(
                logger,
                httpContext.TraceIdentifier,
                exception.GetType().Name,
                error.Code);
        }
        else
        {
            LogRequestRejected(
                logger,
                httpContext.TraceIdentifier,
                error.Code);
        }

        var problemDetails = new ProblemDetails
        {
            Status = error.Status,
            Title = error.Title,
            Detail = error.Detail,
            Type = $"https://localhost/problems/{error.Code}",
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["code"] = error.Code;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = error.Status;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static ApiError MapException(Exception exception) => exception switch
    {
        ApiValidationException or ArgumentException or BadHttpRequestException => new ApiError(
            StatusCodes.Status400BadRequest,
            "invalid_request",
            "The request is invalid.",
            exception.Message),
        OllamaUnavailableException => new ApiError(
            StatusCodes.Status503ServiceUnavailable,
            "ollama_unavailable",
            "Ollama is unavailable.",
            exception.Message),
        ModelNotFoundException => new ApiError(
            StatusCodes.Status503ServiceUnavailable,
            "model_not_found",
            "The configured model is unavailable.",
            "The configured model is unavailable. Check the provider configuration."),
        ModelTimeoutException => new ApiError(
            StatusCodes.Status504GatewayTimeout,
            "model_timeout",
            "The model timed out.",
            exception.Message),
        InvalidModelResponseException => new ApiError(
            StatusCodes.Status502BadGateway,
            "invalid_model_response",
            "The model response is invalid.",
            exception.Message),
        ModelProviderException providerException => new ApiError(
            StatusCodes.Status502BadGateway,
            providerException.Code,
            "The model provider returned an error.",
            ProviderDetail(providerException.Code)),
        _ => new ApiError(
            StatusCodes.Status500InternalServerError,
            "internal_error",
            "An unexpected error occurred.",
            "The request could not be completed.")
    };

    private static string ProviderDetail(string code) => code switch
    {
        "openai_authentication_failed" => "OpenAI rejected the configured API key.",
        "openai_rate_limited" => "OpenAI rate limit or quota was exceeded.",
        "openai_unavailable" => "OpenAI is unreachable. Check the connection and provider configuration.",
        _ => "The model provider could not complete the request. Check the provider configuration."
    };

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Request {TraceId} failed with {ExceptionType} and code {ErrorCode}.")]
    private static partial void LogRequestFailed(
        ILogger logger,
        string traceId,
        string exceptionType,
        string errorCode);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Request {TraceId} was rejected with code {ErrorCode}.")]
    private static partial void LogRequestRejected(
        ILogger logger,
        string traceId,
        string errorCode);

    private sealed record ApiError(int Status, string Code, string Title, string Detail);
}

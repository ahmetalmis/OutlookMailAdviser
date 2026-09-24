using OutlookMailAdviser.Api.Infrastructure;
using OutlookMailAdviser.Application.MailAnalysis;
using OutlookMailAdviser.Application.MailAnalysis.Models;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Api.Features.MailAnalysis;

public static class MailAnalysisEndpoints
{
    public static IEndpointRouteBuilder MapMailAnalysisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/mail")
            .WithTags("Mail Analysis");

        group.MapPost("/analysis", AnalyzeAsync)
            .WithName("AnalyzeMail")
            .Produces<AnalyzeMailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return endpoints;
    }

    private static async Task<IResult> AnalyzeAsync(
        AnalyzeMailRequest request,
        IAnalyzeMailUseCase useCase,
        CancellationToken cancellationToken)
    {
        var command = MapCommand(request);
        var result = await useCase.ExecuteAsync(command, cancellationToken);
        var response = MapResponse(request.ClientRequestId ?? Guid.NewGuid(), result);

        return Results.Ok(response);
    }

    private static AnalyzeMailCommand MapCommand(AnalyzeMailRequest request)
    {
        if (request.Message is null)
        {
            throw new ApiValidationException("The message field is required.");
        }

        var message = request.Message;
        if (message.From is null)
        {
            throw new ApiValidationException("The message.from field is required.");
        }

        if (message.To is null || message.To.Count == 0)
        {
            throw new ApiValidationException("At least one message.to participant is required.");
        }

        if (string.IsNullOrWhiteSpace(message.Body))
        {
            throw new ApiValidationException("The message.body field is required.");
        }

        try
        {
            var domainMessage = new MailMessage(
                message.ItemId,
                message.Subject,
                MapParticipant(message.From),
                message.To.Select(MapParticipant),
                message.Cc?.Select(MapParticipant),
                message.SentAt,
                message.Body,
                MapBodyFormat(message.BodyFormat));

            return new AnalyzeMailCommand(
                new MailConversation([domainMessage]),
                request.PreferredLanguage ?? "tr",
                request.CurrentUser is null ? null : MapParticipant(request.CurrentUser));
        }
        catch (ArgumentException exception)
        {
            throw new ApiValidationException(exception.Message);
        }
    }

    private static MailParticipant MapParticipant(MailParticipantRequest participant)
    {
        if (string.IsNullOrWhiteSpace(participant.Address))
        {
            throw new ApiValidationException("Each participant must have an address.");
        }

        return new MailParticipant(participant.Name, participant.Address);
    }

    private static MailBodyFormat MapBodyFormat(MailBodyFormatRequest bodyFormat) => bodyFormat switch
    {
        MailBodyFormatRequest.PlainText => MailBodyFormat.PlainText,
        MailBodyFormatRequest.Html => MailBodyFormat.Html,
        _ => throw new ApiValidationException("The message.bodyFormat value is invalid.")
    };

    private static AnalyzeMailResponse MapResponse(Guid clientRequestId, AnalyzeMailResult result) =>
        new(
            clientRequestId,
            result.Analysis.Summary,
            result.Analysis.ActionRequired,
            result.Analysis.Actions.Any(action => action.AssignedToCurrentUser),
            result.Analysis.Actions
                .Select(action => new MailActionResponse(
                    action.Description,
                    action.Owner,
                    action.DueDate,
                    action.Confidence,
                    action.AssignedToCurrentUser))
                .ToArray(),
            result.Analysis.Priority.ToString().ToLowerInvariant(),
            result.Analysis.Sentiment.ToString().ToLowerInvariant(),
            result.Analysis.Warnings,
            result.Model,
            result.DurationMilliseconds,
            result.WasTruncated,
            result.ContentProcessing);
}

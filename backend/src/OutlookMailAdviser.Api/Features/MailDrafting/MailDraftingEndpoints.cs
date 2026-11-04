using OutlookMailAdviser.Api.Features.MailAnalysis;
using OutlookMailAdviser.Api.Infrastructure;
using OutlookMailAdviser.Application.MailDrafting;
using OutlookMailAdviser.Application.MailDrafting.Models;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Api.Features.MailDrafting;

public static class MailDraftingEndpoints
{
    public static IEndpointRouteBuilder MapMailDraftingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/mail")
            .WithTags("Mail Drafting");

        group.MapPost("/draft", DraftAsync)
            .WithName("DraftMail")
            .Produces<DraftMailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return endpoints;
    }

    private static async Task<IResult> DraftAsync(
        DraftMailRequest request,
        IDraftMailUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(MapCommand(request), cancellationToken);
        return Results.Ok(new DraftMailResponse(
            request.ClientRequestId ?? Guid.NewGuid(),
            result.Draft.Subject,
            result.Draft.Body,
            result.Tone.ToString().ToLowerInvariant(),
            result.Model,
            result.DurationMilliseconds,
            result.WasTruncated,
            result.ContentProcessing));
    }

    private static DraftMailCommand MapCommand(DraftMailRequest request)
    {
        if (request.Message is null)
        {
            throw new ApiValidationException("The message field is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Instructions))
        {
            throw new ApiValidationException("The instructions field is required.");
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

            return new DraftMailCommand(
                new MailConversation([domainMessage]),
                request.Tone,
                request.Instructions,
                request.PreferredLanguage ?? "tr",
                request.ToneDetails);
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
}

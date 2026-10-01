using OutlookMailAdviser.Api.Features.MailAnalysis;
using OutlookMailAdviser.Api.Infrastructure;
using OutlookMailAdviser.Application.MailQuestions;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Api.Features.MailQuestions;

public sealed record AskMailQuestionRequest(MailMessageRequest? Message, string? Question,
    string? PreferredLanguage = "tr");

public static class MailQuestionEndpoints
{
    public static IEndpointRouteBuilder MapMailQuestionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/mail/questions", async (AskMailQuestionRequest request,
            IAskMailQuestionUseCase useCase, CancellationToken cancellationToken) =>
        {
            var message = request.Message;
            if (message?.From is null || message.To is null || message.To.Count == 0
                || string.IsNullOrWhiteSpace(message.Body) || string.IsNullOrWhiteSpace(request.Question)
                || !Enum.IsDefined(message.BodyFormat))
            {
                throw new ApiValidationException("A question and a valid message with body, sender and recipients are required.");
            }
            var domainMessage = new MailMessage(message.ItemId, message.Subject,
                MapParticipant(message.From),
                message.To.Select(MapParticipant),
                message.Cc?.Select(MapParticipant),
                message.SentAt, message.Body,
                message.BodyFormat == MailBodyFormatRequest.Html ? MailBodyFormat.Html : MailBodyFormat.PlainText);
            return Results.Ok(await useCase.ExecuteAsync(domainMessage, request.Question,
                request.PreferredLanguage ?? "tr", cancellationToken));
        })
            .WithTags("Mail Questions")
            .WithName("AskMailQuestion")
            .Produces<MailQuestionResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
        return endpoints;
    }
    private static MailParticipant MapParticipant(MailParticipantRequest? person)
    {
        if (person is null || string.IsNullOrWhiteSpace(person.Address))
        {
            throw new ApiValidationException("Each participant must have an address.");
        }
        return new MailParticipant(person.Name, person.Address);
    }
}

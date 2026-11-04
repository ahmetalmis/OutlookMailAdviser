using OutlookMailAdviser.Api.Features.MailAnalysis;
using OutlookMailAdviser.Api.Infrastructure;
using OutlookMailAdviser.Application.MailboxTriage;
using OutlookMailAdviser.Application.MailboxTriage.Models;
using OutlookMailAdviser.Domain.Mails;

namespace OutlookMailAdviser.Api.Features.MailboxTriage;

public static class MailboxTriageEndpoints
{
    public const int MaxMessages = 250;

    public static IEndpointRouteBuilder MapMailboxTriageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/mailbox")
            .WithTags("Mailbox Triage");

        group.MapGet("/folders", ListFolders)
            .WithName("ListOutlookFolders")
            .Produces<IReadOnlyList<OutlookMailFolder>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/inbox", ListInbox)
            .WithName("ListOutlookInbox")
            .Produces<IReadOnlyList<OutlookInboxItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/bodies", ReadBodies)
            .WithName("ReadOutlookBodies")
            .Produces<IReadOnlyList<OutlookBodyItem>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/open", OpenMessage)
            .WithName("OpenOutlookMessage")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/mark-read", MarkRead)
            .WithName("MarkOutlookMessageRead")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/reply-draft", SaveReplyDraft)
            .WithName("SaveOutlookReplyDraft")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/reply-send", SendReply)
            .WithName("SendOutlookReply")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/triage", TriageAsync)
            .WithName("TriageMailbox")
            .Produces<MailboxTriageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return endpoints;
    }

    private static IResult ListFolders(OutlookComMailbox mailbox) =>
        Results.Ok(mailbox.ListFolders());

    private static IResult ListInbox(DateTimeOffset? since, bool? unreadOnly, string[]? folder, OutlookComMailbox mailbox)
    {
        if (since is null)
        {
            throw new ApiValidationException("The since field is required.");
        }

        return Results.Ok(mailbox.List(since.Value, unreadOnly ?? true, folder));
    }

    private static IResult ReadBodies(OutlookBodiesRequest request, OutlookComMailbox mailbox)
    {
        if (request.ItemIds is null)
        {
            throw new ApiValidationException("The itemIds field is required.");
        }

        if (request.ItemIds.Count > 20)
        {
            throw new ApiValidationException("At most 20 message bodies can be read at once.");
        }

        return Results.Ok(mailbox.GetBodies(request.ItemIds));
    }

    private static IResult OpenMessage(OutlookOpenRequest request, OutlookComMailbox mailbox)
    {
        if (string.IsNullOrWhiteSpace(request.ItemId))
        {
            throw new ApiValidationException("The itemId field is required.");
        }

        mailbox.Display(request.ItemId);
        return Results.NoContent();
    }

    private static IResult MarkRead(OutlookOpenRequest request, OutlookComMailbox mailbox)
    {
        if (string.IsNullOrWhiteSpace(request.ItemId))
        {
            throw new ApiValidationException("The itemId field is required.");
        }

        mailbox.MarkRead(request.ItemId);
        return Results.NoContent();
    }

    private static IResult SaveReplyDraft(OutlookReplyDraftRequest request, OutlookComMailbox mailbox)
    {
        if (string.IsNullOrWhiteSpace(request.ItemId))
        {
            throw new ApiValidationException("The itemId field is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
        {
            throw new ApiValidationException("The subject and body fields are required.");
        }

        mailbox.SaveReply(request.ItemId, request.Subject, request.Body);
        return Results.NoContent();
    }

    private static IResult SendReply(OutlookReplyDraftRequest request, OutlookComMailbox mailbox)
    {
        if (string.IsNullOrWhiteSpace(request.ItemId))
        {
            throw new ApiValidationException("The itemId field is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
        {
            throw new ApiValidationException("The subject and body fields are required.");
        }

        var replyAll = request.ReplyAll == true;
        var to = ReadReplyRecipients(request.To);
        var cc = ReadReplyRecipients(request.Cc);
        if (replyAll && request.To is not null && to.Length == 0 && cc.Length == 0)
        {
            throw new ApiValidationException("En az bir alıcı seçin.");
        }

        mailbox.SendReply(request.ItemId, request.Subject, request.Body, replyAll, replyAll ? to : null, replyAll ? cc : null);
        return Results.NoContent();
    }

    private static async Task<IResult> TriageAsync(
        MailboxTriageRequest request,
        IMailboxTriageUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(MapCommand(request), cancellationToken);
        return Results.Ok(new MailboxTriageResponse(
            request.ClientRequestId ?? Guid.NewGuid(),
            result.ScannedCount,
            result.HeaderFilteredCount,
            result.ClassifiedCount,
            result.Model,
            result.DurationMilliseconds,
            result.ActionItems.Select(MapItem).ToArray(),
            result.ReviewItems.Select(MapItem).ToArray()));
    }

    private static MailboxTriageCommand MapCommand(MailboxTriageRequest request)
    {
        if (request.Messages is null)
        {
            throw new ApiValidationException("The messages field is required.");
        }

        if (request.Messages.Count > MaxMessages)
        {
            throw new ApiValidationException($"At most {MaxMessages} messages can be triaged at once.");
        }

        try
        {
            var messages = request.Messages.Select(MapMessage).ToArray();
            return new MailboxTriageCommand(
                request.PreferredLanguage ?? "tr",
                request.CurrentUser is null ? null : MapParticipant(request.CurrentUser),
                messages);
        }
        catch (ArgumentException exception)
        {
            throw new ApiValidationException(exception.Message);
        }
    }

    private static MailboxPreviewMessage MapMessage(MailboxTriageMessageRequest message)
    {
        if (string.IsNullOrWhiteSpace(message.ItemId))
        {
            throw new ApiValidationException("Each message must have an itemId.");
        }

        if (message.From is null)
        {
            throw new ApiValidationException("Each message must have a from participant.");
        }

        return new MailboxPreviewMessage(
            message.ItemId.Trim(),
            string.IsNullOrWhiteSpace(message.ConversationId) ? null : message.ConversationId.Trim(),
            string.IsNullOrWhiteSpace(message.Subject) ? "(Konu yok)" : message.Subject.Trim(),
            MapParticipant(message.From),
            message.To?.Select(MapParticipant).ToArray() ?? [],
            message.Cc?.Select(MapParticipant).ToArray() ?? [],
            message.ReceivedAt,
            message.Preview?.Trim() ?? string.Empty,
            message.HasUnsubscribeHint,
            message.IsAutoSubmitted);
    }

    private static OutlookParticipant[] ReadReplyRecipients(IReadOnlyList<MailParticipantRequest>? people)
    {
        if (people is null)
        {
            return [];
        }

        var selected = new List<OutlookParticipant>();
        foreach (var person in people)
        {
            var address = person.Address?.Trim();
            if (string.IsNullOrWhiteSpace(address) || !address.Contains('@'))
            {
                continue;
            }

            if (selected.Count == 50)
            {
                break;
            }

            var name = string.IsNullOrWhiteSpace(person.Name) ? null : person.Name.Trim();
            selected.Add(new OutlookParticipant(name, address));
        }

        return selected.ToArray();
    }

    private static MailParticipant MapParticipant(MailParticipantRequest participant)
    {
        if (string.IsNullOrWhiteSpace(participant.Address))
        {
            throw new ApiValidationException("Each participant must have an address.");
        }

        return new MailParticipant(participant.Name, participant.Address);
    }

    private static MailboxTriageItemResponse MapItem(MailboxTriageItem item) =>
        new(
            item.ItemId,
            item.Subject,
            item.FromName,
            item.FromAddress,
            item.ReceivedAt,
            item.Reason,
            item.Priority);
}

public sealed record OutlookBodiesRequest(IReadOnlyList<string>? ItemIds);

public sealed record OutlookOpenRequest(string? ItemId);

public sealed record OutlookReplyDraftRequest(
    string? ItemId,
    string? Subject,
    string? Body,
    bool? ReplyAll,
    IReadOnlyList<MailParticipantRequest>? To,
    IReadOnlyList<MailParticipantRequest>? Cc);

public sealed record MailboxTriageRequest(
    Guid? ClientRequestId,
    string? PreferredLanguage,
    MailParticipantRequest? CurrentUser,
    IReadOnlyList<MailboxTriageMessageRequest>? Messages);

public sealed record MailboxTriageMessageRequest(
    string? ItemId,
    string? ConversationId,
    string? Subject,
    MailParticipantRequest? From,
    IReadOnlyList<MailParticipantRequest>? To,
    IReadOnlyList<MailParticipantRequest>? Cc,
    DateTimeOffset? ReceivedAt,
    string? Preview,
    bool HasUnsubscribeHint,
    bool IsAutoSubmitted);

public sealed record MailboxTriageResponse(
    Guid ClientRequestId,
    int ScannedCount,
    int HeaderFilteredCount,
    int ClassifiedCount,
    string Model,
    long DurationMilliseconds,
    IReadOnlyList<MailboxTriageItemResponse> ActionItems,
    IReadOnlyList<MailboxTriageItemResponse> ReviewItems);

public sealed record MailboxTriageItemResponse(
    string ItemId,
    string Subject,
    string? FromName,
    string FromAddress,
    DateTimeOffset? ReceivedAt,
    string Reason,
    string Priority);

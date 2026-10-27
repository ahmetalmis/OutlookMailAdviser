using OutlookMailAdviser.Application.ActionNotes;
using OutlookMailAdviser.Api.Infrastructure;
using OutlookMailAdviser.Domain.ActionNotes;

namespace OutlookMailAdviser.Api.Features.ActionNotes;

public sealed record SaveActionNoteRequest(Guid Id, string MailSubject, string Description, string? Owner, DateOnly? DueDate);
public sealed record EditActionNoteRequest(string MailSubject, string Description, string? Owner, DateOnly? DueDate);
public sealed record CompleteActionNoteRequest(bool? Completed);

public static class ActionNoteEndpoints
{
    public static IEndpointRouteBuilder MapActionNoteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/action-notes").WithTags("Action Notes");
        group.MapGet("/", (ActionNoteService service, CancellationToken cancellationToken) => service.ListAsync(cancellationToken))
            .Produces<IReadOnlyList<ActionNote>>();
        group.MapPost("/", async (SaveActionNoteRequest request, ActionNoteService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.CreateAsync(request.Id, request.MailSubject, request.Description, request.Owner,
                request.DueDate, cancellationToken))).Produces<ActionNote>();
        group.MapPut("/{id:guid}", async (Guid id, EditActionNoteRequest request, ActionNoteService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.EditAsync(id, request.MailSubject, request.Description, request.Owner,
                request.DueDate, cancellationToken))).Produces<ActionNote>();
        group.MapPatch("/{id:guid}/completion", async (Guid id, CompleteActionNoteRequest request, ActionNoteService service, CancellationToken cancellationToken) =>
        {
            if (!request.Completed.HasValue) throw new ApiValidationException("A completion value is required.");
            return Results.Ok(await service.CompleteAsync(id, request.Completed.Value, cancellationToken));
        }).Produces<ActionNote>();
        return endpoints;
    }
}

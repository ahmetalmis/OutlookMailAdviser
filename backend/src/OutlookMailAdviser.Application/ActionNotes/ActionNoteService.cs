using OutlookMailAdviser.Domain.ActionNotes;

namespace OutlookMailAdviser.Application.ActionNotes;

public sealed class ActionNoteService(IActionNoteRepository repository)
{
    public Task<IReadOnlyList<ActionNote>> ListAsync(CancellationToken cancellationToken) => repository.ListAsync(cancellationToken);

    public Task<ActionNote> CreateAsync(Guid id, string mailSubject, string description, string? owner,
        DateOnly? dueDate, CancellationToken cancellationToken) => repository.CreateAsync(
            ActionNote.Create(id, mailSubject, description, owner, dueDate, DateTimeOffset.UtcNow), cancellationToken);

    public async Task<ActionNote> EditAsync(Guid id, string mailSubject, string description, string? owner,
        DateOnly? dueDate, CancellationToken cancellationToken)
    {
        var edited = ActionNote.Create(id, mailSubject, description, owner, dueDate, DateTimeOffset.UtcNow);
        return await repository.UpdateAsync(id, current => edited with
        {
            CreatedAt = current.CreatedAt,
            CompletedAt = current.CompletedAt
        }, cancellationToken) ?? throw new ActionNoteNotFoundException();
    }

    public async Task<ActionNote> CompleteAsync(Guid id, bool completed, CancellationToken cancellationToken) =>
        await repository.UpdateAsync(id, current => current.IsCompleted == completed ? current : current with
        {
            CompletedAt = completed ? DateTimeOffset.UtcNow : null,
            UpdatedAt = DateTimeOffset.UtcNow
        }, cancellationToken) ?? throw new ActionNoteNotFoundException();
}

public sealed class ActionNoteNotFoundException() : Exception("The action note was not found.");
public sealed class ActionNoteConflictException() : Exception("This ID already belongs to a different action note.");
public sealed class ActionNoteStorageException(Exception innerException)
    : Exception("The action note store could not be read or written.", innerException);

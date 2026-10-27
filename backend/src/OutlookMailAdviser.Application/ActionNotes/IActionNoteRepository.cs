using OutlookMailAdviser.Domain.ActionNotes;

namespace OutlookMailAdviser.Application.ActionNotes;

public interface IActionNoteRepository
{
    Task<IReadOnlyList<ActionNote>> ListAsync(CancellationToken cancellationToken);
    Task<ActionNote> CreateAsync(ActionNote note, CancellationToken cancellationToken);
    Task<ActionNote?> UpdateAsync(Guid id, Func<ActionNote, ActionNote> update, CancellationToken cancellationToken);
}

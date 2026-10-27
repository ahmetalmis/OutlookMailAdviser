using System.Text.Json;
using OutlookMailAdviser.Application.ActionNotes;
using OutlookMailAdviser.Domain.ActionNotes;

namespace OutlookMailAdviser.Adapters.ActionNotes;

// Register one instance per service process. Each mutation reads the latest file under the same lock.
public sealed class JsonActionNoteRepository(string dataDirectory) : IActionNoteRepository, IDisposable
{
    private readonly string filePath = Path.Combine(Path.GetFullPath(dataDirectory), "action-notes.json");
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public Task<IReadOnlyList<ActionNote>> ListAsync(CancellationToken cancellationToken) =>
        LockedAsync<IReadOnlyList<ActionNote>>(async () =>
            (await ReadAsync(cancellationToken)).OrderByDescending(note => note.CreatedAt).ThenBy(note => note.Id).ToArray(), cancellationToken);

    public Task<ActionNote> CreateAsync(ActionNote note, CancellationToken cancellationToken) => LockedAsync(async () =>
    {
        var notes = await ReadAsync(cancellationToken);
        var existing = notes.Find(current => current.Id == note.Id);
        if (existing is not null)
        {
            if (existing.MailSubject != note.MailSubject || existing.Description != note.Description
                || existing.Owner != note.Owner || existing.DueDate != note.DueDate) throw new ActionNoteConflictException();
            return existing;
        }
        notes.Add(note);
        await WriteAsync(notes, cancellationToken);
        return note;
    }, cancellationToken);

    public Task<ActionNote?> UpdateAsync(Guid id, Func<ActionNote, ActionNote> update, CancellationToken cancellationToken) =>
        LockedAsync<ActionNote?>(async () =>
        {
            var notes = await ReadAsync(cancellationToken);
            var index = notes.FindIndex(note => note.Id == id);
            if (index < 0) return null;
            var note = update(notes[index]);
            notes[index] = note;
            await WriteAsync(notes, cancellationToken);
            return note;
        }, cancellationToken);

    private async Task<T> LockedAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try { return await operation(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        { throw new ActionNoteStorageException(exception); }
        finally { gate.Release(); }
    }

    private async Task<List<ActionNote>> ReadAsync(CancellationToken cancellationToken)
    {
        // Only absence is an empty store. Invalid or unreadable files must never be replaced with an empty list.
        FileStream stream;
        try { stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read); }
        catch (FileNotFoundException) { return []; }
        catch (DirectoryNotFoundException) { return []; }
        await using (stream)
        {
            var document = await JsonSerializer.DeserializeAsync<StoreDocument>(stream, JsonOptions, cancellationToken);
            if (document is null || document.Version != 1 || document.Notes is null
                || document.Notes.Any(note => note is null || note.Id == Guid.Empty
                    || string.IsNullOrWhiteSpace(note.MailSubject) || string.IsNullOrWhiteSpace(note.Description)
                    || note.CreatedAt == default || note.UpdatedAt == default)
                || document.Notes.Select(note => note.Id).Distinct().Count() != document.Notes.Count)
                throw new JsonException("Invalid action note store.");
            return document.Notes;
        }
    }

    private async Task WriteAsync(List<ActionNote> notes, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".action-notes-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, new StoreDocument(1, notes), JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            // Failure to clean a temporary file must not hide the original storage error.
            try { File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public void Dispose() => gate.Dispose();
    private sealed record StoreDocument(int Version, List<ActionNote>? Notes);
}

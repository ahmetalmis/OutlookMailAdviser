using OutlookMailAdviser.Adapters.ActionNotes;
using OutlookMailAdviser.Application.ActionNotes;
using OutlookMailAdviser.Domain.ActionNotes;

namespace OutlookMailAdviser.Adapters.Tests;

public sealed class ActionNoteRepositoryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "OutlookMailAdviser-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ConcurrentCreatesAreIdempotentAndSurviveReopeningTheStore()
    {
        using var repository = new JsonActionNoteRepository(directory);
        var note = NewNote();
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => repository.CreateAsync(note, CancellationToken.None)));
        var otherNotes = Enumerable.Range(0, 12).Select(_ => NewNote()).ToArray();
        await Task.WhenAll(otherNotes.Select(other => repository.CreateAsync(other, CancellationToken.None)));
        using var reopened = new JsonActionNoteRepository(directory);
        Assert.Equal(13, (await reopened.ListAsync(CancellationToken.None)).Count);
        var file = await File.ReadAllTextAsync(Path.Combine(directory, "action-notes.json"));
        Assert.DoesNotContain("email body", file, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public async Task EditingAndCompletionDoNotOverwriteEachOther()
    {
        using var repository = new JsonActionNoteRepository(directory);
        var service = new ActionNoteService(repository);
        var note = await service.CreateAsync(Guid.NewGuid(), " Subject ", " Action ", " ", new DateOnly(2026, 11, 2), CancellationToken.None);
        Assert.Null(note.Owner);
        await Task.WhenAll(service.CompleteAsync(note.Id, true, CancellationToken.None),
            service.EditAsync(note.Id, "Edited subject", "Edited action", "Alex", null, CancellationToken.None));
        var saved = Assert.Single(await repository.ListAsync(CancellationToken.None));
        Assert.True(saved.IsCompleted);
        Assert.Equal("Edited action", saved.Description);
        Assert.Equal(note.CreatedAt, saved.CreatedAt);
        var reopened = await service.CompleteAsync(note.Id, false, CancellationToken.None);
        Assert.False(reopened.IsCompleted);
        Assert.Null(reopened.CompletedAt);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"version\":2,\"notes\":[]}")]
    [InlineData("{\"version\":1,\"notes\":[{}]}")]
    public async Task CorruptOrUnsupportedFilesAreNeverSilentlyOverwritten(string content)
    {
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "action-notes.json");
        await File.WriteAllTextAsync(filePath, content);
        using var repository = new JsonActionNoteRepository(directory);
        await Assert.ThrowsAsync<ActionNoteStorageException>(() => repository.ListAsync(CancellationToken.None));
        await Assert.ThrowsAsync<ActionNoteStorageException>(() => repository.CreateAsync(NewNote(), CancellationToken.None));
        Assert.Equal(content, await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task AFailedAtomicReplacementPreservesThePreviousFile()
    {
        using var repository = new JsonActionNoteRepository(directory);
        var note = NewNote();
        await repository.CreateAsync(note, CancellationToken.None);
        var filePath = Path.Combine(directory, "action-notes.json");
        var original = await File.ReadAllBytesAsync(filePath);
        // A reader that disallows deletion prevents Windows from atomically replacing the destination.
        using (var locked = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsAsync<ActionNoteStorageException>(() => repository.CreateAsync(NewNote(), CancellationToken.None));
        Assert.Equal(original, await File.ReadAllBytesAsync(filePath));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public async Task ExistingIdsWithDifferentContentsFailRatherThanDuplicateOrReplace()
    {
        using var repository = new JsonActionNoteRepository(directory);
        var note = NewNote();
        await repository.CreateAsync(note, CancellationToken.None);
        await Assert.ThrowsAsync<ActionNoteConflictException>(() => repository.CreateAsync(note with { Description = "Different" }, CancellationToken.None));
        Assert.Equal(note, Assert.Single(await repository.ListAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task InvalidInputsAndMissingRecordsDoNotChangeTheStore()
    {
        using var repository = new JsonActionNoteRepository(directory);
        var service = new ActionNoteService(repository);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Guid.NewGuid(), " ", "Action", null, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Guid.Empty, "Subject", "Action", null, null, CancellationToken.None));
        await Assert.ThrowsAsync<ActionNoteNotFoundException>(() => service.CompleteAsync(Guid.NewGuid(), true, CancellationToken.None));
        Assert.Empty(await repository.ListAsync(CancellationToken.None));
    }

    private static ActionNote NewNote() => ActionNote.Create(Guid.NewGuid(), "Subject", "Action", null, null, DateTimeOffset.UtcNow);
    public void Dispose()
    {
        var resolved = Path.GetFullPath(directory);
        var testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OutlookMailAdviser-tests")) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(testRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe test directory.");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
    }
}

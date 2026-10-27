using System.Net;
using System.Net.Http.Json;
using OutlookMailAdviser.Domain.ActionNotes;

namespace OutlookMailAdviser.Api.IntegrationTests;

public sealed class ActionNoteEndpointTests
{
    [Fact]
    public async Task SavesEditsCompletesAndReopensNotesWithoutCallingAi()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();
        var input = new { id, mailSubject = "Mail subject", description = "Confirm the plan", owner = (string?)null, dueDate = "2026-11-02" };
        (await client.PostAsJsonAsync("/api/v1/action-notes", input)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/v1/action-notes", input)).EnsureSuccessStatusCode();
        Assert.Single((await client.GetFromJsonAsync<ActionNote[]>("/api/v1/action-notes"))!);
        (await client.PatchAsJsonAsync($"/api/v1/action-notes/{id}/completion", new { completed = true })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/v1/action-notes/{id}", new { mailSubject = "Edited", description = "Updated action", owner = "Alex", dueDate = (string?)null })).EnsureSuccessStatusCode();
        var completed = Assert.Single((await client.GetFromJsonAsync<ActionNote[]>("/api/v1/action-notes"))!);
        Assert.True(completed.IsCompleted);
        Assert.Equal("Updated action", completed.Description);
        (await client.PatchAsJsonAsync($"/api/v1/action-notes/{id}/completion", new { completed = false })).EnsureSuccessStatusCode();
        Assert.False(Assert.Single((await client.GetFromJsonAsync<ActionNote[]>("/api/v1/action-notes"))!).IsCompleted);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/action-notes", new { id, mailSubject = "Different", description = "Action" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/v1/action-notes/{Guid.NewGuid()}/completion", new { completed = true })).StatusCode);
    }

    [Fact]
    public async Task RejectsInvalidFieldsAndMissingCompletionValue()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/action-notes", new { id = Guid.NewGuid(), mailSubject = " ", description = "Action" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/v1/action-notes/{Guid.NewGuid()}/completion", new { })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<ActionNote[]>("/api/v1/action-notes"))!);
    }
}

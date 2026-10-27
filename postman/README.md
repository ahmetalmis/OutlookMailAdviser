# Postman Setup

This folder contains an importable Postman collection and local environment for mail analysis and reply drafting.

## Files

- `Outlook-Mail-Adviser.postman_collection.json` — API requests and assertions.
- `Outlook-Mail-Adviser.local.postman_environment.json` — local `baseUrl` and language variables.

Neither file contains an OpenAI API key. Postman calls the Outlook Mail Adviser backend; the backend calls the selected AI provider.

## Import into Postman

1. Open Postman and select **Import**.
2. Import `Outlook-Mail-Adviser.postman_collection.json`.
3. Import `Outlook-Mail-Adviser.local.postman_environment.json`.
4. Select **Outlook Mail Adviser - Local** in Postman's environment selector.

## Start the backend

Configure Ollama or OpenAI as described in [`../PROVIDER-CONFIGURATION.md`](../PROVIDER-CONFIGURATION.md), then start the API:

```powershell
cd C:\src\OutlookMailAdviser

dotnet run `
    --project backend/src/OutlookMailAdviser.Api `
    --launch-profile https
```

For OpenAI, `OPENAI_API_KEY` and `Ai__Provider=OpenAI` must be set in the same PowerShell window before `dotnet run`. Do not add the OpenAI key to Postman—the backend owns provider authentication.

## Send requests

Run these requests in order:

1. **Service / Service Info** — confirms the base URL.
2. **Health / Liveness** — confirms the API process is running.
3. **Health / Readiness** — confirms the selected provider and model are ready.
4. **Mail Analysis / Analyze Mail** — sends the sample email for analysis.
5. **Mail Drafting / Create Reply Draft** — creates a subject and reply body using `draftTone`, instructions, and the sample email.

The analysis request uses Postman's `{{$guid}}` dynamic variable for `clientRequestId` and includes a sample `currentUser`. The response exposes `actionRequiredFromCurrentUser` and `assignedToCurrentUser` for personal-action highlighting. After a successful response, the test script stores the returned model in the environment variable `lastModel`.

## Local HTTPS certificate

Trust the ASP.NET Core development certificate once:

```powershell
dotnet dev-certs https --trust
```

Restart Postman after trusting the certificate. If Postman still rejects localhost during development, open **Settings > General** and temporarily disable **SSL certificate verification**. Re-enable it for non-local APIs.

## Expected results

- `GET /health/live`: HTTP `200`, body contains `Healthy`.
- `GET /health/ready`: HTTP `200`, body contains `Healthy`.
- `POST /api/v1/mail/analysis`: HTTP `200`, JSON analysis contract.
- `POST /api/v1/mail/draft`: HTTP `200`, JSON draft contract containing `subject`, `body`, `tone`, and `model`.

If readiness fails, fix the selected provider before running analysis. For OpenAI, check `OPENAI_API_KEY`, account quota, model access, and `Ai__Provider`. For Ollama, check that Ollama is running and the configured model is installed.
# Context processing

Analysis and draft responses include `contentProcessing`:
`originalCharacters`, `includedCharacters`, `includedHistoryMessages`,
`historyLimited`, `currentMessageTruncated`, `parsingUncertain`.
Default maximums are 16,000 total characters, two previous messages and
4,000 history characters. Signature/duplicate cleanup alone does not set
`wasTruncated`; omitted message content due to limits does.

For a manual chain test, append three blocks to the HTML body, each containing
`From:`, `Sent:`, `To:`, `Subject:` on separate lines followed by unique
message text. Expect two included history messages, `historyLimited=true`,
and `currentMessageTruncated=false` for a short current message.

## 1.0.0: Questions about the current correspondence

**Mail Questions / Ask Current Correspondence** examines the entire supplied body, including quoted
history. A 200 response can be partial: inspect `isPartial`, `completedChunks`, `totalChunks`, and
`warnings` before interpreting absence of evidence. **Draft With Source Quotes** demonstrates the
optional `sourceQuotes` array; changing a quote to text absent from the body returns 400.

### Audience-specific drafts

Add the following optional fields to the existing `POST /api/v1/mail/draft` request:

```json
{
  "draftMode": "forward",
  "targetAudience": "My department head and operations manager",
  "considerations": "Avoid personal blame and date commitments."
}
```

If `draftMode` is omitted, `reply` is used. `forward` requires `targetAudience` (up to 500 characters); `considerations` is optional (up to 2,000 characters). Send these fields with the existing `message`, `tone` and `instructions` fields. The response schema is unchanged; no email is automatically forwarded or sent.

The environment language variable sets `preferredLanguage` for model output (`tr` or `en`). The Outlook panel's interface language is a separate local preference and does not change the HTTP API contract.

## Local action-note API

These endpoints use the local backend and do not call the AI provider:

- `GET /api/v1/action-notes` returns all notes, including completed notes, newest first.
- `POST /api/v1/action-notes` saves `{ "id": "<client-generated UUID>", "mailSubject": "...", "description": "...", "owner": null, "dueDate": "2026-11-02" }`.
- `PUT /api/v1/action-notes/{id}` edits the same fields except `id`, preserving completion state.
- `PATCH /api/v1/action-notes/{id}/completion` accepts `{ "completed": true }` or `{ "completed": false }`.

Successful operations return HTTP 200. Reusing an ID with the same fields returns the existing note;
reusing it with different fields returns 409. Empty subject/action fields return 400; missing notes return
404. Local storage failures return 503 with `action_note_storage_error`; invalid stores are never silently
reset. Responses include `createdAt`, `updatedAt`, nullable `completedAt` and `isCompleted`. `owner` and
`dueDate` are optional; dates use `YYYY-MM-DD`. Subject and owner are limited to 1,000 characters each,
and action text to 8,000 characters. Do not include email bodies in these requests.

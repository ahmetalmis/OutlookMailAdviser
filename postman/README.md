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
cd C:\Users\aalmis\Documents\ChatGPT\Outlook_Mail_Adviser

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

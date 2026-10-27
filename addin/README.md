# Outlook Add-in

This folder contains the React + TypeScript task pane that reads the open Outlook email and sends it to the existing .NET API. The add-in requests only `ReadItem` permission; it does not modify the email or send a reply.

## Daily use on this computer

The `OutlookMailAdviser` Windows service serves both the interface and API at
`https://localhost:7047/addin/index.html` and starts automatically with Windows.
You do not need Vite or a terminal. Install `manifest.xml` or its identical copy,
`manifest.localhost.xml`, in Outlook. Development uses `manifest.dev.xml` with a separate identity;
closing port 3000 does not affect the daily-use installation.

The API status in the panel explains rejected keys/tokens, access restrictions, quota, rate limits
and connection failures. After fixing the key in the service configuration, choose **Check again**.
Analysis, draft and question operations also report input/output token limits and provider failures.
API keys and raw provider error text are not displayed. These messages appear after the panel loads;
Outlook failing to launch the panel is a separate Office error.

See the [Windows service guide](../docs/windows-service.md) for installation and updates.
Linked detailed guides and existing screenshots may still be in Turkish.

## Development environment (optional)

Node.js 22.12 or later is required. In PowerShell:

```powershell
cd addin
npm ci
npm run certs
npm run icons
```

`npm run certs` exports the .NET development certificate to `addin/.certs/localhost.pfx` for the Outlook task pane server. Accept the Windows trust prompt if shown. The certificate and password are for local development only; `.certs` is excluded from Git.

## Run locally

Open two terminals.

Terminal 1 — select the AI provider and start the API:

```powershell
# Local Ollama alternative (the default provider is OpenAI):
$env:Ai__Provider = 'Ollama'

dotnet run --project backend/src/OutlookMailAdviser.Api --launch-profile https
```

Terminal 2 — start the task pane web application:

```powershell
cd addin
npm run dev
```

The default API URL is `https://localhost:7047`. To change it, create `addin/.env.local`:

```text
VITE_API_BASE_URL=https://localhost:7047
```

## Sideload into Outlook

1. Open `https://aka.ms/olksideload` in a browser.
2. Under **My add-ins**, find **Custom Addins**.
3. Choose **Add from File** and upload `addin/manifest.dev.xml`.
4. Open an email in read mode in Outlook.
5. In new Outlook or Outlook on the web, choose **Apps → Outlook Mail Adviser → Analyze email**. In classic Outlook the button appears in the ribbon. Ribbon labels follow the Outlook display language.

The task pane displays the selected email's subject and sender. **Interface language** changes the panel between English and Turkish. Initially, it follows the Outlook display language, or the browser language if Outlook does not provide one; non-Turkish languages use English. Manual choices are remembered locally when storage is available. Changing the interface language preserves results, entered text and selected source quotes and does not make a model request.

**Output language (analysis, answers and drafts)** is a separate setting. It defaults to Turkish and remembers the existing preference. Choose **Analyze this email** to send the content to the .NET API using the selected output language and display the structured result. Model-generated text and source quotes retain their original language when you change the interface language.

The analysis request includes the active Outlook user's display name and SMTP address as `currentUser`. The model marks explicit name/email assignments and clear requests directed at the user as personal actions. If present, the result shows **Action required from you** and the relevant row shows **Assigned to you**. Group assignments and uncertain ownership are not highlighted. If the Outlook user profile cannot be read, analysis still works without personal-action highlighting.

After analysis, the **Compose email** section appears:

1. Select **Reply to sender** or **Forward to others** as the **Draft type**.
2. For forwarding, enter names or roles in **Who is the audience?** (up to 500 characters); multiple recipients are allowed. Email addresses are not required.
3. Select a tone and optionally add up to 500 characters of **Tone details**.
4. Describe what you want to communicate in **Purpose / main message**.
5. Optionally enter email-specific rules in **Considerations**, such as “Avoid personal blame and date commitments” (up to 2,000 characters).
6. Choose **Create draft**. Use the subject separately; **Copy** copies only the body.

In forward mode, the original email is source material and the draft addresses your chosen audience.
For example, for feedback from Dilek, enter “My department head” to draft a request for management action.
Moving to a new email resets the draft type and clears the audience, main message and considerations.
Considerations are not stored persistently.

The draft uses the selected output language. This POC does not automatically modify Outlook messages or send email; review and copy the draft yourself.

## Verification

Analysis and draft context contains the current email and at most two previous messages.
Limits are 16,000 total characters and 4,000 history characters. Normal history reduction appears
as an informational notice. Truncation of the current message or unparsed text produces a yellow
warning. No separate Outlook requests fetch older messages.

```powershell
cd addin
npm test
npm run build
npm run validate
npm run validate:dev
npm run validate:local-host
```

If API status is red, first open `https://localhost:7047/health/ready` in a browser. If a certificate warning appears, trust the local .NET development certificate:

```powershell
dotnet dev-certs https --trust
```

API CORS allows the `https://localhost:3000` origin. If you change the port, also update `Cors:AllowedOrigins` in `backend/src/OutlookMailAdviser.Api/appsettings.Development.json`.

## Persistent local installation

The development manifest, `manifest.dev.xml`, uses the Vite server at
`https://localhost:3000`. The persistent installation starts with Windows and uses
`manifest.localhost.xml`, serving the add-in and API from one origin:

```text
https://localhost:7047/addin/index.html
```

From the repository root, follow **Windows'ta sürekli çalıştırma** (persistent operation on Windows)
in the [detailed development guide](../docs/DEVELOPMENT.md). After installation, sideload
`manifest.xml` or `manifest.localhost.xml` in Outlook. The development add-in has a separate identity;
choose **Outlook Mail Adviser** for daily use.

## 1.0.0: Questions about the conversation

**Ask about this conversation** appears below the open email. You do not need to analyze first.
The entire history quoted in the body is examined. Select answer sources and choose **Use in draft**
to pass them to the composer. Cancellation and changing emails invalidate pending question results.
An activity indicator appears during processing; the result shows the number of sections examined.

## Translation maintenance

Panel strings are defined in `src/i18n/messages.ts`; English keys are checked against the Turkish dictionary by TypeScript. `LanguageContext` handles detection, persistence and locale-aware formatting. Keep UI messages as codes until rendering so an interface-language change updates existing notices. Keep `preferredLanguage` exclusively for model output. Localize ribbon descriptions and strings in all three manifests together; the standard and localhost manifests must remain identical.


## Local action tracking

After analyzing an email, choose **Track action** next to an action. Review or edit the email subject,
action text, owner and optional due date, then choose **Save**. A missing owner is allowed and appears
as **Owner not specified**. Only the saved note fields are retained; the email body is not stored.

The **My actions** button at the top of the panel shows the active count and opens saved actions from
all emails. Search by subject/action, filter by owner, or edit a note. **Mark completed** moves a note
out of the active list; use the **Completed** view and **Reopen** to bring it back. Returning to the
email preserves the current analysis and draft fields. There are no automatic notifications or messages;
use the list to give reminders yourself during meetings.

Notes are stored on this computer, independently of browser storage and the AI provider. The Windows
service uses `C:\ProgramData\OutlookMailAdviser\data\action-notes.json`; development uses
`.artifacts/action-data/action-notes.json` in the repository. These files contain saved email subjects
and action details: keep them private. Service updates preserve the data folder and back up the store
while the service is stopped. To back up manually, stop the service before copying the file.

This is a single-user local list, separate from Outlook Notes, Outlook Tasks and Microsoft To Do.
It does not synchronize across devices. Both English and Turkish interfaces are supported; saved
note content keeps the language you entered. See the [Windows service guide](../docs/windows-service.md) for update details.

# Security and privacy

This is a local, single-user proof of concept, not an internet-facing service.
Only the latest source revision is maintained. Do not expose its API or Vite
server to the internet; CORS is a browser policy, not authentication.

## Reporting a vulnerability

Use GitHub's **Security → Report a vulnerability** on this repository:
https://github.com/ahmetalmis/OutlookMailAdviser/security/advisories/new

The repository owner must enable private vulnerability reporting before the
public release. If the private reporting option is unavailable, do not post
exploit details, personal messages, credentials or logs in a public issue.
An issue asking only for a private reporting channel is appropriate.

Include affected revision, reproduction using synthetic data, impact and a
suggested fix. Never include real API keys or real mailbox content.

## Data flow

Outlook (open message) → local ASP.NET API → configured AI provider → analysis
or draft → Outlook task pane. Processing starts when the user clicks the
analysis or draft button. Provider health checks may run independently.

The payload includes subject, sender/recipient names and addresses, timestamp,
message content and, for analysis, the current user's name/address when available.
Drafting also sends the user's reply instructions and tone. The API removes
some HTML/signatures/history and limits context; **this is not anonymization**.
Sensitive information may remain in both the input and the generated output.

OpenAI is the default provider: these fields leave the computer and an API key
belonging to the user is required; usage can incur provider charges. The adapter
sets `store: false`; this is not a promise of zero retention by the provider.
Review the provider's current terms before using real messages.
With the default local Ollama endpoint, inference runs on the user's computer.
A custom remote endpoint changes that boundary.

API keys belong in backend environment variables, never `VITE_*` variables,
screenshots, Postman exports or commits. The application has no mailbox-content
database. Normal API logs must not contain message bodies, prompts, model output
or credentials. Local host scripts write operational logs to their local logs folder.
Browser state retains the output-language preference, not a mailbox archive.

The add-in requests `ReadItem`; it does not automatically send email. Review
every AI-generated action/date and draft before using it. Prompt instructions
reduce but cannot eliminate incorrect output or prompt-injection risk.

# AI Provider Configuration

The API supports two mail-analysis providers:

- `Ollama` — local inference.
- `OpenAI` — remote inference through the OpenAI Responses API.

The checked-in `appsettings.json` currently selects `OpenAI`. `Ai__Provider`
always takes precedence and is the recommended way to choose a provider for the
persistent local host.

Provider selection happens when the API starts. Stop and restart the API after changing provider settings.

## Configuration variables

ASP.NET Core maps a double underscore (`__`) in an environment-variable name to a configuration colon (`:`). For example, `Ai__Provider` overrides `Ai:Provider` in `appsettings.json`.

| Environment variable | Required | Default | Purpose |
| --- | --- | --- | --- |
| `Ai__Provider` | No | `OpenAI` | Selects `Ollama` or `OpenAI`. |
| `OPENAI_API_KEY` | OpenAI only | None | Secret API key used by the OpenAI adapter. |
| `Ai__OpenAI__Model` | No | `gpt-4.1-mini` | OpenAI model ID. |
| `Ai__OpenAI__TimeoutSeconds` | No | `60` | OpenAI request timeout. |
| `Ai__OpenAI__MaxOutputTokens` | No | `512` | Maximum OpenAI response tokens. |
| `Ai__OpenAI__Temperature` | No | `0.1` | OpenAI sampling temperature. |
| `Ai__OpenAI__BaseUrl` | No | `https://api.openai.com/v1/` | OpenAI API base URL. |
| `Ai__Ollama__Model` | No | `qwen3.5:4b` | Ollama model tag. |
| `Ai__Ollama__TimeoutSeconds` | No | `180` | Ollama request timeout. |
| `Ai__Ollama__MaxOutputTokens` | No | `512` | Maximum Ollama response tokens. |
| `Ai__Ollama__ContextWindow` | No | `4096` | Ollama context-window size. |
| `Ai__Ollama__EnableThinking` | No | `false` | Enables supported Ollama model thinking mode. |

Do not add `OPENAI_API_KEY` to `appsettings.json`, source code, a committed script, or a checked-in `.env` file.

## Recommended for local testing: current PowerShell session

Open PowerShell in the repository root. Set the variables in the same window that will start the API:

```powershell
cd C:\Users\aalmis\Documents\ChatGPT\Outlook_Mail_Adviser

$env:OPENAI_API_KEY = Read-Host 'OpenAI API key' -MaskInput
$env:Ai__Provider = 'OpenAI'

dotnet run `
    --project backend/src/OutlookMailAdviser.Api `
    --launch-profile https
```

These variables exist only in that PowerShell process and its child processes. Closing the window removes them. A different PowerShell window will not see them.

Confirm the configuration without displaying the secret:

```powershell
$env:Ai__Provider

if ([string]::IsNullOrWhiteSpace($env:OPENAI_API_KEY)) {
    'OPENAI_API_KEY is missing'
} else {
    'OPENAI_API_KEY is set'
}
```

## Persistent Windows user environment

Use this only if the API should use OpenAI after opening a new terminal or restarting the computer:

```powershell
$apiKey = Read-Host 'OpenAI API key' -MaskInput

[Environment]::SetEnvironmentVariable(
    'OPENAI_API_KEY',
    $apiKey,
    'User')

[Environment]::SetEnvironmentVariable(
    'Ai__Provider',
    'OpenAI',
    'User')

$apiKey = $null
```

Close and reopen PowerShell, Visual Studio, VS Code, or Rider after setting persistent variables. Existing applications do not automatically receive updated user-environment values.

The same values can be created through Windows **Edit environment variables for your account**:

1. Add `OPENAI_API_KEY` with the API key as its value.
2. Add `Ai__Provider` with `OpenAI` as its value.
3. Restart the terminal or IDE that launches the API.

Persistent user environment variables are available to processes running as that Windows user. For shared machines, prefer a development secret store instead.

## CI, containers, and deployed environments

Store `OPENAI_API_KEY` in the deployment platform's secret manager and inject it into the API process as an environment variable. Set `Ai__Provider=OpenAI` as ordinary deployment configuration. Do not bake the key into an image or commit it to the repository.

## Verify the OpenAI provider

After starting the API, open a second PowerShell window:

```powershell
Invoke-RestMethod https://localhost:7047/health/live
Invoke-RestMethod https://localhost:7047/health/ready
```

Both endpoints should report `Healthy`. The readiness check validates the selected OpenAI model and credentials.

Then use the browser test page:

```text
https://localhost:7047/test
```

Run a mail analysis and confirm that the response `model` field reports the configured OpenAI model.

## Switch back to Ollama

For session-only variables, stop the API and run:

```powershell
Remove-Item Env:Ai__Provider -ErrorAction SilentlyContinue
Remove-Item Env:OPENAI_API_KEY -ErrorAction SilentlyContinue
Remove-Item Env:Ai__OpenAI__Model -ErrorAction SilentlyContinue
Remove-Item Env:Ai__OpenAI__TimeoutSeconds -ErrorAction SilentlyContinue
Remove-Item Env:Ai__OpenAI__MaxOutputTokens -ErrorAction SilentlyContinue

dotnet run `
    --project backend/src/OutlookMailAdviser.Api `
    --launch-profile https
```

Because `appsettings.json` currently defaults to `OpenAI`, explicitly set
`Ai__Provider=Ollama` to switch back.

To remove persistent Windows-user values:

```powershell
[Environment]::SetEnvironmentVariable('OPENAI_API_KEY', $null, 'User')
[Environment]::SetEnvironmentVariable('Ai__Provider', $null, 'User')
```

Open a new terminal after removing persistent values.

## Troubleshooting

- **The API still uses Ollama:** `Ai__Provider` was probably set in another PowerShell window, or the API was not restarted.
- **Startup says the API key is missing:** set `OPENAI_API_KEY` before running `dotnet run` in the same window.
- **Readiness returns unhealthy or authentication fails:** verify the key, project access, model access, and account billing/quota.
- **A value set with `setx` is not visible:** `setx` affects future processes only. Open a new terminal before starting the API.
- **The test page warns about remote processing:** this is expected. When OpenAI is selected, sanitized mail content leaves the machine and is sent to the OpenAI API.

OpenAI's official quickstart also recommends exporting the API key through an environment variable: <https://developers.openai.com/api/docs/quickstart>.

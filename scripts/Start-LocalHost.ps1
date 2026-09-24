[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$hostRoot = $PSScriptRoot
$executable = Join-Path $hostRoot "OutlookMailAdviser.Api.exe"

if (-not (Test-Path -LiteralPath $executable)) {
    throw "Published API executable was not found: $executable"
}

$env:ASPNETCORE_ENVIRONMENT = "Production"

# Read the latest per-user values even when Task Scheduler has an older
# environment block from the current Windows session.
$configurationVariables = @(
    "Ai__Provider",
    "MailProcessing__MaxInputCharacters",
    "MailProcessing__MaxHistoryMessages",
    "MailProcessing__MaxHistoryCharacters",
    "Ai__OpenAI__Model",
    "Ai__OpenAI__TimeoutSeconds",
    "Ai__OpenAI__MaxOutputTokens",
    "Ai__OpenAI__Temperature",
    "Ai__OpenAI__BaseUrl",
    "Ai__Ollama__Model",
    "Ai__Ollama__TimeoutSeconds",
    "Ai__Ollama__MaxOutputTokens",
    "Ai__Ollama__ContextWindow",
    "Ai__Ollama__EnableThinking",
    "OPENAI_API_KEY"
)

foreach ($variableName in $configurationVariables) {
    $userValue = [Environment]::GetEnvironmentVariable($variableName, "User")
    if (-not [string]::IsNullOrWhiteSpace($userValue)) {
        Set-Item -LiteralPath "Env:$variableName" -Value $userValue
    }
}

$logsRoot = Join-Path $hostRoot "logs"
New-Item -ItemType Directory -Path $logsRoot -Force | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$stdoutPath = Join-Path $logsRoot "$timestamp.stdout.log"
$stderrPath = Join-Path $logsRoot "$timestamp.stderr.log"

$process = Start-Process `
    -FilePath $executable `
    -WorkingDirectory $hostRoot `
    -WindowStyle Hidden `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath `
    -Wait `
    -PassThru

exit $process.ExitCode

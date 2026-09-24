[CmdletBinding()]
param(
    [ValidateSet("OpenAI", "Ollama")]
    [string]$Provider = "OpenAI",

    [string]$Model,

    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$taskName = "Outlook Mail Adviser"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$publishRoot = Join-Path $repositoryRoot ".artifacts/local-host"

& (Join-Path $PSScriptRoot "Set-LocalProvider.ps1") -Provider $Provider -Model $Model

Write-Host "Making sure the trusted localhost certificate exists..."
& dotnet dev-certs https --trust
if ($LASTEXITCODE -ne 0) { throw "The localhost HTTPS certificate could not be trusted." }

$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($null -ne $existingTask) {
    Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

$portClient = [System.Net.Sockets.TcpClient]::new()
$portInUse = $false
try {
    $connection = $portClient.ConnectAsync("127.0.0.1", 7047)
    $portInUse = $connection.Wait([TimeSpan]::FromSeconds(1)) -and $portClient.Connected
}
catch {
    $portInUse = $false
}
finally {
    $portClient.Dispose()
}

if ($portInUse) {
    throw "Port 7047 is already in use. Stop the existing development API and run this installer again."
}

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot "Publish-LocalHost.ps1") -OutputPath $publishRoot
}

$startScript = Join-Path $publishRoot "Start-LocalHost.ps1"
if (-not (Test-Path -LiteralPath $startScript)) {
    throw "The local host package is missing. Run without -SkipPublish first."
}

$powerShellExecutable = Join-Path $PSHOME "pwsh.exe"
$actionArguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$startScript`""
$action = New-ScheduledTaskAction `
    -Execute $powerShellExecutable `
    -Argument $actionArguments `
    -WorkingDirectory $publishRoot
$trigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
$settings = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -MultipleInstances IgnoreNew
$principal = New-ScheduledTaskPrincipal `
    -UserId "$env:USERDOMAIN\$env:USERNAME" `
    -LogonType Interactive `
    -RunLevel Limited

Register-ScheduledTask `
    -TaskName $taskName `
    -Description "Runs Outlook Mail Adviser locally after Windows logon." `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Force | Out-Null

Start-ScheduledTask -TaskName $taskName

$healthy = $false
for ($attempt = 1; $attempt -le 20; $attempt++) {
    Start-Sleep -Seconds 1
    try {
        $response = Invoke-WebRequest `
            -Uri "https://localhost:7047/health/live" `
            -UseBasicParsing `
            -TimeoutSec 2
        if ($response.StatusCode -eq 200) {
            $healthy = $true
            break
        }
    }
    catch {
        # The process may still be starting.
    }
}

if (-not $healthy) {
    $logsRoot = Join-Path $publishRoot "logs"
    throw "The task was registered, but the API did not become healthy. Check logs in $logsRoot"
}

Write-Host "Outlook Mail Adviser is running at https://localhost:7047/addin/"
Write-Host "Sideload this manifest once: $(Join-Path $publishRoot 'manifest.xml')"

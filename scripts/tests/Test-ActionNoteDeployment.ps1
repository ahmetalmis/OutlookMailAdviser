[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $repositoryRoot 'scripts/Initialize-ActionNoteData.ps1')
$updater = Get-Content -LiteralPath (Join-Path $repositoryRoot 'scripts/Update-WindowsService.ps1') -Raw
# Exercise the production update flow against a disposable filesystem and a simulated service.
# This process does not contact or stop the installed service or change registry/ACL permissions.
$updater = $updater.Replace(". (Join-Path `$PSScriptRoot 'Grant-UserEnvironmentReadAccess.ps1')", '')
$updater = $updater.Replace(". (Join-Path `$PSScriptRoot 'Initialize-ActionNoteData.ps1')", '')
$updater = $updater.Replace("`$installRoot = Join-Path `$env:ProgramData 'OutlookMailAdviser'", '$installRoot = $script:fixtureInstall')
function Grant-UserEnvironmentReadAccess { param($UserSid) }
function Initialize-ActionNoteDataDirectory { param($InstallRoot) New-Item -ItemType Directory -Path (Join-Path $InstallRoot 'data') -Force | Out-Null }
function Get-CimInstance { param($ClassName, $Filter) [pscustomobject]@{ PathName = '"' + (Join-Path $script:fixtureInstall 'OutlookMailAdviser.Api.exe') + '" --environment WindowsService' } }
function Stop-Service { [CmdletBinding()]param($Name) $script:stopped = $true }
function Start-Service {
    [CmdletBinding()]param($Name)
    if ($script:failStartup) { $script:failStartup = $false; throw 'Simulated startup failure.' }
    $script:stopped = $false
}
function Get-Service { param($Name) [pscustomobject]@{ Name = $Name; Status = 'Running'; StartType = 'Automatic' } }
function Invoke-WebRequest { param($Uri, [switch]$NoProxy, $TimeoutSec) [pscustomobject]@{ StatusCode = 200 } }
function Copy-Item {
    [CmdletBinding()]param($LiteralPath, $Destination, [switch]$Force)
    if ([IO.Path]::GetFileName($LiteralPath) -eq 'action-notes.json' -and -not $script:stopped) { throw 'Data copied while service was running.' }
    Microsoft.PowerShell.Management\Copy-Item -LiteralPath $LiteralPath -Destination $Destination -Force:$Force
}
foreach ($simulateFailure in @($false, $true)) {
    $fixtureRoot = Join-Path $repositoryRoot ('.artifacts/action-update-tests/' + [guid]::NewGuid().ToString('N'))
    $script:fixtureInstall = Join-Path $fixtureRoot 'installed'
    $packageRoot = Join-Path $fixtureRoot 'package'
    $script:stopped = $false
    $script:failStartup = $simulateFailure
    New-Item -ItemType Directory -Path (Join-Path $script:fixtureInstall 'data'), (Join-Path $packageRoot 'wwwroot/addin') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $script:fixtureInstall 'appsettings.WindowsService.json') -Value '{"Ai:OpenAI:UserEnvironmentSid":"test-sid"}'
    Set-Content -LiteralPath (Join-Path $script:fixtureInstall 'OutlookMailAdviser.Api.exe') -Value 'old-binary'
    $notesPath = Join-Path $script:fixtureInstall 'data/action-notes.json'
    Set-Content -LiteralPath $notesPath -Value '{"version":1,"notes":[{"description":"Saved before update"}]}'
    $before = (Get-FileHash -LiteralPath $notesPath).Hash
    Set-Content -LiteralPath (Join-Path $packageRoot 'OutlookMailAdviser.Api.exe') -Value 'new-binary'
    Set-Content -LiteralPath (Join-Path $packageRoot 'wwwroot/addin/index.html') -Value '<html>new</html>'
    $failed = $false
    try { & ([scriptblock]::Create($updater)) -PackagePath $packageRoot | Out-Null } catch { $failed = $true }
    if ($failed -ne $simulateFailure) { throw 'Unexpected updater outcome.' }
    if ((Get-FileHash -LiteralPath $notesPath).Hash -ne $before) { throw 'Saved notes changed during update/rollback.' }
    $snapshot = @(Get-ChildItem -LiteralPath (Join-Path $script:fixtureInstall '.rollback') -Recurse -File | Where-Object Name -eq 'action-notes.json')
    if ($snapshot.Count -ne 1 -or (Get-FileHash -LiteralPath $snapshot[0].FullName).Hash -ne $before) { throw 'Data snapshot missing or inconsistent.' }
    $expectedBinary = if ($simulateFailure) { 'old-binary' } else { 'new-binary' }
    if ((Get-Content -LiteralPath (Join-Path $script:fixtureInstall 'OutlookMailAdviser.Api.exe') -Raw).Trim() -ne $expectedBinary) { throw 'Incorrect binary after update/rollback.' }
    if ($script:stopped) { throw 'Service was not restarted.' }
}
Write-Output 'Action-note preservation, stopped-service snapshots and rollback checks passed.'

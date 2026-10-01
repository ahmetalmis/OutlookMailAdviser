[CmdletBinding()]
param([switch]$Disable)
$ErrorActionPreference = 'Stop'
$key = 'HKCU:\Software\Microsoft\Office\16.0\Wef\Developer\RuntimeLogging'
if ($Disable) {
    Remove-ItemProperty -Path $key -Name '(default)' -ErrorAction SilentlyContinue
    Write-Host 'Office runtime logging disabled.'
    return
}
$logsRoot = Join-Path (Split-Path $PSScriptRoot -Parent) '.artifacts/diagnostics'
New-Item -ItemType Directory -Path $logsRoot -Force | Out-Null
$logPath = Join-Path $logsRoot 'office-runtime.log'
New-Item -Path $key -Force | Out-Null
Set-Item -Path $key -Value $logPath
Write-Host "Office runtime logging enabled: $logPath"
Write-Host 'Restart Outlook and reproduce the add-in error. Disable logging after diagnosis.'

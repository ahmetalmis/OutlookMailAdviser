[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$toolRoot = Join-Path $root '.artifacts/tools/gitleaks'
New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null
$archive = Join-Path $toolRoot 'gitleaks.zip'
Invoke-WebRequest 'https://github.com/gitleaks/gitleaks/releases/download/v8.30.1/gitleaks_8.30.1_windows_x64.zip' -OutFile $archive
$expected = 'D29144DEFF3A68AA93CED33DDDF84B7FDC26070ADD4AA0F4513094C8332AFC4E'
if ((Get-FileHash $archive -Algorithm SHA256).Hash -ne $expected) {
    throw 'Gitleaks archive checksum mismatch.'
}
Expand-Archive -LiteralPath $archive -DestinationPath $toolRoot -Force
Write-Output (Join-Path $toolRoot 'gitleaks.exe')

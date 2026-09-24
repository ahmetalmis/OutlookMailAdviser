[CmdletBinding()]
param([string]$GitleaksPath = '.artifacts/tools/gitleaks/gitleaks.exe')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
    $scanner = (Resolve-Path $GitleaksPath).Path
    $scanRoot = Join-Path $root ('.artifacts/source-scan/' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scanRoot -Force | Out-Null
    # Snapshot includes tracked modifications and new, non-ignored source files.
    $files = @(& git -c core.quotepath=false ls-files --cached --others --exclude-standard) | Sort-Object -Unique
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate public source.' }
    foreach ($file in $files) {
        if (Test-Path -LiteralPath $file -PathType Leaf) {
            $destination = Join-Path $scanRoot $file
            New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
            Copy-Item -LiteralPath $file -Destination $destination
        }
    }
    & $scanner git . --log-opts=--all --redact --no-banner
    if ($LASTEXITCODE -ne 0) { throw 'Git history secret scan failed.' }
    & $scanner dir $scanRoot --redact --no-banner
    if ($LASTEXITCODE -ne 0) { throw 'Public source secret scan failed.' }
    Write-Host "Secret checks passed: all local refs and $($files.Count) source files."
}
finally { Pop-Location }

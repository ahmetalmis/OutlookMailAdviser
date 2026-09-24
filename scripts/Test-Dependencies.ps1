[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
    $raw = & dotnet list OutlookMailAdviser.sln package --vulnerable --include-transitive --format json
    if ($LASTEXITCODE -ne 0) { throw 'NuGet vulnerability lookup failed.' }
    $report = ($raw -join "`n") | ConvertFrom-Json
    if (@($report.problems | Where-Object { $_ }).Count -gt 0) { throw 'NuGet reported lookup problems; audit is incomplete.' }
    $blocking = @($report.projects | ForEach-Object { $_.frameworks } |
        ForEach-Object { @($_.topLevelPackages) + @($_.transitivePackages) } |
        Where-Object { $_ } | ForEach-Object { $_.vulnerabilities } |
        Where-Object { $_.severity -in @('High', 'Critical') })
    if ($blocking.Count -gt 0) { throw "$($blocking.Count) high/critical NuGet vulnerabilities found." }
    Push-Location addin
    try {
        & npm.cmd audit --audit-level=high
        if ($LASTEXITCODE -ne 0) { throw 'npm vulnerability audit failed.' }
    }
    finally { Pop-Location }
}
finally { Pop-Location }

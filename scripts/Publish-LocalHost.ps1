[CmdletBinding()]
param(
    [string]$OutputPath = ".artifacts/local-host"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$addinRoot = Join-Path $repositoryRoot "addin"
$apiProject = Join-Path $repositoryRoot "backend/src/OutlookMailAdviser.Api/OutlookMailAdviser.Api.csproj"
$outputRoot = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    [System.IO.Path]::GetFullPath($OutputPath)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))
}

$repositoryPrefix = $repositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $outputRoot.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputPath must stay inside the repository: $outputRoot"
}

Write-Host "Building the Outlook add-in..."
Push-Location $addinRoot
try {
    & npm.cmd ci
    if ($LASTEXITCODE -ne 0) { throw "npm ci failed with exit code $LASTEXITCODE." }

    & npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}

if (Test-Path -LiteralPath $outputRoot) {
    $resolvedOutput = (Resolve-Path -LiteralPath $outputRoot).Path
    if (-not $resolvedOutput.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a directory outside the repository: $resolvedOutput"
    }

    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

Write-Host "Publishing the .NET API..."
& dotnet publish $apiProject --configuration Release --output $outputRoot
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

$addinOutput = Join-Path $outputRoot "wwwroot/addin"
New-Item -ItemType Directory -Path $addinOutput -Force | Out-Null
Copy-Item -Path (Join-Path $addinRoot "dist/*") -Destination $addinOutput -Recurse -Force
Copy-Item -LiteralPath (Join-Path $addinRoot "manifest.localhost.xml") `
    -Destination (Join-Path $outputRoot "manifest.xml") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Start-LocalHost.ps1") `
    -Destination (Join-Path $outputRoot "Start-LocalHost.ps1") -Force

Write-Host "Local host package created at: $outputRoot"

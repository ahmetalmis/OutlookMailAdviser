[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
$serviceName = 'OutlookMailAdviser'
$installRoot = Join-Path $env:ProgramData 'OutlookMailAdviser'
$packageRoot = (Resolve-Path -LiteralPath $PackagePath).Path
$expectedExe = Join-Path $installRoot 'OutlookMailAdviser.Api.exe'
$service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
if (-not $service -or -not $service.PathName.StartsWith(('"' + $expectedExe + '"'), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The expected OutlookMailAdviser Windows service is not installed.'
}
foreach ($required in @('OutlookMailAdviser.Api.exe', 'wwwroot/addin/index.html')) {
    if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $required))) { throw "Missing package file: $required" }
}
$files = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File)
if ($files | Where-Object { $_.Name -eq 'appsettings.WindowsService.json' -or $_.Extension -in '.pfx','.p12','.key' }) {
    throw 'Deployment packages must not contain service credentials or private certificates.'
}
$backup = Join-Path $installRoot ('.rollback/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($packageRoot, $file.FullName)
    $target = Join-Path $installRoot $relative
    if (Test-Path -LiteralPath $target) {
        $backupFile = Join-Path $backup $relative
        New-Item -ItemType Directory -Path (Split-Path $backupFile -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $backupFile
    }
}
try {
    Stop-Service $serviceName
    foreach ($file in $files) {
        $target = Join-Path $installRoot ([IO.Path]::GetRelativePath($packageRoot, $file.FullName))
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }
    Start-Service $serviceName
    $healthy = $false
    for ($attempt = 0; $attempt -lt 15; $attempt++) {
        try {
            $response = Invoke-WebRequest 'https://localhost:7047/health/live' -NoProxy -TimeoutSec 2
            if ($response.StatusCode -eq 200) { $healthy = $true; break }
        } catch { Start-Sleep -Seconds 1 }
    }
    if (-not $healthy) { throw 'Updated service did not become live.' }
    Get-Service $serviceName | Select-Object Name, Status, StartType
    Write-Host "Updated. Existing API key and certificate preserved. Rollback files: $backup"
} catch {
    Stop-Service $serviceName -ErrorAction SilentlyContinue
    foreach ($file in (Get-ChildItem -LiteralPath $backup -Recurse -File)) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $installRoot ([IO.Path]::GetRelativePath($backup, $file.FullName))) -Force
    }
    Start-Service $serviceName -ErrorAction SilentlyContinue
    throw
}

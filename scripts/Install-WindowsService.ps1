[CmdletBinding()]
param([string]$PackagePath = (Join-Path $PSScriptRoot '../.artifacts/windows-service'))

$ErrorActionPreference = 'Stop'
$serviceName = 'OutlookMailAdviser'
$taskName = 'Outlook Mail Adviser'
$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell session.'
}
$packageRoot = (Resolve-Path -LiteralPath $PackagePath).Path
if (-not (Test-Path (Join-Path $packageRoot 'OutlookMailAdviser.Api.exe'))) { throw 'Publish the service package first.' }
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) { throw 'The service already exists. Stop and update it explicitly.' }
$installRoot = Join-Path $env:ProgramData 'OutlookMailAdviser'
if (Test-Path $installRoot) { throw "Installation directory already exists: $installRoot" }

# Only administrators and SYSTEM can change binaries or configuration. LocalService can read them.
New-Item -ItemType Directory -Path $installRoot | Out-Null
$acl = Get-Acl $installRoot
$acl.SetAccessRuleProtection($true, $false)
foreach ($entry in @(@('S-1-5-18','FullControl'), @('S-1-5-32-544','FullControl'), @('S-1-5-19','ReadAndExecute'))) {
    $sid = [Security.Principal.SecurityIdentifier]::new($entry[0])
    $rule = [Security.AccessControl.FileSystemAccessRule]::new($sid, $entry[1], 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.AddAccessRule($rule)
}
Set-Acl -LiteralPath $installRoot -AclObject $acl
Copy-Item -Path (Join-Path $packageRoot '*') -Destination $installRoot -Recurse

# A dedicated server certificate avoids depending on an interactive user's dev certificate.
$certificate = New-SelfSignedCertificate -DnsName 'localhost' -CertStoreLocation 'Cert:\LocalMachine\My' `
    -FriendlyName 'Outlook Mail Adviser localhost service' -NotAfter (Get-Date).AddYears(1) `
    -KeyExportPolicy Exportable -Type SSLServerAuthentication
$password = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$pfxPath = Join-Path $installRoot 'localhost.pfx'
Export-PfxCertificate -Cert $certificate -FilePath $pfxPath -Password (ConvertTo-SecureString $password -AsPlainText -Force) | Out-Null
$publicPath = Join-Path $installRoot 'localhost.cer'
Export-Certificate -Cert $certificate -FilePath $publicPath | Out-Null
Import-Certificate -FilePath $publicPath -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null

$settings = @{
    Kestrel = @{ Endpoints = @{ Https = @{ Url = 'https://localhost:7047'; Certificate = @{ Path = $pfxPath; Password = $password } } } }
    Logging = @{ EventLog = @{ LogLevel = @{ Default = 'Information'; 'Microsoft.AspNetCore' = 'Warning' } } }
}
# Preserve the interactive installation's provider settings without printing any credentials.
$userEnvironment = [Environment]::GetEnvironmentVariables('User')
foreach ($name in $userEnvironment.Keys) {
    if ($name -match '^(Ai__|MailProcessing__)' -or $name -eq 'OPENAI_API_KEY') {
        $settings[$name.Replace('__', ':')] = $userEnvironment[$name]
    }
}
$settings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $installRoot 'appsettings.WindowsService.json') -Encoding utf8
if (-not [Diagnostics.EventLog]::SourceExists('OutlookMailAdviser.Api')) {
    [Diagnostics.EventLog]::CreateEventSource('OutlookMailAdviser.Api', 'Application')
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$oldExecutable = Join-Path $repositoryRoot '.artifacts/local-host/OutlookMailAdviser.Api.exe'
$oldTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
$oldTaskWasRunning = $null -ne $oldTask -and $oldTask.State -eq 'Running'
$listeners = @(Get-NetTCPConnection -LocalPort 7047 -State Listen -ErrorAction SilentlyContinue)
foreach ($processId in ($listeners.OwningProcess | Select-Object -Unique)) {
    $process = Get-Process -Id $processId
    if ($process.Path -ne $oldExecutable) { throw "Port 7047 belongs to another process: $processId" }
}
try {
    if ($oldTask) { Stop-ScheduledTask -TaskName $taskName }
    foreach ($processId in ($listeners.OwningProcess | Select-Object -Unique)) {
        if (Get-Process -Id $processId -ErrorAction SilentlyContinue) { Stop-Process -Id $processId -Force }
    }
    $exe = Join-Path $installRoot 'OutlookMailAdviser.Api.exe'
    New-Service -Name $serviceName -DisplayName 'Outlook Mail Adviser' `
        -BinaryPathName ('"' + $exe + '" --environment WindowsService --contentRoot "' + $installRoot + '"') `
        -Description 'Local HTTPS host for the Outlook Mail Adviser add-in.' `
        -StartupType Automatic -Credential ([PSCredential]::new('NT AUTHORITY\LocalService', [SecureString]::new())) | Out-Null
    & sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not configure service recovery.' }
    Start-Service $serviceName
    $healthy = $false
    for ($attempt = 0; $attempt -lt 15; $attempt++) {
        try {
            $response = Invoke-WebRequest 'https://localhost:7047/health/live' -NoProxy -TimeoutSec 2
            if ($response.StatusCode -eq 200) { $healthy = $true; break }
        } catch { Start-Sleep -Seconds 1 }
    }
    if (-not $healthy) { throw 'Service health check failed. Inspect Application event log, source OutlookMailAdviser.Api.' }
    if ($oldTask) { Disable-ScheduledTask -TaskName $taskName | Out-Null }
    Get-Service $serviceName | Select-Object Name, Status, StartType
    Write-Host 'Service ready: https://localhost:7047/addin/index.html'
    Write-Host "Certificate expires: $($certificate.NotAfter)"
} catch {
    Stop-Service $serviceName -ErrorAction SilentlyContinue
    if ($oldTaskWasRunning) { Start-ScheduledTask -TaskName $taskName }
    throw
}

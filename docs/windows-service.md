# Local Windows service

The native service is named `OutlookMailAdviser` (display name: Outlook Mail Adviser).
It starts automatically as `NT AUTHORITY\LocalService`, serves only localhost on
port 7047, and restarts after failures. Installation files live under
`C:\ProgramData\OutlookMailAdviser`. Administrators and SYSTEM can modify them;
LocalService has read/execute access. The configuration includes a copy of the
installing user's AI settings and API key; never share that configuration file.
Changes to the user's environment after installation do not update this copy.

Prepare a package with the API and the published add-in:

```powershell
dotnet publish backend/src/OutlookMailAdviser.Api/OutlookMailAdviser.Api.csproj -c Release -o .artifacts/windows-service
Copy-Item .artifacts/local-host/wwwroot .artifacts/windows-service -Recurse -Force
Copy-Item addin/manifest.localhost.xml .artifacts/windows-service/manifest.xml
```

Run `scripts/Install-WindowsService.ps1` in elevated PowerShell 7. The installer
creates a dedicated localhost server certificate trusted on this machine, copies
the settings, registers the service, and verifies HTTPS without bypassing certificate
validation. The certificate expires after one year and must then be renewed.
After a successful health check, the previous scheduled task is disabled and retained.
The installer refuses to overwrite an existing service or installation directory.

```powershell
Get-Service OutlookMailAdviser
Invoke-WebRequest https://localhost:7047/addin/index.html -NoProxy
Get-WinEvent -FilterHashtable @{LogName='Application'; ProviderName='OutlookMailAdviser.Api'} -MaxEvents 30
```

## Outlook startup diagnostics

For updates, build a fresh package with `scripts/Publish-LocalHost.ps1 -OutputPath .artifacts/service-update`,
then run `scripts/Update-WindowsService.ps1 -PackagePath .artifacts/service-update` in elevated PowerShell 7.
The updater preserves the service's API key and certificate, backs up replaced files in the restricted
installation directory, and restores them if the service cannot start. A liveness success does not imply
that the AI provider accepts the configured key; inspect `/health/ready` separately.

The default `addin/manifest.xml` and `addin/manifest.localhost.xml` now both target the service on 7047.
Only `addin/manifest.dev.xml` uses Vite on 3000, with a separate add-in identity and a Dev display name.
For an existing Outlook installation using the old default manifest, remove it and sideload the updated default manifest.

The readiness endpoint returns safe JSON codes, and the Outlook panel displays Turkish explanations for
authentication, permission, quota, rate-limit and connectivity failures. Use **Yeniden kontrol et** after
correcting the service settings. Input/output token limits are also shown when an actual AI request fails.
The API model-access probe cannot guarantee enough quota or output tokens for a later generation request.
Neither the API key nor raw upstream error bodies are returned to the panel.

The generic “This add-in could not be started” message does not identify the cause.
First check that `/health/live`, `/addin/index.html`, and the assets referenced by
the page return HTTP 200 with certificate validation enabled.

Run `scripts/Set-OfficeRuntimeLogging.ps1` as the Outlook user, restart Outlook,
and reproduce the error. The log is `.artifacts/diagnostics/office-runtime.log`.
Find SolutionId `8f777abc-707d-4c23-b609-5f138a45dd1a` and nearby errors. This log
contains Office manifest/loading diagnostics, not JavaScript console output.
Disable it afterward with `scripts/Set-OfficeRuntimeLogging.ps1 -Disable`.

In classic Outlook, focus the add-in pane and press Ctrl+Shift+I for WebView2
developer tools; inspect Console and Network. New Outlook instead supports
`olk.exe --devtools`. Do not export mail content, authorization headers, or API keys
when sharing diagnostics.

References:
- https://learn.microsoft.com/en-us/office/dev/add-ins/testing/runtime-logging
- https://learn.microsoft.com/en-us/office/dev/add-ins/testing/debug-add-ins-using-devtools-edge-chromium

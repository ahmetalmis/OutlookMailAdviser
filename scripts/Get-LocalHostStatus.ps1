[CmdletBinding()]
param()

$taskName = "Outlook Mail Adviser"
$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($null -eq $task) {
    Write-Host "Scheduled task is not installed."
    exit 1
}

$taskInfo = Get-ScheduledTaskInfo -TaskName $taskName
[pscustomobject]@{
    TaskName = $taskName
    State = $task.State
    LastRunTime = $taskInfo.LastRunTime
    LastTaskResult = $taskInfo.LastTaskResult
    Url = "https://localhost:7047/addin/"
}

try {
    Invoke-RestMethod -Uri "https://localhost:7047/health/live" -TimeoutSec 3
}
catch {
    Write-Warning "The scheduled task exists, but the API health endpoint is unavailable."
}


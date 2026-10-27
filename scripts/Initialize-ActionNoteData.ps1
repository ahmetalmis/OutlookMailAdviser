function Initialize-ActionNoteDataDirectory {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$InstallRoot)
    $dataRoot = Join-Path $InstallRoot 'data'
    New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null
    $acl = Get-Acl -LiteralPath $dataRoot
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($entry in @(@('S-1-5-18', 'FullControl'), @('S-1-5-32-544', 'FullControl'), @('S-1-5-19', 'Modify'))) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new(
            [Security.Principal.SecurityIdentifier]::new($entry[0]),
            $entry[1], 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.SetAccessRule($rule)
    }
    Set-Acl -LiteralPath $dataRoot -AclObject $acl
}

function Assert-NoActionNoteDataInPackage {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$PackageRoot)
    foreach ($reserved in @('data', '.rollback')) {
        if (Test-Path -LiteralPath (Join-Path $PackageRoot $reserved)) {
            throw "Deployment packages cannot contain the reserved $reserved directory."
        }
    }
    if (Get-ChildItem -LiteralPath $PackageRoot -Recurse -File | Where-Object { $_.Name -eq 'action-notes.json' -or $_.Name -like '.action-notes-*.tmp' }) {
        throw 'Deployment packages cannot contain saved action notes.'
    }
}

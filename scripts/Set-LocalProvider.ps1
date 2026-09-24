[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("OpenAI", "Ollama")]
    [string]$Provider,

    [string]$Model
)

$ErrorActionPreference = "Stop"
[Environment]::SetEnvironmentVariable("Ai__Provider", $Provider, "User")

if ($Provider -eq "OpenAI") {
    $existingKey = [Environment]::GetEnvironmentVariable("OPENAI_API_KEY", "User")
    if ([string]::IsNullOrWhiteSpace($existingKey)) {
        $secureKey = Read-Host "OpenAI API key" -AsSecureString
        $keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
        try {
            $plainKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)
            if ([string]::IsNullOrWhiteSpace($plainKey)) {
                throw "OpenAI API key cannot be empty."
            }

            [Environment]::SetEnvironmentVariable("OPENAI_API_KEY", $plainKey, "User")
        }
        finally {
            if ($keyPointer -ne [IntPtr]::Zero) {
                [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer)
            }
            $plainKey = $null
            $secureKey = $null
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($Model)) {
        [Environment]::SetEnvironmentVariable("Ai__OpenAI__Model", $Model, "User")
    }
}
elseif (-not [string]::IsNullOrWhiteSpace($Model)) {
    [Environment]::SetEnvironmentVariable("Ai__Ollama__Model", $Model, "User")
}

Write-Host "$Provider provider configuration was saved for the current Windows user."


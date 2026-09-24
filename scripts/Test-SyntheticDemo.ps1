[CmdletBinding()]
param([string]$BaseUrl = 'https://localhost:7047')
$ErrorActionPreference = 'Stop'
$message = @{
    subject = 'Demo ortamı geçiş planı'
    from = @{ name = 'Deniz Kaya'; address = 'deniz.kaya@example.com' }
    to = @(@{ name = 'Eren Yılmaz'; address = 'eren.yilmaz@example.com' })
    cc = @()
    sentAt = '2026-09-24T10:00:00+03:00'
    bodyFormat = 'plainText'
    body = "Merhaba Eren,`nDemo ortamına geçiş tamamlanamadı. Bu nedenle kabul testleri başlayamadı. Gecikmenin nedenini ve güncellenmiş geçiş planını 25 Eylül 2026 saat 14:00'e kadar paylaşabilir misin? Kontrol ve geri dönüş listesini de aynı günün sonuna kadar hazırlamanı rica ederim.`nTeşekkürler,`nDeniz"
}
$analysisRequest = @{
    preferredLanguage = 'tr'
    currentUser = @{ name = 'Eren Yılmaz'; address = 'eren.yilmaz@example.com' }
    message = $message
}
$analysis = Invoke-RestMethod "$BaseUrl/api/v1/mail/analysis" -Method Post -ContentType 'application/json; charset=utf-8' -Body ($analysisRequest | ConvertTo-Json -Depth 8) -TimeoutSec 240
if ([string]::IsNullOrWhiteSpace($analysis.summary) -or @($analysis.actions).Count -lt 2) {
    throw 'Expected a summary and at least two synthetic actions.'
}
if (-not $analysis.actionRequiredFromCurrentUser) { throw 'Expected current-user action assignment.' }
if (@($analysis.actions | Where-Object { $_.dueDate -eq '2026-09-25' }).Count -lt 2) {
    throw 'Expected both synthetic deadlines on 2026-09-25.'
}
$draftRequest = @{
    preferredLanguage = 'tr'
    message = $message
    tone = 'professional'
    instructions = 'Gecikmenin nedenini inceliyoruz. Güncellenen planı ve kontrol listesini belirtilen tarihe kadar paylaşacağımızı belirt.'
}
$draft = Invoke-RestMethod "$BaseUrl/api/v1/mail/draft" -Method Post -ContentType 'application/json; charset=utf-8' -Body ($draftRequest | ConvertTo-Json -Depth 8) -TimeoutSec 240
if ([string]::IsNullOrWhiteSpace($draft.subject) -or [string]::IsNullOrWhiteSpace($draft.body)) {
    throw 'Expected a non-empty draft subject and body.'
}
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$output = Join-Path $root '.artifacts/public-release'
New-Item -ItemType Directory -Path $output -Force | Out-Null
@{ analysis = $analysis; draft = $draft } | ConvertTo-Json -Depth 15 |
    Set-Content -LiteralPath (Join-Path $output 'synthetic-demo.json') -Encoding utf8
Write-Host "Synthetic analysis and draft passed; provider model: $($analysis.model)."

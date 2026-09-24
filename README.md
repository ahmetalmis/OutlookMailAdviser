# Outlook Mail Adviser

Outlook içinde açık e-postayı seçili AI sağlayıcısıyla analiz eden ve kullanıcı onayıyla cevap taslağı hazırlayan bir POC.

Hedef teknoloji seti:

- Outlook Office Add-in (Office.js, React, TypeScript)
- ASP.NET Core Web API (.NET 8)
- Ollama
- Varsayılan model: `qwen3.5:4b`
- Opsiyonel OpenAI Responses API sağlayıcısı; varsayılan model: `gpt-4.1-mini`
- Clean Architecture / Hexagonal (Onion) Architecture

Sağlayıcı ve model yapılandırmadan değiştirilebilir; Application ve Domain katmanları Ollama, OpenAI veya belirli bir modele bağımlı değildir.

Detaylı kapsam, mimari kararlar, API sözleşmeleri ve geliştirme planı için [POC-DESIGN.md](POC-DESIGN.md) belgesine bakın.

Provider seçimi, ortam değişkenlerinin nereye tanımlanacağı ve Ollama/OpenAI geçiş adımları için [PROVIDER-CONFIGURATION.md](PROVIDER-CONFIGURATION.md) belgesine bakın.

Postman ile test etmek için hazır collection ve environment dosyaları [postman/README.md](postman/README.md) altında bulunur.

## Mevcut durum

Faz 0 ve Faz 1 backend vertical slice tamamlandı:

- .NET 8 solution ve Clean Architecture katmanları
- Merkezi build ve NuGet paket yönetimi
- Derleme zamanında warning-as-error ve önerilen analyzer kuralları
- Proje bağımlılıklarını koruyan architecture testi
- Problem Details ve Swagger/OpenAPI
- `/health/live` ve seçili AI sağlayıcısını kontrol eden `/health/ready`
- Başlangıçta doğrulanan, config tabanlı sağlayıcı/model seçenekleri
- Mail domain modelleri ve `AnalyzeMail` use case'i
- HTML-to-text temizleme ve içerik limiti/truncation uyarısı
- Ollama `/api/chat` structured-output adaptörü ve tek repair denemesi
- OpenAI `/v1/responses` strict structured-output adaptörü
- `POST /api/v1/mail/analysis` endpoint'i
- Domain, application, adapter, architecture ve API integration testleri

## Yerel geliştirme

Gereksinimler:

- .NET 8 SDK
- Ollama
- Readiness kontrolü için `qwen3.5:4b` modeli

```powershell
ollama pull qwen3.5:4b
dotnet restore OutlookMailAdviser.sln --configfile NuGet.config
dotnet test OutlookMailAdviser.sln --no-build --no-restore
dotnet run --project backend/src/OutlookMailAdviser.Api --launch-profile https
```

Development adresleri:

- API: `https://localhost:7047`
- Swagger: `https://localhost:7047/swagger`
- Görsel test ekranı: `https://localhost:7047/test`
- Liveness: `https://localhost:7047/health/live`
- Readiness: `https://localhost:7047/health/ready`

Yerel HTTPS sertifikasına ilk kez güvenmek için gerekirse `dotnet dev-certs https --trust` çalıştırılabilir.

## Nasıl test edilir?

### 1. Ollama olmadan otomatik testler

Önce çalışan bir API prosesi varsa açık bıraktığınız terminalden durdurun. Ardından repository kökünde:

```powershell
dotnet restore OutlookMailAdviser.sln --configfile NuGet.config
dotnet build OutlookMailAdviser.sln --no-restore
dotnet test OutlookMailAdviser.sln --no-build --no-restore
dotnet format OutlookMailAdviser.sln --no-restore --verify-no-changes
```

Bu testler gerçek modele ihtiyaç duymaz. Ollama ve OpenAI HTTP çağrıları kontrollü test handler'larıyla, API endpoint'i ise fake `IMailIntelligenceGateway` ile sınanır.

### 2. Windows'a Ollama kurulumu

Ollama henüz kurulu değilse Windows installer'ı [resmî Ollama Windows sayfasından](https://docs.ollama.com/windows) indirin. Kurulumdan sonra yeni bir PowerShell açın:

```powershell
ollama --version
ollama pull qwen3.5:4b
ollama list
Invoke-RestMethod http://127.0.0.1:11434/api/tags
```

Son komut model listesini döndürüyorsa yerel Ollama API hazırdır.

### 3. API'yi çalıştırma

Yeni bir PowerShell penceresinde repository kökünden:

```powershell
dotnet dev-certs https --trust
dotnet run --project backend/src/OutlookMailAdviser.Api --launch-profile https
```

Başka bir PowerShell penceresinde:

```powershell
Invoke-RestMethod https://localhost:7047/health/live
Invoke-RestMethod https://localhost:7047/health/ready
```

Her iki çağrının da sağlıklı dönmesi gerekir. `ready` seçili sağlayıcıyı ve yapılandırılmış modeli kontrol eder.

### 4. Gerçek mail analizi

PowerShell kullanmadan test etmek için tarayıcıda `https://localhost:7047/test` adresini açın. E-posta alanlarını doldurup **Maili analiz et** butonuna basın. Ekran API ve seçili AI sağlayıcısının durumunu, yapılandırılmış analiz sonucunu ve ham JSON yanıtını birlikte gösterir.

PowerShell ile aynı çağrıyı yapmak isterseniz:

```powershell
$payload = @{
    clientRequestId = [guid]::NewGuid()
    preferredLanguage = 'tr'
    currentUser = @{
        name = 'Ali'
        address = 'ali@example.com'
    }
    message = @{
        itemId = 'manual-test-1'
        subject = 'Üretim geçiş planı'
        from = @{
            name = 'Ayşe'
            address = 'ayse@example.com'
        }
        to = @(
            @{
                name = 'Ali'
                address = 'ali@example.com'
            }
        )
        cc = @()
        sentAt = '2026-09-02T09:00:00+03:00'
        body = '<p>Merhaba Ali,</p><p>Üretim geçiş tarihini 3 Eylül saat 15:00’e kadar teyit eder misin?</p>'
        bodyFormat = 'html'
    }
} | ConvertTo-Json -Depth 8

$result = Invoke-RestMethod `
    -Method Post `
    -Uri https://localhost:7047/api/v1/mail/analysis `
    -ContentType 'application/json' `
    -Body $payload

$result | ConvertTo-Json -Depth 8
```

Beklenen sonuçta `summary`, `actionRequired`, `actions`, `priority`, `sentiment`, `model` ve `durationMilliseconds` alanları bulunur. Aynı çağrı Swagger üzerinden de yapılabilir: `https://localhost:7047/swagger`.

CPU üzerinde daha öngörülebilir analiz süresi için varsayılan Ollama ayarları düşünme modunu kapatır, bağlamı 4096 token ve çıktıyı 512 token ile sınırlar. Analiz timeout değeri 180 saniyedir. Bu değerler `Ai__Ollama__EnableThinking`, `Ai__Ollama__ContextWindow`, `Ai__Ollama__MaxOutputTokens` ve `Ai__Ollama__TimeoutSeconds` ortam değişkenleriyle değiştirilebilir.

### 5. Modeli config ile değiştirme

Örneğin daha küçük bir model denemek için önce modeli indirin, sonra API'yi o terminalde yeniden başlatın:

```powershell
ollama pull qwen3.5:2b
$env:Ai__Ollama__Model = 'qwen3.5:2b'
dotnet run --project backend/src/OutlookMailAdviser.Api --launch-profile https
```

Kalıcı dosya değişikliği gerekmez. Terminaldeki override'ı temizlemek için:

```powershell
Remove-Item Env:Ai__Ollama__Model
```

### 6. OpenAI provider ile çalıştırma

Tam provider ve environment-variable rehberi: [PROVIDER-CONFIGURATION.md](PROVIDER-CONFIGURATION.md).

OpenAI seçildiğinde mail içeriği analiz için OpenAI API'ye gönderilir. API anahtarını `appsettings.json` içine yazmayın. Anahtarı terminal geçmişinde göstermeden geçici ortam değişkeni olarak alın ve provider'ı seçin:

```powershell
$env:OPENAI_API_KEY = Read-Host 'OpenAI API key' -MaskInput
$env:Ai__Provider = 'OpenAI'
dotnet run --project backend/src/OutlookMailAdviser.Api --launch-profile https
```

Varsayılan OpenAI modeli `gpt-4.1-mini`'dir. Model ve üretim seçenekleri aynı şekilde değiştirilebilir:

```powershell
$env:Ai__OpenAI__Model = 'gpt-4.1-mini'
$env:Ai__OpenAI__MaxOutputTokens = '512'
$env:Ai__OpenAI__TimeoutSeconds = '60'
```

Ollama'ya geri dönmek için API'yi durdurup ilgili ortam değişkenlerini temizleyin:

```powershell
Remove-Item Env:Ai__Provider
Remove-Item Env:OPENAI_API_KEY
Remove-Item Env:Ai__OpenAI__Model -ErrorAction SilentlyContinue
Remove-Item Env:Ai__OpenAI__MaxOutputTokens -ErrorAction SilentlyContinue
Remove-Item Env:Ai__OpenAI__TimeoutSeconds -ErrorAction SilentlyContinue
```

## Outlook içinde kullanma (Faz 2)

Outlook task pane uygulaması `addin/` klasöründedir. Açık e-postanın konu, gönderen, alıcı, tarih ve HTML gövdesini Office.js ile okuyup `POST /api/v1/mail/analysis` endpoint'ine gönderir. Analiz isteğindeki opsiyonel `currentUser` alanı Outlook kullanıcısının ad/e-posta bilgisini taşır. Yanıttaki `actionRequiredFromCurrentUser` ve aksiyon seviyesindeki `assignedToCurrentUser` alanları, kullanıcıdan beklenen aksiyonların ayrıca vurgulanmasını sağlar. Analiz sonrasında ton ve kullanıcı açıklamasını alan `POST /api/v1/mail/draft` endpoint'i seçili Ollama veya OpenAI modeliyle konu ve yanıt gövdesi üretir.

Desteklenen tonlar: `professional`, `friendly`, `concise`, `persuasive` ve `empathetic`. Taslak isteğindeki opsiyonel `toneDetails` alanı ana tona ek olarak “hafif sert ve uyarıcı” gibi en fazla 500 karakterlik üslup yönlendirmesi taşır. Add-in yalnızca `ReadItem` iznine sahiptir; oluşturulan taslak kullanıcıya gösterilir ve panoya kopyalanabilir, ancak Outlook mesajı otomatik değiştirilmez veya gönderilmez.

İlk kurulum ve Outlook'a manifest yükleme adımları için [addin/README.md](addin/README.md) dosyasını izleyin. Kısa akış:

```powershell
cd addin
npm install
npm run certs
npm run dev
```

Ardından `addin/manifest.xml` dosyasını Outlook'ta özel eklenti olarak yükleyin. API'nin ayrı terminalde `https://localhost:7047` adresinde çalışıyor olması gerekir.

## Windows'ta sürekli çalıştırma

Kalıcı yerel kurulumda Vite geliştirme sunucusu kullanılmaz. React uygulaması
production build olarak hazırlanır, .NET API'nin `wwwroot/addin` klasöründen
sunulur ve ikisi de `https://localhost:7047` origin'ini kullanır. Windows Görev
Zamanlayıcı'daki **Outlook Mail Adviser** görevi kullanıcı oturumu açıldığında
API'yi başlatır; başarısız çıkışlarda üç kez yeniden deneme yapar.

Önce açık `npm run dev` ve `dotnet run` süreçlerini durdurun. Ardından repository
kökünde sağlayıcınıza göre aşağıdaki komutlardan birini çalıştırın:

```powershell
# OpenAI: OPENAI_API_KEY kullanıcı ortamında yoksa güvenli giriş ister.
.\scripts\Install-LocalHost.ps1 -Provider OpenAI -Model gpt-4.1-mini

# veya Ollama:
.\scripts\Install-LocalHost.ps1 -Provider Ollama -Model qwen3.5:4b
```

Kurulum scripti şunları yapar:

1. Sağlayıcı ve modeli Windows kullanıcı ortamına kaydeder.
2. `localhost` geliştirme sertifikasını oluşturur/güvenilir hale getirir.
3. Add-in'i build eder ve .NET API'yi `.artifacts/local-host` altına publish eder.
4. Kullanıcı girişinde çalışan görevi kaydeder, hemen başlatır ve health check yapar.

OpenAI anahtarı dosyaya veya repository'ye yazılmaz; mevcut Windows kullanıcısının
ortam değişkeninde tutulur. Ollama seçildiğinde Ollama'nın Windows oturumunda
çalışıyor ve seçilen modelin indirilmiş olması gerekir.

Kurulum tamamlanınca Outlook'taki özel add-in kaydına aşağıdaki dosyayı bir kez
yükleyin:

```text
.artifacts/local-host/manifest.xml
```

Durumu kontrol etmek için:

```powershell
.\scripts\Get-LocalHostStatus.ps1
Invoke-RestMethod https://localhost:7047/health/ready
```

Kod değişikliğinden sonra build'i ve görevi güncellemek için kurulum komutunu
yeniden çalıştırabilirsiniz. Yalnızca sağlayıcıyı değiştirmek için:

```powershell
.\scripts\Set-LocalProvider.ps1 -Provider Ollama -Model qwen3.5:4b
Stop-ScheduledTask -TaskName 'Outlook Mail Adviser'
Start-ScheduledTask -TaskName 'Outlook Mail Adviser'
```

Otomatik başlangıcı kaldırmak için:

```powershell
.\scripts\Uninstall-LocalHost.ps1
```

Kaldırma scripti yalnızca zamanlanmış görevi siler; publish çıktısını ve kullanıcı
ortamındaki sağlayıcı ayarlarını korur.

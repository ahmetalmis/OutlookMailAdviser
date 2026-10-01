# Outlook Mail Adviser

Ahmet Almış tarafından geliştirilen, Outlook'ta açık e-postadan özet, aksiyon ve yanıt taslağı çıkaran yapay zekâ destekli **yerel portföy / POC projesi**.

## Problem ve özellikler

“Bu e-postada benden ne bekleniyor?” sorusuna Outlook'tan ayrılmadan yanıt bulmayı kolaylaştırır. Mesajın özetini, önceliğini, duygu değerlendirmesini ve tarih içeren aksiyonlarını gösterir. Kullanıcıya ait aksiyonları ayrıca vurgular. Kullanıcı yanıtın ana fikrini ve tonunu belirleyerek taslak oluşturur; son kontrol ve gönderim kullanıcıdadır. Eklenti yalnızca `ReadItem` izni ister ve otomatik e-posta göndermez.

## Teknoloji ve mimari

![Sentetik e-postayla gerçek API analizi](docs/images/synthetic-analysis.png)

Görsel, uygulamanın tarayıcı test ekranından alınmıştır; Outlook task pane görüntüsü değildir.

| Katman | Teknoloji / karar |
| --- | --- |
| Outlook arayüzü | Office.js, React, TypeScript, Vite |
| Yerel API | ASP.NET Core / .NET 8 |
| Model sağlayıcıları | OpenAI (varsayılan) veya Ollama |
| Mimari | Domain/Application çekirdeği, portlar ve sağlayıcı adaptörleri |
| Kontroller | xUnit, Vitest, entegrasyon ve mimari bağımlılık testleri |

Sağlayıcı bağımlılıklarını adaptörlerde tutmak, iş kurallarını değiştirmeden OpenAI/Ollama geçişini mümkün kılar. JSON şemaları model çıktısını yapılandırır; testlerde sağlayıcılar taklit edilerek API anahtarı gereksinimi kaldırılır.
[Mimari tasarım](POC-DESIGN.md) ve [ayrıntılı geliştirme rehberi](docs/DEVELOPMENT.md).

## Veri akışı ve gizlilik

**Outlook → yerel API → seçilen model sağlayıcısı → analiz/taslak → Outlook.**

OpenAI seçildiğinde e-posta içeriği ve katılımcı bilgileri bilgisayar dışına çıkar. Kendi API anahtarınız gerekir; sağlayıcı kullanımı ücretli olabilir. Varsayılan yerel Ollama adresinde çıkarım bilgisayarınızda yapılır. HTML, imza ve geçmiş temizliği **anonimleştirme değildir**. Önce [sentetik demo](docs/DEMO.md) kullanın. Ayrıntılar: [SECURITY.md](SECURITY.md).

## Gereksinimler ve doğrulanan ortam

- Windows, PowerShell 7, Git, `global.json` ile belirtilen .NET 8 SDK.
- Node.js 22.12+ (CI: Node 22); yerel doğrulama sürümleri [yayın raporunda](docs/PUBLIC-RELEASE.md).
- Office eklentilerini ve özel manifest yüklemeyi destekleyen Outlook hesabı/istemcisi. Kurum politikaları özel eklenti yüklemeyi engelleyebilir.
- OpenAI için kendi API anahtarınız veya yerel Ollama kurulumu ve seçilen model.

Kullanıcı tarafından sağlanan önceki ekran görüntüleri klasik Windows Outlook'ta çalışan akışı gösterir. Bu yayın adayının yeniden doğrulama durumu yayın raporundadır. Yeni Outlook, Outlook Web ve macOS için doğrulanmış uyumluluk iddiası yoktur.

## Hızlı kurulum

PowerShell 7'de:

```powershell
git clone https://github.com/ahmetalmis/OutlookMailAdviser.git
cd OutlookMailAdviser
dotnet restore OutlookMailAdviser.sln --configfile NuGet.config
dotnet build OutlookMailAdviser.sln --no-restore
dotnet dev-certs https --trust
```

Sağlayıcınızı seçin; iki seçenekten yalnızca birini uygulayın:

```powershell
# OpenAI — anahtar terminal geçmişine yazılmaz.
$env:Ai__Provider = 'OpenAI'
$env:OPENAI_API_KEY = Read-Host 'OpenAI API key' -MaskInput

# Alternatif: Ollama servisi açık olmalı.
# ollama pull qwen3.5:4b
# $env:Ai__Provider = 'Ollama'

dotnet run --project backend/src/OutlookMailAdviser.Api --launch-profile https --no-build
```

Ayrı terminalde, depo kökünden:

```powershell
cd addin
npm ci
npm run certs
npm run dev
```

Geliştirmede API `https://localhost:7047`, arayüz `https://localhost:3000` üzerinde çalışır. [Outlook'a yükleme adımlarını](addin/README.md) izleyerek `addin/manifest.dev.xml` dosyasını yükleyin. Anahtar hiçbir `VITE_*` değişkenine yazılmaz. Yerel `.env.local` dosyası yalnızca API adresi gibi gizli olmayan frontend ayarları içindir.

Kalıcı Windows hizmeti, otomatik başlangıç ve güncelleme: [Windows hizmeti rehberi](docs/windows-service.md). Günlük kullanımda arayüz ve API 7047 üzerinden sunulur; `addin/manifest.xml` veya `addin/manifest.localhost.xml` kullanılır. Vite başlatılması gerekmez.
Sağlayıcı değişiklikleri: [yapılandırma rehberi](PROVIDER-CONFIGURATION.md).

## Test ve güvenlik kontrolleri

```powershell
dotnet test OutlookMailAdviser.sln --no-build --no-restore
dotnet format OutlookMailAdviser.sln --no-restore --verify-no-changes
cd addin
npm test
npm run build
npm run validate
npm run validate:local-host
cd ..
./scripts/Test-Dependencies.ps1
./scripts/Install-Gitleaks.ps1
./scripts/Test-PublicSource.ps1
```

Otomatik testler gerçek modele veya API anahtarına ihtiyaç duymaz. Manifest doğrulaması, bağımlılık sorguları ve araç indirmeleri ağ erişimi gerektirir. CI aynı kontrolleri Windows üzerinde çalıştırır. Elle demo kontrolü için [sentetik senaryoyu](docs/DEMO.md) izleyin.

## Bilinen sınırlamalar

- Tek kullanıcılı yerel POC; internet erişimine açık bir servis olarak tasarlanmamıştır. CORS, kullanıcı kimlik doğrulaması değildir.
- Model çıktısı hatalı olabilir; tarihler, atamalar ve taslaklar kontrol edilmelidir.
- Bağlam en fazla 16.000 karakter; önceki iki mesaj için toplam 4.000 karakterdir. Eski konuşma bilgileri dışarıda kalabilir; uygulama kırpma/belirsizlik uyarısı gösterir.
- Ekler analiz edilmez; geçmiş ayrıca Outlook'tan indirilmez. Otomatik gönderim yoktur.
- Eksik OpenAI anahtarı başlangıç doğrulamasında bildirilir. Sağlayıcı erişilemiyor/zaman aşımı hataları sonuç yerine kullanıcıya gösterilir.
- Sentetik demo veya otomatik test başarısı, tüm Outlook sürümlerinin doğrulandığı anlamına gelmez.

## Lisans ve katkı

[MIT](LICENSE) — Copyright © 2026 Ahmet Almış.
[Üçüncü taraf lisansları ve varlık kaynakları](THIRD-PARTY-NOTICES.md).
Sorun bildirirken gerçek e-posta, kişisel veri veya API anahtarı paylaşmayın.
Güvenlik sorunları için [özel bildirim yönergesini](SECURITY.md) izleyin.

## 1.0.0 — Bu yazışmaya sor

Outlook eklentisindeki **Bu yazışmaya sor** alanına açık e-postayla ilgili bir soru yazın.
Genel analiz yapmanız gerekmez. Örneğin: “Teslim tarihiyle ilgili daha önce ne söylenmiş?”
Yalnızca açık e-postanın gövdesi ve bu gövdenin içinde bulunan geçmiş yazışmalar kullanılır;
posta kutusundaki başka mesajlara veya eklere erişilmez, indeks veya kalıcı arşiv oluşturulmaz.

Soru akışı, normal analizdeki geçmiş mesaj/karakter sınırlarını uygulamaz. Temizlenmiş gövdenin
tamamını modelin bağlam bütçesine göre örtüşen parçalarda inceler. Uzun yazışmalar birden fazla
model çağrısı gerektirir. İşlem iptal edilebilir. Tamamlanan/toplam bölüm sayısı sonuçta gösterilir;
zaman aşımı veya doğrulanamayan alıntı durumunda sonuç açıkça **kısmi** olarak işaretlenir.

Yanıtın altında metinle birebir doğrulanmış kaynak alıntıları bulunur. Alıntıda yer alan tarih ve
gönderen başlıkları korunur; olmayan bilgiler eklenmez. Birden fazla bölümde bulgu varsa ve kanıtlar
model bütçesine sığıyorsa çelişkileri karşılaştıran birleşik yanıt oluşturulur. Sığmıyorsa bölüm
bulguları ve karşılaştırmanın yapılamadığı uyarısı gösterilir; hiçbir kaynak sessizce atılmaz.
Modelin yorumunu alıntılar üzerinden kontrol edin.

Alıntıları seçip **Taslakta kullan** düğmesine basın (en fazla 20 alıntı / toplam 4000 karakter).
Mevcut ton ve yanıt talimatlarıyla taslak oluşturabilirsiniz. Sunucu kaynakları mevcut e-posta
gövdesine karşı yeniden doğrular. Başka bir e-postaya geçildiğinde kaynaklar ve sonuçlar temizlenir.

API: `POST /api/v1/mail/questions`, gövde: `{ "message": <mevcut mesaj>, "question": "...", "preferredLanguage": "tr" }`.
Yanıt: `answer`, `sources` (`id`, `quote`, temizlenmiş gövde içindeki UTF-16 `startOffset`),
`completedChunks`, `totalChunks`, `isPartial`, `warnings`. Soru en fazla 1000 karakterdir;
yapılandırılan model bağlamı soruya yer bırakmıyorsa 400 döner. `POST /api/v1/mail/draft`
isteğindeki isteğe bağlı `sourceQuotes` dizisi seçilen alıntıları taşır. Kaynaksız mevcut istekler uyumludur.

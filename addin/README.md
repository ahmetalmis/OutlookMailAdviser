# Outlook Add-in

Bu klasör, açık Outlook e-postasını okuyup mevcut .NET API'ye gönderen React + TypeScript task pane uygulamasıdır. Add-in yalnızca `ReadItem` izni ister; e-postayı değiştirmez ve yanıt göndermez.

## Bu bilgisayarda günlük kullanım

`OutlookMailAdviser` Windows hizmeti arayüzü ve API'yi birlikte
`https://localhost:7047/addin/index.html` adresinde sunar ve Windows açılışında otomatik başlar.
Vite veya terminal açmanız gerekmez. Outlook'a `manifest.xml` ya da onun aynı içeriğe
sahip kopyası `manifest.localhost.xml` yüklenir. Geliştirme yayını yalnızca ayrı kimlikli
`manifest.dev.xml` dosyasıyla kullanılır; 3000 portunun kapalı olması günlük yayını etkilemez.

Paneldeki API durumu artık anahtar/token reddi, erişim izni, kota, hız sınırı ve bağlantı
hatalarını açıklamasıyla gösterir. Anahtarı hizmet yapılandırmasında düzelttikten sonra
**Yeniden kontrol et** seçilebilir. Analiz, taslak ve soru işlemleri de giriş/çıkış token
sınırını ve sağlayıcı hatalarını gösterir. API anahtarı ve ham sağlayıcı hata metni gösterilmez.
Bu mesajlar panel yüklendikten sonra görünür; Outlook'un paneli başlatamaması ayrı bir Office hatasıdır.

Hizmet kurulumu ve güncellemesi için [Windows hizmeti rehberine](../docs/windows-service.md) bakın.

## Geliştirme ortamı (opsiyonel)

Node.js 22.12 veya üzeri gereklidir. PowerShell'de:

```powershell
cd addin
npm ci
npm run certs
npm run icons
```

`npm run certs`, .NET geliştirme sertifikasını Outlook task pane sunucusunun kullanacağı `addin/.certs/localhost.pfx` dosyasına aktarır. Windows güven penceresi açarsa onaylayın. Sertifika ve parolası yalnızca yerel geliştirme içindir; `.certs` klasörü Git'e dahil edilmez.

## Çalıştırma

İki terminal açın.

Terminal 1 — AI sağlayıcısını seçip API'yi başlatın:

```powershell
# Yerel Ollama alternatifi (varsayılan provider OpenAI):
$env:Ai__Provider = 'Ollama'
dotnet run --project backend/src/OutlookMailAdviser.Api --launch-profile https
```

Terminal 2 — task pane web uygulamasını başlatın:

```powershell
cd addin
npm run dev
```

API adresi varsayılan olarak `https://localhost:7047`'dir. Farklı bir adres için `addin/.env.local` oluşturun:

```text
VITE_API_BASE_URL=https://localhost:7047
```

## Outlook'a yükleme

1. Tarayıcıda `https://aka.ms/olksideload` adresini açın.
2. **My add-ins / Eklentilerim** bölümünde **Custom Addins / Özel eklentiler** seçeneğini bulun.
3. **Add from File / Dosyadan ekle** ile `addin/manifest.dev.xml` dosyasını yükleyin.
4. Outlook'ta bir e-postayı okuma modunda açın.
5. Yeni Outlook veya Outlook Web'de **Apps / Uygulamalar → Outlook Mail Adviser → Maili analiz et** yolunu izleyin. Klasik Outlook'ta düğme şeritte görünür.

Task pane açıldığında seçili e-postanın konusu ve göndereni görünür. **Analiz sonucu dili** alanından Türkçe veya English seçilebilir; varsayılan Türkçedir ve seçim sonraki açılışlar için saklanır. **Bu e-postayı analiz et** düğmesi, içeriği seçilen çıktı diliyle .NET API'ye yollar ve yapılandırılmış sonucu gösterir.

Analiz isteği, Outlook'taki aktif kullanıcının görünen adını ve SMTP adresini de `currentUser` olarak gönderir. Model açık ad/e-posta atamalarını ve kullanıcıya yöneltilmiş net istekleri kişisel aksiyon olarak işaretler. Böyle bir aksiyon bulunduğunda sonuç kartında **Senden aksiyon bekleniyor** uyarısı, ilgili satırda ise **Senden bekleniyor** etiketi görünür. Grup veya belirsiz sahiplik durumları özellikle vurgulanmaz. Outlook kullanıcı profili okunamazsa analiz normal çalışır, fakat kişisel aksiyon vurgusu yapılmaz.

Analiz tamamlandıktan sonra **Mail hazırla** bölümü görünür:

1. **Yazışma türü** olarak **Gönderene yanıt** veya **Başka kişilere ilet** seçin.
2. İletme için **Kime yazılacak?** alanına isim veya rol girin (en fazla 500 karakter); birden fazla muhatap yazabilirsiniz. E-posta adresi gerekmez.
3. Tonu seçin; isterseniz **Ton detayı** alanına en fazla 500 karakterlik üslup yönlendirmesi yazın.
4. **Mailin amacı / ana mesajı** alanına ne anlatmak istediğinizi yazın.
5. İsteğe bağlı **Dikkat edilecek hususlar** alanına “Kişisel suçlama yapma, tarih taahhüdü verme” gibi mail bazında kurallar girin (en fazla 2000 karakter).
6. **Taslak oluştur** düğmesine basın. Konuyu ayrıca kullanın; **Kopyala** yalnızca gövdeyi panoya alır.

İletme modunda özgün mail kaynak olarak kullanılır; taslak belirttiğiniz muhataba hitap eder. Örneğin Dilek Hanım'dan gelen geri bildirim için “Grup müdürüm” yazarak yönetimden aksiyon isteyen bir metin hazırlayabilirsiniz. Yeni maile geçildiğinde tür varsayılana döner; muhatap, ana mesaj ve hususlar temizlenir. Hususlar kalıcı olarak saklanmaz.

Taslak, analiz için seçilen çıktı dilinde hazırlanır. Bu POC sürümü Outlook mesajını otomatik değiştirmez ve mail göndermez; kullanıcı taslağı görüp kopyalayarak son kontrolü yapar.

## Doğrulama

Analiz ve taslak bağlamı güncel mail ve en fazla önceki iki mesajdan oluşur.
Toplam 16.000, geçmiş için 4.000 karakter sınırı uygulanır. Normal geçmiş
azaltımı bilgi notu olarak gösterilir. Güncel mail veya ayrıştırılamayan metin
kırpılırsa sarı uyarı gösterilir. Eski yazışmalar için ayrıca Outlook isteği yapılmaz.

```powershell
cd addin
npm test
npm run build
npm run validate
npm run validate:local-host
```

API durumu kırmızı görünürse önce `https://localhost:7047/health/ready` adresini tarayıcıda açın. Sertifika uyarısı varsa yerel .NET geliştirme sertifikasına güvenin:

```powershell
dotnet dev-certs https --trust
```

API CORS ayarı `https://localhost:3000` origin'ine izin verir. Portu değiştirirseniz `backend/src/OutlookMailAdviser.Api/appsettings.Development.json` içindeki `Cors:AllowedOrigins` değerini de değiştirin.

## Sürekli çalışan yerel kurulum

Geliştirme manifesti `manifest.dev.xml`, Vite sunucusundaki
`https://localhost:3000` adresini kullanır. Windows oturumu açıldığında başlayan
kalıcı kurulum ise `manifest.localhost.xml` dosyasını kullanır ve add-in ile
API'yi tek origin üzerinden sunar:

```text
https://localhost:7047/addin/index.html
```

Kalıcı kurulumu repository kökünden yapmak için [ayrıntılı geliştirme rehberindeki](../docs/DEVELOPMENT.md) **Windows'ta
sürekli çalıştırma** bölümünü izleyin. Kurulumdan sonra Outlook'a
`manifest.xml` veya `manifest.localhost.xml` dosyasını yükleyin. Geliştirme eklentisi
ayrı kimliğe sahiptir; günlük kullanım için **Outlook Mail Adviser** eklentisini seçin.

## 1.0.0: Yazışmaya soru sorma

Açık e-postanın altında **Bu yazışmaya sor** alanı yer alır. Soru sormak için önce analiz yapmak
gerekmez. Gövde içindeki geçmişin tamamı incelenir. Yanıtın kaynaklarını seçip **Taslakta kullan**
ile taslak hazırlama alanına aktarabilirsiniz. İptal ve e-posta değişimi bekleyen soru sonucunu
geçersiz kılar. İşlem sürerken ilerleme göstergesi, sonuçta incelenen bölüm sayısı gösterilir.

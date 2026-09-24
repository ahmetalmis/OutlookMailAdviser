# Outlook Add-in

Bu klasör, açık Outlook e-postasını okuyup mevcut .NET API'ye gönderen React + TypeScript task pane uygulamasıdır. Add-in yalnızca `ReadItem` izni ister; e-postayı değiştirmez ve yanıt göndermez.

## İlk kurulum

Node.js 20 veya üzeri gereklidir. PowerShell'de:

```powershell
cd addin
npm install
npm run certs
npm run icons
```

`npm run certs`, .NET geliştirme sertifikasını Outlook task pane sunucusunun kullanacağı `addin/.certs/localhost.pfx` dosyasına aktarır. Windows güven penceresi açarsa onaylayın. Sertifika ve parolası yalnızca yerel geliştirme içindir; `.certs` klasörü Git'e dahil edilmez.

## Çalıştırma

İki terminal açın.

Terminal 1 — AI sağlayıcısını seçip API'yi başlatın:

```powershell
# Ollama için varsayılan ayarlar yeterlidir.
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
3. **Add from File / Dosyadan ekle** ile `addin/manifest.xml` dosyasını yükleyin.
4. Outlook'ta bir e-postayı okuma modunda açın.
5. Yeni Outlook veya Outlook Web'de **Apps / Uygulamalar → Outlook Mail Adviser → Maili analiz et** yolunu izleyin. Klasik Outlook'ta düğme şeritte görünür.

Task pane açıldığında seçili e-postanın konusu ve göndereni görünür. **Analiz sonucu dili** alanından Türkçe veya English seçilebilir; varsayılan Türkçedir ve seçim sonraki açılışlar için saklanır. **Bu e-postayı analiz et** düğmesi, içeriği seçilen çıktı diliyle .NET API'ye yollar ve yapılandırılmış sonucu gösterir.

Analiz isteği, Outlook'taki aktif kullanıcının görünen adını ve SMTP adresini de `currentUser` olarak gönderir. Model açık ad/e-posta atamalarını ve kullanıcıya yöneltilmiş net istekleri kişisel aksiyon olarak işaretler. Böyle bir aksiyon bulunduğunda sonuç kartında **Senden aksiyon bekleniyor** uyarısı, ilgili satırda ise **Senden bekleniyor** etiketi görünür. Grup veya belirsiz sahiplik durumları özellikle vurgulanmaz. Outlook kullanıcı profili okunamazsa analiz normal çalışır, fakat kişisel aksiyon vurgusu yapılmaz.

Analiz tamamlandıktan sonra **Mail hazırla** bölümü görünür:

1. `Profesyonel`, `Samimi`, `Kısa ve net`, `İkna edici` veya `Empatik` tonlarından birini seçin.
2. İsterseniz **Ton detayı** alanına “Hafif sert, net ve uyarıcı olsun” gibi en fazla 500 karakterlik ek üslup yönlendirmesi yazın.
3. **Açıklama / yanıt özeti** alanına vermek istediğiniz cevabın ana fikrini yazın.
4. **Yanıt taslağı oluştur** düğmesine basın.
5. Oluşan konu ve gövdeyi **Kopyala** ile Outlook yanıtına aktarın.

Taslak, analiz için seçilen çıktı dilinde hazırlanır. Bu POC sürümü Outlook mesajını otomatik değiştirmez ve mail göndermez; kullanıcı taslağı görüp kopyalayarak son kontrolü yapar.

## Doğrulama

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

Geliştirme manifesti `manifest.xml`, Vite sunucusundaki
`https://localhost:3000` adresini kullanır. Windows oturumu açıldığında başlayan
kalıcı kurulum ise `manifest.localhost.xml` dosyasını kullanır ve add-in ile
API'yi tek origin üzerinden sunar:

```text
https://localhost:7047/addin/index.html
```

Kalıcı kurulumu repository kökünden yapmak için ana README'deki **Windows'ta
sürekli çalıştırma** bölümünü izleyin. Kurulumdan sonra Outlook'a
`.artifacts/local-host/manifest.xml` dosyasını bir kez yükleyin. Aynı add-in
kimliği kullanıldığı için Outlook mevcut geliştirme kaydını bu adreslerle
günceller.

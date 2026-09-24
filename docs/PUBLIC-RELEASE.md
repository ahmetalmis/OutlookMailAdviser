# Public yayın kontrol raporu

Tarih: 24 Eylül 2026. Kapsam: MIT lisanslı, yerelde çalışan portföy/POC.

**Durum:** Yerel kaynak ve API hazırlığı tamamlandı. GitHub üzerindeki CI koşusu
ve özel güvenlik bildirimi ayarı henüz doğrulanmadı. Outlook istemcisindeki son
deneme kullanıcı isteğiyle ertelendi. Depo görünürlüğü değiştirilmedi, push yapılmadı.

## Uygulanan değişiklikler

- MIT lisansı (Ahmet Almış, 2026), güvenlik/veri akışı açıklaması, bağımlılık lisans
  envanteri, sentetik demo ve portföy odaklı README eklendi. Önceki ayrıntılı README
  içeriği DEVELOPMENT.md altında korundu.
- Vite localhost'a bağlanıyor. API launch/production ayarları zaten localhost ile
  sınırlıydı. Özel adres override'ları kullanıcı sorumluluğundadır.
- Sağlayıcı bağlantı ayrıntıları API cevabına taşınmıyor. Framework'ün ham exception
  kaydı kapatıldı; uygulamanın kod/trace-id içeren güvenli hata kayıtları korunuyor.
  Sağlayıcı health-check hataları da ham iç exception kaydetmiyor.
- `.env` varyantları, loglar, sertifikalar, derleme önbellekleri ve geçici çıktılar
  Git dışında. İki tsbuildinfo dosyası diskte korunarak takipten çıkarıldı.
- Vitest 4.1.11, adm-zip >=0.6.1 override ve xUnit 2.9.3 ile bulunan güvenlik
  sorunları giderildi. Test/manifest kontrolleri güncellemelerle tekrar geçti.
- Windows CI, haftalık Dependabot ve gizli anahtar/bağımlılık tarama betikleri eklendi.
  Gitleaks sürümü ve Windows arşiv SHA-256 değeri sabitlendi.

## Doğrulama sonuçları

| Kontrol | Sonuç |
| --- | --- |
| .NET restore ve build | Başarılı; temiz kaynak kopyasında 0 uyarı, 0 hata |
| Backend testleri | 49/49 geçti |
| C# format kontrolü | Değişiklik gerektirmeden geçti |
| Temiz frontend kurulumu | `npm ci` başarılı |
| Frontend testleri | 13/13 geçti |
| Frontend production build | Başarılı |
| İki Outlook manifesti | Doğrulandı; bu sonuç istemci çalışma testi değildir |
| npm audit | 0 güvenlik bulgusu |
| NuGet audit | Güncelleme sonrası yüksek/kritik bulgu yok; kontrol betiği geçti |
| Gitleaks: tüm yerel Git referansları | Tarandı, gizli anahtar bulunmadı |
| Gitleaks: yayınlanacak çalışma ağacı | Takip edilen ve yeni, ignore edilmemiş kaynaklar tarandı; temiz |
| Gerçek model analizi | OpenAI `gpt-4.1-mini-2025-04-14`; iki aksiyon, doğru tarih ve kullanıcı ataması |
| Gerçek model yanıt taslağı | Sentetik içerikle konu/gövde üretimi başarılı |
| Güvenlik regresyonları | İzinsiz CORS origin'i; yanıt ve loglarda hassas hata içeriğinin gizlenmesi |
| Hata senaryoları | Eksik anahtar, bağlantı hatası, Ollama erişilememe ve zaman aşımı otomatik testleri geçti |

Yerel ortam: Windows, .NET SDK 8.0.416, Node.js 24.18.1. CI Node 22 kullanacak;
uzak koşu yapılmadığı için bu CI ortamı henüz başarılı olarak işaretlenmemiştir.
Temiz kaynak doğrulaması mevcut kullanıcı araçları/paket önbelleğiyle, bin/obj ve
node_modules içermeyen ayrı bir dizinde yapıldı; yeni Windows makinesi kurulumu değildir.
Gerçek API demosu mevcut servisi etkilememek için geçici localhost:5147 HTTP
adresinde yürütüldü; README ve Outlook manifestlerindeki HTTPS varsayılanları korunmuştur.

## Veri ve varlık incelemesi

Kaynaklardaki örnek adresler `example.com` alanını kullanıyor. Gerçek kurumsal
e-posta/telefon içeriği kaynak taramasında bulunmadı. Kişisel bilgisayar yolları
güncel dokümanlarda genel örneklerle değiştirildi. İlk commit'in iki rehberinde
eski yerel kullanıcı yolu kalıyor; bu bir erişim anahtarı değildir ve geçmiş
yeniden yazılmadı. Yazar kimliği/telif bilgisi bilinçli olarak korunuyor.

Ignore edilen dosyaları da kapsayan ek tarama; yerel geliştirme sertifikasını,
Visual Studio/IIS yerel yapılandırmasındaki olası anahtarları ve indirilen
Gitleaks dokümantasyonundaki örnekleri işaretledi. Bu dosyalar yayın kaynaklarına
dahil değildir. Ham tarama raporları ve araçlar `.artifacts` altında kalır.
Depo klasörünün tamamını ZIP olarak paylaşmayın; yalnızca Git tarafından takip
edilen yayın kaynaklarını paylaşın.

İkonlar repo içindeki üretici betiğe dayanır. Doğrudan bağımlılıklar yerel paket
metadata'sından incelendi; envanter THIRD-PARTY-NOTICES.md dosyasındadır.
Önceki gerçek e-posta görüntüleri ve AI ile düzenlenen LinkedIn görselleri yayın
dışında tutuldu. Yeni görsel yalnızca sentetik veri içeren gerçek tarayıcı/API
ekranıdır; Outlook ekranı veya üretilmiş bir arayüz değildir.

## GitHub ve yayın için kalan adımlar

1. GitHub CLI oturumu bu ortamda HTTP 401 döndürdü. Depo sahibi oturumunu açmalı.
2. Yayın adayı commit'i private depoya gönderilip **Public release checks** koşusu
   başarılı tamamlanmalı. GitHub'daki gerçek CI sonucu henüz mevcut değildir.
3. Depoda **Settings → Code security → Private vulnerability reporting** etkinleştirilmeli;
   SECURITY.md içindeki özel bildirim bağlantısının çalıştığı doğrulanmalı.
4. Outlook'ta sentetik test hesabıyla son istemci denemesi, kullanıcı talebiyle
   sonraya bırakıldı. Destek matrisi bu doğrulama yapılmadan genişletilmemeli.
5. Bu sonuçlar incelendikten sonra public görünürlük ayrı, açıkça onaylanan son
   işlem olarak uygulanmalı. Bu hazırlık public görünürlüğe geçiş yapmaz.

Son kaynak değişikliklerinden sonra aynı tarama ve testler tekrar çalıştırılmalıdır.

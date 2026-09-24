# Sentetik demo ve elle doğrulama

Bu senaryodaki kişiler ve adresler kurgudur. Gerçek kurumsal e-posta kullanmayın.
Gerçek modele gönderilen sonuçlar değişebilir; aşağıdaki beklenenler kontrol
kriterleridir, önceden üretilmiş sonuçlar değildir.

## E-posta

- Gönderen: Deniz Kaya <deniz.kaya@example.com>
- Alıcı / demo kullanıcısı: Eren Yılmaz <eren.yilmaz@example.com>
- Tarih: 24 Eylül 2026
- Konu: Demo ortamı geçiş planı

```text
Merhaba Eren,

Demo ortamına geçiş tamamlanamadı. Bu nedenle kabul testleri başlayamadı.
Gecikmenin nedenini ve güncellenmiş geçiş planını 25 Eylül 2026 saat 14:00'e
kadar paylaşabilir misin? Kontrol ve geri dönüş listesini de aynı günün
sonuna kadar hazırlamanı rica ederim.

Teşekkürler,
Deniz
```

## Kontrol adımları

1. README ile yerel API'yi ve add-in'i başlatın. Sağlayıcı readiness kontrolü başarılı olmalı.
2. Outlook için yalnızca sentetik verili bir test hesabı kullanın; analiz isteği
   aktif Outlook hesabının ad/adresini de gönderir. Kurumsal profil kullanmayın.
3. Özel manifesti yükleyin, demo mesajını okuma modunda açın, analizi başlatın.
4. Özette gecikme ve kabul testlerine etkisi; aksiyonlarda güncel plan ve kontrol
   listesi görünmeli. Tarihler 25 Eylül 2026 olmalı. Model çıktısını elle doğrulayın.
5. Profesyonel tonu seçin; “Gecikmenin nedenini inceliyoruz. Güncellenen planı ve
   kontrol listesini belirtilen tarihe kadar paylaşacağımızı belirt.” yazın.
6. Taslak konu/gövdesini ve kopyalama davranışını doğrulayın. Gerçek alıcıya göndermeyin.
7. Ekran görüntüsünü kaydetmeden önce adlar, adresler, Outlook başlık çubuğu,
   bildirimler ve olası imzalar dahil görüntünün tamamını kontrol edin.

Outlook olmadan backend analizi `https://localhost:7047/test` ekranında aynı
sentetik içerikle denenebilir. Bu, Outlook entegrasyon testi yerine geçmez.
Taslak endpoint'i Postman koleksiyonuyla aynı içerik üzerinden denenebilir.

API çalışırken `./scripts/Test-SyntheticDemo.ps1` komutu hem analizi hem yanıt
taslağını gerçek sağlayıcı üzerinden kontrol eder. Sentetik girdiler kullanır;
OpenAI seçiliyse sağlayıcı kullanımı oluşur. Sonuçlar yayın dışı
`.artifacts/public-release/synthetic-demo.json` dosyasına yazılır.

![Tarayıcı test ekranından sentetik analiz](images/synthetic-analysis.png)

Bu görsel 24 Eylül 2026'da gerçek API ve OpenAI sağlayıcısıyla alınmıştır.
Görseldeki adlar/adresler kurgudur; görsel yapay zekâyla yeniden üretilmemiştir.

## Hata senaryoları

- OpenAI anahtarı yok: başlangıçta `Set OPENAI_API_KEY` yönlendirmesi beklenir.
- Ollama kapalı: readiness başarısız, analizde `ollama_unavailable` beklenir.
- Sağlayıcı zaman aşımı: `model_timeout`; yeniden deneme kullanıcı kararıdır.
- Sağlayıcı beklenmeyen veri döndürür: ham model içeriği gösterilmeden hata oluşur.

Bu sürümde yapılan ve bekleyen doğrulamaları [yayın raporundan](PUBLIC-RELEASE.md) takip edin.
